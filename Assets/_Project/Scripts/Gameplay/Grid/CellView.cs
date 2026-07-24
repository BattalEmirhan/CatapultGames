using System.Collections;
using UnityEngine;

namespace CatapultGames
{
    // Each grid cell is a 3-D cube.
    //   Unfilled → flat thin plate (height = ThinH), muted colour
    //   Filled   → full cube (height = FullH) rises with a punch animation
    [DisallowMultipleComponent]
    public class CellView : MonoBehaviour
    {
        public int       GridX        { get; private set; }
        public int       GridY        { get; private set; }
        public CellColor OutlineColor { get; private set; }
        public bool      IsFilled     { get; private set; }

        private MeshRenderer _mr;
        private Material     _mat;        // per-cell instance (animates its own colour)
        private float        _baseSide;   // resting XZ cube width (for squash & stretch)

        private const float ThinH     = 0.14f;  // height when unfilled
        private const float FullH     = 0.80f;  // height when filled
        private const float CubeGap   = 0.92f;  // fraction of cellSize (leaves gap between cubes)

        // Per-cell materials all share ONE shader, so URP's SRP Batcher folds the whole
        // grid into a single batch — efficient without a MaterialPropertyBlock (an MPB
        // would actually break SRP-batcher compatibility for these renderers).
        private static Material _sharedBase;

        // ─── Factory ──────────────────────────────────────────────────────
        public static CellView Create(Transform parent, int gx, int gy,
                                      float cellSize, CellColor color, bool filled)
        {
            var go = new GameObject($"Cell_{gx}_{gy}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(gx * cellSize, 0f, gy * cellSize);

            var view = go.AddComponent<CellView>();
            view.Init(gx, gy, cellSize, color, filled);
            return view;
        }

        // ─── Init ─────────────────────────────────────────────────────────
        private void Init(int gx, int gy, float cellSize, CellColor color, bool filled)
        {
            GridX        = gx;
            GridY        = gy;
            OutlineColor = color;
            IsFilled     = filled;

            int gridLayer = LayerMask.NameToLayer("CG_Grid");
            if (gridLayer < 0) gridLayer = 0;
            gameObject.layer = gridLayer;

            EnsureBaseShader();
            _mat = new Material(_sharedBase);   // own instance; same shader → SRP-batched

            // ── Cube ──────────────────────────────────────────────────────
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name  = "CellCube";
            cube.layer = gridLayer;
            Destroy(cube.GetComponent<BoxCollider>());
            cube.transform.SetParent(transform, false);

            float side = cellSize * CubeGap;
            _baseSide  = side;
            cube.transform.localScale = new Vector3(side, ThinH, side);

            _mr = cube.GetComponent<MeshRenderer>();
            _mr.sharedMaterial    = _mat;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _mr.receiveShadows    = true;

            Refresh();  // sets colour + correct height/position
        }

        private static void EnsureBaseShader()
        {
            if (_sharedBase) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");
            _sharedBase = new Material(shader);
            if (_sharedBase.HasProperty("_Smoothness")) _sharedBase.SetFloat("_Smoothness", 0.20f);
            if (_sharedBase.HasProperty("_Metallic"))   _sharedBase.SetFloat("_Metallic",   0f);
        }

        // Single funnel for every colour change (refresh / highlight / preview / anim),
        // so the colour writes live in one place.
        private void SetColor(Color c)
        {
            if (_mat != null) _mat.color = c;
        }

        // ─── Public API ───────────────────────────────────────────────────
        public void SetFilled(bool filled)
        {
            if (IsFilled == filled) return;
            if (_previewing) SetPreview(false);   // stop any aim ghost before the real fill
            IsFilled = filled;
            Refresh();
            if (filled)
            {
                StartCoroutine(RisePunch());
                Color col = GameConstants.GetColorF(OutlineColor);
                GameFX.Instance.CellPop(transform.position + Vector3.up * (FullH * 0.6f), col);
            }
        }

        public void SetOutlineColor(CellColor color)
        {
            OutlineColor = color;
            Refresh();
        }

        // ─── Visual ───────────────────────────────────────────────────────
        private void Refresh()
        {
            if (_mr == null) return;

            Color32 c32    = GameConstants.GetColor(OutlineColor);
            Color   col    = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);
            bool    isNone = OutlineColor == CellColor.None;

            if (isNone)
            {
                SetColor(new Color(0.16f, 0.16f, 0.20f));  // dark slate base
                ApplyHeight(ThinH * 0.7f);
            }
            else if (IsFilled)
            {
                SetColor(col);
                ApplyHeight(FullH);
            }
            else
            {
                // Muted version of the outline colour (shows "this cell needs this colour")
                SetColor(Color.Lerp(col, new Color(0.08f, 0.09f, 0.14f), 0.62f));
                ApplyHeight(ThinH);
            }
        }

