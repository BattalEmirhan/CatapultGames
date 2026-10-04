namespace CatapultGames.Editor
{
    internal sealed class ColorFocusBot : ISolverBot
    {
        public string Id => "color-focus";
        public string DisplayName => "Colour finisher";
        public string Description => "Finishes the colour with the fewest hits left first, to trigger purges early; falls back to greedy window.";
        public bool IsExpensive => false;
        internal readonly int[] _need = new int[8];

        public void BeginEpisode(PlayoutBoard board, System.Random rng) { }

        public SolverMove ChooseMove(PlayoutBoard board, System.Random rng)
        {
            TargetBoard b = board.Board;
            System.Array.Clear(_need, 0, _need.Length);
            for (int i = 0; i < b.hits.Length; i++)
                if (b.hits[i] > 0 && !b.wild[i])
                    _need[(int)b.colors[i]] += b.hits[i];

            // Among balls in the window, prefer the colour that is closest to done.
            int bestSlot = -1, bestNeed = int.MaxValue;
            for (int s = 0; s < board.SelectableCount; s++)
            {
                int need = _need[(int)board.Peek(s).color];
                if (need > 0 && need < bestNeed)
                {
                    bestNeed = need;
                    bestSlot = s;
                }
            }
            if (bestSlot >= 0)
            {
                int hits = board.Best(bestSlot, out int lx, out int ly);
                if (hits > 0)
                    return new SolverMove { slot = bestSlot, landX = lx, landY = ly };
            }
            return SolverRoster.BestOverWindow(board, out _);
        }
    }
}
