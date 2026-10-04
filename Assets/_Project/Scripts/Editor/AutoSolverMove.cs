using UnityEngine;

namespace CatapultGames.Editor
{
    public struct AutoSolverMove
    {
        public bool          wasted;       // true when the ball painted nothing
        public int           landX, landY; // chosen landing cell (-1 when wasted)
        public CellColor     color;
        public int           power;
        public Vector2Int[]  hit;          // cells this move put paint into
        public Vector2Int[]  filled;       // the subset that finished (Ice needs two)
    }
}
