using System.Collections.Generic;
using System.IO;

namespace CatapultGames.Editor
{
    public sealed class LevelCatalogEntry
    {
        public string path;     // absolute
        public string name;     // file stem, e.g. "level12"
        public int    number;   // trailing number, -1 when there is none

        public override string ToString() => name;
    }

    // The "live level set": every *.json in Resources/Levels, sorted by trailing
    // number then name. The search is folder-limited on purpose — a same-named
    // copy in an archive folder must never leak into the catalog.
    public static class LevelCatalog
    {
        public static List<LevelCatalogEntry> Scan()
        {
            var list = new List<LevelCatalogEntry>();
            string dir = EditorConstants.LevelsAbsoluteFolder;
            if (!Directory.Exists(dir)) return list;

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
                if (a.number >= 0 && b.number >= 0 && a.number != b.number) return a.number.CompareTo(b.number);
                if (a.number >= 0 && b.number < 0) return -1;
                if (a.number < 0 && b.number >= 0) return 1;
                return string.CompareOrdinal(a.name, b.name);
            });
            return list;
        }

        // Loads and normalises. Returns null (and logs) on a parse failure.
        public static LevelData Load(LevelCatalogEntry entry)
        {
            if (entry == null) return null;
            var level = LevelSerializer.Load(entry.path);
            if (level == null) return null;
            LevelEditOps.Normalize(level);
            return level;
        }

        public static bool Exists(int number) => File.Exists(EditorConstants.LevelPath(number));
    }

    // ◀ [n] / N ▶ state for the title bar. Holds the sorted list and the current
    // index; it does not load levels and does not know the UI.
    public sealed class LevelCatalogBrowser
    {
        private List<LevelCatalogEntry> _entries = new List<LevelCatalogEntry>();
        private int _index = -1;

        public IReadOnlyList<LevelCatalogEntry> Entries => _entries;
        public int  Count   => _entries.Count;
        public int  Index   => _index;
        public bool CanPrev => _index > 0;
        public bool CanNext => _index >= 0 && _index < _entries.Count - 1;
        public LevelCatalogEntry Current => _index >= 0 && _index < _entries.Count ? _entries[_index] : null;

        public void Refresh(string keepPath = null)
        {
            _entries = LevelCatalog.Scan();
            _index   = IndexOfPath(keepPath);
        }

        public int IndexOfPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return -1;
            string full = Path.GetFullPath(path);
            for (int i = 0; i < _entries.Count; i++)
                if (Path.GetFullPath(_entries[i].path) == full) return i;
            return -1;
        }

        public void SelectPath(string path) => _index = IndexOfPath(path);

        public LevelCatalogEntry Prev() { if (CanPrev) _index--; return Current; }
        public LevelCatalogEntry Next() { if (CanNext) _index++; return Current; }
    }
}
