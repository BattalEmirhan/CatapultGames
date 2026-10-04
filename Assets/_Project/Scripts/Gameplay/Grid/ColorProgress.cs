namespace CatapultGames
{
    // Per-color progress: how many cells of each color are filled vs. how
    // many that color needs in total. Ordered by the CellColor enum so the
    // HUD stays stable between refreshes. Used by ProgressHUD.
    public struct ColorProgress
    {
        public CellColor color;
        public int       filled;
        public int       total;
    }
}
