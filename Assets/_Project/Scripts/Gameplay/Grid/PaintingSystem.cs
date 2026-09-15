using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Task 10 — Painting rules.
    // Colour-match check + the stamp's footprint, applied to the live grid.
    //
    // Three rules decide whether a covered cell takes paint, and all three live in
    // GameConstants so this class and CoverageAnalyzer (which answers the same
    // question against arrays, for the editor and the dead-end check) can never
    // disagree:
    //   · shape  — GameConstants.GetPaintedCells
    //   · match  — GameConstants.ColorMatches (Joker takes any colour, Stone none)
    //   · reach  — GameConstants.GetStampPath (a Stone shadows what is behind it)
    public static class PaintingSystem
    {
        // Reused by the shadow walk. Aiming calls Preview every frame while the
        // finger is down, so this path must not allocate; the game is single-
        // threaded, which is what makes one shared buffer safe.
        private static readonly List<Vector2Int> _pathBuffer = new();

        // Apply paint: one hit into every cell the stamp reaches and matches.
        // Returns the cells that took a hit (an Ice cell may only have cracked).
        public static List<Vector2Int> Paint(GridRenderer grid, int landX, int landY, BallData ball)
        {
            var painted = Preview(grid, landX, landY, ball);
            foreach (var c in painted)
                grid.ApplyHit(c.x, c.y);
            return painted;
        }

        // Preview: which cells WOULD take a hit — used by aim preview (Task 13).
        // Does NOT mutate the grid.
        public static List<Vector2Int> Preview(GridRenderer grid, int landX, int landY, BallData ball)
        {
            var result = new List<Vector2Int>();

            // Aimed straight at a Stone: the stamp is absorbed whole. Checked here
            // because the shadow walk only covers cells BEYOND the landing cell.
            if (grid.IsBlocking(landX, landY)) return result;

            foreach (var c in GameConstants.GetPaintedCells(landX, landY, ball, grid.Width, grid.Height))
            {
                if (OutOfBounds(grid, c)) continue;
                if (!grid.TryGetCell(c.x, c.y, out var cell)) continue;
                if (cell.IsFilled) continue;
                if (!GameConstants.ColorMatches(cell.OutlineColor, cell.Type, ball.color)) continue;
                if (IsShadowed(grid, landX, landY, c)) continue;
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

        // Does a Stone sit between the landing cell and this one? The stamp stops
        // there, so everything further along that ray stays unpainted.
        private static bool IsShadowed(GridRenderer grid, int landX, int landY, Vector2Int cell)
        {
            GameConstants.GetStampPath(landX, landY, cell.x, cell.y, _pathBuffer);
            foreach (var p in _pathBuffer)
                if (grid.IsBlocking(p.x, p.y)) return true;
            return false;
        }

        private static bool OutOfBounds(GridRenderer grid, Vector2Int c) =>
            c.x < 0 || c.x >= grid.Width || c.y < 0 || c.y >= grid.Height;
    }
}
