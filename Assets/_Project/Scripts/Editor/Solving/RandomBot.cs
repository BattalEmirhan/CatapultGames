namespace CatapultGames.Editor
{
    internal sealed class RandomBot : ISolverBot
    {
        public string Id => "random";
        public string DisplayName => "Random";
        public string Description => "Uniform over every legal slot and cell. Zero hypothesis — no tie-breaker, no preference.";
        public bool IsExpensive => false;
        public void BeginEpisode(PlayoutBoard board, System.Random rng) { }
        public SolverMove ChooseMove(PlayoutBoard board, System.Random rng) => SolverRoster.RandomLegal(board, rng);
    }
}
