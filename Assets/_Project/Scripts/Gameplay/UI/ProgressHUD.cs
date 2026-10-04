using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames
{
    // Per-colour progress: a bar for every colour in the level, plus one total.
    //
    // Was a block of "Colour  filled / total" text, which nobody reads mid-shot —
    // the numbers only tell you where you stand if you stop and add them up. Bars
    // answer "how close am I, and in which colour" at a glance.
    //
    // Rows are built from code (levels vary in colour count) and only rebuilt when
    // the SET of colours changes; a normal refresh just moves the fills.
    //
    // Subscribe to GridRenderer.OnGridChanged at runtime.
    [RequireComponent(typeof(RectTransform))]
    public sealed class ProgressHUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;   // the running total
        [SerializeField] private GridRenderer    grid;

        private const float RowHeight  = 62f;
        private const float BarsTop    = -230f;  // clears the total label's band above
        private const float SwatchSize = 35f;
        private const float BarLeft    = 49f;
        private const float BarRight   = 130f;    // room for the "12/34" readout
        private const float BarHeight  = 19f;
        private const float PanelWidth = 567f;
        private readonly List<ProgressHudRow> _rows = new();
        private bool      _configured;
        private int       _prevFilled = -1;     // total filled last refresh (for the pop)
        private Vector3   _labelRest  = Vector3.one;
        private Coroutine _punch;
        private bool      _dirty;               // grid changed → refresh once next LateUpdate
        private RectTransform _barsRoot;

        private void Awake()
        {
            // The builder parents this under the safe-area rect but adds a plain
            // MonoBehaviour, so without RequireComponent there was no RectTransform
            // here and the children anchored against a degenerate parent. Stretch to
            // fill the safe area so child anchors mean what they say.
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private void OnEnable()  {
            if (grid)
                grid.OnGridChanged += MarkDirty;
        }
        private void Start() => Refresh();

        private void LateUpdate()
        {
            if (!_dirty)
                return;
            _dirty = false;
            Refresh();
        }

        private void OnDisable() {
            if (grid)
                grid.OnGridChanged -= MarkDirty;
        }

        // Called by LevelLoader.Apply so the count resets for each new level
        public void Bind(GridRenderer grid)
        {
            if (this.grid)
                this.grid.OnGridChanged -= MarkDirty;
            this.grid = grid;
            if (this.grid)
                this.grid.OnGridChanged += MarkDirty;
            _prevFilled = -1;   // fresh baseline — don't pop on level load
            Refresh();
        }

        // Coalesce many same-frame grid changes (a paint wave fills cells one by one)
        // into a single rebuild per frame instead of one full rebuild + alloc per cell.
        private void MarkDirty() => _dirty = true;

        private void ConfigureLabel()
        {
            if (_configured || label == null)
                return;
#pragma warning disable CS0618 // enableWordWrapping is obsolete but still works across TMP versions
            label.enableWordWrapping = false;
#pragma warning restore CS0618
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment    = TextAlignmentOptions.TopLeft;
            _labelRest          = label.rectTransform.localScale;
            _configured = true;
        }

        private void Refresh()
        {
            if (grid == null)
                return;
            ConfigureLabel();

            var progress = grid.CountByColor();
            EnsureRows(progress);

            int totalFilled = 0, totalCells = 0;
            for (int i = 0; i < progress.Count && i < _rows.Count; i++)
            {
                ColorProgress p = progress[i];
                totalFilled += p.filled;
                totalCells  += p.total;

                ProgressHudRow row = _rows[i];
                float ratio = p.total > 0 ? (float)p.filled / p.total : 0f;

                // Width by anchor, so the fill tracks the bar at any screen size.
                if (row.fill != null)
                    row.fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
                if (row.count != null)
                    row.count.text = $"{p.filled}/{p.total}";
            }

            if (label != null)
                label.text = totalCells > 0 ? $"{totalFilled} / {totalCells}" : string.Empty;

            // Pop the counter whenever progress goes up.
            if (_prevFilled >= 0 && totalFilled > _prevFilled && isActiveAndEnabled && label != null)
            {
                if (_punch != null)
                    StopCoroutine(_punch);
                _punch = StartCoroutine(PunchLabel());
            }
            _prevFilled = totalFilled;
        }

        // CountByColor returns colours in enum order, so a level's row order is
        // stable and only the SET of colours can change (a colour is never removed
        // mid-level). Rebuild only then.
        private void EnsureRows(List<ColorProgress> progress)
        {
            bool same = _rows.Count == progress.Count;
            if (same)
                for (int i = 0; i < progress.Count; i++)
                    if (_rows[i].color != progress[i].color)
                    {
                        same = false;
                        break;
                    }

            if (same)
                return;

            if (_barsRoot == null)
                _barsRoot = MakeRect("ProgressBars", (RectTransform)transform);
            for (int i = _barsRoot.childCount - 1; i >= 0; i--)
                Destroy(_barsRoot.GetChild(i).gameObject);
            _rows.Clear();

            _barsRoot.anchorMin        = new Vector2(0f, 1f);
            _barsRoot.anchorMax        = new Vector2(0f, 1f);
            _barsRoot.pivot            = new Vector2(0f, 1f);
            _barsRoot.anchoredPosition = new Vector2(32f, BarsTop);
            _barsRoot.sizeDelta        = new Vector2(PanelWidth, RowHeight * progress.Count);

            for (int i = 0; i < progress.Count; i++)
                _rows.Add(BuildRow(progress[i].color, i));
        }

        private ProgressHudRow BuildRow(CellColor color, int index)
        {
            Color tint = Legible(GameConstants.GetColor(color));

            var row = MakeRect($"Row_{color}", _barsRoot);
            row.anchorMin        = new Vector2(0f, 1f);
            row.anchorMax        = new Vector2(1f, 1f);
            row.pivot            = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(0f, -index * RowHeight);
            row.sizeDelta        = new Vector2(0f, RowHeight);

            // Colour swatch — identity never rests on the bar's colour alone, since
            // two palette hues can read alike at bar width on a small screen.
            var swatch = MakeRect("Swatch", row);
            swatch.anchorMin = swatch.anchorMax = new Vector2(0f, 0.5f);
            swatch.pivot     = new Vector2(0f, 0.5f);
            swatch.sizeDelta = new Vector2(SwatchSize, SwatchSize);
            AddImage(swatch, tint);

            // Track
            var track = MakeRect("Track", row);
            track.anchorMin = new Vector2(0f, 0.5f);
            track.anchorMax = new Vector2(1f, 0.5f);
            track.pivot     = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(BarLeft,   -BarHeight * 0.5f);
            track.offsetMax = new Vector2(-BarRight,  BarHeight * 0.5f);
            AddImage(track, new Color(1f, 1f, 1f, 0.13f));

            // Fill — width driven by anchorMax.x in Refresh
            var fill = MakeRect("Fill", track);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot     = new Vector2(0f, 0.5f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            AddImage(fill, tint);

            // Readout
            var countGo = new GameObject("Count", typeof(RectTransform));
            var countRt = (RectTransform)countGo.transform;
            countRt.SetParent(row, false);
            countRt.anchorMin = countRt.anchorMax = new Vector2(1f, 0.5f);
            countRt.pivot     = new Vector2(1f, 0.5f);
            countRt.sizeDelta = new Vector2(BarRight - 8f, RowHeight);
            countRt.anchoredPosition = Vector2.zero;

            var tmp = countGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize  = 38;
            tmp.color     = tint;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
#pragma warning disable CS0618
            tmp.enableWordWrapping = false;
#pragma warning restore CS0618

            return new ProgressHudRow { color = color, fill = fill, count = tmp };
        }

        private static RectTransform MakeRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        // An Image with no sprite draws a plain coloured quad — which is all a bar
        // needs, and avoids shipping sprite assets for the HUD.
        private static void AddImage(RectTransform rt, Color color)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color       = color;
            img.raycastTarget = false;   // the HUD must never eat aim gestures
        }

        private IEnumerator PunchLabel()
        {
            const float dur = 0.10f; // faster pop
            var rt = label.rectTransform;
            float t = 0f;
            while (t < dur)
            {
                rt.localScale = _labelRest * Mathf.Lerp(1.22f, 1f, t / dur);
                t += Time.deltaTime;
                yield return null;
            }
            rt.localScale = _labelRest;
            _punch = null;
        }

        // Lifts very dark colours (e.g. the charcoal "Black") toward white so they
        // stay readable on the dark HUD.
        private static Color Legible(Color32 c)
        {
            float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            Color col = new Color(c.r / 255f, c.g / 255f, c.b / 255f);
            return lum < 0.5f ? Color.Lerp(col, Color.white, 0.5f - lum) : col;
        }
    }
}
