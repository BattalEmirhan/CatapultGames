using UnityEngine;

namespace CatapultGames
{
    // Level loader.
    // Reads a LevelData JSON from Resources/Levels/ (TextAsset) and populates
    // the GridRenderer, BallQueue, and GridCameraController at runtime.
    // Resources.Load works on every platform, including Android — and it is the
    // one and only place levels live (the Level Editor saves straight into it).
    //
    // PlayerPrefs key "SelectedLevel" stores the level name (file name w/o ext).
    // If the key is missing, defaultLevelName is used.
    public sealed class LevelLoader : MonoBehaviour
    {
        [SerializeField] private GridRenderer         grid;
        [SerializeField] private BallQueue            queue;
        [SerializeField] private GridCameraController cam;
        [SerializeField] private GridBoard            board;        // optional
        [SerializeField] private ProgressHUD          progressHUD;  // optional
        [SerializeField] private LevelPickerHUD       levelPicker;  // optional
        [SerializeField] private LaunchAreaAnchor     launchAnchor; // optional — pins balls to screen bottom
        [SerializeField] private GameManager          gameManager;  // optional — score reset on load
        [SerializeField] private BoosterSystem        boosters;     // optional — booster counts per level
        [SerializeField] private TutorialHint         tutorial;     // optional — first-run tutorial + level hint

        [Header("Fallback")]
        [SerializeField] private string defaultLevelName = "Level_01";

        private const string SelectedLevelKey = "SelectedLevel";

        // Name of the level last loaded from a file; null after Apply(data) from a
        // harness. GameManager keys progress and "next level" on it.
        private string _currentLevelName;

        private void Start()
        {
            string name = PlayerPrefs.GetString(SelectedLevelKey, defaultLevelName);
            if (string.IsNullOrEmpty(name))
                name = defaultLevelName;
            LoadByName(name);
        }

        // Load by name (file name without extension) from Resources/Levels/
        public void LoadByName(string levelName)
        {
            var ta = Resources.Load<TextAsset>("Levels/" + levelName);

            // Fallback to the first available level if the name is stale/missing,
            // so the game never boots into an empty grid.
            if (ta == null)
            {
                var all = Resources.LoadAll<TextAsset>("Levels");
                if (all.Length > 0)
                {
                    ta = all[0];
                    levelName = ta.name;
                }
            }

            if (ta == null)
            {
                Debug.LogError("[LevelLoader] No levels found in Resources/Levels.");
                return;
            }

            var data = LevelSerializer.FromJson(ta.text);
            if (data == null)
            {
                Debug.LogError($"[LevelLoader] Could not parse level '{levelName}'.");
                return;
            }

            // Remember it as the selection too, so Retry (a scene reload) replays THIS
            // level even when it was reached by the in-place dev picker.
            SelectLevel(levelName);
            _currentLevelName = levelName;
            Apply(data);
            _currentLevelName = null;
            levelPicker?.SetCurrent(levelName);
        }

        // Load from a pre-parsed LevelData (e.g. from a test harness)
        public void Apply(LevelData data)
        {
            if (data == null)
                return;
            grid.BuildGrid(data);
            board?.Rebuild(data.grid);
            if (cam)
                cam.FitToGrid(data.grid, data.camera);
            // Pin the launch area to the screen bottom BEFORE loading balls, so the
            // queue rebuilds at the final (anchored) waypoint positions.
            launchAnchor?.Reanchor();
            queue.Load(SanitizeBalls(data.balls));
            progressHUD?.Bind(grid);
            // A level switch is a new run: the picker HUD loads in place rather than
            // reloading the scene, so score, rescue offer and game-over state would
            // otherwise carry over.
            gameManager?.BeginRun(_currentLevelName);
            boosters?.ResetForLevel();
            tutorial?.BeginLevel(_currentLevelName, data.metadata?.hint);
        }

        // Set which level will be loaded when the Gameplay scene starts.
        public static void SelectLevel(string levelName) =>
            PlayerPrefs.SetString(SelectedLevelKey, levelName);

        public static void ClearSelection() =>
            PlayerPrefs.DeleteKey(SelectedLevelKey);

        // Strip null entries and clamp powerLevel to 1-3 so bad JSON never crashes gameplay.
        private static BallData[] SanitizeBalls(BallData[] raw)
        {
            if (raw == null || raw.Length == 0)
                return System.Array.Empty<BallData>();
            var result = new System.Collections.Generic.List<BallData>(raw.Length);
            foreach (var b in raw)
            {
                if (b == null)
                    continue;
                if (b.powerLevel < 1 || b.powerLevel > 3)
                    b.powerLevel = Mathf.Clamp(b.powerLevel, 1, 3);
                result.Add(b);
            }
            return result.ToArray();
        }
    }
}
