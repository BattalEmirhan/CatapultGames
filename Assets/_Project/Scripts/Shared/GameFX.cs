using System.Collections;
using UnityEngine;

namespace CatapultGames
{
    // Procedural juice / VFX hub. Every effect is a short-lived ParticleSystem
    // configured in code that destroys itself when done; also owns the camera
    // shake / zoom driven off Camera.main.
    //
    // Lives in GameScene (the scene builder places it) because its materials come
    // from the serialized MaterialSet — a hub created on the fly would have none,
    // and Shader.Find returns null in player builds.
    public sealed class GameFX : MonoBehaviour
    {
        public static GameFX Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GameObject("GameFX").AddComponent<GameFX>();
                return _instance;
            }
        }

        // Current camera shake displacement in WORLD space (Vector3.zero when not
        // shaking). Screen→world picking subtracts this so the aim stays put while
        // the view shakes. See TapLaunchController.
        public  Vector3 ShakeOffset { get; private set; }

        // Reads the offset without forcing the singleton to spawn (picking runs every
        // frame; we don't want a stray GameFX created just to read zero).
        public static Vector3 CurrentShakeOffset =>
            _instance != null ? _instance.ShakeOffset : Vector3.zero;

        [SerializeField] private MaterialSet materials;

        private static GameFX _instance;
        private Material  _particleMat;
        private Texture2D _particleTex;
        private Material  _ringMat;       // shared by every shockwave ring (was 1 Material/ring)
        private Transform _camT;
        private Vector3   _camRest;
        private Coroutine _shake;
        private Coroutine _zoom;
        private float     _camRestFov;

        // Freezes the game clock for a blink so a big hit lands with weight.
        // Unscaled time, so the freeze itself is not frozen; never stacks — a
        // second call while paused just re-arms the release.
        private Coroutine _hitStop;
        private float     _timeScaleBefore = 1f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (materials == null)
            {
                Debug.LogError("[GameFX] No MaterialSet — place GameFX in the scene (CatapultGames/Build Scenes).");
                return;
            }
            _particleTex = BuildSoftCircle();
            _particleMat = new Material(materials.Sprite) { mainTexture = _particleTex };
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (_particleMat)
                Destroy(_particleMat);
            if (_particleTex)
                Destroy(_particleTex);
            if (_ringMat)
                Destroy(_ringMat);
        }

        // Colored splash + small shake when a ball lands and paints.
        public void Impact(Vector3 pos, Color color)
        {
            var ps   = NewSystem("FX_Impact", pos, 0.8f);
            var main = ps.main;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.30f, 0.55f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(3.5f, 7.5f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.12f, 0.30f);
            main.startColor      = TintRange(color);
            main.gravityModifier = 1.4f;
            Burst(ps, 26);
            Shape(ps, ParticleSystemShapeType.Hemisphere, 0.15f);
            FadeOut(ps);
            ShrinkOverLife(ps);
            ps.Play();

            Shake(0.18f, 0.18f);
        }

        // Big celebratory bloom — fired once a painted block has finished its
        // outward rising wave. `scale` grows it with the size of the paint hit.
        public void Bloom(Vector3 pos, Color color, float scale = 1f)
        {
            float s = Mathf.Clamp(scale, 0.6f, 2.2f);

            var ps   = NewSystem("FX_Bloom", pos + Vector3.up * 0.4f, 1.2f);
            var main = ps.main;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.45f, 0.90f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(5f * s, 11f * s);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.18f * s, 0.42f * s);
            main.startColor      = TintRange(color);
            main.gravityModifier = 0.6f;
            Burst(ps, Mathf.RoundToInt(48 * s));
            Shape(ps, ParticleSystemShapeType.Hemisphere, 0.25f * s);
            FadeOut(ps);
            ShrinkOverLife(ps);
            ps.Play();

            ImpactRing(pos, color, s);
            Shake(0.16f + 0.07f * s, 0.28f);
            ZoomPunch(1.2f * s);   // subtle — scales with hit size; big zoom reserved for wins
        }

        // Expanding flat shockwave ring on the grid plane — great landing punch.
        public void ImpactRing(Vector3 pos, Color color, float scale = 1f)
        {
            StartCoroutine(RingRoutine(pos + Vector3.up * 0.05f, color, Mathf.Clamp(scale, 0.6f, 2.2f)));
        }

        // Small color burst when an individual cell flips to "filled".
        public void CellPop(Vector3 pos, Color color)
        {
            var ps   = NewSystem("FX_CellPop", pos, 0.6f);
            var main = ps.main;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor      = TintRange(color);
            main.gravityModifier = 0.6f;
            Burst(ps, 8);
            Shape(ps, ParticleSystemShapeType.Hemisphere, 0.08f);
            FadeOut(ps);
            ShrinkOverLife(ps);
            ps.Play();
        }

        // Pale dust puff at the catapult when a ball launches.
        public void LaunchPuff(Vector3 pos)
        {
            var ps   = NewSystem("FX_Launch", pos, 0.6f);
            var main = ps.main;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.20f, 0.40f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(1.0f, 3.0f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor      = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.95f, 0.8f, 0.9f), new Color(0.8f, 0.8f, 0.85f, 0.7f));
            main.gravityModifier = -0.2f;
            Burst(ps, 12);
            Shape(ps, ParticleSystemShapeType.Sphere, 0.12f);
            FadeOut(ps);
            ps.Play();

            Shake(0.07f, 0.10f);
        }

        // A short confetti shower over the board — "one colour is completely done".
        // Smaller than Win so the two never read the same; tinted toward the
        // finished colour so the shower says WHICH colour.
        public void Confetti(Vector3 center, Color color)
        {
            var ps   = NewSystem("FX_Confetti", center + Vector3.up * 5f, 1.8f);
            var main = ps.main;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.8f, 3.0f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor      = new ParticleSystem.MinMaxGradient(Color.Lerp(color, Color.white, 0.25f), Color.white);
            main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.gravityModifier = 0.9f;
            Burst(ps, 60);
            Shape(ps, ParticleSystemShapeType.Box, 0f, new Vector3(6f, 0.2f, 6f));
            FadeOut(ps);
            ps.Play();
        }

        public void HitStop(float seconds)
        {
            if (_hitStop != null)
                StopCoroutine(_hitStop);
            else
                _timeScaleBefore = Time.timeScale;
            _hitStop = StartCoroutine(HitStopRoutine(Mathf.Clamp(seconds, 0.01f, 0.25f)));
        }

        // Celebration confetti rain on win.
        public void Win(Vector3 center)
        {
            var ps   = NewSystem("FX_Win", center + Vector3.up * 6f, 2.6f);
            var main = ps.main;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(1.0f, 4.0f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startColor      = ConfettiRange();
            main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
            main.gravityModifier  = 1.0f;
            Burst(ps, 140);
            Shape(ps, ParticleSystemShapeType.Box, 0f, new Vector3(8f, 0.2f, 8f));
            FadeOut(ps);
            ps.Play();

            Shake(0.25f, 0.35f);
            Flash(new Color(1f, 0.93f, 0.55f), 0.40f, 0.55f);   // warm gold burst
            ZoomPunch(6f);
        }

        // Firework burst — colored sparks shooting out in all directions with
        // gravity + spark trails. Fired when a completed colour's leftover balls
        // rocket up the screen and pop.
        public void Firework(Vector3 pos, Color color)
        {
            var ps   = NewSystem("FX_Firework", pos, 1.4f);
            var main = ps.main;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(4.5f, 9.5f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
            main.startColor      = TintRange(color);
            main.gravityModifier = 1.3f;
            Burst(ps, 64);
            Shape(ps, ParticleSystemShapeType.Sphere, 0.05f);
            FadeOut(ps);
            ShrinkOverLife(ps);

            // Sparkly trails behind each spark for the classic firework look.
            var tr = ps.trails;
            tr.enabled              = true;
            tr.ratio                = 0.6f;
            tr.lifetime             = new ParticleSystem.MinMaxCurve(0.25f);
            tr.dieWithParticles     = true;
            tr.inheritParticleColor = true;
            ps.GetComponent<ParticleSystemRenderer>().trailMaterial = _particleMat;

            ps.Play();

            ImpactRing(pos, color, 0.7f);
            Shake(0.05f, 0.07f);
        }

        public void Shake(float intensity, float duration)
        {
            if (_camT == null)
            {
                var cam = Camera.main;
                if (cam == null)
                    return;
                _camT = cam.transform;
            }

            // Capture the resting pose only when no shake is currently running,
            // so overlapping shakes don't accumulate drift.
            if (_shake == null)
                _camRest = _camT.localPosition;
            else
                StopCoroutine(_shake);

            _shake = StartCoroutine(ShakeRoutine(intensity, duration));
        }

        // Brief dolly-in on the main (perspective) camera, then back to rest.
        public void ZoomPunch(float degrees)
        {
            var cam = Camera.main;
            if (cam == null || cam.orthographic)
                return;

            if (_zoom == null)
                _camRestFov = cam.fieldOfView;
            else
                StopCoroutine(_zoom);

            _zoom = StartCoroutine(ZoomRoutine(cam, degrees));
        }

        // Spawns a self-destructing overlay canvas that flashes then fades out.
        public void Flash(Color color, float maxAlpha = 0.5f, float duration = 0.35f)
        {
            StartCoroutine(FlashRoutine(color, maxAlpha, duration));
        }

        private IEnumerator RingRoutine(Vector3 center, Color color, float scale)
        {
            const int seg = 48;
            var go = new GameObject("FX_Ring");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace     = true;
            lr.loop              = true;
            lr.positionCount     = seg;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.sortingOrder      = 60;

            if (_ringMat == null && materials != null)
                _ringMat = new Material(materials.Sprite);
            lr.sharedMaterial = _ringMat;

            float dur = 0.30f;   // quicker shockwave (was 0.42)
            float r0  = 0.15f * scale, r1 = 2.3f * scale;
            float w0  = 0.20f * scale;
            float t   = 0f;

            while (t < dur)
            {
                float p = t / dur;
                float r = Mathf.Lerp(r0, r1, Mathf.Sqrt(p));   // fast burst out, easing
                float w = Mathf.Lerp(w0, 0f, p);
                lr.startWidth = lr.endWidth = w;

                Color c = Color.Lerp(Color.white, color, 0.5f);
                c.a = 1f - p;
                lr.startColor = lr.endColor = c;

                for (int i = 0; i < seg; i++)
                {
                    float a = (i / (float)seg) * Mathf.PI * 2f;
                    lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                }

                t += Time.deltaTime;
                yield return null;
            }

            Destroy(go);   // _ringMat is shared — don't destroy it here
        }

        private IEnumerator HitStopRoutine(float seconds)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = _timeScaleBefore;
            _hitStop = null;
        }

        private IEnumerator ShakeRoutine(float intensity, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                float   damp = 1f - (t / duration);
                Vector3 off  = Random.insideUnitSphere * (intensity * damp);
                off.z *= 0.3f;   // dampen depth wobble — looks better on a tilted cam
                _camT.localPosition = _camRest + off;
                // World-space equivalent of the local nudge (parent-aware), so picking
                // can cancel it exactly.
                ShakeOffset = _camT.parent != null ? _camT.parent.TransformVector(off) : off;
                t += Time.deltaTime;
                yield return null;
            }
            _camT.localPosition = _camRest;
            ShakeOffset = Vector3.zero;
            _shake = null;
        }

        private IEnumerator ZoomRoutine(Camera cam, float degrees)
        {
            const float dur = 0.24f;
            float t = 0f;
            while (t < dur)
            {
                float p = t / dur;
                float k = Mathf.Sin(p * Mathf.PI);              // 0 → 1 → 0
                cam.fieldOfView = _camRestFov - degrees * k;     // dip in, ease back
                t += Time.deltaTime;
                yield return null;
            }
            cam.fieldOfView = _camRestFov;
            _zoom = null;
        }

        private IEnumerator FlashRoutine(Color color, float maxAlpha, float duration)
        {
            var go     = new GameObject("FX_Flash");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode    = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera   = Camera.main;
            canvas.planeDistance = 0.5f;   // in front of Canvas_Game, so it covers the HUD too
            canvas.sortingOrder  = 999;

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            float t = 0f;
            while (t < duration)
            {
                float p = t / duration;
                Color c = color;
                c.a     = maxAlpha * (1f - p);     // peak immediately, fade out
                img.color = c;
                t += Time.deltaTime;
                yield return null;
            }
            Destroy(go);
        }

        private ParticleSystem NewSystem(string name, Vector3 pos, float duration)
        {
            var go = new GameObject(name);
            go.transform.position = pos;

            var ps   = go.AddComponent<ParticleSystem>();

            // A freshly added ParticleSystem defaults to playOnAwake = true and is
            // already playing, which makes setting main.duration throw. Stop and
            // clear it first so the system is idle while we configure it.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration        = duration;
            main.loop            = false;
            main.playOnAwake     = false;
            main.maxParticles    = 256;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction      = ParticleSystemStopAction.Destroy;   // self-cleanup

            var emission = ps.emission;
            emission.rateOverTime = 0f;   // burst-only

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = _particleMat;
            rend.renderMode     = ParticleSystemRenderMode.Billboard;
            rend.sortingOrder   = 100;
            return ps;
        }

        private static void Burst(ParticleSystem ps, int count)
        {
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void Shape(ParticleSystem ps, ParticleSystemShapeType type,
                                  float radius, Vector3 scale = default)
        {
            var sh = ps.shape;
            sh.enabled   = true;
            sh.shapeType = type;
            sh.radius    = Mathf.Max(0.01f, radius);
            if (scale != Vector3.zero)
                sh.scale = scale;
        }

        private static void FadeOut(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        private static void ShrinkOverLife(ParticleSystem ps)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size    = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
        }

        private static ParticleSystem.MinMaxGradient TintRange(Color c)
        {
            Color bright = Color.Lerp(c, Color.white, 0.35f);
            return new ParticleSystem.MinMaxGradient(c, bright);  // RandomBetweenTwoColors
        }

        private static ParticleSystem.MinMaxGradient ConfettiRange()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.86f, 0.20f, 0.20f), 0.00f),
                    new GradientColorKey(new Color(0.95f, 0.80f, 0.20f), 0.25f),
                    new GradientColorKey(new Color(0.20f, 0.70f, 0.30f), 0.50f),
                    new GradientColorKey(new Color(0.20f, 0.50f, 0.90f), 0.75f),
                    new GradientColorKey(new Color(0.85f, 0.30f, 0.65f), 1.00f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(g)
            {
                mode = ParticleSystemGradientMode.RandomColor
            };
        }

        // Soft round particle sprite (radial alpha falloff), generated once.
        private static Texture2D BuildSoftCircle()
        {
            const int   S      = 64;
            const float center = S * 0.5f;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x + 0.5f) - center;
                float dy = (y + 0.5f) - center;
                float d  = Mathf.Sqrt(dx * dx + dy * dy) / center;
                float a  = Mathf.Clamp01(1f - d);
                a *= a;   // softer edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }
    }
}
