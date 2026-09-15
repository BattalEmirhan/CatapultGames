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
            foreach (var b in All) if (b.Id == id) return b;
            return null;
        }

        // ── Shared helpers ────────────────────────────────────────────────
        private static SolverMove BestOverWindow(PlayoutBoard board, out int bestHits)
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
            if (bestHits <= 0) { best.slot = 0; best.landX = 0; best.landY = 0; bestHits = 0; }
            return best;
        }

        private static SolverMove RandomLegal(PlayoutBoard board, System.Random rng)
        {
            int slot = rng.Next(Mathf.Max(1, board.SelectableCount));
            int x, y, guard = 0;
            do { x = rng.Next(board.Width); y = rng.Next(board.Height); }
            while (board.IsStone(x, y) && ++guard < 64);
            return new SolverMove { slot = slot, landX = x, landY = y };
        }

        // ── Baseline ──────────────────────────────────────────────────────
        private sealed class RandomBot : ISolverBot
        {
            public string Id => "random";
            public string DisplayName => "Random";
            public string Description => "Uniform over every legal slot and cell. Zero hypothesis — no tie-breaker, no preference.";
            public bool IsExpensive => false;
            public void BeginEpisode(PlayoutBoard board, System.Random rng) { }
            public SolverMove ChooseMove(PlayoutBoard board, System.Random rng) => RandomLegal(board, rng);
        }

        private sealed class GateGreedyBot : ISolverBot
        {
            public string Id => "gate-greedy";
            public string DisplayName => "Gate greedy";
            public string Description => "The save gate's own solver: front ball only, most hits. Anything under 100 % is a level defect.";
            public bool IsExpensive => false;
            public void BeginEpisode(PlayoutBoard board, System.Random rng) { }
            public SolverMove ChooseMove(PlayoutBoard board, System.Random rng)
            {
                int hits = board.Best(0, out int lx, out int ly);
                return hits > 0 ? new SolverMove { slot = 0, landX = lx, landY = ly }
                                : new SolverMove { slot = 0, landX = 0, landY = 0 };
            }
        }

        // ── One-ply heuristics ────────────────────────────────────────────
        private sealed class GreedyWindowBot : ISolverBot
        {
            public string Id => "greedy-window";
            public string DisplayName => "Greedy window";
            public string Description => "Looks at all three selectable balls and throws whichever lands the most hits; ties go to the smaller stamp.";
            public bool IsExpensive => false;
            public void BeginEpisode(PlayoutBoard board, System.Random rng) { }
            public SolverMove ChooseMove(PlayoutBoard board, System.Random rng) => BestOverWindow(board, out _);
        }

        private sealed class ColorFocusBot : ISolverBot
        {
            public string Id => "color-focus";
            public string DisplayName => "Colour finisher";
            public string Description => "Finishes the colour with the fewest hits left first, to trigger purges early; falls back to greedy window.";
            public bool IsExpensive => false;
            private readonly int[] _need = new int[8];

            public void BeginEpisode(PlayoutBoard board, System.Random rng) { }

            public SolverMove ChooseMove(PlayoutBoard board, System.Random rng)
            {
                var b = board.Board;
                System.Array.Clear(_need, 0, _need.Length);
                for (int i = 0; i < b.hits.Length; i++)
                    if (b.hits[i] > 0 && !b.wild[i]) _need[(int)b.colors[i]] += b.hits[i];

                // Among balls in the window, prefer the colour that is closest to done.
                int bestSlot = -1, bestNeed = int.MaxValue;
                for (int s = 0; s < board.SelectableCount; s++)
                {
                    int need = _need[(int)board.Peek(s).color];
                    if (need > 0 && need < bestNeed) { bestNeed = need; bestSlot = s; }
                }
                if (bestSlot >= 0)
                {
                    int hits = board.Best(bestSlot, out int lx, out int ly);
                    if (hits > 0) return new SolverMove { slot = bestSlot, landX = lx, landY = ly };
                }
                return BestOverWindow(board, out _);
            }
        }

        // ── Human models (none of these optimise) ─────────────────────────
        // Satisficer: throws the front ball at the first cell (in a random scan)
        // that paints anything at all. Models a player who does not read the board.
        private sealed class ImpulsiveBot : ISolverBot
        {
            public string Id => "impulsive";
            public string DisplayName => "Impulsive";
            public string Description => "Front ball, first cell that paints anything, in a random scan. Does not read the board.";
            public bool IsExpensive => false;
            public void BeginEpisode(PlayoutBoard board, System.Random rng) { }
            public SolverMove ChooseMove(PlayoutBoard board, System.Random rng)
            {
                int n = board.Width * board.Height;
                int start = rng.Next(n);
                for (int k = 0; k < n; k++)
                {
                    int i = (start + k) % n;
                    int x = i % board.Width, y = i / board.Width;
                    if (board.Measure(0, x, y) > 0) return new SolverMove { slot = 0, landX = x, landY = y };
                }
                return RandomLegal(board, rng);
            }
        }

        // Greedy-window intent with a mis-aim: with probability p the throw
        // lands one cell off in x and/or y. The skill spectrum is this one
        // policy at different error rates.
        private sealed class CarelessBot : ISolverBot
        {
            private readonly float _p;
            public CarelessBot(string id, string name, float errorRate) { Id = id; DisplayName = name; _p = errorRate; }
            public string Id { get; }
            public string DisplayName { get; }
            public string Description => $"Greedy window, but {_p * 100:0} % of throws land one cell off. The band bot models a careful human.";
            public bool IsExpensive => false;
            public void BeginEpisode(PlayoutBoard board, System.Random rng) { }
            public SolverMove ChooseMove(PlayoutBoard board, System.Random rng)
            {
                var m = BestOverWindow(board, out _);
                if (rng.NextDouble() < _p)
                {
                    m.landX = Mathf.Clamp(m.landX + rng.Next(-1, 2), 0, board.Width - 1);
                    m.landY = Mathf.Clamp(m.landY + rng.Next(-1, 2), 0, board.Height - 1);
                }
                return m;
            }
        }

        // ── Search ────────────────────────────────────────────────────────
        // Two-ply: for each selectable ball, its top-K landing cells; for each,
        // apply on a private clone and add the best follow-up. Commits ONE move
        // on the real board; never undoes there.
        private sealed class LookaheadBot : ISolverBot
        {
            private const int TopK = 5;
            public string Id => "lookahead-2";
            public string DisplayName => "Look-ahead (2)";
            public string Description => "Two-ply search over the top landing cells of each selectable ball, exploring on a private copy.";
            public bool IsExpensive => true;
            private readonly List<(int hits, int x, int y)> _cands = new List<(int, int, int)>();

            public void BeginEpisode(PlayoutBoard board, System.Random rng) { }

            public SolverMove ChooseMove(PlayoutBoard board, System.Random rng)
            {
                var best = BestOverWindow(board, out int bestNow);
                int bestScore = -1;

                for (int s = 0; s < board.SelectableCount; s++)
                {
                    _cands.Clear();
                    for (int y = 0; y < board.Height; y++)
                    for (int x = 0; x < board.Width; x++)
                    {
                        int h = board.Measure(s, x, y);
                        if (h > 0) _cands.Add((h, x, y));
                    }
                    _cands.Sort((a, b) => b.hits.CompareTo(a.hits));
                    int take = Mathf.Min(TopK, _cands.Count);
                    for (int c = 0; c < take; c++)
                    {
                        var (h, x, y) = _cands[c];
                        var clone = board.Clone();
                        clone.Apply(new SolverMove { slot = s, landX = x, landY = y });
                        // A placement that proves the level lost one move later is
                        // worth nothing, whatever it painted — that is the whole
                        // point of looking ahead.
                        var after = clone.Evaluate();
                        if (after == PlayoutOutcome.DeadEnd || after == PlayoutOutcome.OutOfBalls) continue;
                        BestOverWindow(clone, out int next);
                        int score = h + Mathf.Max(0, next) + (after == PlayoutOutcome.Won ? 1000 : 0);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = new SolverMove { slot = s, landX = x, landY = y };
                        }
                    }
                }
                return best;
            }
        }
    }
}
