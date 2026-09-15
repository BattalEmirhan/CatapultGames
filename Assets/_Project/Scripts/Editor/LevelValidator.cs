using System.Collections.Generic;

namespace CatapultGames.Editor
{
    // Authoring-time check: is this level completable, and is it pleasant?
    //
    // Coverage comes from CoverageAnalyzer (Shared/), which measures what each
    // ball can paint ON THIS BOARD rather than how big its stamp is. The two
    // numbers diverge badly on thin shapes — a 4x4 stamp covers 4 cells of a
    // one-row stripe, not 16 — and the stamp-area version used to green-light
    // levels that could not be finished.
    //
    // A green result here still is not a promise: coverage is an upper bound
    // that ignores balls competing for the same cells. LevelAutoSolver is the
    // second gate, and the two are meant to be read together.
    public static class LevelValidator
    {
        public enum Severity { OK, Warning, Error }

        // A colour is worth calling out as "barely there" below this many cells —
        // one or two stray pixels are almost always an import artefact, and they
        // cost the player a whole ball to hunt down.
        private const int SparseColorCells = 3;

        // Isolated-cell warnings are listed individually up to this many, then
        // summarised, so a badly imported image cannot flood the footer.
        private const int MaxListedIsolated = 6;

        public struct ColorRow
        {
            public CellColor color;
            public bool      isWild;     // the Joker row — any colour pays for it
            public int       required;   // paint hits still needed (an Ice cell counts twice)
            public int       coverage;   // max hits balls of this color could actually land
            public float     headroom;   // coverage / required
            public Severity  severity;
            public string    note;
        }

        public struct Result
        {
            public ColorRow[] rows;
            public string[]   globalErrors;
            public string[]   globalWarnings;
            public bool       isValid;
        }

        public static Result Validate(LevelData level)
        {
            var cells = level?.cells ?? System.Array.Empty<CellData>();
            var balls = level?.balls ?? System.Array.Empty<BallData>();

            // ── per-color required vs REAL coverage ───────────────────────
            var coverage = CoverageAnalyzer.Analyze(level);

            var rows = new List<ColorRow>(coverage.Count);
            foreach (var c in coverage)
            {
                Severity sev;
                string   note;

                // "required" is paint HITS, not cells: an Ice cell owes two of them.
                // The Joker row is measured against every ball in the queue, since
                // any colour may spend itself on a wild cell.
                string subject = c.isWild ? "joker hits" : "hits";

                if (c.required == 0)
                {
                    sev  = Severity.Warning;
                    note = $"{c.ballCount} balls assigned but no grid cells of this color";
                }
                else if (c.ballCount == 0)
                {
                    sev  = Severity.Error;
                    note = $"{c.required} {subject} needed — no balls assigned";
                }
                else if (c.Impossible)
                {
                    sev  = Severity.Error;
                    note = $"can land at most {c.ceiling} of {c.required} {subject} — impossible " +
                           $"({c.ballCount} balls, best-case placement)";
                }
                else if (c.Tight)
                {
                    sev  = Severity.Warning;
                    note = $"{c.ceiling} coverage for {c.required} {subject} ({c.Headroom:0.00}x) — " +
                           "no margin for a wasted stamp";
                }
                else
                {
                    sev  = Severity.OK;
                    note = $"{c.ceiling} coverage for {c.required} {subject} ({c.Headroom:0.00}x)";
                }

                if (c.isWild) note = "any colour fills these — " + note;

                rows.Add(new ColorRow
                {
                    color    = c.color,
                    isWild   = c.isWild,
                    required = c.required,
                    coverage = c.ceiling,
                    headroom = c.Headroom,
                    severity = sev,
                    note     = note
                });
            }

            // ── global errors ─────────────────────────────────────────────
            var globalErrors = new List<string>();
            bool hasBadBall = false;
            foreach (var b in balls)
                if (b != null && b.color == CellColor.None) hasBadBall = true;

            // A Stone is scenery, not a target — a board of nothing but stone has
            // nothing to paint.
            bool hasColoredCells = false;
            foreach (var c in cells)
                if (IsPaintTarget(c)) { hasColoredCells = true; break; }

            if (!hasColoredCells)  globalErrors.Add("No colored cells in the grid.");
            if (balls.Length == 0) globalErrors.Add("No balls in the queue.");
            if (hasBadBall)        globalErrors.Add("One or more balls have no color assigned.");

            // ── global warnings: stray cells ──────────────────────────────
            var globalWarnings = new List<string>();
            CollectStrayCellWarnings(cells, globalWarnings);
            CollectCellTypeWarnings(cells, globalWarnings);

            bool isValid = globalErrors.Count == 0 &&
                           rows.TrueForAll(r => r.severity != Severity.Error);

            return new Result
            {
                rows           = rows.ToArray(),
                globalErrors   = globalErrors.ToArray(),
                globalWarnings = globalWarnings.ToArray(),
                isValid        = isValid
            };
        }

