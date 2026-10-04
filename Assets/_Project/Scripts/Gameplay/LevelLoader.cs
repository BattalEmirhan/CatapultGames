using UnityEngine;

namespace CatapultGames
{
    // Reads a LevelData JSON from Resources/Levels/ — the one place levels live;
    // the Level Editor saves straight into it — and lays it out on the board.
    // Levels load in place on GameFlow.LevelRequested (menus, Retry, Next), so the
    // scene set never reloads. Save key "SelectedLevel" remembers the last one.
    public sealed class LevelLoader : MonoBehaviour
    {
        public static string SelectedLevel => SaveStore.Current.GetString(SelectedLevelKey, "");

        [SerializeField] private GridRenderer         grid;
        [SerializeField] private BallQueue            queue;
        [SerializeField] private GridCameraController cam;
        [SerializeField] private GridBoard            board;        // optional
        [SerializeField] private ProgressHUD          progressHUD;  // optional
        [SerializeField] private LaunchAreaAnchor     launchAnchor; // optional — pins balls to screen bottom
        [SerializeField] private GameManager          gameManager;  // optional — a fresh run per level
        [SerializeField] private BoosterSystem        boosters;     // optional — booster counts per level
        [SerializeField] private TutorialHint         tutorial;     // optional — first-run tutorial + level hint

        [Header("Fallback")]
        [SerializeField] private string defaultLevelName = "level1";

        private const string SelectedLevelKey = "SelectedLevel";

        // Name of the level being applied; null for Apply(data) from a harness.
        // GameManager keys progress and "next level" on it.
        private string _currentLevelName;

        private void OnEnable() => GameFlow.LevelRequested += LoadByName;

        private void Start() => LoadByName(SaveStore.Current.GetString(SelectedLevelKey, defaultLevelName));

        private void OnDisable() => GameFlow.LevelRequested -= LoadByName;

        public void LoadByName(string levelName)
        {
            var asset = FindLevel(ref levelName);
            var data  = asset != null ? LevelSerializer.FromJson(asset.text) : null;
            if (data == null)
            {
                Debug.LogError($"[LevelLoader] Could not load level '{levelName}' from Resources/Levels.");
                return;
            }
            SelectLevel(levelName);
            _currentLevelName = levelName;
            Apply(data);
            _currentLevelName = null;
            GameFlow.ReportLevelStarted(levelName);
        }

        public void Apply(LevelData data)
        {
            if (data == null)
                return;
            LayOutBoard(data);
            queue.Load(SanitizeBalls(data.balls));
            progressHUD?.Bind(grid);
            gameManager?.BeginRun(_currentLevelName);
            boosters?.ResetForLevel();
            tutorial?.BeginLevel(_currentLevelName, data.metadata?.hint);
        }

        public static void SelectLevel(string levelName) =>
            SaveStore.Current.SetString(SelectedLevelKey, levelName);

        public static void ClearSelection() =>
            SaveStore.Current.Delete(SelectedLevelKey);

        // The launch area is re-pinned BEFORE the queue loads, so the tray balls are
        // built at their final anchored positions.
        private void LayOutBoard(LevelData data)
        {
            grid.BuildGrid(data);
            board?.Rebuild(data.grid);
            if (cam)
                cam.FitToGrid(data.grid, data.camera);
            launchAnchor?.Reanchor();
        }

        // A stale saved name must not boot into an empty grid: fall back to the first level.
        private static TextAsset FindLevel(ref string levelName)
        {
            var asset = string.IsNullOrEmpty(levelName) ? null : Resources.Load<TextAsset>("Levels/" + levelName);
            if (asset != null)
                return asset;
            levelName = LevelOrder.First;
            return levelName != null ? Resources.Load<TextAsset>("Levels/" + levelName) : null;
        }

        // Null entries dropped and powerLevel clamped to 1-3, so bad JSON never crashes play.
        private static BallData[] SanitizeBalls(BallData[] raw)
        {
            if (raw == null || raw.Length == 0)
                return System.Array.Empty<BallData>();
            var result = new System.Collections.Generic.List<BallData>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == null)
                    continue;
                raw[i].powerLevel = Mathf.Clamp(raw[i].powerLevel, 1, 3);
                result.Add(raw[i]);
            }
            return result.ToArray();
        }
    }
}
