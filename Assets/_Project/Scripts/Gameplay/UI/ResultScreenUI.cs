using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace CatapultGames
{
    // Task 15 — Win / fail result panel.
    //
    // Inspector setup:
    //   _panel        — root panel GameObject (inactive by default)
    //   _titleText    — "You Win!" / "Try Again"
    //   _subtitleText — supporting line
    //   _retryButton  — restart current level
    //   _menuButton   — go to main menu
    //   _extraBallsButton — take the "keep going" offer (hidden when not offered)
    //   _nextButton   — next level (shown on a win when there is one)
    //   _gameManager  — reference for button callbacks
    public class ResultScreenUI : MonoBehaviour
    {
        // Why the level ended. The panel owns all the wording, so GameManager
        // passes the reason rather than a string.
        public enum Reason
        {
            Won,
            OutOfBalls    // queue ran dry with cells still empty. A proven dead end
                          // with balls left is only a warning (GameManager), since
                          // undo and boosters can still turn it around.
        }

        [SerializeField] private GameObject      _panel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _subtitleText;
        [SerializeField] private Button          _retryButton;
        [SerializeField] private Button          _menuButton;
        [SerializeField] private Button          _extraBallsButton;
        [SerializeField] private Button          _nextButton;
        [SerializeField] private GameManager     _gameManager;

        [Header("Messages")]
        [SerializeField] private string _winTitle     = "You Win!";
        [SerializeField] private string _failTitle    = "Try Again";
        [SerializeField] private string _winSubtitle  = "All cells painted!";
        [SerializeField] private string _failSubtitle = "Not enough balls...";

        [Tooltip("Prefix for the final score line appended to the subtitle.")]
        [SerializeField] private string _scoreLabel = "Score";

        private Vector3 _panelRest = Vector3.one;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (_panel)
            {
                _panelRest = _panel.transform.localScale;
                _panel.SetActive(false);
            }
            if (_retryButton)      _retryButton.onClick.AddListener(() => _gameManager?.RestartLevel());
            if (_menuButton)       _menuButton.onClick.AddListener(() => _gameManager?.GoToMainMenu());
            if (_extraBallsButton) _extraBallsButton.onClick.AddListener(() => _gameManager?.GrantExtraBalls());
            if (_nextButton)       _nextButton.onClick.AddListener(() => _gameManager?.NextLevel());
        }

        // ── API ───────────────────────────────────────────────────────────
        // `score` is appended to the subtitle rather than given its own text object:
        // the panel is built by GameplaySceneBuilder, and a second label there would
        // be one more reference to lose on a scene rebuild for no extra information.
        // Pass a negative score to leave it out.
        public void Show(Reason reason, bool offerExtraBalls = false, int score = -1, bool hasNextLevel = false)
        {
            bool won = reason == Reason.Won;

            if (_panel) _panel.SetActive(true);

            if (_titleText)
                _titleText.text = won ? _winTitle : _failTitle;

            if (_subtitleText)
            {
                string sub = won ? _winSubtitle : _failSubtitle;
                if (score >= 0) sub += $"\n\n{_scoreLabel} {score:n0}";
                _subtitleText.text = sub;
            }

            // The offer is made once per level, so the button is not a permanent
            // fixture of the panel — it appears only when there is one going.
            if (_extraBallsButton) _extraBallsButton.gameObject.SetActive(offerExtraBalls);
            // Next sits where the offer would: a win never has an offer, a loss
            // never has a next level.
            if (_nextButton) _nextButton.gameObject.SetActive(won && hasNextLevel);

            // Win already flashes gold via GameFX.Win; give the fail screen a red one.
            if (!won) GameFX.Instance.Flash(new Color(0.9f, 0.25f, 0.25f), 0.32f, 0.40f);

            if (_panel && isActiveAndEnabled) StartCoroutine(BounceIn(_panel.transform));
        }

        // Scale the panel up from nothing with an overshoot for a snappy entrance.
        private IEnumerator BounceIn(Transform tr)
        {
            const float dur = 0.20f; // shortened for snappier result
            float t = 0f;
            while (t < dur)
            {
                if (AnyPointerPressedThisFrame())
                {
                    tr.localScale = _panelRest;
                    yield break;
                }
                tr.localScale = _panelRest * EaseOutBack(t / dur);
                t += Time.deltaTime;
                yield return null;
            }
            tr.localScale = _panelRest;
        }

        private static bool AnyPointerPressedThisFrame()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.wasPressedThisFrame) return true;

            return false;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }

        public void Hide()
        {
            if (_panel) _panel.SetActive(false);
        }
    }
}
