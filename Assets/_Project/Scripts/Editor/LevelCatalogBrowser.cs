using System.Collections.Generic;
using System.IO;

namespace CatapultGames.Editor
{
    // ◀ [n] / N ▶ state for the title bar. Holds the sorted list and the current
    // index; it does not load levels and does not know the UI.
    public sealed class LevelCatalogBrowser
    {
        public IReadOnlyList<LevelCatalogEntry> Entries => _entries;
        public int  Count   => _entries.Count;
        public int  Index   => _index;
        public bool CanPrev => _index > 0;
        public bool CanNext => _index >= 0 && _index < _entries.Count - 1;
        public LevelCatalogEntry Current => _index >= 0 && _index < _entries.Count ? _entries[_index] : null;

        private List<LevelCatalogEntry> _entries = new List<LevelCatalogEntry>();
        private int _index = -1;

        public void Refresh(string keepPath = null)
        {
            _entries = LevelCatalog.Scan();
            _index   = IndexOfPath(keepPath);
        }

        public int IndexOfPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return -1;
            string full = Path.GetFullPath(path);
            for (int i = 0; i < _entries.Count; i++)
                if (Path.GetFullPath(_entries[i].path) == full)
                    return i;
            return -1;
        }

        public void SelectPath(string path) => _index = IndexOfPath(path);
        public LevelCatalogEntry Prev() {
            if (CanPrev)
                _index--;
            return Current;
        }
        public LevelCatalogEntry Next() {
            if (CanNext)
                _index++;
            return Current;
        }
    }
}
