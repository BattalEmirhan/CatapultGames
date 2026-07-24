using UnityEngine;

namespace CatapultGames
{
    public static class GameConstants
    {
        // Shared trajectory simulation resolution.
        // AimPreview and BallLauncher MUST use the SAME values so the previewed
        // landing cell always matches where the ball actually lands.
        public const int   TrajectorySteps    = 96;
        public const float TrajectoryTimeStep = 0.04f;

        // Centered placement rule:
        //   Power 1 (2x2): landing cell = top-left    → offset (0, 0), size 2
        //   Power 2 (3x3): landing cell = center       → offset (-1,-1), size 3
        //   Power 3 (4x4): landing cell = (1,1) inside → offset (-1,-1), size 4
        public static Vector2Int GetPaintOffset(int powerLevel)
        {
            return powerLevel switch
            {
                1 => new Vector2Int(0, 0),
                2 => new Vector2Int(-1, -1),
                3 => new Vector2Int(-1, -1),
                _ => Vector2Int.zero
            };
        }

        public static int GetPaintSize(int powerLevel)
        {
            return powerLevel switch
            {
                1 => 2,
                2 => 3,
                3 => 4,
                _ => 1
            };
        }

        // Returns all grid coords painted by a SQUARE ball landing at (landX, landY).
        public static Vector2Int[] GetPaintedCells(int landX, int landY, int powerLevel)
        {
            Vector2Int offset = GetPaintOffset(powerLevel);
            int size          = GetPaintSize(powerLevel);
            int startX        = landX + offset.x;
            int startY        = landY + offset.y;

            var result = new Vector2Int[size * size];
            int i = 0;
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    result[i++] = new Vector2Int(startX + dx, startY + dy);

            return result;
        }

        // Shape-aware paint footprint. Square balls use the NxN rule above; L balls
        // use a corner L that auto-rotates toward the nearest grid corner (needs the
        // grid size to know which corner the landing cell is closest to).
        public static Vector2Int[] GetPaintedCells(int landX, int landY, BallData ball, int gridW, int gridH)
        {
            if (ball != null && ball.shape == BallShape.L)
                return GetLCells(landX, landY, gridW, gridH);
            return GetPaintedCells(landX, landY, ball?.powerLevel ?? 1);
        }

        // The L: bend cell on the landing position, both arms running toward the grid
        // INTERIOR and all the way to the grid EDGES. Aiming at a corner therefore
        // paints that corner's two full edges, scaling to the level size (10x10 → a
        // 10+10 L, 10x4 → 10+4, etc.). The arm directions auto-rotate to whichever
        // corner the landing cell is closest to.
        private static Vector2Int[] GetLCells(int landX, int landY, int gridW, int gridH)
        {
            int sx = landX <= (gridW - 1) * 0.5f ? 1 : -1;   // interior x direction
            int sy = landY <= (gridH - 1) * 0.5f ? 1 : -1;   // interior y direction
            int endX = sx > 0 ? gridW - 1 : 0;               // far vertical edge this arm reaches
            int endY = sy > 0 ? gridH - 1 : 0;               // far horizontal edge this arm reaches

            var cells = new System.Collections.Generic.List<Vector2Int>(gridW + gridH);
            // Horizontal arm: from the bend along row landY out to the far edge.
            for (int x = landX; ; x += sx) { cells.Add(new Vector2Int(x, landY)); if (x == endX) break; }
            // Vertical arm: from the cell above/below the bend out to the far edge
            // (the bend itself is already added above).
            for (int y = landY + sy; sy > 0 ? y <= endY : y >= endY; y += sy)
                cells.Add(new Vector2Int(landX, y));
            return cells.ToArray();
        }

        // How many cells a ball paints at most — square area, or the L's two full grid
        // edges (W + H - 1). Used by coverage validation. Shape-aware.
        public static int GetPaintCellCount(BallData ball, int gridW, int gridH)
        {
            if (ball != null && ball.shape == BallShape.L) return gridW + gridH - 1;
            int s = GetPaintSize(ball?.powerLevel ?? 1);
            return s * s;
        }

        public static readonly Color32[] CellColorPalette = new Color32[]
        {
            new Color32(  0,   0,   0,   0),  // None    (transparent)
            new Color32(220,  50,  50, 255),  // Red
            new Color32( 60, 180,  60, 255),  // Green
            new Color32( 50, 100, 220, 255),  // Blue
            new Color32( 65,  65,  75, 255),  // Black (charcoal — visible on dark bg)
            new Color32(240, 240, 240, 255),  // White
            new Color32(230,  80, 160, 255),  // Pink
            new Color32(130,  50, 210, 255),  // Purple
        };

        public static Color32 GetColor(CellColor c) => CellColorPalette[(int)c];

        // Same as GetColor but as a float Color (handy for VFX / materials).
        public static Color GetColorF(CellColor c)
        {
            Color32 c32 = CellColorPalette[(int)c];
            return new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f, c32.a / 255f);
        }
    }
}
