using System;

namespace CatapultGames
{
    // powerLevel 1 → 2x2, 2 → 3x3, 3 → 4x4
    [Serializable]
    public sealed class BallData
    {
        public CellColor  color;
        public int        powerLevel = 1;
        public BallShape  shape      = BallShape.Square;

        public BallData() { }

        public BallData(CellColor color, int powerLevel, BallShape shape = BallShape.Square)
        {
            this.color      = color;
            this.powerLevel = powerLevel;
            this.shape      = shape;
        }
    }
}
