using System;

namespace CatapultGames
{
    [Serializable]
    public class CellData
    {
        public int gridX;
        public int gridY;
        public CellColor outlineColor;
        public bool isFilled;

        public CellData() { }

        public CellData(int x, int y, CellColor color)
        {
            gridX        = x;
            gridY        = y;
            outlineColor = color;
            isFilled     = false;
        }
    }
}
