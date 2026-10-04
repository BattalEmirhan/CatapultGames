using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames
{
    // Main menu panel in UIScene, drawn over the board. Shown at boot and on
    // GameFlow.MenuRequested; Play asks for the first level not yet won. Lives on
    // the always-active canvas root rather than on the panel it hides, so it keeps
    // hearing GameFlow while the panel is off.
    public sealed class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject      panel;
        [SerializeField] private Button          playButton;
        [SerializeField] private TextMeshProUGUI playLabel;
        [SerializeField] private Button          levelSelectButton;
        [SerializeField] private TextMeshProUGUI progressLabel;

        private void OnEnable()
        {
            playButton.onClick.AddListener(OnPlay);
            levelSelectButton.onClick.AddListener(OnLevelSelect);
            GameFlow.MenuRequested        += Show;
            GameFlow.LevelSelectRequested += Hide;
            GameFlow.LevelRequested       += OnLevelRequested;
        }

        private void Start() => Show();

        private void OnDisable()
        {
            playButton.onClick.RemoveListener(OnPlay);
            levelSelectButton.onClick.RemoveListener(OnLevelSelect);
            GameFlow.MenuRequested        -= Show;
            GameFlow.LevelSelectRequested -= Hide;
            GameFlow.LevelRequested       -= OnLevelRequested;
        }

        public void Show()
        {
            RefreshLabels();
            panel.SetActive(true);
        }

        public void Hide() => panel.SetActive(false);

        private void RefreshLabels()
        {
            string next  = PlayerProgress.NextToPlay();
            int    n     = LevelOrder.NumberOf(next);
            int    total = LevelOrder.Names.Count;
            playLabel.text          = n > 0 ? $"Play  ·  Level {n}" : "Play";
            progressLabel.text      = total > 0 ? $"{PlayerProgress.WonCount()} / {total} levels" : "";
            playButton.interactable = next != null;
        }

        private void OnPlay() => GameFlow.RequestLevel(PlayerProgress.NextToPlay());

        private void OnLevelSelect() => GameFlow.RequestLevelSelect();

        private void OnLevelRequested(string levelName) => Hide();
    }
}
