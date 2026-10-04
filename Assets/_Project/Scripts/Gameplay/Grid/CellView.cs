using System.Collections;
using UnityEngine;

namespace CatapultGames
{
    // Each grid cell is a 3-D cube.
    //   Unfilled → flat thin plate (height = ThinH), muted colour
    //   Filled   → full cube (height = FullH) rises with a punch animation
    //
    // Special types (CellType) ride on the same cube: Ice needs two hits and shows
    // a cracked mid-height state after the first, Stone is a permanent block that
    // never fills, Joker reads as a pale wildcard. All of it resolves in Refresh(),
    // so the aim states keep working unchanged.
    [DisallowMultipleComponent]
    public sealed class CellView : MonoBehaviour
    {
        public int       GridX        { get; private set; }
        public int       GridY        { get; private set; }
        public CellColor OutlineColor { get; private set; }
        public CellType  Type         { get; private set; }

        // Paint hits taken so far, and how many this cell needs (Ice: 2).
        // IsFilled is derived rather than stored, so "cracked" is not a third state
        // to keep in sync — it is simply 1 of 2 hits.
        public int  HitsTaken    { get; private set; }
        public int  HitsRequired => GameConstants.GetRequiredHits(Type);
        public bool IsFilled     => HitsTaken >= HitsRequired;

        // Does this cell want paint at all? Stone never does, and neither does bare
        // board — every progress, win and coverage count goes through this.
        public bool IsPaintTarget => Type != CellType.Stone && OutlineColor != CellColor.None;

        private MeshRenderer _mr;
        private Material     _mat;        // per-cell instance (animates its own colour)
        private float        _baseSide;   // resting XZ cube width (for squash & stretch)
        private const float ThinH     = 0.11f;  // height when unfilled
        private const float FullH     = 0.80f;  // height when filled
        private const float CubeGap   = 0.86f;  // fraction of cellSize (leaves gap between cubes)

        // How far an unfilled cell's colour is darkened toward the board — a
        // "socket" in a dim shade of its target colour. Resting cells stay quiet;
        // the colour currently selected in the tray reads much closer to its true
        // hue, which is what makes "what can I paint right now" answerable by
        // looking at the board instead of at the tray.
        //
        // Dark theme again (2026-10-04, as before the light-board pass): the pale
        // board and its white wash made every colour read washed out. Measured
        // against the dark board, 0.35 keeps the closest empty pair at ΔE 39
        // (filled cubes: ΔE 52); push it higher and orange/yellow start to merge.
        private const float RestingMute  = 0.35f;
        private const float AwaitingMute = 0.12f;
        private static readonly Color DarkTint      = new Color(0.08f, 0.09f, 0.14f);   // the board's own tone
        private static readonly Color EmptyBase     = new Color(0.16f, 0.16f, 0.20f);   // bare socket
        private static readonly Color FootprintTint = new Color(0.55f, 0.78f, 1f);

        // Special-type tints. Each one has to be recognisable at a glance in
        // perspective on a phone, so they differ in HEIGHT as well as colour.
        private static readonly Color IceTint   = new Color(0.72f, 0.92f, 1f);
        private static readonly Color StoneTint = new Color(0.40f, 0.40f, 0.45f);

        // Rounded "toy block" mesh shared by every cell (see RoundedCubeMesh).
        private const float CornerRadius = 0.14f;
        private const float StoneH   = FullH * 0.62f;   // a block, clearly not a filled cell
        private const float CrackedH = ThinH * 3.2f;    // ice, one hit in: visibly half-risen

        // Per-cell materials all share ONE shader, so URP's SRP Batcher folds the whole
        // grid into a single batch — efficient without a MaterialPropertyBlock (an MPB
        // would actually break SRP-batcher compatibility for these renderers).
        private static Material _sharedBase;

        // Set on the cells the stamp covers but does NOT paint. Together with the
        // paint ghosts below, the player sees the whole stamp centred on the
        // aimed cell — the shape of the throw, before the throw.
        private bool _footprint;

        private bool _awaiting;

        // A "ghost" of the filled cube: the cell rises part-way in its real fill
        // colour and gently breathes while the player aims, so the painted
        // footprint — and therefore how many cells this shot would fill — is read
        // at a glance, before firing. Toggled by AimPreview for every cell the
        // current ball would paint.
        private bool      _previewing;
        private Coroutine _previewCo;
        private const float PreviewH = FullH * 0.6f;   // ghost rises to 60% of a real fill

        private void OnDestroy()
        {
            if (_mat)
                Destroy(_mat);
        }

