using System;

namespace CatapultGames
{
    // Paint footprint of a ball.
    //   Square — fills an NxN block sized by powerLevel (1→2x2, 2→3x3, 3→4x4).
    //   L      — fills the two grid edges that meet at the nearest corner, scaling
    //            to the level size (10x10 → a 10+10 L). Auto-rotates to the aimed
    //            corner. powerLevel only affects the ball's size/look, not the paint.
    // Serialized as an int by JsonUtility, so old levels (no "shape") default to Square.
    public enum BallShape { Square = 0, L = 1 }

    // powerLevel 1 → 2x2, 2 → 3x3, 3 → 4x4
    [Serializable]
    public class BallData
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
