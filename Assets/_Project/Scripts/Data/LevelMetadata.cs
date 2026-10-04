using System;

namespace CatapultGames
{
    [Serializable]
    public class LevelMetadata
    {
        public string levelName = "Untitled";
        public string author    = "";
        public int    version   = 1;

        // Optional one-liner shown when the level starts (TutorialHint) — where a
        // level introduces something new, say it in a few words. Empty = no banner.
        // Missing in older files, which JsonUtility reads as "".
        public string hint      = "";
    }
}
