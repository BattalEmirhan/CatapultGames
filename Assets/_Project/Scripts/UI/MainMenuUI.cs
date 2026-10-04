using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatapultGames
{
    // Main menu screen. Scene "MainMenu", built by MenuSceneBuilder.
    //
    //   playButton        → the first level not yet won (PlayerProgress.NextToPlay)
    //   playLabel         → optional; shows which level Play goes to
    //   levelSelectButton → LevelSelect scene
    //   progressLabel     → optional; "3 / 5 levels"
    public sealed class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Button          playButton;
        [SerializeField] private TextMeshProUGUI playLabel;
        [SerializeField] private Button          levelSelectButton;
        [SerializeField] private TextMeshProUGUI progressLabel;

        [Header("Scene names")]
        [SerializeField] private string gameplayScene    = "Gameplay2";
        [SerializeField] private string levelSelectScene = "LevelSelect";

        private void Awake()
        {
            if (playButton)
                playButton.onClick.AddListener(OnPlay);
            if (levelSelectButton)
                levelSelectButton.onClick.AddListener(OnLevelSelect);
        }

        private void Start()
        {
            string next = PlayerProgress.NextToPlay();
            int    n    = LevelOrder.NumberOf(next);
            if (playLabel)
                playLabel.text = n > 0 ? $"Play  ·  Level {n}" : "Play";

            int total = LevelOrder.Names.Count;
            if (progressLabel)
                progressLabel.text = total > 0 ? $"{PlayerProgress.WonCount()} / {total} levels" : "";
            if (playButton)
                playButton.interactable = next != null;
        }

        private void OnPlay()
        {
            string next = PlayerProgress.NextToPlay();
            if (next == null)
                return;
            LevelLoader.SelectLevel(next);
            SceneManager.LoadScene(gameplayScene);
        }

        private void OnLevelSelect() =>
            SceneManager.LoadScene(levelSelectScene);
    }
}
