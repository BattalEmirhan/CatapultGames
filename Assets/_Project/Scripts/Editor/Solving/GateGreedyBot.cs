namespace CatapultGames.Editor
{
    internal sealed class GateGreedyBot : ISolverBot
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
}
