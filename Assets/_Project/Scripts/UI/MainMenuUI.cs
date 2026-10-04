using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatapultGames
{
    // Main menu screen. Scene "MainMenu", built by MenuSceneBuilder.
    //
    //   _playButton        → the first level not yet won (PlayerProgress.NextToPlay)
    //   _playLabel         → optional; shows which level Play goes to
    //   _levelSelectButton → LevelSelect scene
    //   _progressLabel     → optional; "3 / 5 levels"
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button          _playButton;
        [SerializeField] private TextMeshProUGUI _playLabel;
        [SerializeField] private Button          _levelSelectButton;
        [SerializeField] private TextMeshProUGUI _progressLabel;

        [Header("Scene names")]
        [SerializeField] private string _gameplayScene    = "Gameplay2";
        [SerializeField] private string _levelSelectScene = "LevelSelect";

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (_playButton)        _playButton.onClick.AddListener(OnPlay);
            if (_levelSelectButton) _levelSelectButton.onClick.AddListener(OnLevelSelect);
        }

        private void Start()
        {
            string next = PlayerProgress.NextToPlay();
            int    n    = LevelOrder.NumberOf(next);
            if (_playLabel) _playLabel.text = n > 0 ? $"Play  ·  Level {n}" : "Play";

            int total = LevelOrder.Names.Count;
            if (_progressLabel) _progressLabel.text = total > 0 ? $"{PlayerProgress.WonCount()} / {total} levels" : "";
            if (_playButton) _playButton.interactable = next != null;
        }

        // ── Handlers ──────────────────────────────────────────────────────
        private void OnPlay()
        {
            string next = PlayerProgress.NextToPlay();
            if (next == null) return;
            LevelLoader.SelectLevel(next);
            SceneManager.LoadScene(_gameplayScene);
        }

        private void OnLevelSelect() =>
            SceneManager.LoadScene(_levelSelectScene);
    }
}
