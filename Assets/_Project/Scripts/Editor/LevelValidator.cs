using System.Collections.Generic;

namespace CatapultGames.Editor
{
    public static class LevelValidator
    {
        public enum Severity { OK, Warning, Error }

        public struct ColorRow
        {
            public CellColor color;
            public int       required;   // colored cells on grid
            public int       coverage;   // max cells balls of this color can paint
            public Severity  severity;
            public string    note;
        }

        public struct Result
        {
            public ColorRow[] rows;
            public string[]   globalErrors;
            public bool       isValid;
        }

        public static Result Validate(LevelData level)
        {
            var cells = level.cells ?? System.Array.Empty<CellData>();
            var balls  = level.balls  ?? System.Array.Empty<BallData>();

            // ── per-color required cell count ─────────────────────────────
            var required = new Dictionary<CellColor, int>();
            foreach (var c in cells)
            {
                if (c.outlineColor == CellColor.None) continue;
                required.TryGetValue(c.outlineColor, out int n);
                required[c.outlineColor] = n + 1;
            }

            // ── per-color max coverage from ball queue ────────────────────
            var coverage = new Dictionary<CellColor, int>();
            bool hasBadBall = false;
            foreach (var b in balls)
            {
                if (b.color == CellColor.None) { hasBadBall = true; continue; }
                coverage.TryGetValue(b.color, out int n);
                coverage[b.color] = n + GameConstants.GetPaintCellCount(b, level.grid.width, level.grid.height);
            }

            // ── build per-color rows ──────────────────────────────────────
            var allColors = new HashSet<CellColor>();
            foreach (var k in required.Keys)  allColors.Add(k);
            foreach (var k in coverage.Keys)  allColors.Add(k);

            var rows = new List<ColorRow>();
            foreach (var c in allColors)
            {
                required.TryGetValue(c,  out int req);
                coverage.TryGetValue(c, out int cov);

                Severity sev;
                string   note;

                if (req == 0)
                {
                    sev  = Severity.Warning;
                    note = $"balls assigned ({cov} cells) but no grid cells of this color";
                }
                else if (cov == 0)
                {
                    sev  = Severity.Error;
                    note = $"{req} cells need filling — no balls assigned";
                }
                else if (cov < req)
                {
                    sev  = Severity.Warning;
                    note = $"max coverage {cov} < required {req}";
                }
                else
                {
                    sev  = Severity.OK;
                    note = $"{cov} coverage ≥ {req} required";
                }

                rows.Add(new ColorRow
                {
                    color    = c,
                    required = req,
                    coverage = cov,
                    severity = sev,
                    note     = note
                });
            }

            // ── global errors ─────────────────────────────────────────────
            var globalErrors = new List<string>();
            if (required.Count == 0) globalErrors.Add("No colored cells in the grid.");
            if (balls.Length   == 0) globalErrors.Add("No balls in the queue.");
            if (hasBadBall)          globalErrors.Add("One or more balls have no color assigned.");

            bool isValid = globalErrors.Count == 0 &&
                           rows.TrueForAll(r => r.severity != Severity.Error);

            return new Result
            {
                rows         = rows.ToArray(),
                globalErrors = globalErrors.ToArray(),
                isValid      = isValid
            };
        }
    }
}
