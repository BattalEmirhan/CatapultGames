using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames.Editor
{
    // The bot population. Its job is FAILURE-MODE COVERAGE, not head count:
    // every entry is a different way of deciding, so a level's win-rate says
    // something about the level rather than about how many hats one greedy
    // loop wears.
    //
    // Two reference bots are mandatory:
    //   · random       — the null hypothesis. If every bot wins 100 %, the level
    //                    carries no information.
    //   · gate-greedy  — the ceiling: the save gate's own solver (LevelAutoSolver
    //                    rules, front ball only). A level cannot be saved without
    //                    passing it, so anything under 100 % here is a level
    //                    DEFECT (or an un-gated save), not difficulty.
    public static class SolverRoster
    {
        // The one bot the verdict and the band gauge read. A careful-but-human
        // player: greedy over the selectable window with a 15 % mis-aim. It has
        // randomness, so its win-rate is a real rate and not a coin that always
        // lands the same way (a deterministic band bot would only ever measure
        // 0 % or 100 %).
        public const string BandBotId = "careless-15";

        public static readonly IReadOnlyList<ISolverBot> All = new ISolverBot[]
        {
            new RandomBot(),
            new GateGreedyBot(),
            new GreedyWindowBot(),
            new ColorFocusBot(),
            new ImpulsiveBot(),
            new CarelessBot("careless-15", "Careful player", 0.15f),
            new CarelessBot("careless-35", "Casual player",  0.35f),
            new LookaheadBot()
        };

        public static ISolverBot Find(string id)
        {
            foreach (var b in All)
                if (b.Id == id)
                    return b;
            return null;
        }

        internal static SolverMove BestOverWindow(PlayoutBoard board, out int bestHits)
        {
            bestHits = -1;
            var best = new SolverMove { slot = 0, landX = 0, landY = 0 };
            int bestSize = int.MaxValue;
            for (int s = 0; s < board.SelectableCount; s++)
            {
                int hits = board.Best(s, out int lx, out int ly);
                var ball = board.Peek(s);
                int size = GameConstants.GetPaintCellCount(ball, board.Width, board.Height);
                // Lexicographic tie-break: hits, then the smaller stamp (don't
                // spend a big ball on a small job), then the front-most slot.
                if (hits > bestHits || (hits == bestHits && size < bestSize))
                {
                    bestHits = hits; bestSize = size;
                    best = new SolverMove { slot = s, landX = lx, landY = ly };
                }
            }
            if (bestHits <= 0)
            {
                best.slot = 0;
                best.landX = 0;
                best.landY = 0;
                bestHits = 0;
            }
            return best;
        }

        internal static SolverMove RandomLegal(PlayoutBoard board, System.Random rng)
        {
            int slot = rng.Next(Mathf.Max(1, board.SelectableCount));
            int x, y, guard = 0;
            do { x = rng.Next(board.Width); y = rng.Next(board.Height); }
            while (board.IsStone(x, y) && ++guard < 64);
            return new SolverMove { slot = slot, landX = x, landY = y };
        }
    }
}
