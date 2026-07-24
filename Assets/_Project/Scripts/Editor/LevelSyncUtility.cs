using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatapultGames.Editor
{
    // Levels are authored in Assets/_Project/Levels but the game loads them from
    // Assets/Resources/Levels at runtime (Resources.Load works on every platform,
    // including Android). It is easy to save a level and forget to export it, so
    // this menu copies every authored level into Resources in one click.
    public static class LevelSyncUtility
    {
        private static string SourceDir => Path.Combine(Application.dataPath, "_Project", "Levels");
        private static string DestDir   => Path.Combine(Application.dataPath, "Resources", "Levels");

        [MenuItem("Window/CatapultGames/Sync Levels → Resources")]
        public static void SyncAll()
        {
            if (!Directory.Exists(SourceDir))
            {
                EditorUtility.DisplayDialog("Sync Levels", "No source folder:\n" + SourceDir, "OK");
                return;
            }
            Directory.CreateDirectory(DestDir);

            int copied = 0;
            foreach (var src in Directory.GetFiles(SourceDir, "*.json"))
            {
                string dest = Path.Combine(DestDir, Path.GetFileName(src));
                File.Copy(src, dest, overwrite: true);
                copied++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[LevelSync] Copied {copied} level(s) → Resources/Levels");
            EditorUtility.DisplayDialog("Sync Levels",
                $"Copied {copied} level(s) to Resources/Levels.", "OK");
        }
    }
}
