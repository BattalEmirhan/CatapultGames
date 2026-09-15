using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Answers one question: can the balls still in hand paint what is still empty?
    //
    // The naive answer — summing stamp AREAS (GameConstants.GetPaintCellCount) —
    // ignores the board. A 4x4 stamp dropped on a one-row-tall stripe paints 4
    // cells, not 16, so a level can report "2.75x headroom" while being provably
    // unwinnable. This computes the honest number: for every ball, the best
    // landing cell on the board as it actually is.
    //
    // The result is an UPPER BOUND, and a deliberately loose one — each ball is
    // measured alone, so two balls competing for the same cells both count it.
    // Painting only ever removes targets, so no ball can beat its own best case.
    // That asymmetry is what makes the verdict usable:
    //     ceiling <  required  →  proof that the colour can never be completed
    //     ceiling >= required  →  no proof of anything; only LevelAutoSolver can
    //                             say whether a real play order gets there
    //
    // Cell types (CellType) all fold into the same measurement, and every one of
    // them is arranged to keep the bound loose rather than tight, because a false
    // "impossible" would end a winnable run:
    //   · Ice   — costs two hits, so it counts twice in `required`
    //   · Stone — not a target at all, and it shadows the stamp behind it
    //   · Joker — taken out of its colour's `required` (any ball can pay for it)
    //             but left IN every colour's `ceiling`, and given its own row
    //             measured against wild cells alone
    //
    // Two consumers, which is why this lives in Shared/ and not Editor/ (runtime
    // code cannot see the Editor assembly):
    //   · LevelValidator / LevelAutoSolver — authoring-time checks in the editor
    //   · GameManager — mid-run check, to call a lost level lost immediately
    public static class CoverageAnalyzer
    {
        // Below this ratio a colour is technically completable but leaves the
        // player no room for a single misplaced stamp. Authoring-time warning.
        public const float TightHeadroom = 1.4f;

        // ── Board snapshot ────────────────────────────────────────────────
        // Everything paint has to know about a board, as flat arrays indexed
        // y * width + x. Cells that need nothing (already filled, bare board,
        // Stone) have hits == 0, which is the single test for "not a target".
        public sealed class TargetBoard
        {
            public readonly int         width;
            public readonly int         height;
            public readonly CellColor[] colors;   // colour that fills this cell
            public readonly byte[]      hits;     // hits still needed (Ice: 2)
            public readonly bool[]      wild;     // Joker — any colour fills it
            public readonly bool[]      stone;    // absorbs the stamp behind it

            public TargetBoard(int w, int h)
            {
                width  = Mathf.Max(0, w);
                height = Mathf.Max(0, h);
                int n  = width * height;
                colors = new CellColor[n];
                hits   = new byte[n];
                wild   = new bool[n];
                stone  = new bool[n];
            }

            public int  Index(int x, int y)     => y * width + x;
            public bool InBounds(int x, int y)  => x >= 0 && x < width && y >= 0 && y < height;

            // Cells still waiting for paint — an Ice cell counts once here (it is
            // one cell) but twice in TotalHits (it takes two balls' worth).
            public int TargetCellCount()
            {
                int n = 0;
                foreach (var h in hits) if (h > 0) n++;
                return n;
            }

            public int TotalHits()
            {
                int n = 0;
                foreach (var h in hits) n += h;
                return n;
            }

            public void Set(int x, int y, CellColor color, CellType type)
            {
                if (!InBounds(x, y)) return;
                int i = Index(x, y);

                if (type == CellType.Stone)
                {
                    stone[i]  = true;
                    colors[i] = CellColor.None;
                    hits[i]   = 0;
                    return;
                }
                if (color == CellColor.None) return;

                colors[i] = color;
                hits[i]   = (byte)GameConstants.GetRequiredHits(type);
                wild[i]   = type == CellType.Joker;
            }
        }

        public struct ColorCoverage
        {
            public CellColor color;
            public bool      isWild;      // the Joker row — any colour pays for it
            public int       required;    // paint hits still needed (Ice counts twice)
            public int       ceiling;     // max hits this colour's balls could land
            public int       ballCount;

            // Proven unreachable: no placement of these balls covers those cells.
            public bool Impossible => required > 0 && ceiling < required;

            // Completable, but with no margin for a wasted stamp.
            public bool Tight => required > 0 && !Impossible && ceiling < required * TightHeadroom;

            // ceiling / required — 1.0 means "every stamp must be perfect".
            public float Headroom => required > 0 ? (float)ceiling / required : float.PositiveInfinity;
        }

        // ── Board builders ────────────────────────────────────────────────
        public static TargetBoard BuildTargets(LevelData level)
        {
            int w = level?.grid?.width  ?? 0;
            int h = level?.grid?.height ?? 0;
            var board = new TargetBoard(w, h);

            if (level?.cells == null) return board;

            foreach (var c in level.cells)
            {
                if (c == null) continue;
                // An authored-filled cell is done; Stone still has to be recorded,
                // because it shadows stamps whether or not anything needs paint.
                if (c.isFilled && c.cellType != CellType.Stone) continue;
                board.Set(c.gridX, c.gridY, c.outlineColor, c.cellType);
            }

            return board;
        }

        // Live-grid version, for the mid-run check. Cheap enough to call per
        // landing (a 15x15 board is 225 lookups). A part-cracked Ice cell reports
        // only the hits it still owes.
        public static TargetBoard BuildTargets(GridRenderer grid)
        {
            int w = grid != null ? grid.Width  : 0;
            int h = grid != null ? grid.Height : 0;
            var board = new TargetBoard(w, h);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!grid.TryGetCell(x, y, out var cell)) continue;

                if (cell.Type == CellType.Stone)
                {
                    board.stone[board.Index(x, y)] = true;
                    continue;
                }
                if (!cell.IsPaintTarget || cell.IsFilled) continue;

                int i = board.Index(x, y);
                board.colors[i] = cell.OutlineColor;
                board.hits[i]   = (byte)Mathf.Max(0, cell.HitsRequired - cell.HitsTaken);
                board.wild[i]   = cell.Type == CellType.Joker;
            }

            return board;
        }

        // ── Stamp walk ────────────────────────────────────────────────────
        // Reused so the per-landing scan below doesn't allocate. Single-threaded
        // by construction (Unity main thread / editor GUI), same as PaintingSystem.
        private static readonly List<Vector2Int> _pathBuffer = new();

        // One walk of one stamp at one placement: counts the cells that would take
        // a hit, and optionally applies them. Measuring and applying share this
        // walk, so a plan can never be measured by one rule and executed by another.
        private static int Walk(TargetBoard b, BallData ball, int landX, int landY,
                                bool wildOnly, bool apply,
                                List<Vector2Int> hitInto, List<Vector2Int> filledInto)
        {
            if (b == null || ball == null || ball.color == CellColor.None) return 0;

            // Aimed straight at a Stone: the stamp is absorbed whole, same as in
            // PaintingSystem. The shadow walk below only covers cells BEYOND the
            // landing cell, so this one has to be asked separately.
            if (b.InBounds(landX, landY) && b.stone[b.Index(landX, landY)]) return 0;

            int count = 0;
            foreach (var p in GameConstants.GetPaintedCells(landX, landY, ball, b.width, b.height))
            {
                if (!b.InBounds(p.x, p.y)) continue;

                int i = b.Index(p.x, p.y);
                if (b.hits[i] == 0) continue;                               // nothing to do here
                if (wildOnly && !b.wild[i]) continue;
                if (!b.wild[i] && b.colors[i] != ball.color) continue;
                if (IsShadowed(b, landX, landY, p.x, p.y)) continue;

                count++;
                hitInto?.Add(p);
                if (!apply) continue;

                b.hits[i]--;
                if (b.hits[i] == 0)
                {
                    b.colors[i] = CellColor.None;
                    b.wild[i]   = false;
                    filledInto?.Add(p);
                }
            }

            return count;
        }

        // Same reach rule the live grid paints with (GameConstants.GetStampPath):
        // a Stone on the straight path out from the landing cell eats the stamp.
        private static bool IsShadowed(TargetBoard b, int landX, int landY, int cellX, int cellY)
        {
            GameConstants.GetStampPath(landX, landY, cellX, cellY, _pathBuffer);
            foreach (var p in _pathBuffer)
                if (b.InBounds(p.x, p.y) && b.stone[b.Index(p.x, p.y)]) return true;
            return false;
        }

        // ── Per-ball best case ────────────────────────────────────────────
        // Most hits this ball could land, over every landing cell on the board,
        // and where that happens. Uses the same footprint, match and reach rules
        // the game paints with, so the two can never drift apart.
        public static int BestPlacement(TargetBoard board, BallData ball, out int landX, out int landY) =>
            Best(board, ball, wildOnly: false, out landX, out landY);

        public static int BestCoverage(TargetBoard board, BallData ball) =>
            Best(board, ball, wildOnly: false, out _, out _);

        // Hits ONE placement would land, without applying it — the same walk
        // BestPlacement scans with, exposed so a solver bot can weigh a specific
        // (not necessarily best) landing cell by the game's own rules.
        public static int CountPlacement(TargetBoard board, BallData ball, int landX, int landY) =>
            Walk(board, ball, landX, landY, wildOnly: false, apply: false, null, null);

        private static int Best(TargetBoard b, BallData ball, bool wildOnly, out int landX, out int landY)
        {
            landX = landY = -1;
            if (b == null) return 0;

            int best = 0;
            for (int ly = 0; ly < b.height; ly++)
            for (int lx = 0; lx < b.width;  lx++)
            {
                int count = Walk(b, ball, lx, ly, wildOnly, apply: false, null, null);
                if (count > best) { best = count; landX = lx; landY = ly; }
            }

            return best;
        }

        // Strike a placement off a scratch board, so a multi-ball plan can be
        // simulated one ball at a time instead of each ball being judged against
        // the same untouched board. Returns how many hits it landed; the optional
        // lists collect the cells it hit and the subset that finished filling
        // (the two differ only on Ice).
        public static int ApplyPlacement(TargetBoard board, BallData ball, int landX, int landY,
                                         List<Vector2Int> hitInto = null,
                                         List<Vector2Int> filledInto = null) =>
            Walk(board, ball, landX, landY, wildOnly: false, apply: true, hitInto, filledInto);

        // ── Per-colour rollup ─────────────────────────────────────────────
        // Rows are returned in CellColor enum order so the report reads the same
        // way every time, with the Joker row (if any) last.
        public static List<ColorCoverage> Analyze(TargetBoard board, IReadOnlyList<BallData> balls)
        {
            var required  = new Dictionary<CellColor, int>();
            var ceiling   = new Dictionary<CellColor, int>();
            var ballCount = new Dictionary<CellColor, int>();
            int wildRequired = 0, wildCeiling = 0, totalBalls = 0;

            if (board != null)
            {
                for (int i = 0; i < board.hits.Length; i++)
                {
                    int h = board.hits[i];
                    if (h == 0) continue;

                    // Joker cells belong to no colour: charging them to their
                    // authored colour would invent an impossibility that a ball of
                    // any other colour could have solved.
                    if (board.wild[i]) { wildRequired += h; continue; }

                    var c = board.colors[i];
                    if (c == CellColor.None) continue;
                    required.TryGetValue(c, out int n);
                    required[c] = n + h;
                }
            }

            // Identical balls land identically, so scanning the board once per
            // (colour, power, shape) is enough — level4's 48 balls collapse to 11.
            // Each kind is measured twice: against everything it could paint, and
            // against wild cells alone (the Joker row's own ceiling).
            var bestByKind = new Dictionary<(CellColor, int, BallShape), (int all, int wild)>();

            if (balls != null)
            {
                foreach (var ball in balls)
                {
                    if (ball == null || ball.color == CellColor.None) continue;

                    totalBalls++;
                    ballCount.TryGetValue(ball.color, out int bn);
                    ballCount[ball.color] = bn + 1;

                    var key = (ball.color, Mathf.Clamp(ball.powerLevel, 1, 3), ball.shape);
                    if (!bestByKind.TryGetValue(key, out var best))
                    {
                        best = (Best(board, ball, false, out _, out _),
                                Best(board, ball, true,  out _, out _));
                        bestByKind[key] = best;
                    }

                    ceiling.TryGetValue(ball.color, out int cn);
                    ceiling[ball.color] = cn + best.all;   // includes wild cells: inflated on purpose
                    wildCeiling += best.wild;
                }
            }

            var colors = new HashSet<CellColor>();
            foreach (var k in required.Keys)  colors.Add(k);
            foreach (var k in ballCount.Keys) colors.Add(k);

            var rows = new List<ColorCoverage>(colors.Count + 1);
            foreach (var c in colors)
            {
                required.TryGetValue(c,  out int req);
                ceiling.TryGetValue(c,   out int cov);
                ballCount.TryGetValue(c, out int bc);
                rows.Add(new ColorCoverage { color = c, required = req, ceiling = cov, ballCount = bc });
            }

            rows.Sort((a, b) => ((int)a.color).CompareTo((int)b.color));

            if (wildRequired > 0)
                rows.Add(new ColorCoverage
                {
                    color     = CellColor.None,
                    isWild    = true,
                    required  = wildRequired,
                    ceiling   = wildCeiling,
                    ballCount = totalBalls     // every ball is a candidate for a Joker
                });

            return rows;
        }

        public static List<ColorCoverage> Analyze(LevelData level) =>
            Analyze(BuildTargets(level), level?.balls);

        // True when at least one colour is proven unreachable — the level (or the
        // run, mid-game) cannot be completed no matter how well it is played.
        public static bool AnyImpossible(List<ColorCoverage> rows)
        {
            if (rows == null) return false;
            foreach (var r in rows)
                if (r.Impossible) return true;
            return false;
        }
    }
}
