using System.Collections;
using UnityEngine;

namespace CatapultGames
{
    // One grid cell, drawn the block-match way: flat sprites stacked in layers,
    // lying on the board plane (TileArt draws them).
    //
    //   Socket — the checkered board square, always there (dark gap for Stone)
    //   Marker — empty target: a small flat square in the TRUE target colour, so
    //            "this cell wants yellow" reads at a glance and is never confused
    //            with a filled cell
    //   Piece  — the glossy tile: shown filled, or as the breathing aim ghost
    //   Ice    — frosted overlay while an Ice cell still needs hits
    //
    // Special types (CellType) ride on the same layers: Ice needs two hits (the
    // frost thins after the first), Stone is a hole that never fills, Joker shows
    // a white "any colour" marker. Every resting look resolves in Refresh(), so the
    // aim states only set flags and call it again.
    [DisallowMultipleComponent]
    public class CellView : MonoBehaviour
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

        // ── Layers ────────────────────────────────────────────────────────
        // Sorting orders, not depth, decide the stacking: every socket draws before
        // every marker before every piece, whatever the camera's angle.
        private const int SocketOrder = -10;
        private const int MarkerOrder = -5;
        private const int PieceOrder  = 0;
        private const int IceOrder    = 5;
        public  const int BoardOrder  = -20;   // GridBoard's panel, under everything

        private const float Padding       = 0.04f;   // gap between sockets, fraction of a cell
        private const float MarkerResting = 0.40f;   // marker size, fraction of a cell
        private const float MarkerAwaiting = 0.56f;  // …while its colour is the ball in hand
        private const float PieceLift     = 0.25f;   // how far a filling tile pops toward the camera

        private static readonly Color FootprintTint = new Color(0.75f, 0.85f, 1f);
        private static readonly Color HighlightTint = new Color(1f, 1f, 1f);

        private Transform      _visual;   // flat on the board plane; children are the layers
        private SpriteRenderer _socket, _marker, _piece, _ice;

