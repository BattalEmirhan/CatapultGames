using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames.Editor
{
    // Owns every write the brushes make to LevelData, and the guards in front
    // of them. The window never touches level.cells / level.balls directly.
    //
    // Guard philosophy: refuse the write instead of invalidating the level.
    // Only CELL-LOCAL rules are guarded here (a joker with no colour, a ball with
    // no colour). Rules that need the whole board — coverage, isolated cells,
    // solvability — are deliberately NOT guarded; LevelValidator reports them in
    // the Status card, because guarding everything makes authoring impossible
    // mid-edit.
    //
    // The cells array is kept NORMALISED: exactly width*height entries in
    // row-major order (index = y * width + x). Older files can be sparse
    // (level1 ships 77 of 144 cells), so Normalize() runs on every load.
    public static class LevelEditOps
    {
        public static void Normalize(LevelData level)
        {
            if (level == null)
                return;
            level.grid   ??= new GridConfig();
            level.camera ??= new CameraConfig();
            level.metadata ??= new LevelMetadata();
            level.balls  ??= new BallData[0];

            int w = Mathf.Clamp(level.grid.width,  1, 50);
            int h = Mathf.Clamp(level.grid.height, 1, 50);
            level.grid.width  = w;
            level.grid.height = h;

            var arr = new CellData[w * h];
            if (level.cells != null)
                foreach (var c in level.cells)
                {
                    if (c == null)
                        continue;
                    if (c.gridX < 0 || c.gridX >= w || c.gridY < 0 || c.gridY >= h)
                        continue;   // prune
                    arr[c.gridY * w + c.gridX] = c;
                }

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                arr[y * w + x] ??= new CellData(x, y, CellColor.None);

            level.cells = arr;
        }

        public static bool InBounds(LevelData level, int x, int y) =>
            level != null && x >= 0 && y >= 0 && x < level.grid.width && y < level.grid.height;

        public static CellData Cell(LevelData level, int x, int y) =>
            InBounds(level, x, y) ? level.cells[y * level.grid.width + x] : null;

        // Resize keeps every cell that still fits and pads the rest with empty
        // board — the one operation that changes the array's shape.
        public static void Resize(LevelData level, int w, int h)
        {
            if (level == null)
                return;
            level.grid.width  = Mathf.Clamp(w, 1, 50);
            level.grid.height = Mathf.Clamp(h, 1, 50);
            Normalize(level);
        }

        // A Stone carries no colour (it never paints, and a coloured one reads as
        // a target on the board), so painting Stone writes None regardless of the
        // swatch. A Joker with no colour is nothing — it has no colour to fill to
        // — so that one is refused.
        public static bool TryPaint(LevelData level, int x, int y, CellColor color, CellType type, out string reason)
        {
            reason = null;
            var cell = Cell(level, x, y);
            if (cell == null)
                return false;

            if (type == CellType.Joker && color == CellColor.None)
            {
                reason = "A joker needs a colour to fill to — pick a colour first.";
                return false;
            }
            if (type == CellType.Stone)
                color = CellColor.None;

            cell.outlineColor = color;
            cell.cellType     = type;
            return true;
        }

        public static void Erase(LevelData level, int x, int y)
        {
            var cell = Cell(level, x, y);
            if (cell == null)
                return;
            // Clears the type too — a bare cell with a leftover Stone flag would
            // still block stamps while looking like empty board.
            cell.outlineColor = CellColor.None;
            cell.cellType     = CellType.Normal;
        }

        public static int PaintMany(LevelData level, IEnumerable<(int x, int y)> cells,
                                    CellColor color, CellType type, out string reason)
        {
            reason = null;
            int n = 0;
            foreach (var (x, y) in cells)
                if (TryPaint(level, x, y, color, type, out reason))
                    n++;
                else if (reason != null)
                    return n;   // guard fired — stop, keep the reason
            return n;
        }

        public static int EraseMany(LevelData level, IEnumerable<(int x, int y)> cells)
        {
            int n = 0;
            foreach (var (x, y) in cells) { Erase(level, x, y); n++; }
            return n;
        }

        public static int Brush(LevelData level, int cx, int cy, int radius,
                                CellColor color, CellType type, out string reason)
        {
            var list = new List<(int, int)>();
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                if (InBounds(level, cx + dx, cy + dy))
                    list.Add((cx + dx, cy + dy));
            return PaintMany(level, list, color, type, out reason);
        }

        // Flood fills the orthogonally-connected region that shares the start
        // cell's colour AND type, so a fill over a red patch does not leak into
        // the red ice next to it.
        public static int FloodFill(LevelData level, int sx, int sy, CellColor color, CellType type, out string reason)
        {
            reason = null;
            var start = Cell(level, sx, sy);
            if (start == null)
                return 0;
            if (start.outlineColor == color && start.cellType == type)
                return 0;

            // Guard once up front — a refused fill must not half-apply.
            if (type == CellType.Joker && color == CellColor.None)
            {
                reason = "A joker needs a colour to fill to — pick a colour first.";
                return 0;
            }

            CellColor tc = start.outlineColor;
            CellType  tt = start.cellType;
            var queue   = new Queue<(int, int)>();
            var visited = new HashSet<(int, int)>();
            var region  = new List<(int, int)>();
            queue.Enqueue((sx, sy));
            visited.Add((sx, sy));

            int[] ddx = { 0, 0, -1, 1 };
            int[] ddy = { -1, 1, 0, 0 };
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                region.Add((x, y));
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + ddx[d], ny = y + ddy[d];
                    if (visited.Contains((nx, ny)))
                        continue;
                    var nc = Cell(level, nx, ny);
                    if (nc == null || nc.outlineColor != tc || nc.cellType != tt)
                        continue;
                    visited.Add((nx, ny));
                    queue.Enqueue((nx, ny));
                }
            }

            return PaintMany(level, region, color, type, out reason);
        }

        public static bool TryAddBalls(LevelData level, CellColor color, int power, BallShape shape, int count, out string reason)
        {
            reason = null;
            if (level == null)
                return false;
            if (color == CellColor.None)
            {
                reason = "A ball needs a colour.";
                return false;
            }
            var list = new List<BallData>(level.balls ?? new BallData[0]);
            for (int i = 0; i < Mathf.Max(1, count); i++)
                list.Add(new BallData(color, Mathf.Clamp(power, 1, 3), shape));
            level.balls = list.ToArray();
            return true;
        }

        public static void RemoveBall(LevelData level, int index)
        {
            if (level?.balls == null || index < 0 || index >= level.balls.Length)
                return;
            var list = new List<BallData>(level.balls);
            list.RemoveAt(index);
            level.balls = list.ToArray();
        }

        public static void MoveBall(LevelData level, int index, int delta)
        {
            var arr = level?.balls;
            if (arr == null)
                return;
            int j = index + delta;
            if (index < 0 || index >= arr.Length || j < 0 || j >= arr.Length)
                return;
            (arr[index], arr[j]) = (arr[j], arr[index]);
        }

        public static void ShuffleBalls(LevelData level, System.Random rng)
        {
            var arr = level?.balls;
            if (arr == null || arr.Length < 2)
                return;
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }

        public static void ClearBalls(LevelData level)
        {
            if (level != null)
                level.balls = new BallData[0];
        }

        // Same rule the game counts progress by: Stone is scenery, bare board is
        // nothing. Keep in step with CellView.IsPaintTarget.
        public static bool IsPaintTarget(CellData c) =>
            c != null && c.outlineColor != CellColor.None && c.cellType != CellType.Stone;

        public static int CountTargets(LevelData level)
        {
            int n = 0;
            if (level?.cells != null)
                foreach (var c in level.cells)
                    if (IsPaintTarget(c))
                        n++;
            return n;
        }

        public static int CountType(LevelData level, CellType type)
        {
            int n = 0;
            if (level?.cells != null)
                foreach (var c in level.cells)
                    if (c != null && c.cellType == type)
                        n++;
            return n;
        }

        public static int CountColors(LevelData level)
        {
            var set = new HashSet<CellColor>();
            if (level?.cells != null)
                foreach (var c in level.cells)
                    if (IsPaintTarget(c))
                        set.Add(c.outlineColor);
            return set.Count;
        }
    }
}
