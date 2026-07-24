using System.IO;
using UnityEngine;

namespace CatapultGames
{
    public static class LevelSerializer
    {
        public static void Save(LevelData level, string filePath)
        {
            string json = JsonUtility.ToJson(level, prettyPrint: true);
            File.WriteAllText(filePath, json);
        }

        public static LevelData Load(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<LevelData>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LevelSerializer] Failed to parse {filePath}: {e.Message}");
                return null;
            }
        }

        public static string ToJson(LevelData level) =>
            JsonUtility.ToJson(level, prettyPrint: true);

        public static LevelData FromJson(string json)
        {
            try   { return JsonUtility.FromJson<LevelData>(json); }
            catch (System.Exception e)
            {
                Debug.LogError($"[LevelSerializer] JSON parse error: {e.Message}");
                return null;
            }
        }
    }
}
