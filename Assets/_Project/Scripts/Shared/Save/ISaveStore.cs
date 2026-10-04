namespace CatapultGames
{
    // Everything the game persists goes through this, so the backing store can
    // change in one place. TODO: replace PlayerPrefsSaveStore with a real save
    // service (check the shared package registry first); PlayerPrefs is the fallback.
    public interface ISaveStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void Delete(string key);
        void Flush();
    }
}