        public static CellView Create(Transform parent, int gx, int gy, float cellSize,
                                      CellColor color, bool filled, CellType type, MaterialSet materials)
        {
            var go = new GameObject($"Cell_{gx}_{gy}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(gx * cellSize, 0f, gy * cellSize);

            var view = go.AddComponent<CellView>();
            view.Init(gx, gy, color, filled, type);
            view.BuildCube(cellSize, materials);
            return view;
        }

        // Jump straight to filled or empty — level load, and clearing a cell whole.
        // Compares HIT COUNTS, not IsFilled: clearing a cracked Ice cell has to reset
        // its hit, and by IsFilled alone that cell already looks "not filled".
        public void SetFilled(bool filled)
        {
            if (Type == CellType.Stone)
                return;          // stone is never a target

            int target = filled ? HitsRequired : 0;
            if (HitsTaken == target)
                return;
            SetHits(target, animate: filled);
        }

        // One stamp's worth of paint. Ice cracks on the first hit and fills on the
        // second; every other type fills at once. Returns true when THIS hit
        // completed the cell, which is what progress and win checks count.
        public bool AddHit()
        {
            if (Type == CellType.Stone || IsFilled)
                return false;
            SetHits(HitsTaken + 1, animate: true);
            return IsFilled;
        }

        // Exact inverse of AddHit, for undo: a filled cell drops back a hit, a
        // cracked ice cell goes back to intact. Painting only ever adds hits, so
        // removing the same ones puts the cell back where it was.
        public void RemoveHit()
        {
            if (Type == CellType.Stone || HitsTaken <= 0)
                return;
            SetHits(HitsTaken - 1, animate: false);
        }

        public void SetOutlineColor(CellColor color)
        {
            OutlineColor = color;
            Refresh();
        }

        public void SetHighlight(bool on)
        {
            if (_mr == null)
                return;
            if (on)
            {
                Color32 c32 = GameConstants.GetColor(OutlineColor);
                Color   col = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);
                SetColor(Color.Lerp(col, Color.white, 0.55f));
            }
            else
                Refresh();
        }

        public void SetFootprint(bool on)
        {
            if (_mr == null || _footprint == on)
                return;
            _footprint = on;
            if (!_previewing)
                Refresh();   // the ghost coroutine owns the look while it runs
        }

        public void SetAwaiting(bool on)
        {
            if (_mr == null || _awaiting == on)
                return;
            _awaiting = on;
            if (!_previewing)
                Refresh();
        }

        public void SetPreview(bool on)
        {
            if (_mr == null || _previewing == on)
                return;
            _previewing = on;

            if (_previewCo != null)
            {
                StopCoroutine(_previewCo);
                _previewCo = null;
            }

            if (on)
                _previewCo = StartCoroutine(PreviewBreathe());
            else
                Refresh();
        }

        // A quick bounce + bright flash on an already-filled cell, to make the
        // whole colour feel alive once it's complete. `delay` staggers a ripple.
        public void Pulse(float delay = 0f) => StartCoroutine(PulseRoutine(delay));

        private void Init(int gx, int gy, CellColor color, bool filled, CellType type)
        {
            GridX        = gx;
            GridY        = gy;
            OutlineColor = color;
            Type         = type;
            HitsTaken    = filled && type != CellType.Stone ? GameConstants.GetRequiredHits(type) : 0;

            int gridLayer = LayerMask.NameToLayer("CG_Grid");
            gameObject.layer = gridLayer < 0 ? 0 : gridLayer;
        }

        // A rounded block rather than a hard-edged primitive: the bevel is what
        // makes the board read as toy pieces instead of a spreadsheet. Each cell
        // owns a material instance of one shared base, so SRP batching still holds.
        private void BuildCube(float cellSize, MaterialSet materials)
        {
            EnsureBaseMaterial(materials);
            _mat = new Material(_sharedBase);

            var cube = new GameObject("CellCube") { layer = gameObject.layer };
            cube.transform.SetParent(transform, false);
            cube.AddComponent<MeshFilter>().sharedMesh = RoundedCubeMesh.Get(CornerRadius, 4);
            _baseSide = cellSize * CubeGap;
            cube.transform.localScale = new Vector3(_baseSide, ThinH, _baseSide);

            _mr = cube.AddComponent<MeshRenderer>();
            _mr.sharedMaterial    = _mat;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            _mr.receiveShadows    = true;
            Refresh();
        }

        private static void EnsureBaseMaterial(MaterialSet materials)
        {
            if (_sharedBase)
                return;
            _sharedBase = new Material(materials.Lit);
            // Low gloss: a strong specular highlight paints a white patch over the
            // colour, which is exactly what the player has to read.
            if (_sharedBase.HasProperty("_Smoothness"))
                _sharedBase.SetFloat("_Smoothness", 0.22f);
            if (_sharedBase.HasProperty("_Metallic"))
                _sharedBase.SetFloat("_Metallic",   0f);
        }

        // Single funnel for every colour change (refresh / highlight / preview / anim),
        // so the colour writes live in one place.
        private void SetColor(Color c)
        {
            if (_mat != null)
                _mat.color = c;
        }

        private void SetHits(int hits, bool animate)
        {
            bool wasFilled = IsFilled;
            if (_previewing)
                SetPreview(false);   // drop the aim ghost before a real change

            HitsTaken = Mathf.Clamp(hits, 0, HitsRequired);
            Refresh();

            if (!animate)
                return;

            if (IsFilled && !wasFilled)
            {
                StartCoroutine(RisePunch());
                GameFX.Instance.CellPop(transform.position + Vector3.up * (FullH * 0.6f),
                                        GameConstants.GetColorF(OutlineColor));
            }
            else if (!IsFilled)
            {
                StartCoroutine(CrackPunch());     // ice took a hit and is still standing
            }
        }

