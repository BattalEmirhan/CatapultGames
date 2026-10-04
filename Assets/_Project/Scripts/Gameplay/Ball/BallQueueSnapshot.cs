namespace CatapultGames
{
    // A whole-queue snapshot rather than a "put the ball back" call, because
    // undoing one shot can also have to undo a RemoveColor purge that the shot
    // triggered. Restoring the entire state covers both without special cases.
    public readonly struct BallQueueSnapshot
    {
        public bool IsValid => balls != null;

        internal readonly BallData[] balls;
        internal readonly int        index;

        internal BallQueueSnapshot(BallData[] balls, int index) { this.balls = balls; this.index = index; }
    }
}
