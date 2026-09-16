namespace CatapultGames
{
    // What a cell does when paint reaches it. Orthogonal to CellColor: a cell has
    // a target colour AND a type, and the type decides how paint behaves there.
    //
    //   Normal — one hit fills it, if the colours match.
    //   Ice    — needs two hits: the first cracks it, the second fills it. Costs
    //            paint, not aim, so it deepens a level without adding cells.
    //   Stone  — never fills: a hole in the board the stamp simply paints around.
    //            (The older "shadows the cells behind it" rule was removed on
    //            2026-09-15 — it could not be predicted from the board.)
    //   Joker  — any colour fills it. It still fills to its authored colour, so
    //            the picture comes out as drawn. Legacy: levels are no longer
    //            generated with jokers — the Rainbow BOOSTER ball plays that
    //            role now — but authored ones keep working.
    //
    // Serialized as an int by JsonUtility. APPEND ONLY — the numbers are written
    // into every level file, exactly like CellColor and BallShape. A level
    // authored before this existed has no "cellType" field and deserialises to
    // 0 = Normal, which is the behaviour it always had.
    public enum CellType
    {
        Normal = 0,
        Ice    = 1,
        Stone  = 2,
        Joker  = 3
    }
}
