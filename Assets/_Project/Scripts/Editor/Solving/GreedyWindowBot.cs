namespace CatapultGames.Editor
{
    internal sealed class GreedyWindowBot : ISolverBot
    {
        public string Id => "greedy-window";
        public string DisplayName => "Greedy window";
        public string Description => "Looks at all three selectable balls and throws whichever lands the most hits; ties go to the smaller stamp.";
        public bool IsExpensive => false;
        public void BeginEpisode(PlayoutBoard board, System.Random rng) { }
        public SolverMove ChooseMove(PlayoutBoard board, System.Random rng) => SolverRoster.BestOverWindow(board, out _);
    }
}
