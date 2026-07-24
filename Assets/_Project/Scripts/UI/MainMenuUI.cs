using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatapultGames
{
    // Task 17 — Main menu screen.
    //
    // Scene: "MainMenu"
    // Expected buttons in Inspector:
    //   _playButton        → loads Gameplay scene directly (uses default/last level)
    //   _levelSelectButton → loads LevelSelect scene
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _levelSelectButton;

        [Header("Scene names")]
        [SerializeField] private string _gameplayScene    = "Gameplay";
        [SerializeField] private string _levelSelectScene = "LevelSelect";

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (_playButton)        _playButton.onClick.AddListener(OnPlay);
            if (_levelSelectButton) _levelSelectButton.onClick.AddListener(OnLevelSelect);
        }

        // ── Handlers ──────────────────────────────────────────────────────
        private void OnPlay()
        {
            LevelLoader.ClearSelection();   // use default level
            SceneManager.LoadScene(_gameplayScene);
        }

        private void OnLevelSelect() =>
            SceneManager.LoadScene(_levelSelectScene);
    }
}
