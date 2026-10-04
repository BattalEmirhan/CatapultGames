using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatapultGames
{
    // Level select screen. Scene "LevelSelect", built by MenuSceneBuilder.
    //
    // One numbered tile per shipped level, in play order (LevelOrder) — the same
    // glossy tiles as the board (TileArt). Made in code, like every other UI in
    // the project, so there is no prefab to keep in sync: the container's
    // GridLayoutGroup does the layout.
    //   won      → green tile
    //   unlocked → blue tile, the one to play next
    //   locked   → grey tile, not tappable (the level before it is not won yet)
    //
    // Inspector:
    //   _buttonContainer — ScrollRect content with a GridLayoutGroup
    //   _backButton      — returns to MainMenu
    public class LevelSelectUI : MonoBehaviour
    {
        [SerializeField] private Transform _buttonContainer;
        [SerializeField] private Button    _backButton;

        [Header("Scene names")]
        [SerializeField] private string _gameplayScene = "Gameplay2";
        [SerializeField] private string _mainMenuScene = "MainMenu";

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Start()
        {
            if (_backButton) _backButton.onClick.AddListener(OnBack);
            PopulateList();
        }

        // ── Build level list ──────────────────────────────────────────────
        private void PopulateList()
        {
            if (_buttonContainer == null) return;

            foreach (var name in LevelOrder.Names)
                SpawnButton(name);
        }

        private void SpawnButton(string levelName)
        {
            bool won      = PlayerProgress.IsWon(levelName);
            bool unlocked = PlayerProgress.IsUnlocked(levelName);

            var go = new GameObject(levelName, typeof(RectTransform));
            go.transform.SetParent(_buttonContainer, false);

            var img    = go.AddComponent<Image>();
            img.sprite = won ? TileArt.Tile(CellColor.Green) : unlocked ? TileArt.Tile(CellColor.Blue) : TileArt.Grey();
            img.color  = Color.white;

            var button = go.AddComponent<Button>();
            button.interactable = unlocked;
            if (unlocked) button.onClick.AddListener(() => LoadLevel(levelName));

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
