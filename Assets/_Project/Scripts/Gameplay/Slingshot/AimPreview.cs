using UnityEngine;

namespace CatapultGames
{
    // Task 13 — Trajectory arc preview and landing cell highlight.
    //
    // Attach to a GameObject with a LineRenderer.
    // Wire up in Inspector:
    //   _slingshot    — SlingshotController
    //   _queue        — BallQueue (to know current ball color)
    //   _grid         — GridRenderer (for highlight + PaintingSystem.Preview)
    //   _launchOrigin — Transform at catapult ball position (arc start point)
    [RequireComponent(typeof(LineRenderer))]
    public class AimPreview : MonoBehaviour
    {
        [SerializeField] private SlingshotController _slingshot;
        [SerializeField] private BallQueue           _queue;
        [SerializeField] private GridRenderer        _grid;
        [SerializeField] private Transform           _launchOrigin;

        [Header("Aim line look")]
        [Tooltip("Tint of the flowing aim dots (valid shot).")]
        [SerializeField] private Color _lineColor = new Color(0.35f, 0.95f, 1f);
        [Tooltip("How fast the dots flow toward the target (world units/sec).")]
        [SerializeField] private float _flowSpeed = 1.6f;

        [Header("Dotted aim (flowing dots)")]
        [Tooltip("ON: a stream of round dots travels along the arc toward the target " +
                 "(guaranteed flow). OFF: the breathing dashed LineRenderer instead.")]
        [SerializeField] private bool  _useDots      = true;
        [SerializeField] private float _dotSpacing    = 0.55f;  // world units between dots
        [SerializeField] private float _dotStartScale = 0.22f;  // dot size near the catapult
        [SerializeField] private float _dotEndScale    = 0.10f;  // dot size at the landing

        private LineRenderer _lr;

        // Arc colours: lively gradient when the shot is legal, red when it would land
        // off the grid (and so won't fire — BallLauncher).
        private Gradient  _validGradient;
        private Gradient  _invalidGradient;

        // Self-owned dashed-line material/texture (line mode).
        private Material  _lineMat;
        private Texture2D _lineTex;
        private float     _scroll;
        private const float BaseWidth = 0.30f;   // taper is in widthCurve; this is the base scale

        // Dotted mode: pooled sphere "dots" placed along the arc, marching to target.
        private readonly System.Collections.Generic.List<Transform>    _dots          = new();
        private readonly System.Collections.Generic.List<MeshRenderer> _dotRenderers  = new();
        private Material                 _dotMat;
        private MaterialPropertyBlock    _mpb;
        private Transform                _dotRoot;
        private System.Collections.Generic.List<Vector3> _arcPoints;
        private bool                     _arcValid;
        private bool                     _dotsActive;
        private float                    _phase;
        private const int                MaxDots = 64;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.enabled       = false;
            StyleLine();
        }

