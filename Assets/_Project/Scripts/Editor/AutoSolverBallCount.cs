namespace CatapultGames.Editor
{
    // A (colour, power) group of leftover balls.
    public struct AutoSolverBallCount
    {
        public CellColor color;
        public int       power;   // 1..3
        public int       count;
    }
}
