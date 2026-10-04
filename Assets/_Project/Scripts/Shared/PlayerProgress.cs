using UnityEngine;

namespace CatapultGames
{
    // What the player has achieved, kept in PlayerPrefs: which levels have been
    // won, keyed by level NAME (not index) so inserting a level later does not
    // mark a different board as done.
    //
    // Unlocking is derived, not stored: a level is open when it is the first one
    // or the level before it has been won. Nothing to migrate when levels are
    // added, and no way for the two to disagree.
    public static class PlayerProgress
    {
        private const string WonKeyPrefix = "Won_";

        public static bool IsWon(string levelName) =>
            !string.IsNullOrEmpty(levelName) && PlayerPrefs.GetInt(WonKeyPrefix + levelName, 0) == 1;

        public static void MarkWon(string levelName)
        {
            if (string.IsNullOrEmpty(levelName) || IsWon(levelName))
                return;
            PlayerPrefs.SetInt(WonKeyPrefix + levelName, 1);
            PlayerPrefs.Save();   // a mobile app can be killed at any moment after a win
        }

        public static bool IsUnlocked(string levelName)
        {
            int i = LevelOrder.IndexOf(levelName);
            if (i < 0)
                return false;
            return i == 0 || IsWon(LevelOrder.Names[i - 1]);
        }

        // Where "Play" should take the player: the first level not yet won, or the
        // last level once everything is done.
        public static string NextToPlay()
        {
            var names = LevelOrder.Names;
            for (int i = 0; i < names.Count; i++)
                if (!IsWon(names[i]))
                    return names[i];
            return names.Count > 0 ? names[names.Count - 1] : null;
        }

        public static int WonCount()
        {
            int n = 0;
            foreach (var name in LevelOrder.Names)
                if (IsWon(name))
                    n++;
            return n;
        }

        // Dev / settings use: forget every level's result.
        public static void ResetAll()
        {
            foreach (var name in LevelOrder.Names)
                PlayerPrefs.DeleteKey(WonKeyPrefix + name);
            PlayerPrefs.Save();
        }
    }
}
