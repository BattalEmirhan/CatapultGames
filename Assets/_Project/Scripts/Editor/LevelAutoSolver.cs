using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames.Editor
{
    // Greedy AI that "plays" a level to check whether the ball queue can clear it.
    // The ball ORDER is fixed (as authored); only the landing cell is chosen.
    // For each ball it lands on the cell that fills the most matching, still-empty
    // cells — the same rule gameplay uses.
    //
    // The board itself is a CoverageAnalyzer.TargetBoard, and every move goes
    // through BestPlacement / ApplyPlacement. That is deliberate: shape, colour
    // match (Joker), stamp reach (Stone) and hit cost (Ice) are then decided by
    // exactly the code the runtime and the validator use, so this solver cannot
    // quietly play by different rules than the game does.
    //
    // This is a heuristic, not an exhaustive solver: if it reports "solved" the
    // level is definitely beatable; a "failed" result means greedy play couldn't
    // clear it (a smarter player might, but it's a strong red flag for the design).
    //
    // Since BallQueueView lets the player pull one of the next few balls forward
    // (BallQueue.SelectSlot), the fixed order makes this solver PESSIMISTIC — the
    // player has strictly more freedom than it does. That is the safe direction:
    // "solved" still guarantees beatable. Only read a "failed" result as weaker
    // evidence than it used to be.
    public static class LevelAutoSolver
    {
        public struct Move
        {
            public bool          wasted;       // true when the ball painted nothing
            public int           landX, landY; // chosen landing cell (-1 when wasted)
            public CellColor     color;
            public int           power;
            public Vector2Int[]  hit;          // cells this move put paint into
            public Vector2Int[]  filled;       // the subset that finished (Ice needs two)
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
            public int        totalColored;   // cells that still needed paint at the start
            public int        remaining;      // still empty when the plan ends
            public int        ballsUsed;      // balls that actually painted something
            public int        totalBalls;
            public BallCount[] leftover;      // balls never thrown (set only when solved)
            public int        leftoverTotal;  // sum of leftover counts
        }

        public static Result Solve(LevelData level)
        {
            var moves = new List<Move>();

            // Authored-filled cells are already absent from the board, so progress
            // starts at zero and "solved" means every remaining target was filled.
            var board      = CoverageAnalyzer.BuildTargets(level);
            int totalCells = board.TargetCellCount();
            int completed  = 0;

            var balls     = level?.balls ?? System.Array.Empty<BallData>();
            int ballsUsed = 0;
            int stopIndex = balls.Length;   // index of the first ball never thrown

            var hitBuf  = new List<Vector2Int>();
            var fillBuf = new List<Vector2Int>();

            for (int bi = 0; bi < balls.Length; bi++)
            {
                if (completed >= totalCells) { stopIndex = bi; break; }   // solved — stop

                var ball = balls[bi];
                if (ball == null) continue;
                int power = Mathf.Clamp(ball.powerLevel, 1, 3);

                // Landing cell that lands the most hits, by the game's own rules.
                int gain = CoverageAnalyzer.BestPlacement(board, ball, out int lx, out int ly);

                if (gain == 0)
                {
                    moves.Add(new Move { wasted = true, landX = -1, landY = -1,
                                         color  = ball.color, power = power,
                                         hit    = System.Array.Empty<Vector2Int>(),
                                         filled = System.Array.Empty<Vector2Int>() });
                    continue;
                }

                hitBuf.Clear();
                fillBuf.Clear();
                CoverageAnalyzer.ApplyPlacement(board, ball, lx, ly, hitBuf, fillBuf);

                completed += fillBuf.Count;
                ballsUsed++;
                moves.Add(new Move
                {
                    wasted = false,
                    landX  = lx, landY = ly,
                    color  = ball.color, power = power,
                    hit    = hitBuf.ToArray(),
                    filled = fillBuf.ToArray()
                });
            }

            bool solved = completed >= totalCells && totalCells > 0;

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
                totalColored  = totalCells,
                remaining     = Mathf.Max(0, totalCells - completed),
                ballsUsed     = ballsUsed,
                totalBalls    = balls.Length,
                leftover      = leftover.ToArray(),
                leftoverTotal = leftoverTotal
            };
        }
    }
}
