using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CatapultGames
{
    // The play order of the shipped levels: every Resources/Levels/*.json, sorted by
    // the number at the end of its name (level2 before level10 — a plain string sort
    // gets that wrong). The runtime twin of the editor's LevelCatalog, for the
    // menus, "next level" and progress unlocking.
    //
    // Scanned once and cached: Resources.LoadAll parses every level file, and the
    // set cannot change in a built game. Call Refresh() after writing levels in
    // the editor if the cached order must see them in the same Play session.
    public static class LevelOrder
    {
        public static IReadOnlyList<string> Names
        {
            get
            {
                if (_names == null)
                    Refresh();
                return _names;
            }
        }

        public static string First => Names.Count > 0 ? Names[0] : null;

        private static readonly Regex TrailingNumber = new Regex(@"(\d+)\s*$", RegexOptions.Compiled);
        private static string[] _names;

        public static void Refresh()
        {
            var assets = Resources.LoadAll<TextAsset>("Levels");
            var names  = new string[assets.Length];
            for (int i = 0; i < assets.Length; i++)
                names[i] = assets[i].name;

            // Numbered levels by number, anything unnumbered after them by name.
            System.Array.Sort(names, (a, b) =>
            {
                int na = NumberOf(a), nb = NumberOf(b);
                if (na >= 0 && nb >= 0 && na != nb)
                    return na.CompareTo(nb);
                if ((na >= 0) != (nb >= 0))
                    return na >= 0 ? -1 : 1;
                return string.CompareOrdinal(a, b);
            });
            _names = names;
        }

        // "level12" → 12; -1 when the name does not end in a number.
        public static int NumberOf(string levelName)
        {
            if (string.IsNullOrEmpty(levelName))
                return -1;
            var m = TrailingNumber.Match(levelName);
            return m.Success && int.TryParse(m.Groups[1].Value, out int n) ? n : -1;
        }

        public static int IndexOf(string levelName)
        {
            var names = Names;
            for (int i = 0; i < names.Count; i++)
                if (names[i] == levelName)
                    return i;
            return -1;
        }

        // The level after this one, or null at the end of the list (or for a name
        // that is not a shipped level).
        public static string Next(string levelName)
        {
            int i = IndexOf(levelName);
            return i >= 0 && i + 1 < Names.Count ? Names[i + 1] : null;
        }
    }
}
