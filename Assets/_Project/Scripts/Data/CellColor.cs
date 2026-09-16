namespace CatapultGames
{
    // Serialized as an int by JsonUtility. APPEND ONLY — the numbers are written
    // into every level file. Index == GameConstants.CellColorPalette index.
    //
    // The names are legacy identifiers; the actual hues are the candy palette in
    // GameConstants (Red is coral, White is lemon, …).
    //
    // Any (8) is a BALL colour only, never a cell colour: the Rainbow booster
    // recolours the selected ball to Any, and GameConstants.ColorMatches lets it
    // fill every target. Levels never author it and the editor never offers it.
    public enum CellColor
    {
        None   = 0,
        Red    = 1,
        Green  = 2,
        Blue   = 3,
        Black  = 4,
        White  = 5,
        Pink   = 6,
        Purple = 7,
        Any    = 8
    }
}
