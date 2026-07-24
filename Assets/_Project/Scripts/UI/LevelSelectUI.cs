using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatapultGames
{
    // Task 17 — Level select screen.
    //
    // Scene: "LevelSelect"
    // Scans StreamingAssets/Levels/*.json and spawns one button per level.
    // Tapping a button sets PlayerPrefs and loads the Gameplay scene.
    //
    // Inspector:
    //   _buttonContainer   — ScrollView content Transform (buttons are added here)
    //   _levelButtonPrefab — prefab with a Button + child TextMeshProUGUI label
    //   _backButton        — returns to MainMenu
    public class LevelSelectUI : MonoBehaviour
    {
        [SerializeField] private Transform  _buttonContainer;
        [SerializeField] private GameObject _levelButtonPrefab;
        [SerializeField] private Button     _backButton;

        [Header("Scene names")]
        [SerializeField] private string _gameplayScene  = "Gameplay";
        [SerializeField] private string _mainMenuScene  = "MainMenu";

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Start()
        {
            if (_backButton) _backButton.onClick.AddListener(OnBack);
            PopulateList();
        }

        // ── Build level list ──────────────────────────────────────────────
        private void PopulateList()
        {
            if (_buttonContainer == null || _levelButtonPrefab == null) return;

            // Resources.LoadAll works on every platform (incl. Android).
            var assets = Resources.LoadAll<TextAsset>("Levels");
            foreach (var ta in assets)
                SpawnButton(ta.name);
        }

        private void SpawnButton(string levelName)
        {
            var go     = Instantiate(_levelButtonPrefab, _buttonContainer);
            var label  = go.GetComponentInChildren<TextMeshProUGUI>();
            var button = go.GetComponent<Button>();

            if (label)  label.text = levelName;
            if (button) button.onClick.AddListener(() => LoadLevel(levelName));
        }

        // ── Handlers ──────────────────────────────────────────────────────
        private void LoadLevel(string levelName)
        {
            LevelLoader.SelectLevel(levelName);
            SceneManager.LoadScene(_gameplayScene);
        }

        private void OnBack() =>
            SceneManager.LoadScene(_mainMenuScene);
    }
}
