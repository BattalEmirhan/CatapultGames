using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames
{
    // Three booster buttons with their remaining counts. Pure view: every rule
    // (can it be used, what it does) is BoosterSystem's. Refreshes on the
    // booster and queue events and once per frame for the game-over gate, which
    // is cheap (three buttons) and keeps it free of GameManager events.
    //
    // Wire up in Inspector: boosters, queue, and one Button + TMP count label
    // per booster (the scene builder creates them).
    public sealed class BoosterBarUI : MonoBehaviour
    {
        [SerializeField] private BoosterSystem boosters;
        [SerializeField] private BallQueue     queue;
        [SerializeField] private Button          rainbowButton;
        [SerializeField] private Button          recolorButton;
        [SerializeField] private Button          bombButton;
        [SerializeField] private TextMeshProUGUI rainbowCount;
        [SerializeField] private TextMeshProUGUI recolorCount;
        [SerializeField] private TextMeshProUGUI bombCount;

        // Last count drawn per booster — the bar refreshes every frame, and a fresh
        // "×N" string each time would be garbage for nothing.
        private readonly int[] _shownCounts = { -1, -1, -1 };

        private void Awake()
        {
            rainbowButton?.onClick.AddListener(() => boosters?.Use(BoosterType.Rainbow));
            recolorButton?.onClick.AddListener(() => boosters?.Use(BoosterType.Recolor));
            bombButton?.onClick.AddListener(() => boosters?.Use(BoosterType.Bomb));
        }

        private void OnEnable()
        {
            if (boosters != null)
                boosters.OnChanged += Refresh;
            if (queue    != null)
                queue.OnChanged    += Refresh;
            Refresh();
        }

        private void Update() => Refresh();   // game-over gate has no event of its own

        private void OnDisable()
        {
            if (boosters != null)
                boosters.OnChanged -= Refresh;
            if (queue    != null)
                queue.OnChanged    -= Refresh;
        }

        private void Refresh()
        {
            if (boosters == null)
                return;
            Apply(rainbowButton, rainbowCount, BoosterType.Rainbow);
            Apply(recolorButton, recolorCount, BoosterType.Recolor);
            Apply(bombButton,    bombCount,    BoosterType.Bomb);
        }

        private void Apply(Button button, TextMeshProUGUI label, BoosterType type)
        {
            int count = boosters.Count(type);
            if (label != null && _shownCounts[(int)type] != count)
            {
                _shownCounts[(int)type] = count;
                label.text = $"×{count}";
            }
            if (button != null)
                button.interactable = boosters.CanUse(type);
        }
    }
}
