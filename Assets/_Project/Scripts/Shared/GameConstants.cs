using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    public static class GameConstants
    {
        // ── Gravity ───────────────────────────────────────────────────────
        // The one and only downward acceleration in the game. Two consumers read
        // it and they must never disagree, or the aim preview stops matching
        // where the ball really lands:
        //   · TrajectorySimulator — integrates the arc AimPreview draws and
        //                           BallLauncher flies the ball along
        //   · LaunchSolver        — solves the velocity that reaches the aimed
        //                           cell (uses the magnitude)
        //
        // Signed world-space Y, matching Physics.gravity's convention. Note the
        // ball is not a physics body, so Unity's own Physics.gravity plays no
        // part — this constant is the whole story.
        //
        // Raise the MAGNITUDE for flatter, faster-looking arcs: a steeper pull
        // needs more launch speed to reach the same cell.
        public const float Gravity = -9.81f;

        // |Gravity| — for range solves that work in positive g.
        public const float GravityMagnitude = -Gravity;

        // Shared trajectory simulation resolution.
        // AimPreview and BallLauncher MUST use the SAME values so the previewed
        // landing cell always matches where the ball actually lands.
        public const int   TrajectorySteps    = 96;
        public const float TrajectoryTimeStep = 0.04f;

        // Square stamps are ODD-sized and CENTRED on the aimed cell:
        //   Power 1 → 1x1, Power 2 → 3x3, Power 3 → 5x5.
        // Even sizes (the old 2x2 / 4x4) had no centre, so the aimed cell sat in a
        // different corner of the stamp per power level — the single most common
        // "it landed off target" complaint. Now every shape (square, run, cross)
        // shares one rule: what you tap is the middle of what you paint.
        public static Vector2Int GetPaintOffset(int powerLevel)
        {
            int half = (GetPaintSize(powerLevel) - 1) / 2;
            return new Vector2Int(-half, -half);
        }

        public static int GetPaintSize(int powerLevel)
        {
            return powerLevel switch
            {
                1 => 1,
                2 => 3,
                3 => 5,
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

        // ── Non-square stamp sizing ───────────────────────────────────────
        // Runs (Line / Column) are odd-length so they centre on the aimed cell;
        // Plus / Diagonal grow by arm length. Both scale with powerLevel, which is
        // what keeps the power buttons meaningful for every shape except L.
        private static int GetRunLength(int powerLevel) => powerLevel switch
        {
            1 => 3, 2 => 5, 3 => 7, _ => 1
        };

        private static int GetArmLength(int powerLevel) => powerLevel switch
        {
            1 => 1, 2 => 2, 3 => 3, _ => 0
        };

        // Shape-aware paint footprint — the single place a stamp's shape is decided.
        // Everything downstream (PaintingSystem, AimPreview, CoverageAnalyzer,
        // LevelValidator, LevelAutoSolver) reads it through here, so a new shape only
        // has to be added once.
        //
        // Coordinates may fall outside the grid; every consumer bounds-checks.
        public static Vector2Int[] GetPaintedCells(int landX, int landY, BallData ball, int gridW, int gridH)
        {
            int power = ball?.powerLevel ?? 1;

            return (ball?.shape ?? BallShape.Square) switch
            {
                BallShape.L        => GetLCells(landX, landY, gridW, gridH),
                BallShape.Line     => GetRunCells(landX, landY, power, 1, 0),
                BallShape.Column   => GetRunCells(landX, landY, power, 0, 1),
                BallShape.Plus     => GetCrossCells(landX, landY, power, diagonal: false),
                BallShape.Diagonal => GetCrossCells(landX, landY, power, diagonal: true),
                _                  => GetPaintedCells(landX, landY, power)
            };
        }

        // A straight run centred on the landing cell, stepping by (dx, dy).
        private static Vector2Int[] GetRunCells(int landX, int landY, int powerLevel, int dx, int dy)
        {
            int len  = GetRunLength(powerLevel);
            int half = (len - 1) / 2;

            var cells = new Vector2Int[len];
            for (int i = 0; i < len; i++)
            {
                int step = i - half;
                cells[i] = new Vector2Int(landX + dx * step, landY + dy * step);
            }
            return cells;
        }

        // Cross on the landing cell: four arms, orthogonal (Plus) or diagonal (X).
        private static Vector2Int[] GetCrossCells(int landX, int landY, int powerLevel, bool diagonal)
        {
            int arm = GetArmLength(powerLevel);

            var cells = new Vector2Int[4 * arm + 1];
            cells[0]  = new Vector2Int(landX, landY);

            int i = 1;
            for (int d = 1; d <= arm; d++)
            {
                if (diagonal)
                {
                    cells[i++] = new Vector2Int(landX + d, landY + d);
                    cells[i++] = new Vector2Int(landX - d, landY - d);
                    cells[i++] = new Vector2Int(landX + d, landY - d);
                    cells[i++] = new Vector2Int(landX - d, landY + d);
                }
                else
                {
                    cells[i++] = new Vector2Int(landX + d, landY);
                    cells[i++] = new Vector2Int(landX - d, landY);
                    cells[i++] = new Vector2Int(landX, landY + d);
                    cells[i++] = new Vector2Int(landX, landY - d);
                }
            }
            return cells;
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
        // edges (W + H - 1). Shape-aware.
        //
        // This is the stamp's SIZE, not what it would paint on a real board: a 4x4
        // stamp dropped on a one-row stripe covers 4 cells, not 16. Do not use it to
        // judge whether a level is completable — CoverageAnalyzer does that against
        // the actual cells.
        public static int GetPaintCellCount(BallData ball, int gridW, int gridH)
        {
            int power = ball?.powerLevel ?? 1;

            return (ball?.shape ?? BallShape.Square) switch
            {
                BallShape.L        => gridW + gridH - 1,
                BallShape.Line     => GetRunLength(power),
                BallShape.Column   => GetRunLength(power),
                BallShape.Plus     => 4 * GetArmLength(power) + 1,
                BallShape.Diagonal => 4 * GetArmLength(power) + 1,
                _                  => GetPaintSize(power) * GetPaintSize(power)
            };
        }

        // ── Cell types: match, cost and reach ─────────────────────────────
        // The three questions a special cell answers, all in one place so that
        // painting (PaintingSystem, live grid), analysis (CoverageAnalyzer, arrays)
        // and the auto-solver can never drift apart on what a cell does.

        // Does this ball's colour fill that cell? Joker takes any colour, Stone
        // takes none, and a bare board cell is not a target at all.
        public static bool ColorMatches(CellColor cellColor, CellType cellType, CellColor ballColor)
        {
            if (cellType == CellType.Stone)     return false;
            if (cellColor == CellColor.None)    return false;
            if (ballColor == CellColor.None)    return false;
            return cellType == CellType.Joker || cellColor == ballColor;
        }

        // Paint hits a cell swallows before it fills. Ice takes two — the first
        // cracks it — so it costs paint without costing an extra cell.
        public static int GetRequiredHits(CellType cellType) =>
            cellType == CellType.Ice ? 2 : 1;

        // Cells strictly BETWEEN the landing cell and (cellX, cellY): the straight
        // path the stamp travels to reach that cell. A Stone anywhere along it
        // absorbs the stamp and the far cell stays unpainted.
        //
        // "Strictly between" leaves the landing cell out, so each consumer checks
        // that one separately: a stamp aimed AT a Stone is absorbed whole and paints
        // nothing at all. (Without that check, aiming at a Stone would be a way to
        // paint straight through it — the shadow only starts one cell out.)
        //
        // Only orthogonal and 45° rays are walked, because those are the lines every
        // stamp is built from — runs, plus/diagonal arms, L arms, and a square's own
        // rows, columns and diagonals. A 4x4 square's off-ray corners have no
        // unambiguous "behind", so they are never shadowed; that keeps the rule
        // statable in one sentence, which matters because the player has to predict
        // it before spending a ball (AimPreview draws the result either way).
        //
        // Fills `into` (cleared first) rather than allocating: BestPlacement calls
        // this for every candidate landing cell on the board.
        public static void GetStampPath(int landX, int landY, int cellX, int cellY,
                                        List<Vector2Int> into)
        {
            into.Clear();

            int dx = cellX - landX;
            int dy = cellY - landY;
            if (!(dx == 0 || dy == 0 || Mathf.Abs(dx) == Mathf.Abs(dy))) return;

            int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            int sx    = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
            int sy    = dy == 0 ? 0 : (dy > 0 ? 1 : -1);

            for (int i = 1; i < steps; i++)
                into.Add(new Vector2Int(landX + sx * i, landY + sy * i));
        }

        // ── Scoring ───────────────────────────────────────────────────────
        // One shot's payout, kept here with the painting rules because it is a rule
        // about the same thing: how much a stamp landing well is worth. GameManager
        // owns the running total, BallLauncher only reports the cell count.
        public const int PointsPerCell = 10;

        // A dense hit pays more per cell than the same cells spread over several
        // shots — that is the whole reward for reading the board before firing.
        public static int GetShotMultiplier(int cellsPainted) =>
            cellsPainted >= 8 ? 4 :
            cellsPainted >= 4 ? 3 :
            cellsPainted >= 2 ? 2 : 1;

        // Consecutive shots that painted something. Capped so a long level cannot
        // run away with the score, and reset by the first wasted ball.
        public const int MaxComboMultiplier = 5;

        public static int GetComboMultiplier(int streak) =>
            Mathf.Clamp(streak, 1, MaxComboMultiplier);

        // Candy / pastel palette (2026-09-15). The enum NAMES are legacy identifiers
        // that stay for JSON compatibility; the hues are what a casual block puzzle
        // reads well on a light board: saturated but soft, no pure black or white.
        // Index == (int)CellColor — never reorder.
        public static readonly Color32[] CellColorPalette = new Color32[]
        {
            new Color32(  0,   0,   0,   0),  // None    (transparent)
            new Color32(255, 107,  97, 255),  // Red     → coral
            new Color32( 86, 214, 156, 255),  // Green   → mint
            new Color32( 86, 168, 255, 255),  // Blue    → sky
            new Color32( 78,  92, 140, 255),  // Black   → navy (still the darkest hue)
            new Color32(255, 216,  92, 255),  // White   → lemon
            new Color32(255, 140, 190, 255),  // Pink    → bubblegum
            new Color32(172, 132, 255, 255),  // Purple  → lavender
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
