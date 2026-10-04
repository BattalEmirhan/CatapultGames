using System.Collections.Generic;
using System.IO;

namespace CatapultGames.Editor
{
    // The "live level set": every *.json in Resources/Levels, sorted by trailing
    // number then name. The search is folder-limited on purpose — a same-named
    // copy in an archive folder must never leak into the catalog.
    public static class LevelCatalog
    {
        public static List<LevelCatalogEntry> Scan()
        {
            var list = new List<LevelCatalogEntry>();
            string dir = EditorConstants.LevelsAbsoluteFolder;
            if (!Directory.Exists(dir))
                return list;

            foreach (var path in Directory.GetFiles(dir, "*" + EditorConstants.LevelExtension))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                list.Add(new LevelCatalogEntry
                {
                    path   = path,
                    name   = name,
                    number = EditorConstants.LevelNumberOf(name)
                });
            }

            list.Sort((a, b) =>
            {
                // Numbered levels first, in numeric order; unnumbered after, by name.
                if (a.number >= 0 && b.number >= 0 && a.number != b.number)
                    return a.number.CompareTo(b.number);
                if (a.number >= 0 && b.number < 0)
                    return -1;
                if (a.number < 0 && b.number >= 0)
                    return 1;
                return string.CompareOrdinal(a.name, b.name);
            });
            return list;
        }

        // Loads and normalises. Returns null (and logs) on a parse failure.
        public static LevelData Load(LevelCatalogEntry entry)
        {
            if (entry == null)
                return null;
            var level = LevelSerializer.Load(entry.path);
            if (level == null)
                return null;
            LevelEditOps.Normalize(level);
            return level;
        }

        public static bool Exists(int number) => File.Exists(EditorConstants.LevelPath(number));
    }
}
