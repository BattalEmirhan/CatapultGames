namespace CatapultGames.Editor
{
    // Brush identities. Pick one, then click or drag straight on the grid; the
    // stroke is committed on pointer-up as one undo step.
    public enum LevelPaintTool
    {
        Paint       = 0,   // colour + type onto one cell
        Erase       = 1,   // colour None, type Normal
        Fill        = 2,   // flood-fill the contiguous same-colour region
        Brush       = 3,   // Paint with a radius
        RectSelect  = 4,   // drag a rectangle → selection
        MultiSelect = 5    // toggle single cells in/out of the selection
    }
}
