using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Painting rules applied to the live grid.
    //
    // Two rules decide whether a covered cell takes paint, and both live in
    // GameConstants so this class and CoverageAnalyzer (which answers the same
    // question against arrays, for the editor and the mid-run check) can never
    // disagree:
    //   · shape  — GameConstants.GetPaintedCells
    //   · match  — GameConstants.ColorMatches (Joker takes any colour, Stone none,
    //              a Rainbow ball matches everything)
    // A Stone is simply a hole: it never paints and never blocks anything else —
    // the old "shadow behind the stone" rule was not readable from the board.
    public static class PaintingSystem
    {
        // Apply paint: one hit into every cell the stamp covers and matches.
        // Returns the cells that took a hit (an Ice cell may only have cracked).
        public static List<Vector2Int> Paint(GridRenderer grid, int landX, int landY, BallData ball)
        {
            var painted = Preview(grid, landX, landY, ball);
            foreach (var c in painted)
                grid.ApplyHit(c.x, c.y);
            return painted;
        }

        // Preview: which cells WOULD take a hit — used by the aim preview.
        // Does NOT mutate the grid.
        public static List<Vector2Int> Preview(GridRenderer grid, int landX, int landY, BallData ball)
        {
            var result = new List<Vector2Int>();

            foreach (var c in GameConstants.GetPaintedCells(landX, landY, ball, grid.Width, grid.Height))
            {
                if (OutOfBounds(grid, c)) continue;
                if (!grid.TryGetCell(c.x, c.y, out var cell)) continue;
                if (cell.IsFilled) continue;
                if (!GameConstants.ColorMatches(cell.OutlineColor, cell.Type, ball.color)) continue;
                result.Add(c);
            }

            return result;
        }

        // Cells that WOULD take a hit, ordered nearest-first from the landing cell.
        // Lets the launcher raise them as a wave rippling outward from the hit.
        // Does NOT mutate the grid (the caller applies the hits over time).
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

        // How many cells a ball would put paint into from here.
        public static int CountPaintable(GridRenderer grid, int landX, int landY, BallData ball) =>
            Preview(grid, landX, landY, ball).Count;

        private static bool OutOfBounds(GridRenderer grid, Vector2Int c) =>
            c.x < 0 || c.x >= grid.Width || c.y < 0 || c.y >= grid.Height;
    }
}