        // Every non-animated look resolves here, so the transient aim states can
        // all return by simply calling Refresh() again.
        private void Refresh()
        {
            if (_mr == null)
                return;

            Color32 c32    = GameConstants.GetColor(OutlineColor);
            Color   col    = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);
            bool    isNone = OutlineColor == CellColor.None;

            Color colour;
            float height;

            if (Type == CellType.Stone)
            {
                // A wall, not a target. Grey and chunky so it reads as an obstacle
                // at the camera's tilt, and unaffected by everything below.
                colour = StoneTint;
                height = StoneH;
            }
            else if (isNone)
            {
                colour = EmptyBase;              // bare socket, a shade above the dark plate
                height = ThinH * 0.7f;
            }
            else if (IsFilled)
            {
                colour = col;
                height = FullH;
            }
            else
            {
                // Pale wash of the outline colour (shows "this cell needs this
                // colour"), lifted a step while that colour is the one selected.
                colour = Color.Lerp(col, DarkTint, _awaiting ? AwaitingMute : RestingMute);
                height = _awaiting ? ThinH * 1.5f : ThinH;

                if (Type == CellType.Ice)
                {
                    // Frosted over its target colour. Once cracked it sits half-risen,
                    // so "this one needs a second ball" is readable without a counter.
                    bool cracked = HitsTaken > 0;
                    colour = Color.Lerp(colour, IceTint, cracked ? 0.72f : 0.52f);
                    height = cracked ? CrackedH : Mathf.Max(height, ThinH * 1.25f);
                }
                else if (Type == CellType.Joker)
                {
                    // Washed toward white: any colour is allowed to spend itself here.
                    colour = Color.Lerp(colour, Color.white, 0.55f);
                    height = Mathf.Max(height, ThinH * 1.7f);
                }
            }

            // The aimed stamp covers this cell but will not paint it — wrong colour,
            // already filled, or bare board. Shown so the player reads where the
            // stamp actually sits, not just the part of it that pays off.
            if (_footprint)
            {
                colour = Color.Lerp(colour, FootprintTint, IsFilled ? 0.26f : 0.50f);
                if (!IsFilled)
                    height = Mathf.Max(height, ThinH * 2.1f);
            }

            SetColor(colour);
            ApplyHeight(height);
        }

        // Sets cube Y scale and lifts it so the bottom sits at Y = 0
        private void ApplyHeight(float h) => ApplyScale(_baseSide, h);

        // Sets cube XZ width + height and keeps the bottom on Y = 0 (squash/stretch).
        private void ApplyScale(float xz, float h)
        {
            if (_mr == null)
                return;
            var t = _mr.transform;
            t.localScale    = new Vector3(xz, h, xz);
            t.localPosition = new Vector3(0f, h * 0.5f, 0f);
        }

        private IEnumerator PreviewBreathe()
        {
            Color32 c32   = GameConstants.GetColor(OutlineColor);
            Color   col   = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);
            Color   ghost = Color.Lerp(col, Color.white, 0.45f);

            // An Ice cell that this shot would only crack rises to the cracked height
            // instead — the preview promises exactly what the ball delivers, which is
            // the whole reason the aim ghost is trusted.
            bool  fillsIt = HitsTaken + 1 >= HitsRequired;
            float top     = fillsIt ? PreviewH : CrackedH;
            if (!fillsIt)
                col = Color.Lerp(col, IceTint, 0.55f);

            while (true)
            {
                float b = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);   // 0 → 1 breathing
                ApplyHeight(Mathf.Lerp(top * 0.80f, top, b));
                SetColor(Color.Lerp(col, ghost, b));
                yield return null;
            }
        }

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

        // A hard squash that springs back, with a white flash — the ball clearly
        // did something, it just wasn't enough. Deliberately unlike RisePunch, so
        // "cracked" is never mistaken for "filled" out of the corner of the eye.
        private IEnumerator CrackPunch()
        {
            const float dur = 0.18f;
            float restH = _mr != null ? _mr.transform.localScale.y : ThinH;
            float t     = 0f;

            Color32 c32   = GameConstants.GetColor(OutlineColor);
            Color   baseCol  = _mat != null ? _mat.color : Color.white;
            Color   flashCol = Color.Lerp(new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f),
                                          Color.white, 0.80f);

            while (t < dur)
            {
                float p     = t / dur;
                float punch = Mathf.Sin(p * Mathf.PI);              // 0 → 1 → 0
                ApplyScale(_baseSide * (1f + 0.16f * punch),        // squash outward
                           restH     * (1f - 0.35f * punch));       // and downward
                SetColor(Color.Lerp(baseCol, flashCol, punch));
                t += Time.deltaTime;
                yield return null;
            }

            Refresh();   // back to whatever the cell's state says it looks like
        }

        private IEnumerator PulseRoutine(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            if (_mr == null)
                yield break;

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
    }
}
