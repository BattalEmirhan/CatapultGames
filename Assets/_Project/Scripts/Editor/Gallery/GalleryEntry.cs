namespace CatapultGames.Editor
{
    internal sealed class GalleryEntry
    {
        public LevelCatalogEntry catalog;
        public LevelData level;
        public LevelDifficulty authored;
        public bool valid;        // LevelValidator (coverage) — no errors
        public bool solvable;     // LevelAutoSolver in authored order
        public int  targets, balls, ice, stone, joker;
    }
}
