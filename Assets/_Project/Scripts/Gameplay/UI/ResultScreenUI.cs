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
    //   panel        — root panel GameObject (inactive by default)
    //   titleText    — "You Win!" / "Try Again"
    //   subtitleText — supporting line
    //   retryButton  — restart current level
    //   menuButton   — go to main menu
    //   extraBallsButton — take the "keep going" offer (hidden when not offered)
    //   nextButton   — next level (shown on a win when there is one)
    //   gameManager  — reference for button callbacks
    public sealed class ResultScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject      panel;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Button          retryButton;
        [SerializeField] private Button          menuButton;
        [SerializeField] private Button          extraBallsButton;
        [SerializeField] private Button          nextButton;
        [SerializeField] private GameManager     gameManager;

        [Header("Messages")]
        [SerializeField] private string winTitle     = "You Win!";
        [SerializeField] private string failTitle    = "Try Again";
        [SerializeField] private string winSubtitle  = "All cells painted!";
        [SerializeField] private string failSubtitle = "Not enough balls...";

        [Tooltip("Prefix for the final score line appended to the subtitle.")]
        [SerializeField] private string scoreLabel = "Score";

        private Vector3 _panelRest = Vector3.one;

        private void Awake()
        {
            if (panel)
            {
                _panelRest = panel.transform.localScale;
                panel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            retryButton.onClick.AddListener(gameManager.RestartLevel);
            menuButton.onClick.AddListener(gameManager.GoToMainMenu);
            extraBallsButton.onClick.AddListener(gameManager.GrantExtraBalls);
            nextButton.onClick.AddListener(gameManager.NextLevel);
        }

        private void OnDisable()
        {
            retryButton.onClick.RemoveListener(gameManager.RestartLevel);
            menuButton.onClick.RemoveListener(gameManager.GoToMainMenu);
            extraBallsButton.onClick.RemoveListener(gameManager.GrantExtraBalls);
            nextButton.onClick.RemoveListener(gameManager.NextLevel);
        }

        // `score` is appended to the subtitle rather than given its own text object:
        // the panel is built by GameHudBuilder, and a second label there would
        // be one more reference to lose on a scene rebuild for no extra information.
        // Pass a negative score to leave it out.
        public void Show(ResultReason reason, bool offerExtraBalls = false, int score = -1, bool hasNextLevel = false)
        {
            bool won = reason == ResultReason.Won;

            if (panel)
                panel.SetActive(true);

            if (titleText)
                titleText.text = won ? winTitle : failTitle;

            if (subtitleText)
            {
                string sub = won ? winSubtitle : failSubtitle;
                if (score >= 0)
                    sub += $"\n\n{scoreLabel} {score:n0}";
                subtitleText.text = sub;
            }

            // The offer is made once per level, so the button is not a permanent
            // fixture of the panel — it appears only when there is one going.
            if (extraBallsButton)
                extraBallsButton.gameObject.SetActive(offerExtraBalls);
            // Next sits where the offer would: a win never has an offer, a loss
            // never has a next level.
            if (nextButton)
                nextButton.gameObject.SetActive(won && hasNextLevel);

            // Win already flashes gold via GameFX.Win; give the fail screen a red one.
            if (!won)
                GameFX.Instance.Flash(new Color(0.9f, 0.25f, 0.25f), 0.32f, 0.40f);

            if (panel && isActiveAndEnabled)
                StartCoroutine(BounceIn(panel.transform));
        }

        public void Hide()
        {
            if (panel)
                panel.SetActive(false);
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
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.wasPressedThisFrame)
                return true;

            return false;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
    }
}
