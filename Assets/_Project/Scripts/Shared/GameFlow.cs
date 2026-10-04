using System;

namespace CatapultGames
{
    // Requests between GameScene (board, result screen) and UIScene (menus, level
    // select, dev picker). Scenes cannot reference each other at author time, and
    // the project has no message bus, so this static hub is the seam. Levels load
    // in place: nothing here ever reloads a scene.
    public static class GameFlow
    {
        public static event Action<string> LevelRequested;
        public static event Action<string> LevelStarted;
        public static event Action MenuRequested;
        public static event Action LevelSelectRequested;

        public static void RequestLevel(string levelName)
        {
            if (!string.IsNullOrEmpty(levelName))
                LevelRequested?.Invoke(levelName);
        }

        public static void ReportLevelStarted(string levelName) => LevelStarted?.Invoke(levelName);

        public static void RequestMenu() => MenuRequested?.Invoke();

        public static void RequestLevelSelect() => LevelSelectRequested?.Invoke();
    }
}