        // ─── Factory ──────────────────────────────────────────────────────
        public static CellView Create(Transform parent, int gx, int gy, float cellSize,
                                      CellColor color, bool filled, CellType type = CellType.Normal)
        {
            var go = new GameObject($"Cell_{gx}_{gy}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(gx * cellSize, 0f, gy * cellSize);

            var view = go.AddComponent<CellView>();
            view.Init(gx, gy, cellSize, color, filled, type);
            return view;
        }

        // ─── Init ─────────────────────────────────────────────────────────
        private void Init(int gx, int gy, float cellSize, CellColor color, bool filled, CellType type)
        {
            GridX        = gx;
            GridY        = gy;
            OutlineColor = color;
            Type         = type;
            HitsTaken    = filled && type != CellType.Stone ? GameConstants.GetRequiredHits(type) : 0;

            int gridLayer = LayerMask.NameToLayer("CG_Grid");
            if (gridLayer < 0) gridLayer = 0;
            gameObject.layer = gridLayer;

            // Sprites live in their XY plane; tipped forward 90° they lie on the
            // board with their top edge pointing up the screen (+Z).
            var visual = new GameObject("Visual") { layer = gridLayer };
            visual.transform.SetParent(transform, false);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visual.transform.localScale    = Vector3.one * cellSize * (1f - Padding);
            _visual = visual.transform;

            _socket = AddLayer("Socket", TileArt.Socket(), SocketOrder, gridLayer);
            _socket.color = (gx + gy) % 2 == 0 ? TileArt.SocketA : TileArt.SocketB;
            _marker = AddLayer("Marker", TileArt.Marker(), MarkerOrder, gridLayer);
            _piece  = AddLayer("Piece",  TileArt.Tile(color), PieceOrder, gridLayer);
            _ice    = AddLayer("Ice",    TileArt.Ice(), IceOrder, gridLayer);

            Refresh();
        }

        private SpriteRenderer AddLayer(string name, Sprite sprite, int order, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(_visual, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.sortingOrder = order;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows    = false;
            return sr;
        }

        // ─── Public API ───────────────────────────────────────────────────
        // Jump straight to filled or empty — level load, and clearing a cell whole.
        // Compares HIT COUNTS, not IsFilled: clearing a cracked Ice cell has to reset
        // its hit, and by IsFilled alone that cell already looks "not filled".
        public void SetFilled(bool filled)
        {
            if (Type == CellType.Stone) return;          // stone is never a target

            int target = filled ? HitsRequired : 0;
            if (HitsTaken == target) return;
            SetHits(target, animate: filled);
        }

        // One stamp's worth of paint. Ice cracks on the first hit and fills on the
        // second; every other type fills at once. Returns true when THIS hit
        // completed the cell, which is what progress and win checks count.
        public bool AddHit()
        {
            if (Type == CellType.Stone || IsFilled) return false;
            SetHits(HitsTaken + 1, animate: true);
            return IsFilled;
        }

        // Exact inverse of AddHit, for undo: a filled cell drops back a hit, a
        // cracked ice cell goes back to intact. Painting only ever adds hits, so
        // removing the same ones puts the cell back where it was.
        public void RemoveHit()
        {
            if (Type == CellType.Stone || HitsTaken <= 0) return;
            SetHits(HitsTaken - 1, animate: false);
        }

        private void SetHits(int hits, bool animate)
        {
            bool wasFilled = IsFilled;
            if (_previewing) SetPreview(false);   // drop the aim ghost before a real change

            HitsTaken = Mathf.Clamp(hits, 0, HitsRequired);
            Refresh();

            if (!animate) return;

            if (IsFilled && !wasFilled)
            {
                StartCoroutine(PopIn());
                GameFX.Instance.CellPop(transform.position + Vector3.up * 0.3f,
                                        GameConstants.GetColorF(OutlineColor));
            }
            else if (!IsFilled)
            {
                StartCoroutine(CrackPunch());     // ice took a hit and is still standing
            }
        }

        public void SetOutlineColor(CellColor color)
        {
            OutlineColor = color;
            if (_piece) _piece.sprite = TileArt.Tile(color);
            Refresh();
        }

        // ─── Visual ───────────────────────────────────────────────────────
        // Every non-animated look resolves here, so the transient aim states can
        // all return by simply calling Refresh() again.
        private void Refresh()
        {
            if (_socket == null) return;

            bool stone  = Type == CellType.Stone;
            bool target = IsPaintTarget;

            // Socket: the board square. Stone is a hole in the board; the aimed
            // stamp's footprint (covered, but not painted) lightens its sockets so
            // the whole shape of the throw is visible.
            Color socket = (GridX + GridY) % 2 == 0 ? TileArt.SocketA : TileArt.SocketB;
            if (stone) socket = TileArt.Hole;
            if (_footprint) socket = Color.Lerp(socket, FootprintTint, IsFilled ? 0.25f : 0.45f);
            if (_highlight) socket = Color.Lerp(socket, HighlightTint, 0.55f);
            _socket.color = socket;

            // Marker: an empty target's colour, larger while that colour is in hand.
            bool showMarker = target && !IsFilled && !_previewing;
            _marker.enabled = showMarker;
            if (showMarker)
            {
                _marker.color = Type == CellType.Joker ? Color.white : GameConstants.GetColorF(OutlineColor);
                SetScale(_marker.transform, _awaiting ? MarkerAwaiting : MarkerResting);
            }

            // Piece: the glossy tile, once filled. (The aim ghost drives it itself.)
            if (!_previewing)
            {
                _piece.enabled = target && IsFilled;
                _piece.color   = Color.white;
                SetScale(_piece.transform, 1f);
                SetLift(0f);
            }

            // Ice: frost over the cell until it fills; thinner once cracked, so
            // "this one needs a second ball" reads without a counter.
            bool icy = Type == CellType.Ice && !IsFilled;
            _ice.enabled = icy;
            if (icy)
            {
                bool cracked = HitsTaken > 0;
                _ice.color = new Color(1f, 1f, 1f, cracked ? 0.45f : 0.9f);
                SetScale(_ice.transform, cracked ? 0.86f : 1f);
            }
        }

        private static void SetScale(Transform t, float s) => t.localScale = new Vector3(s, s, 1f);

        // Toward the camera: the visual is tipped 90°, so its local -Z is world up.
        private void SetLift(float worldUp)
        {
            float local = _visual != null ? worldUp / Mathf.Max(0.001f, _visual.localScale.x) : worldUp;
            _piece.transform.localPosition = new Vector3(0f, 0f, -local);
        }

        // ─── Highlight (aim: the landing cell) ────────────────────────────
        private bool _highlight;

        public void SetHighlight(bool on)
        {
            if (_socket == null || _highlight == on) return;
            _highlight = on;
            Refresh();
        }

        // ─── Stamp footprint (aim) ────────────────────────────────────────
        // Set on the cells the stamp covers but does NOT paint. Together with the
        // paint ghosts below, the player sees the whole stamp centred on the
        // aimed cell — the shape of the throw, before the throw.
        private bool _footprint;

        public void SetFootprint(bool on)
        {
            if (_socket == null || _footprint == on) return;
            _footprint = on;
            Refresh();
        }

        // ─── Awaiting (this cell's colour is loaded right now) ────────────
        private bool _awaiting;

        public void SetAwaiting(bool on)
        {
            if (_socket == null || _awaiting == on) return;
            _awaiting = on;
            Refresh();
        }

        // ─── Paint preview (aim) ──────────────────────────────────────────
        // A ghost of the filled tile, breathing while the player aims, on every
        // cell the current ball would paint — how many cells this shot fills is
        // read before firing. An Ice cell this shot would only crack shows the
        // ghost under its frost instead: the preview promises exactly what the
        // ball delivers, which is why the aim ghost is trusted.
        private bool      _previewing;
        private Coroutine _previewCo;

        public void SetPreview(bool on)
        {
            if (_socket == null || _previewing == on) return;
            _previewing = on;

            if (_previewCo != null) { StopCoroutine(_previewCo); _previewCo = null; }

            Refresh();
            if (on) _previewCo = StartCoroutine(PreviewBreathe());
        }

        private IEnumerator PreviewBreathe()
        {
            bool fillsIt = HitsTaken + 1 >= HitsRequired;
            _piece.enabled = true;
            SetLift(0f);

            while (true)
            {
                float b = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);   // 0 → 1 breathing
                _piece.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 0.85f, b) * (fillsIt ? 1f : 0.6f));
                SetScale(_piece.transform, Mathf.Lerp(0.84f, 0.94f, b));
                yield return null;
            }
        }

        // ─── Fill animation ───────────────────────────────────────────────
        // The tile pops in from the marker's size, overshoots and settles, rising
        // a touch toward the camera on the way — the "placed piece" feel.
        private IEnumerator PopIn()
        {
            const float dur = 0.18f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float p = t / dur;
                float s = p < 0.6f ? Mathf.Lerp(MarkerResting, 1.15f, p / 0.6f)
                                   : Mathf.Lerp(1.15f, 1f, (p - 0.6f) / 0.4f);
                SetScale(_piece.transform, s);
                SetLift(PieceLift * Mathf.Sin(p * Mathf.PI));
                yield return null;
            }
            SetScale(_piece.transform, 1f);
            SetLift(0f);
        }

        // ─── Ice crack (a hit that did not fill) ──────────────────────────
        // A quick shake of the frost — the ball clearly did something, it just
        // wasn't enough. Deliberately unlike PopIn, so "cracked" is never mistaken
        // for "filled" out of the corner of the eye.
        private IEnumerator CrackPunch()
        {
            const float dur = 0.2f;
            var ice = _ice.transform;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float p = t / dur;
                float punch = Mathf.Sin(p * Mathf.PI);
                SetScale(ice, 0.86f * (1f + 0.18f * punch));
                ice.localRotation = Quaternion.Euler(0f, 0f, 9f * Mathf.Sin(p * Mathf.PI * 3f) * (1f - p));
                yield return null;
            }
            ice.localRotation = Quaternion.identity;
            Refresh();   // back to whatever the cell's state says it looks like
        }

        // ─── Celebration pulse (its colour was fully cleared) ─────────────
        // A quick bounce on an already-filled tile, to make the whole colour feel
        // alive once it's complete. `delay` staggers a ripple.
        public void Pulse(float delay = 0f) => StartCoroutine(PulseRoutine(delay));

        private IEnumerator PulseRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (_piece == null || !_piece.enabled) yield break;

            const float dur = 0.34f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float punch = Mathf.Sin(t / dur * Mathf.PI);   // 0 → 1 → 0
                SetScale(_piece.transform, 1f + 0.14f * punch);
                SetLift(PieceLift * 0.6f * punch);
                yield return null;
            }
            SetScale(_piece.transform, 1f);
            SetLift(0f);
        }
    }
}
