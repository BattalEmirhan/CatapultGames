namespace CatapultGames.Editor
{
    public sealed class LevelCatalogEntry
    {
        public string path;     // absolute
        public string name;     // file stem, e.g. "level12"
        public int    number;   // trailing number, -1 when there is none

        public override string ToString() => name;
    }
}
