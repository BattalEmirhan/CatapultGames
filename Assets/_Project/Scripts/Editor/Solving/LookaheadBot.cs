using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames.Editor
{
    // Two-ply: for each selectable ball, its top-K landing cells; for each,
    // apply on a private clone and add the best follow-up. Commits ONE move
    // on the real board; never undoes there.
    internal sealed class LookaheadBot : ISolverBot
    {
        public string Id => "lookahead-2";
        public string DisplayName => "Look-ahead (2)";
        public string Description => "Two-ply search over the top landing cells of each selectable ball, exploring on a private copy.";
        public bool IsExpensive => true;

        internal const int TopK = 5;
        internal readonly List<(int hits, int x, int y)> _cands = new List<(int, int, int)>();

        public void BeginEpisode(PlayoutBoard board, System.Random rng) { }

        public SolverMove ChooseMove(PlayoutBoard board, System.Random rng)
        {
            var best = SolverRoster.BestOverWindow(board, out int bestNow);
            int bestScore = -1;

            for (int s = 0; s < board.SelectableCount; s++)
            {
                _cands.Clear();
                for (int y = 0; y < board.Height; y++)
                for (int x = 0; x < board.Width; x++)
                {
                    int h = board.Measure(s, x, y);
                    if (h > 0)
                        _cands.Add((h, x, y));
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
                    if (after == PlayoutOutcome.DeadEnd || after == PlayoutOutcome.OutOfBalls)
                        continue;
                    SolverRoster.BestOverWindow(clone, out int next);
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
