namespace CatapultGames.Editor
{
    // One value per UXML panel and per tab-bar button (see LevelEditorWindow.uxml:
    // "tab-panel-<name>" / "tab-<name>"). The four are not arbitrary — they are
    // the produce / look / fix / measure loop the whole tool is built around.
    public enum LevelEditorTab
    {
        Editor  = 0,
        Solving = 1,
        Produce = 2,
        Gallery = 3
    }
}
