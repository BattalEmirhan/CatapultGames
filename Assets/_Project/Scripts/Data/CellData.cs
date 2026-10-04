using System;

namespace CatapultGames
{
    [Serializable]
    public sealed class CellData
    {
        public int gridX;
        public int gridY;
        public CellColor outlineColor;
        public bool isFilled;

        // Ice / Stone / Joker — see CellType. Levels written before this field
        // existed simply lack it, and JsonUtility leaves it at 0 = Normal.
        public CellType cellType;

        public CellData() { }

        public CellData(int x, int y, CellColor color, CellType type = CellType.Normal)
        {
            gridX        = x;
            gridY        = y;
            outlineColor = color;
            isFilled     = false;
            cellType     = type;
        }
    }
}
