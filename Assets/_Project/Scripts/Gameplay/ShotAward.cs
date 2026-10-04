namespace CatapultGames
{
    // What the last landed shot paid, so undo can take it back. Without this,
    // "paint, undo, paint the same cells again" would be a score farm.
    internal struct ShotAward
    {
        public bool valid;
        public int  points;
        public int  streakBefore;
    }
}
