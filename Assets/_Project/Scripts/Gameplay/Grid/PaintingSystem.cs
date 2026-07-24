using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Task 10 — Painting rules.
    // Color-match check + power-level area coverage using the locked placement rule.
    public static class PaintingSystem
    {
        // Apply paint: fills every color-matched, unfilled cell in the NxN area.
        // Returns the list of cells that were newly filled.
        public static List<Vector2Int> Paint(GridRenderer grid, int landX, int landY, BallData ball)
        {
            var painted = new List<Vector2Int>();

            foreach (var c in GameConstants.GetPaintedCells(landX, landY, ball, grid.Width, grid.Height))
            {
                if (OutOfBounds(grid, c)) continue;
                if (!grid.TryGetCell(c.x, c.y, out var cell)) continue;
                if (cell.OutlineColor != ball.color) continue;
                if (cell.IsFilled) continue;

                grid.SetFilled(c.x, c.y, true);
                painted.Add(c);
            }

            return painted;
        }

        // Preview: which cells WOULD be filled — used by aim preview (Task 13).
        // Does NOT mutate the grid.
        public static List<Vector2Int> Preview(GridRenderer grid, int landX, int landY, BallData ball)
        {
            var result = new List<Vector2Int>();

            foreach (var c in GameConstants.GetPaintedCells(landX, landY, ball, grid.Width, grid.Height))
            {
                if (OutOfBounds(grid, c)) continue;
                if (!grid.TryGetCell(c.x, c.y, out var cell)) continue;
                if (cell.OutlineColor == ball.color && !cell.IsFilled)
                    result.Add(c);
            }

            return result;
        }

        // Cells that WOULD be filled, ordered nearest-first from the landing cell.
        // Lets the launcher raise them as a wave rippling outward from the hit.
        // Does NOT mutate the grid (the caller fills them over time).
        public static List<Vector2Int> PaintTargetsOrdered(GridRenderer grid, int landX, int landY, BallData ball)
        {
            var result = Preview(grid, landX, landY, ball);
            result.Sort((a, b) =>
            {
                int da = (a.x - landX) * (a.x - landX) + (a.y - landY) * (a.y - landY);
                int db = (b.x - landX) * (b.x - landX) + (b.y - landY) * (b.y - landY);
                return da.CompareTo(db);
            });
            return result;
        }

        // How many color-matching unfilled cells exist for a ball in its NxN area.
        public static int CountPaintable(GridRenderer grid, int landX, int landY, BallData ball)
        {
            int count = 0;
            foreach (var c in GameConstants.GetPaintedCells(landX, landY, ball, grid.Width, grid.Height))
            {
                if (OutOfBounds(grid, c)) continue;
                if (!grid.TryGetCell(c.x, c.y, out var cell)) continue;
                if (cell.OutlineColor == ball.color && !cell.IsFilled)
                    count++;
            }
            return count;
        }

        private static bool OutOfBounds(GridRenderer grid, Vector2Int c) =>
            c.x < 0 || c.x >= grid.Width || c.y < 0 || c.y >= grid.Height;
    }
}