        // Builds the flowing-dots look: soft round dots tiled along the arc,
        // billboarded to the camera, tapering toward the landing, scrolling toward
        // it over time. Self-contained so it works regardless of the scene setup.
        private void StyleLine()
        {
            _lineTex = BuildDotTexture();
            var sh = Shader.Find("Sprites/Default")
                  ?? Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            _lineMat = new Material(sh) { mainTexture = _lineTex };
            _lineMat.mainTextureScale = new Vector2(1.3f, 1f);   // dot density along the line
            _lr.sharedMaterial = _lineMat;

            _lr.textureMode       = LineTextureMode.Tile;
            _lr.alignment         = LineAlignment.View;          // ribbon always faces the camera
            _lr.numCapVertices    = 6;
            _lr.numCornerVertices = 4;
            _lr.widthCurve        = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.55f)); // taper
            _lr.widthMultiplier   = BaseWidth;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _validGradient = new Gradient();
            _validGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(_lineColor, 0.45f),
                    new GradientColorKey(_lineColor, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.90f, 0.7f),
                    new GradientAlphaKey(0.45f, 1f),
                });

            Color red = new Color(1f, 0.32f, 0.30f);
            _invalidGradient = new Gradient();
            _invalidGradient.SetKeys(
                new[] { new GradientColorKey(red, 0f), new GradientColorKey(red, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.4f, 1f) });
        }

        // Animate while a preview is visible.
        private void Update()
        {
            if (_useDots)
            {
                if (!_dotsActive) return;
                // March the dots toward the target; wrap by spacing for a seamless loop.
                _phase += _flowSpeed * Time.deltaTime;
                if (_dotSpacing > 0.001f) _phase %= _dotSpacing;
                DrawDots();
                return;
            }

            // ── Line mode ─────────────────────────────────────────────────
            if (_lr == null || !_lr.enabled) return;

            // Flow the dashes (bonus: only if the shader honours texture offset).
            if (_lineMat != null)
            {
                _scroll -= _flowSpeed * Time.deltaTime;
                _lineMat.mainTextureOffset = new Vector2(_scroll, 0f);
            }

            // Gentle breathing on the width — guaranteed liveliness.
            _lr.widthMultiplier = BaseWidth * (1f + 0.12f * Mathf.Sin(Time.time * 7f));
        }

        // Soft round dot on a transparent tile — repeats into a row of dots.
        private static Texture2D BuildDotTexture()
        {
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Vector2 c      = new Vector2(S * 0.5f, S * 0.5f);
            float   radius = S * 0.34f;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d / radius));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            if (_lineMat) Destroy(_lineMat);
            if (_lineTex) Destroy(_lineTex);
            if (_dotMat)  Destroy(_dotMat);
        }

        private void OnEnable()
        {
            if (_slingshot) _slingshot.OnDragUpdated += OnDragUpdated;
        }

        private void OnDisable()
        {
            if (_slingshot) _slingshot.OnDragUpdated -= OnDragUpdated;
            ClearPreview();
        }

        // ── Drag callback (slingshot mode) ────────────────────────────────
        private void OnDragUpdated(Vector2 _)
        {
            if (_slingshot == null || !_slingshot.IsDragging)
            {
                ClearPreview();
                return;
            }
            ShowArc(_slingshot.LaunchVelocity);
        }

        // ── Public preview API ────────────────────────────────────────────
        // Draw the snapped arc + landing highlight for a launch velocity. Used by
        // the slingshot drag (above) and by Gameplay2's tap-to-target controller.
        public void ShowArc(Vector3 velocity)
        {
            if (velocity == Vector3.zero)
            {
                ClearPreview();
                return;
            }

            Vector3 origin = _launchOrigin ? _launchOrigin.position : transform.position;

            // Snap the preview (and the shot) to the aimed grid cell — aim assist.
            Vector3 snappedVel = LaunchSolver.SnapToCell(_grid, origin, velocity);

            var arc = TrajectorySimulator.Simulate(
                origin, snappedVel, GameConstants.TrajectorySteps,
                GameConstants.TrajectoryTimeStep, out Vector3 landPos);

            int gx = 0, gy = 0;
            bool inGrid = _grid != null && _grid.WorldToGrid(landPos, out gx, out gy);

            if (_useDots)
            {
                // Store the path; Update() places + flows the dots each frame.
                _arcPoints  = arc;
                _arcValid   = inGrid;
                _dotsActive = true;
                if (_lr) _lr.enabled = false;
            }
            else
            {
                DrawArc(arc, inGrid);
            }

            HighlightLanding(inGrid, gx, gy);
        }

        // Hide the arc + clear cell highlights.
        public void Hide() => ClearPreview();

        // ── Visuals ──────────────────────────────────────────────────────
        private void DrawArc(System.Collections.Generic.List<Vector3> points, bool valid)
        {
            _lr.enabled       = true;
            _lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                _lr.SetPosition(i, points[i]);

            _lr.colorGradient = valid ? _validGradient : _invalidGradient;
        }

        // ── Dotted mode ───────────────────────────────────────────────────
        // Walks the stored arc once, dropping a pooled dot every _dotSpacing units
        // starting at _phase (which Update() scrolls), tapering size + colour toward
        // the landing. Spare dots are deactivated.
        private void DrawDots()
        {
            var pts = _arcPoints;
            if (pts == null || pts.Count < 2) { HideDots(); return; }

            float total = 0f;
            for (int i = 1; i < pts.Count; i++) total += Vector3.Distance(pts[i - 1], pts[i]);
            if (total < 0.01f || _dotSpacing < 0.01f) { HideDots(); return; }

            Color red = new Color(1f, 0.32f, 0.30f);

            int   used     = 0;
            int   seg      = 1;
            float segStart = 0f;
            float segLen   = Vector3.Distance(pts[0], pts[1]);

            for (float dist = _phase; dist <= total && used < MaxDots; dist += _dotSpacing)
            {
                while (seg < pts.Count - 1 && dist > segStart + segLen)
                {
                    segStart += segLen;
                    seg++;
                    segLen = Vector3.Distance(pts[seg - 1], pts[seg]);
                }

                float   f   = segLen > 0.0001f ? (dist - segStart) / segLen : 0f;
                Vector3 pos = Vector3.Lerp(pts[seg - 1], pts[seg], f);
                float   t   = dist / total;

                float scale = Mathf.Lerp(_dotStartScale, _dotEndScale, t);
                Color c = _arcValid
                    ? Color.Lerp(Color.white, _lineColor, Mathf.SmoothStep(0f, 1f, t * 1.6f))
                    : red;

                PlaceDot(used++, pos, scale, c);
            }

            for (int i = used; i < _dots.Count; i++)
                if (_dots[i]) _dots[i].gameObject.SetActive(false);
        }

        private void PlaceDot(int i, Vector3 pos, float scale, Color color)
        {
            while (_dots.Count <= i) CreateDot();

            var tr = _dots[i];
            if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
            tr.position   = pos;
            tr.localScale = Vector3.one * scale;

            _mpb ??= new MaterialPropertyBlock();
            var mr = _dotRenderers[i];
            mr.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", color);   // URP Unlit
            _mpb.SetColor("_Color",     color);   // legacy Unlit/Color fallback
            mr.SetPropertyBlock(_mpb);
        }

        private void CreateDot()
        {
            if (_dotRoot == null)
            {
                _dotRoot = new GameObject("AimDots").transform;
                _dotRoot.SetParent(transform, false);
            }
            if (_dotMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color");
                _dotMat = new Material(sh);
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Dot_{_dots.Count}";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_dotRoot, false);

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial     = _dotMat;
            mr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows     = false;

            _dots.Add(go.transform);
            _dotRenderers.Add(mr);
        }

        private void HideDots()
        {
            foreach (var d in _dots)
                if (d && d.gameObject.activeSelf) d.gameObject.SetActive(false);
        }

        private void HighlightLanding(bool inGrid, int gx, int gy)
        {
            if (_grid == null) return;
            _grid.ClearHighlights();
            if (!inGrid) return;   // off-grid shot: no landing highlight (and won't fire)

            // Mark the landing cell itself.
            _grid.SetHighlight(gx, gy, true);

            // Ghost-raise every cell this ball would actually paint, in its real
            // fill colour — so the player reads the painted footprint (and how many
            // cells it covers) directly, before firing.
            var current = _queue?.Current;
            if (current == null) return;

            foreach (var c in PaintingSystem.Preview(_grid, gx, gy, current))
                _grid.SetPreview(c.x, c.y, true);
        }

        private void ClearPreview()
        {
            if (_lr)   _lr.enabled = false;
            if (_grid) _grid.ClearHighlights();
            _dotsActive = false;
            HideDots();
        }
    }
}
