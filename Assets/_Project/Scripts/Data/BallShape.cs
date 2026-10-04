namespace CatapultGames
{
    // Paint footprint of a ball. The rules themselves live in GameConstants —
    // add a value here and the case there, and painting, the aim preview, the
    // validator and the auto-solver all follow.
    //
    //   Square   — an NxN block sized by powerLevel (1→2x2, 2→3x3, 3→4x4).
    //   L        — the two grid edges meeting at the nearest corner, scaling to the
    //              level size (10x10 → a 10+10 L). Auto-rotates to the aimed corner.
    //              powerLevel only affects the ball's size/look, not the paint.
    //   Line     — a horizontal run centred on the aimed cell (3 / 5 / 7 by power).
    //   Column   — the same run, vertical.
    //   Plus     — a cross, arms 1 / 2 / 3 cells long (5 / 9 / 13 cells).
    //   Diagonal — an X, arms 1 / 2 / 3 cells long (5 / 9 / 13 cells).
    //
    // Line and Column are deliberately fixed to an axis rather than auto-rotating:
    // the player has to be able to predict the stamp before committing a ball, and
    // a shape that picks its own orientation cannot be reasoned about.
    //
    // Serialized as an int by JsonUtility, so old levels (no "shape") default to
    // Square. APPEND ONLY — the numbers are written into every level file.
    public enum BallShape
    {
        Square   = 0,
        L        = 1,
        Line     = 2,
        Column   = 3,
        Plus     = 4,
        Diagonal = 5
    }
}
