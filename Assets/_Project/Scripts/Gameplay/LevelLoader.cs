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
    // If the key is missing, _defaultLevelName is used.
    public class LevelLoader : MonoBehaviour
    {
        [SerializeField] private GridRenderer         _grid;
        [SerializeField] private BallQueue            _queue;
        [SerializeField] private GridCameraController _cam;
        [SerializeField] private GridBoard            _board;        // optional
        [SerializeField] private ProgressHUD          _progressHUD;  // optional
        [SerializeField] private LevelPickerHUD       _levelPicker;  // optional
        [SerializeField] private LaunchAreaAnchor     _launchAnchor; // optional — pins balls to screen bottom
        [SerializeField] private GameManager          _gameManager;  // optional — score reset on load
        [SerializeField] private BoosterSystem        _boosters;     // optional — booster counts per level

        [Header("Fallback")]
        [SerializeField] private string _defaultLevelName = "Level_01";

        private const string SelectedLevelKey = "SelectedLevel";

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Start()
        {
            string name = PlayerPrefs.GetString(SelectedLevelKey, _defaultLevelName);
            if (string.IsNullOrEmpty(name)) name = _defaultLevelName;
            LoadByName(name);
        }

        // ── Public ────────────────────────────────────────────────────────

        // Load by name (file name without extension) from Resources/Levels/
        public void LoadByName(string levelName)
        {
            var ta = Resources.Load<TextAsset>("Levels/" + levelName);

            // Fallback to the first available level if the name is stale/missing,
            // so the game never boots into an empty grid.
            if (ta == null)
            {
                var all = Resources.LoadAll<TextAsset>("Levels");
                if (all.Length > 0) { ta = all[0]; levelName = ta.name; }
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

            Apply(data);
            _levelPicker?.SetCurrent(levelName);
        }

        // Load from a pre-parsed LevelData (e.g. from a test harness)
        public void Apply(LevelData data)
        {
            if (data == null) return;
            _grid.BuildGrid(data);
            _board?.Rebuild(data.grid);
            if (_cam) _cam.FitToGrid(data.grid, data.camera);
            // Pin the launch area to the screen bottom BEFORE loading balls, so the
            // queue rebuilds at the final (anchored) waypoint positions.
            _launchAnchor?.Reanchor();
            _queue.Load(SanitizeBalls(data.balls));
            _progressHUD?.Bind(_grid);
            // A level switch is a new run: the picker HUD loads in place rather than
            // reloading the scene, so the score would otherwise carry over.
            _gameManager?.ResetScore();
            _boosters?.ResetForLevel();
        }

        // Strip null entries and clamp powerLevel to 1-3 so bad JSON never crashes gameplay.
        private static BallData[] SanitizeBalls(BallData[] raw)
        {
            if (raw == null || raw.Length == 0) return System.Array.Empty<BallData>();
            var result = new System.Collections.Generic.List<BallData>(raw.Length);
            foreach (var b in raw)
            {
                if (b == null) continue;
                if (b.powerLevel < 1 || b.powerLevel > 3)
                    b.powerLevel = Mathf.Clamp(b.powerLevel, 1, 3);
                result.Add(b);
            }
            return result.ToArray();
        }

        // ── Helpers ───────────────────────────────────────────────────────

        // Set which level will be loaded when the Gameplay scene starts.
        public static void SelectLevel(string levelName) =>
            PlayerPrefs.SetString(SelectedLevelKey, levelName);

        public static void ClearSelection() =>
            PlayerPrefs.DeleteKey(SelectedLevelKey);
    }
}
