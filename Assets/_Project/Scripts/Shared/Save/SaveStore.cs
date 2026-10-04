namespace CatapultGames
{
    // The one ISaveStore the game uses. A static seam rather than injection: the
    // project has no DI container, and every reader is a static helper
    // (PlayerProgress, LevelLoader.SelectLevel, GameAudio.Muted).
    public static class SaveStore
    {
        public static ISaveStore Current { get; set; } = new PlayerPrefsSaveStore();
    }
}
