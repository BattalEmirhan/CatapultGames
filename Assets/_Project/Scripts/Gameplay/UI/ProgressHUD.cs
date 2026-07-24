using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

namespace CatapultGames
{
    // Shows per-color progress: for every colour in the level, how many of its
    // cubes already exist and how many are needed in total ("filled / total").
    // Subscribe to GridRenderer.OnGridChanged at runtime.
    public class ProgressHUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private GridRenderer    _grid;

        private readonly StringBuilder _sb = new();
        private bool      _configured;
        private int       _prevFilled = -1;     // total filled last refresh (for the pop)
        private Vector3   _labelRest  = Vector3.one;
        private Coroutine _punch;
        private bool      _dirty;               // grid changed → refresh once next LateUpdate

        private void OnEnable()  { if (_grid) _grid.OnGridChanged += MarkDirty; }
        private void OnDisable() { if (_grid) _grid.OnGridChanged -= MarkDirty; }
        private void Start() => Refresh();

        // Coalesce many same-frame grid changes (a paint wave fills cells one by one)
        // into a single rebuild per frame instead of one full rebuild + alloc per cell.
        private void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            Refresh();
        }

        // The original label was a tiny single-number box, so multi-line per-colour
        // text would wrap one char per line. Force no-wrap, overflow, top-left.
        private void ConfigureLabel()
        {
            if (_configured || _label == null) return;
#pragma warning disable CS0618 // enableWordWrapping is obsolete but still works across TMP versions
            _label.enableWordWrapping = false;
#pragma warning restore CS0618
            _label.overflowMode = TextOverflowModes.Overflow;
            _label.alignment    = TextAlignmentOptions.TopLeft;
            _labelRest          = _label.rectTransform.localScale;
            _configured = true;
        }

        // Called by LevelLoader.Apply so the count resets for each new level
        public void Bind(GridRenderer grid)
        {
            if (_grid) _grid.OnGridChanged -= MarkDirty;
            _grid = grid;
            if (_grid) _grid.OnGridChanged += MarkDirty;
            _prevFilled = -1;   // fresh baseline — don't pop on level load
            Refresh();
        }

        private void Refresh()
        {
            if (!_label || _grid == null) return;
            ConfigureLabel();

            var rows = _grid.CountByColor();
            if (rows.Count == 0) { _label.text = string.Empty; return; }

            // Each line: "<Colour>  filled / total", tinted with that colour.
            // Plain ASCII only so it renders with the default TMP font.
            _sb.Clear();
            int totalFilled = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                totalFilled += r.filled;
                _sb.Append("<color=#").Append(LegibleHex(GameConstants.GetColor(r.color))).Append('>')
                   .Append(r.color.ToString()).Append("  ")
                   .Append(r.filled).Append(" / ").Append(r.total)
                   .Append("</color>");
                if (i < rows.Count - 1) _sb.Append('\n');
            }
            _label.text = _sb.ToString();

            // Pop the counter whenever progress goes up.
            if (_prevFilled >= 0 && totalFilled > _prevFilled && isActiveAndEnabled)
            {
                if (_punch != null) StopCoroutine(_punch);
                _punch = StartCoroutine(PunchLabel());
            }
            _prevFilled = totalFilled;
        }

        private IEnumerator PunchLabel()
        {
            const float dur = 0.10f; // faster pop
            var rt = _label.rectTransform;
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

        // Lifts very dark colours (e.g. the charcoal "Black") toward white so the
        // text stays readable on the dark HUD, then returns it as RRGGBB hex.
        private static string LegibleHex(Color32 c)
        {
            float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            Color col = new Color(c.r / 255f, c.g / 255f, c.b / 255f);
            if (lum < 0.5f)
                col = Color.Lerp(col, Color.white, 0.5f - lum);
            Color32 o = col;
            return o.r.ToString("X2") + o.g.ToString("X2") + o.b.ToString("X2");
        }
    }
}
