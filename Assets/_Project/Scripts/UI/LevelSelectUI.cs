using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames
{
    // Level select panel in UIScene. One numbered tile per shipped level, in play
    // order (LevelOrder), rebuilt every time the panel opens so it always shows the
    // current progress. Tiles are made in code — the container's GridLayoutGroup
    // lays them out, so there is no prefab to keep in sync.
    //   won      → mint
    //   unlocked → sky blue
    //   locked   → grey, not tappable (the level before it is not won yet)
    public sealed class LevelSelectUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform  buttonContainer;
        [SerializeField] private Button     backButton;

        private const int LabelFontSize = 97;

        private static readonly Color WonColor    = new Color(0.34f, 0.80f, 0.60f);
        private static readonly Color OpenColor   = new Color(0.34f, 0.62f, 0.95f);
        private static readonly Color LockedColor = new Color(0.62f, 0.64f, 0.70f);

        private void OnEnable()
        {
            backButton.onClick.AddListener(OnBack);
            GameFlow.LevelSelectRequested += Show;
            GameFlow.MenuRequested        += Hide;
            GameFlow.LevelRequested       += OnLevelRequested;
        }

        private void Start() => Hide();

        private void OnDisable()
        {
            backButton.onClick.RemoveListener(OnBack);
            GameFlow.LevelSelectRequested -= Show;
            GameFlow.MenuRequested        -= Hide;
            GameFlow.LevelRequested       -= OnLevelRequested;
        }

        public void Show()
        {
            Rebuild();
            panel.SetActive(true);
        }

        public void Hide() => panel.SetActive(false);

        private void Rebuild()
        {
            for (int i = buttonContainer.childCount - 1; i >= 0; i--)
                Destroy(buttonContainer.GetChild(i).gameObject);
            var names = LevelOrder.Names;
            for (int i = 0; i < names.Count; i++)
                SpawnTile(names[i]);
        }

        private void SpawnTile(string levelName)
        {
            bool unlocked = PlayerProgress.IsUnlocked(levelName);
            var  go       = new GameObject(levelName, typeof(RectTransform)) { layer = buttonContainer.gameObject.layer };
            go.transform.SetParent(buttonContainer, false);
            go.AddComponent<Image>().color = PlayerProgress.IsWon(levelName) ? WonColor : unlocked ? OpenColor : LockedColor;

            var button = go.AddComponent<Button>();
            button.interactable = unlocked;
            if (unlocked)
                button.onClick.AddListener(() => GameFlow.RequestLevel(levelName));
            AddLabel(go.transform, levelName, unlocked);
        }

        private static void AddLabel(Transform tile, string levelName, bool unlocked)
        {
            var go = new GameObject("Label", typeof(RectTransform)) { layer = tile.gameObject.layer };
            go.transform.SetParent(tile, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;

            int n     = LevelOrder.NumberOf(levelName);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text          = n > 0 ? n.ToString() : levelName;
            label.fontSize      = LabelFontSize;
            label.fontStyle     = FontStyles.Bold;
            label.alignment     = TextAlignmentOptions.Center;
            label.color         = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            label.raycastTarget = false;
        }

        private void OnBack() => GameFlow.RequestMenu();

        private void OnLevelRequested(string levelName) => Hide();
    }
}
