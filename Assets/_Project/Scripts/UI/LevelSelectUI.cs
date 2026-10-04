using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatapultGames
{
    // Level select screen. Scene "LevelSelect", built by MenuSceneBuilder.
    //
    // One numbered tile per shipped level, in play order (LevelOrder). Tiles are
    // made in code, like every other UI in the project, so there is no prefab to
    // keep in sync: the container's GridLayoutGroup does the layout.
    //   won      → mint
    //   unlocked → sky blue, the one to play next
    //   locked   → grey, not tappable (the level before it is not won yet)
    //
    // Inspector:
    //   buttonContainer — ScrollRect content with a GridLayoutGroup
    //   backButton      — returns to MainMenu
    public sealed class LevelSelectUI : MonoBehaviour
    {
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private Button    backButton;

        [Header("Scene names")]
        [SerializeField] private string gameplayScene = "Gameplay2";
        [SerializeField] private string mainMenuScene = "MainMenu";

        private static readonly Color WonColor    = new Color(0.34f, 0.80f, 0.60f);
        private static readonly Color OpenColor   = new Color(0.34f, 0.62f, 0.95f);
        private static readonly Color LockedColor = new Color(0.62f, 0.64f, 0.70f);

        private void Start()
        {
            if (backButton)
                backButton.onClick.AddListener(OnBack);
            PopulateList();
        }

        private void PopulateList()
        {
            if (buttonContainer == null)
                return;

            foreach (var name in LevelOrder.Names)
                SpawnButton(name);
        }

        private void SpawnButton(string levelName)
        {
            bool won      = PlayerProgress.IsWon(levelName);
            bool unlocked = PlayerProgress.IsUnlocked(levelName);

            var go = new GameObject(levelName, typeof(RectTransform));
            go.transform.SetParent(buttonContainer, false);

            var img   = go.AddComponent<Image>();
            img.color = won ? WonColor : unlocked ? OpenColor : LockedColor;

            var button = go.AddComponent<Button>();
            button.interactable = unlocked;
            if (unlocked)
                button.onClick.AddListener(() => LoadLevel(levelName));

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var r = (RectTransform)labelGo.transform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            int n = LevelOrder.NumberOf(levelName);
            label.text          = n > 0 ? n.ToString() : levelName;
            label.fontSize      = 72;
            label.fontStyle     = FontStyles.Bold;
            label.alignment     = TextAlignmentOptions.Center;
            label.color         = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            label.raycastTarget = false;
        }

        private void LoadLevel(string levelName)
        {
            LevelLoader.SelectLevel(levelName);
            SceneManager.LoadScene(gameplayScene);
        }

        private void OnBack() =>
            SceneManager.LoadScene(mainMenuScene);
    }
}
