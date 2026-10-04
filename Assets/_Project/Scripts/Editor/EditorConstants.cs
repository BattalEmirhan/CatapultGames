using System.IO;
using UnityEngine;

namespace CatapultGames.Editor
{
    // Every path the level tools touch, in one place. The window, the catalog,
    // the Produce runner and the Gallery all read from here, so moving the
    // levels folder or renaming the layout asset is a one-line change.
    public static class EditorConstants
    {
        // Absolute path of the levels folder, created on first use so a fresh
        // checkout can save without a manual mkdir.
        public static string LevelsAbsoluteFolder
        {
            get
            {
                string dir = Path.Combine(Application.dataPath, "Resources", "Levels");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }

        // Levels live where the runtime loads them from (Resources/Levels) —
        // there is no separate authoring format and no export step. See
        // ARCHITECTURE.md § 9.
        public const string LevelsAssetFolder = "Assets/Resources/Levels";
        public const string LevelExtension    = ".json";
        public const string EditorRoot           = "Assets/_Project/Scripts/Editor";
        public const string LevelEditorLayoutPath = EditorRoot + "/UI/LevelEditorWindow.uxml";
        public const string LevelEditorStylePath  = EditorRoot + "/UI/LevelEditorWindow.uss";
        public const string LevelGridStylePath    = EditorRoot + "/UI/LevelGridElement.uss";

        // File name for level number n. Deliberately NOT zero-padded: the shipped
        // levels are already level1…level5, LevelLoader defaults to "level1", and
        // the catalog sorts by the trailing number rather than alphabetically, so
        // padding buys nothing here and renaming would break existing selections.
        public static string LevelFileName(int number) => $"level{number}";

        public static string LevelPath(int number) =>
            Path.Combine(LevelsAbsoluteFolder, LevelFileName(number) + LevelExtension);

        // "level12" → 12, "boss_final" → -1. The catalog's sort key and the
        // difficulty schedule's input — both read it through here. The rule itself
        // lives in the runtime LevelOrder, so the game's menus number levels the
        // same way the editor does.
        public static int LevelNumberOf(string levelName) => LevelOrder.NumberOf(levelName);
    }
}
