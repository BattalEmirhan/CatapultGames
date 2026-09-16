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
    // Wire up in Inspector: _boosters, _queue, and one Button + TMP count label
    // per booster (the scene builder creates them).
    public class BoosterBarUI : MonoBehaviour
    {
        [SerializeField] private BoosterSystem _boosters;
        [SerializeField] private BallQueue     _queue;
        [SerializeField] private Button          _rainbowButton;
        [SerializeField] private Button          _recolorButton;
        [SerializeField] private Button          _bombButton;
        [SerializeField] private TextMeshProUGUI _rainbowCount;
        [SerializeField] private TextMeshProUGUI _recolorCount;
        [SerializeField] private TextMeshProUGUI _bombCount;

        private void Awake()
        {
            _rainbowButton?.onClick.AddListener(() => _boosters?.Use(BoosterType.Rainbow));
            _recolorButton?.onClick.AddListener(() => _boosters?.Use(BoosterType.Recolor));
            _bombButton?.onClick.AddListener(() => _boosters?.Use(BoosterType.Bomb));
        }

        private void OnEnable()
        {
            if (_boosters != null) _boosters.OnChanged += Refresh;
            if (_queue    != null) _queue.OnChanged    += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_boosters != null) _boosters.OnChanged -= Refresh;
            if (_queue    != null) _queue.OnChanged    -= Refresh;
        }

        private void Update() => Refresh();   // game-over gate has no event of its own

        private void Refresh()
        {
            if (_boosters == null) return;
            Apply(_rainbowButton, _rainbowCount, BoosterType.Rainbow);
            Apply(_recolorButton, _recolorCount, BoosterType.Recolor);
            Apply(_bombButton,    _bombCount,    BoosterType.Bomb);
        }

        private void Apply(Button button, TextMeshProUGUI label, BoosterType type)
        {
            int count = _boosters.Count(type);
            if (label != null) label.text = $"×{count}";
            if (button != null) button.interactable = _boosters.CanUse(type);
        }
    }
}
