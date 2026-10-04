using UnityEngine;

namespace CatapultGames
{
    public sealed class PlayerPrefsSaveStore : ISaveStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);

        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);

        public void Delete(string key) => PlayerPrefs.DeleteKey(key);

        public void Flush() => PlayerPrefs.Save();
    }
}
