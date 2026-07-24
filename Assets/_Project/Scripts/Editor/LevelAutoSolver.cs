using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames.Editor
{
    // Greedy AI that "plays" a level to check whether the ball queue can clear it.
    // The ball ORDER is fixed (as authored); only the landing cell is chosen.
    // For each ball it lands on the cell that fills the most matching, still-empty
    // cells — the same rule gameplay uses (PaintingSystem + GameConstants).
    //
    // This is a heuristic, not an exhaustive solver: if it reports "solved" the
    // level is definitely beatable; a "failed" result means greedy play couldn't
    // clear it (a smarter player might, but it's a strong red flag for the design).
    public static class LevelAutoSolver
    {
        public struct Move
        {
            public bool          wasted;       // true when the ball filled nothing
            public int           landX, landY; // chosen landing cell (-1 when wasted)
            public CellColor     color;
            public int           power;
            public Vector2Int[]  filled;       // cells newly filled by this move
        }

        // A (colour, power) group of leftover balls.
        public struct BallCount
        {
            public CellColor color;
            public int       power;   // 1..3
            public int       count;
        }

        public struct Result
        {
            public List<Move> moves;
            public bool       solved;
            public int        totalColored;   // colored cells needing fill
            public int        remaining;      // still empty when the plan ends
            public int        ballsUsed;      // balls that actually painted something
            public int        totalBalls;
            public BallCount[] leftover;      // balls never thrown (set only when solved)
            public int        leftoverTotal;  // sum of leftover counts
        }

        public static Result Solve(LevelData level)
        {
            var moves = new List<Move>();

            int w = level?.grid?.width  ?? 0;
            int h = level?.grid?.height ?? 0;

            // Index colored cells; seed the filled set with any authored-filled cells.
            var outline = new Dictionary<(int, int), CellColor>();
            var filled  = new HashSet<(int, int)>();
            int totalColored = 0;

            if (level?.cells != null)
            {
                foreach (var c in level.cells)
                {
                    if (c == null || c.outlineColor == CellColor.None) continue;
                    outline[(c.gridX, c.gridY)] = c.outlineColor;
                    totalColored++;
                    if (c.isFilled) filled.Add((c.gridX, c.gridY));
                }
            }

            var balls = level?.balls ?? System.Array.Empty<BallData>();
            int ballsUsed = 0;
            int stopIndex = balls.Length;   // index of the first ball never thrown

            for (int bi = 0; bi < balls.Length; bi++)
            {
                var ball = balls[bi];
                if (filled.Count >= totalColored) { stopIndex = bi; break; }   // solved — stop
                if (ball == null) continue;

                int power = Mathf.Clamp(ball.powerLevel, 1, 3);

                // Find the landing cell that fills the most matching empty cells.
                int bestCount = 0;
                int bestX = -1, bestY = -1;
                List<Vector2Int> bestCells = null;

                if (ball.color != CellColor.None)
                {
                    for (int ly = 0; ly < h; ly++)
                    for (int lx = 0; lx < w; lx++)
                    {
                        List<Vector2Int> hit = null;
                        int count = 0;
                        foreach (var p in GameConstants.GetPaintedCells(lx, ly, ball, w, h))
                        {
                            if (p.x < 0 || p.x >= w || p.y < 0 || p.y >= h) continue;
                            var key = (p.x, p.y);
                            if (!outline.TryGetValue(key, out var oc) || oc != ball.color) continue;
                            if (filled.Contains(key)) continue;
                            (hit ??= new List<Vector2Int>()).Add(p);
                            count++;
                        }
                        if (count > bestCount)
                        {
                            bestCount = count;
                            bestX = lx; bestY = ly;
                            bestCells = hit;
                        }
                    }
                }

                if (bestCount == 0 || bestCells == null)
                {
                    moves.Add(new Move { wasted = true, landX = -1, landY = -1,
                                         color = ball.color, power = power,
                                         filled = System.Array.Empty<Vector2Int>() });
                    continue;
                }

                foreach (var p in bestCells) filled.Add((p.x, p.y));
                ballsUsed++;
                moves.Add(new Move
                {
                    wasted = false,
                    landX  = bestX, landY = bestY,
                    color  = ball.color, power = power,
                    filled = bestCells.ToArray()
                });
            }

            bool solved = filled.Count >= totalColored && totalColored > 0;

            // When solved before the queue ran out, the balls from stopIndex on
            // were never thrown — group them by (colour, power) for the report.
            var leftCounts = new Dictionary<(CellColor, int), int>();
            int leftoverTotal = 0;
            if (solved)
            {
                for (int bi = stopIndex; bi < balls.Length; bi++)
                {
                    var b = balls[bi];
                    if (b == null) continue;
                    var key = (b.color, Mathf.Clamp(b.powerLevel, 1, 3));
                    leftCounts.TryGetValue(key, out int n);
                    leftCounts[key] = n + 1;
                    leftoverTotal++;
                }
            }

            var leftover = new List<BallCount>(leftCounts.Count);
            foreach (var kv in leftCounts)
                leftover.Add(new BallCount { color = kv.Key.Item1, power = kv.Key.Item2, count = kv.Value });
            leftover.Sort((a, b) =>
            {
                int byColor = ((int)a.color).CompareTo((int)b.color);
                return byColor != 0 ? byColor : a.power.CompareTo(b.power);
            });

            return new Result
            {
                moves         = moves,
                solved        = solved,
                totalColored  = totalColored,
                remaining     = Mathf.Max(0, totalColored - filled.Count),
                ballsUsed     = ballsUsed,
                totalBalls    = balls.Length,
                leftover      = leftover.ToArray(),
                leftoverTotal = leftoverTotal
            };
        }
    }
}
