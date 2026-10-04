using UnityEngine;

namespace CatapultGames
{
    // Everything paint has to know about a board, as flat arrays indexed
    // y * width + x. Cells that need nothing (already filled, bare board,
    // Stone) have hits == 0, which is the single test for "not a target".
    public sealed class TargetBoard
    {
        public readonly int         width;
        public readonly int         height;
        public readonly CellColor[] colors;   // colour that fills this cell
        public readonly byte[]      hits;     // hits still needed (Ice: 2)
        public readonly bool[]      wild;     // Joker — any colour fills it
        public readonly bool[]      stone;    // a hole — never painted

        public TargetBoard(int w, int h)
        {
            width  = Mathf.Max(0, w);
            height = Mathf.Max(0, h);
            int n  = width * height;
            colors = new CellColor[n];
            hits   = new byte[n];
            wild   = new bool[n];
            stone  = new bool[n];
        }

        public int  Index(int x, int y)     => y * width + x;
        public bool InBounds(int x, int y)  => x >= 0 && x < width && y >= 0 && y < height;

        // Cells still waiting for paint — an Ice cell counts once here (it is
        // one cell) but twice in TotalHits (it takes two balls' worth).
        public int TargetCellCount()
        {
            int n = 0;
            foreach (var h in hits)
                if (h > 0)
                    n++;
            return n;
        }

        public int TotalHits()
        {
            int n = 0;
            foreach (var h in hits)
                n += h;
            return n;
        }

        public void Set(int x, int y, CellColor color, CellType type)
        {
            if (!InBounds(x, y))
                return;
            int i = Index(x, y);

            if (type == CellType.Stone)
            {
                stone[i]  = true;
                colors[i] = CellColor.None;
                hits[i]   = 0;
                return;
            }
            if (color == CellColor.None)
                return;

            colors[i] = color;
            hits[i]   = (byte)GameConstants.GetRequiredHits(type);
            wild[i]   = type == CellType.Joker;
        }
    }
}
