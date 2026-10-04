namespace CatapultGames.Editor
{
    // How one simulated run ended. Finite on purpose: the four sum to the run
    // count, and each maps to a designer action (see the Solving tab legend).
    public enum PlayoutOutcome
    {
        Won        = 0,   // every target filled
        OutOfBalls = 1,   // queue empty, targets left — the level is too tight (or the bot too weak)
        DeadEnd    = 2,   // balls left but proven unable to finish (CoverageAnalyzer) — the game only
                          // warns here (undo, boosters); a bot has neither, so for it this is a loss
        Unplayable = 3    // nothing to paint, or no balls, before the first shot
    }
}
