namespace CatapultGames.Editor
{
    // A decision procedure that plays a level to the end. Adding a bot means
    // adding a genuinely different way of choosing a throw — a new scorer is a
    // component, not a bot (see SolverRoster).
    public interface ISolverBot
    {
        string Id          { get; }
        string DisplayName { get; }
        string Description { get; }   // one sentence, shown in the roster card
        bool   IsExpensive { get; }   // warns before a large sweep

        // Once per run. The same instance is reused for the whole sweep, so any
        // per-run state must be reset here.
        void BeginEpisode(PlayoutBoard board, System.Random rng);

        // One throw. The board is the measured board — read it, do not mutate it;
        // explore on board.Clone() if you must look ahead.
        SolverMove ChooseMove(PlayoutBoard board, System.Random rng);
    }
}