        // Sets cube Y scale and lifts it so the bottom sits at Y = 0
        private void ApplyHeight(float h) => ApplyScale(_baseSide, h);

        // Sets cube XZ width + height and keeps the bottom on Y = 0 (squash/stretch).
        private void ApplyScale(float xz, float h)
        {
            if (_mr == null) return;
            var t = _mr.transform;
            t.localScale    = new Vector3(xz, h, xz);
            t.localPosition = new Vector3(0f, h * 0.5f, 0f);
        }

        // ─── Highlight (aim preview) ──────────────────────────────────────
        public void SetHighlight(bool on)
        {
            if (_mr == null) return;
            if (on)
            {
                Color32 c32 = GameConstants.GetColor(OutlineColor);
                Color   col = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);
                SetColor(Color.Lerp(col, Color.white, 0.55f));
            }
            else
            {
                Refresh();
            }
        }

        // ─── Paint preview (aim) ──────────────────────────────────────────
        // A "ghost" of the filled cube: the cell rises part-way in its real fill
        // colour and gently breathes while the player aims, so the painted
        // footprint — and therefore how many cells this shot would fill — is read
        // at a glance, before firing. Toggled by AimPreview for every cell the
        // current ball would paint.
        private bool      _previewing;
        private Coroutine _previewCo;
        private const float PreviewH = FullH * 0.6f;   // ghost rises to 60% of a real fill

        public void SetPreview(bool on)
        {
            if (_mr == null || _previewing == on) return;
            _previewing = on;

            if (_previewCo != null) { StopCoroutine(_previewCo); _previewCo = null; }

            if (on) _previewCo = StartCoroutine(PreviewBreathe());
            else    Refresh();
        }

        private IEnumerator PreviewBreathe()
        {
            Color32 c32   = GameConstants.GetColor(OutlineColor);
            Color   col   = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);
            Color   ghost = Color.Lerp(col, Color.white, 0.45f);

            while (true)
            {
                float b = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);   // 0 → 1 breathing
                ApplyHeight(Mathf.Lerp(PreviewH * 0.80f, PreviewH, b));
                SetColor(Color.Lerp(col, ghost, b));
                yield return null;
            }
        }

        // ─── Fill animation ───────────────────────────────────────────────
        private IEnumerator RisePunch()
        {
            const float dur = 0.15f;   // snappier cube rise (was 0.22)
            float elapsed = 0f;

            Color32 c32  = GameConstants.GetColor(OutlineColor);
            Color   baseCol  = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);
            Color   flashCol = Color.Lerp(baseCol, Color.white, 0.7f);

            while (elapsed < dur)
            {
                float p = elapsed / dur;
                // Rise quickly → slight overshoot → settle
                float h = p < 0.65f
                    ? Mathf.Lerp(ThinH, FullH * 1.10f, p / 0.65f)
                    : Mathf.Lerp(FullH * 1.10f, FullH, (p - 0.65f) / 0.35f);
                // Squash & stretch: wide at the base, thins as it shoots up, settles back.
                float xz = p < 0.65f
                    ? Mathf.Lerp(_baseSide * 1.18f, _baseSide * 0.90f, p / 0.65f)
                    : Mathf.Lerp(_baseSide * 0.90f, _baseSide, (p - 0.65f) / 0.35f);
                ApplyScale(xz, h);

                // Bright flash that fades back to the solid fill color
                SetColor(Color.Lerp(flashCol, baseCol, p));

                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyHeight(FullH);
            SetColor(baseCol);
        }

        // ─── Celebration pulse (its colour was fully cleared) ─────────────
        // A quick bounce + bright flash on an already-filled cell, to make the
        // whole colour feel alive once it's complete. `delay` staggers a ripple.
        public void Pulse(float delay = 0f) => StartCoroutine(PulseRoutine(delay));

        private IEnumerator PulseRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (_mr == null) yield break;

            Color32 c32      = GameConstants.GetColor(OutlineColor);
            Color   baseCol  = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);
            Color   flashCol = Color.Lerp(baseCol, Color.white, 0.75f);

            const float dur = 0.34f;
            float t = 0f;
            while (t < dur)
            {
                float p     = t / dur;
                float punch = Mathf.Sin(p * Mathf.PI);            // 0 → 1 → 0
                ApplyScale(_baseSide * (1f + 0.12f * punch),       // swell out
                           FullH     * (1f + 0.40f * punch));      // bounce up
                SetColor(Color.Lerp(baseCol, flashCol, punch));
                t += Time.deltaTime;
                yield return null;
            }

            ApplyScale(_baseSide, FullH);
            SetColor(baseCol);
        }

        private void OnDestroy()
        {
            if (_mat) Destroy(_mat);
        }
    }
}
