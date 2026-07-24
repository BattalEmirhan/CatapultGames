using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CatapultGames
{
    // Task 15 — Win / fail detection.
    // Orchestrates all gameplay systems and triggers the result screen.
    //
    // Wire up in Inspector:
    //   _grid         — GridRenderer
    //   _queue        — BallQueue
    //   _launcher     — BallLauncher
    //   _aimPreview   — AimPreview
    //   _resultScreen — ResultScreenUI
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private GridRenderer   _grid;
        [SerializeField] private BallQueue      _queue;
        [SerializeField] private BallLauncher   _launcher;
        [SerializeField] private AimPreview     _aimPreview;
        [SerializeField] private ResultScreenUI _resultScreen;

        [Header("Scene names")]
        [SerializeField] private string _mainMenuScene = "MainMenu";

        private bool _gameOver;

        // True once the level has been won or lost — other systems (e.g. Gameplay2's
        // tap input) check this to stop accepting launches.
        public bool IsOver => _gameOver;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (_launcher) _launcher.OnBallLanded += OnBallLanded;
        }

        private void OnDisable()
        {
            if (_launcher) _launcher.OnBallLanded -= OnBallLanded;
        }

        // ── Game events ───────────────────────────────────────────────────
        // Evaluated after every ball lands. Because shots can overlap, a loss is
        // only declared once the queue is empty AND no balls are still in flight.
        private void OnBallLanded(Vector3 _)
        {
            if (_gameOver) return;

            // Once a colour is fully painted its leftover balls are useless — drop
            // them from the queue. Done before the win/lose check so an empty queue
            // afterwards is evaluated correctly.
            PurgeCompletedColors();

            if (_grid != null && _grid.AllColoredCellsFilled())
            {
                EndGame(won: true);
                return;
            }

            if (_queue != null && _queue.IsEmpty && _launcher != null && !_launcher.IsBusy)
                EndGame(won: false);
        }

        // Remove queued balls whose colour is already complete (all its cells filled).
        // Colour-agnostic and level-agnostic: it reads live per-colour progress, and is
        // idempotent (re-running finds nothing to remove) so it needs no reset on reload.
        private void PurgeCompletedColors()
        {
            if (_grid == null || _queue == null) return;

            foreach (var cp in _grid.CountByColor())
            {
                if (cp.total <= 0 || cp.filled < cp.total) continue;

                int removed = _queue.RemoveColor(cp.color);
                if (removed > 0)
                {
                    Haptics.Light();                              // tactile confirmation
                    StartCoroutine(CelebrateColorCleared(cp.color));
                }
            }
        }

        // After a color's leftover balls rocket up and pop, make that color's
        // now-complete cells bounce and give the screen a shake — a punchy payoff.
        private IEnumerator CelebrateColorCleared(CellColor color)
        {
            yield return new WaitForSeconds(0.35f);  // let the firework volley start popping
            if (_grid != null) _grid.PulseColor(color);
            GameFX.Instance.Shake(0.22f, 0.30f);
        }

        // ── End game ──────────────────────────────────────────────────────
        private void EndGame(bool won)
        {
            _gameOver = true;
            if (_aimPreview) _aimPreview.enabled = false;

            if (won)
            {
                if (_grid != null) GameFX.Instance.Win(_grid.WorldCenter);
            }
            else
            {
                GameFX.Instance.Shake(0.12f, 0.25f);
            }

            // Brief delay before the panel covers the screen so the win burst is seen.
            float delay = won ? 0.5f : 0.2f;
            StartCoroutine(ShowResultDelayed(won, delay));
        }

        private IEnumerator ShowResultDelayed(bool won, float delay)
        {
                float elapsed = 0f;
                while (elapsed < delay)
                {
                    if (AnyPointerPressedThisFrame())
                        break;
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (_resultScreen) _resultScreen.Show(won);
        }

            private static bool AnyPointerPressedThisFrame()
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

                var touch = Touchscreen.current?.primaryTouch;
                if (touch != null && touch.press.wasPressedThisFrame) return true;

                return false;
            }

        // ── Public — called by UI buttons ─────────────────────────────────
        public void RestartLevel()
        {
            _gameOver = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void GoToMainMenu() =>
            SceneManager.LoadScene(_mainMenuScene);
    }
}