        // Two shapes of the same authoring mistake, both of which cost the player
        // a ball and a lot of squinting:
        //   · a colour that exists as one or two cells — usually left over from a
        //     test or from ImageImportUtility matching a stray antialiased pixel
        //   · a cell with no orthogonal neighbour of its own colour — an island
        //     the player has to spot on a 12x12 board in perspective
        // Jokers are excluded along with Stone: a wild cell belongs to no colour's
        // count, and a lone one is a deliberate gift rather than a stray pixel.
        private static void CollectStrayCellWarnings(CellData[] cells, List<string> into)
        {
            var byColor  = new Dictionary<CellColor, int>();
            var occupied = new HashSet<(int, int, CellColor)>();

            foreach (var c in cells)
            {
                if (!IsPaintTarget(c) || c.cellType == CellType.Joker) continue;
                byColor.TryGetValue(c.outlineColor, out int n);
                byColor[c.outlineColor] = n + 1;
                occupied.Add((c.gridX, c.gridY, c.outlineColor));
            }

            foreach (var kv in byColor)
                if (kv.Value < SparseColorCells)
                    into.Add($"{kv.Key}: only {kv.Value} cell(s) on the board — stray pixel? " +
                             "It still costs the player a dedicated ball.");

            int isolated = 0;
            var listed   = new List<string>();
            foreach (var c in cells)
            {
                if (!IsPaintTarget(c) || c.cellType == CellType.Joker) continue;

                var col = c.outlineColor;
                bool hasNeighbour =
                    occupied.Contains((c.gridX + 1, c.gridY, col)) ||
                    occupied.Contains((c.gridX - 1, c.gridY, col)) ||
                    occupied.Contains((c.gridX, c.gridY + 1, col)) ||
                    occupied.Contains((c.gridX, c.gridY - 1, col));

                if (hasNeighbour) continue;

                isolated++;
                if (listed.Count < MaxListedIsolated)
                    listed.Add($"({c.gridX},{c.gridY}) {col}");
            }

            if (isolated > 0)
            {
                string tail = isolated > listed.Count ? $" +{isolated - listed.Count} more" : "";
                into.Add($"Isolated cell(s) with no same-color neighbour: " +
                         string.Join(", ", listed) + tail);
            }
        }

        // Mistakes that only special cells can make. Both are silent in play —
        // the level just behaves differently than the picture in the editor
        // suggests — so they are worth saying out loud while authoring.
        private static void CollectCellTypeWarnings(CellData[] cells, List<string> into)
        {
            int colouredStone = 0, wildWithoutColor = 0;

            foreach (var c in cells)
            {
                if (c == null) continue;

                // Stone never paints, so its colour is decoration that reads as a
                // target on the board — the classic "why won't this level finish".
                if (c.cellType == CellType.Stone && c.outlineColor != CellColor.None)
                    colouredStone++;

                // A Joker with no colour is not wild, it is nothing: it has no fill
                // colour to reach, so no ball can spend itself on it.
                if (c.cellType == CellType.Joker && c.outlineColor == CellColor.None)
                    wildWithoutColor++;
            }

            if (colouredStone > 0)
                into.Add($"{colouredStone} stone cell(s) also carry a colour — stone never " +
                         "paints, so the colour is ignored. Erase it to keep the board honest.");

            if (wildWithoutColor > 0)
                into.Add($"{wildWithoutColor} joker cell(s) have no colour — a joker still " +
                         "fills to its own colour, so an uncoloured one can never be painted.");
        }

        // Same rule the game counts progress by (CellView.IsPaintTarget): Stone is
        // scenery and bare board is nothing.
        private static bool IsPaintTarget(CellData c) =>
            c != null && c.outlineColor != CellColor.None && c.cellType != CellType.Stone;
    }
}
