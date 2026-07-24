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
    //   _gameManager  — reference for button callbacks
    public class ResultScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject      _panel;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _subtitleText;
        [SerializeField] private Button          _retryButton;
        [SerializeField] private Button          _menuButton;
        [SerializeField] private GameManager     _gameManager;

        [Header("Messages")]
        [SerializeField] private string _winTitle     = "You Win!";
        [SerializeField] private string _failTitle    = "Try Again";
        [SerializeField] private string _winSubtitle  = "All cells painted!";
        [SerializeField] private string _failSubtitle = "Not enough balls...";

        private Vector3 _panelRest = Vector3.one;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            if (_panel)
            {
                _panelRest = _panel.transform.localScale;
                _panel.SetActive(false);
            }
            if (_retryButton) _retryButton.onClick.AddListener(() => _gameManager?.RestartLevel());
            if (_menuButton)  _menuButton.onClick.AddListener(() => _gameManager?.GoToMainMenu());
        }

        // ── API ───────────────────────────────────────────────────────────
        public void Show(bool won)
        {
            if (_panel)        _panel.SetActive(true);
            if (_titleText)    _titleText.text    = won ? _winTitle    : _failTitle;
            if (_subtitleText) _subtitleText.text = won ? _winSubtitle : _failSubtitle;

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
