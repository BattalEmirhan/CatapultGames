namespace CatapultGames.Editor
{
    // Satisficer: throws the front ball at the first cell (in a random scan)
    // that paints anything at all. Models a player who does not read the board.
    internal sealed class ImpulsiveBot : ISolverBot
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
                if (board.Measure(0, x, y) > 0)
                    return new SolverMove { slot = 0, landX = x, landY = y };
            }
            return SolverRoster.RandomLegal(board, rng);
        }
    }
}
