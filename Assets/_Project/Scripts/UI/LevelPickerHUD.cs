using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames
{
    // Dev tool in UIScene: a top-right dropdown that jumps to any level. Only the
    // rows are built at runtime — the set of levels is data; the frame (button,
    // scrolling list) comes from the scene builder.
    public sealed class LevelPickerHUD : MonoBehaviour
    {
        [SerializeField] private Button          toggleButton;
        [SerializeField] private TextMeshProUGUI toggleLabel;
        [SerializeField] private GameObject      listPanel;
        [SerializeField] private Transform       content;

        private const float RowHeight   = 103f;
        private const int   RowFontSize = 49;

        private static readonly Color RowColor    = new Color(0.18f, 0.18f, 0.26f);
        private static readonly Color ActiveColor = new Color(0.22f, 0.48f, 0.90f);

        private string _current = "";

        private void OnEnable()
        {
            toggleButton.onClick.AddListener(Toggle);
            GameFlow.LevelStarted += OnLevelStarted;
        }

        // UIScene loads after GameScene has already started its level, so the first
        // LevelStarted is missed; the saved selection names it.
        private void Start()
        {
            _current = LevelLoader.SelectedLevel;
            listPanel.SetActive(false);
            UpdateLabel();
        }

        private void OnDisable()
        {
            toggleButton.onClick.RemoveListener(Toggle);
            GameFlow.LevelStarted -= OnLevelStarted;
        }

        private void Toggle()
        {
            bool open = !listPanel.activeSelf;
            if (open)
                Populate();
            listPanel.SetActive(open);
            UpdateLabel();
        }

        private void OnLevelStarted(string levelName)
        {
            _current = levelName;
            listPanel.SetActive(false);
            UpdateLabel();
        }

        private void Populate()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
            LevelOrder.Refresh();
            var names = LevelOrder.Names;
            for (int i = 0; i < names.Count; i++)
                SpawnRow(names[i]);
        }

        // A fixed row height, or the layout group collapses the rows to nothing.
        private void SpawnRow(string levelName)
        {
            bool active = levelName == _current;
            var  row    = new GameObject(levelName, typeof(RectTransform)) { layer = content.gameObject.layer };
            row.transform.SetParent(content, false);
            var img = row.AddComponent<Image>();
            img.color = active ? ActiveColor : RowColor;
            row.AddComponent<Button>().onClick.AddListener(() => GameFlow.RequestLevel(levelName));
            var element = row.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = RowHeight;
            AddRowLabel(row.transform, (active ? ">  " : "    ") + levelName);
        }

        private static void AddRowLabel(Transform row, string text)
        {
            var go = new GameObject("Label", typeof(RectTransform)) { layer = row.gameObject.layer };
            go.transform.SetParent(row, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text          = text;
            label.fontSize      = RowFontSize;
            label.alignment     = TextAlignmentOptions.MidlineLeft;
            label.margin        = new Vector4(22f, 0f, 11f, 0f);
            label.raycastTarget = false;
        }

        private void UpdateLabel()
        {
            string name = string.IsNullOrEmpty(_current) ? "Levels" : _current;
            toggleLabel.text = listPanel.activeSelf ? "[ Close ]" : $"v  {name}";
        }
    }
}
