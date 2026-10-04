using System;

namespace CatapultGames
{
    [Serializable]
    public sealed class GridConfig
    {
        public int width    = 8;
        public int height   = 8;
        public float cellSize = 1f;
    }
}
