namespace CatapultGames
{
    // What a cell does when paint reaches it. Orthogonal to CellColor: a cell has
    // a target colour AND a type, and the type decides how paint behaves there.
    //
    //   Normal — one hit fills it, if the colours match.
    //   Ice    — needs two hits: the first cracks it, the second fills it. Costs
    //            paint, not aim, so it deepens a level without adding cells.
    //   Stone  — never fills, and absorbs the stamp: cells behind it on the
    //            straight path out from the landing cell stay unpainted
    //            (GameConstants.GetStampPath). A Line cut in half by one Stone
    //            paints only the near side, which is what makes it a puzzle
    //            piece rather than a hole in the board.
    //   Joker  — any colour fills it. It still fills to its authored colour, so
    //            the picture comes out as drawn; the wildcard is only about
    //            which ball is allowed to spend itself on it.
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
