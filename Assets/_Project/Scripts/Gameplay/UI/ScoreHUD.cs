using System.Collections;
using TMPro;
using UnityEngine;

namespace CatapultGames
{
    // Score, and the combo that earned it.
    //
    // Two labels: the running total, and a burst under it saying what the last shot
    // paid ("x6   +720"). The burst is the point of the whole feature — a total that
    // climbs silently teaches nobody that a denser hit, or a streak without a wasted
    // ball, is worth aiming for.
    //
    // Holds no scoring rules: GameManager reports what a shot paid (rules live in
    // GameConstants), and this only renders it. Event-driven, never polled, so a
    // level with no shots costs nothing per frame.
    //
    // Wire up in Inspector:
    //   gameManager — GameManager (source of both events)
    //   scoreLabel  — running total
    //   comboLabel  — per-shot burst (fades itself out; starts invisible)
    //
    // RequireComponent for the same reason ProgressHUD has it: the scene builder
    // parents this under the safe-area rect as a plain GameObject, and child anchors
    // against a non-RectTransform parent are meaningless.
    [RequireComponent(typeof(RectTransform))]
    public sealed class ScoreHUD : MonoBehaviour
    {
        [SerializeField] private GameManager     gameManager;
        [SerializeField] private TextMeshProUGUI scoreLabel;
        [SerializeField] private TextMeshProUGUI comboLabel;
        [SerializeField] private TextMeshProUGUI praiseLabel;   // optional: "Great!" in the middle of the screen

        [Tooltip("How long a shot's burst stays on screen before it has faded out.")]
        [SerializeField] private float burstDuration = 0.95f;

        // Praise words by cells painted — the block-puzzle "Good / Great / Amazing"
        // that tells the player a dense throw was noticed. Thresholds line up with
        // GameConstants.GetShotMultiplier's steps.
        private static readonly (int cells, string word, Color tint)[] Praise =
        {
            (8, "AMAZING!", new Color(1f, 0.80f, 0.25f)),
            (5, "GREAT!",   new Color(0.55f, 0.85f, 1f)),
            (3, "GOOD",     new Color(0.85f, 0.95f, 1f)),
        };

        // Gold at the top of the multiplier range, plain white at the bottom — the
        // colour is the fastest read of "that shot was worth something".
        private static readonly Color PlainTint = new Color(0.92f, 0.94f, 1f);
        private static readonly Color BigTint   = new Color(1f, 0.84f, 0.28f);
        private static readonly Color LostTint  = new Color(1f, 0.45f, 0.42f);
        private Vector3   _scoreRest = Vector3.one;
        private Coroutine _punch;
        private Coroutine _burst;
        private int       _shownStreak;   // last streak drawn, to know a combo was lost

        private Coroutine _praise;
        private Vector2   _praiseRest;   // read once: a praise cut off mid-drift must not move the next one

        private void Awake()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            if (scoreLabel)
            {
                _scoreRest = scoreLabel.rectTransform.localScale;
#pragma warning disable CS0618 // enableWordWrapping is obsolete but works across TMP versions
                scoreLabel.enableWordWrapping = false;
#pragma warning restore CS0618
            }
            if (comboLabel)
            {
#pragma warning disable CS0618
                comboLabel.enableWordWrapping = false;
#pragma warning restore CS0618
                SetComboAlpha(0f);
            }
            if (praiseLabel)
            {
                praiseLabel.raycastTarget = false;
                praiseLabel.color = new Color(1f, 1f, 1f, 0f);
                _praiseRest = praiseLabel.rectTransform.anchoredPosition;
            }
        }

        private void OnEnable()
        {
            if (gameManager == null)
                return;
            gameManager.OnScoreChanged += HandleScoreChanged;
            gameManager.OnShotScored   += HandleShotScored;
            HandleScoreChanged(gameManager.Score);   // shows 0 on a fresh level
        }

        private void OnDisable()
        {
            if (gameManager == null)
                return;
            gameManager.OnScoreChanged -= HandleScoreChanged;
            gameManager.OnShotScored   -= HandleShotScored;
        }

        // No Bind() counterpart to ProgressHUD's: GameManager outlives a level swap,
        // and it announces the reset itself (GameManager.BeginRun).
        private void HandleScoreChanged(int score)
        {
            if (scoreLabel == null)
                return;

            scoreLabel.text = score.ToString("n0");

            // Undo lowers the score, and popping the label then would read as a
            // reward for undoing.
            if (score <= 0 || !isActiveAndEnabled)
                return;
            if (_punch != null)
                StopCoroutine(_punch);
            _punch = StartCoroutine(PunchScore());
        }

        private void HandleShotScored(ShotScore shot)
        {
            if (!isActiveAndEnabled)
                return;
            ShowPraise(shot.cells);
            if (comboLabel == null)
                return;

            bool lostCombo = shot.cells == 0 && _shownStreak > 1;
            _shownStreak   = shot.streak;

            // A plain single-cell shot says nothing worth a burst; a wasted ball only
            // matters if it cost the player a streak they had going.
            if (!lostCombo && shot.Multiplier <= 1)
                return;

            string text = lostCombo
                ? "COMBO LOST"
                : (shot.Multiplier > 1 ? $"x{shot.Multiplier}   +{shot.points:n0}"
                                       : $"+{shot.points:n0}");

            Color tint = lostCombo
                ? LostTint
                : Color.Lerp(PlainTint, BigTint,
                             Mathf.InverseLerp(2f, 12f, shot.Multiplier));

            comboLabel.text  = text;
            comboLabel.color = new Color(tint.r, tint.g, tint.b, 1f);

            if (_burst != null)
                StopCoroutine(_burst);
            _burst = StartCoroutine(BurstOut());
        }

        private IEnumerator PunchScore()
        {
            const float dur = 0.12f;
            var rt = scoreLabel.rectTransform;
            float t = 0f;
            while (t < dur)
            {
                rt.localScale = _scoreRest * Mathf.Lerp(1.20f, 1f, t / dur);
                t += Time.deltaTime;
                yield return null;
            }
            rt.localScale = _scoreRest;
            _punch = null;
        }

        // Rises slightly and fades — held fully opaque for the first third so the
        // number is readable before it starts leaving.
        private IEnumerator BurstOut()
        {
            var rt = comboLabel.rectTransform;
            Vector2 rest = rt.anchoredPosition;
            float dur = Mathf.Max(0.2f, burstDuration);
            float t = 0f;

            while (t < dur)
            {
                float p = t / dur;
                rt.anchoredPosition = rest + new Vector2(0f, 35f * p);
                SetComboAlpha(p < 0.34f ? 1f : 1f - (p - 0.34f) / 0.66f);
                t += Time.deltaTime;
                yield return null;
            }

            rt.anchoredPosition = rest;
            SetComboAlpha(0f);
            _burst = null;
        }

        private void SetComboAlpha(float a)
        {
            if (comboLabel == null)
                return;
            Color c = comboLabel.color;
            comboLabel.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
        }

        private void ShowPraise(int cells)
        {
            for (int i = 0; i < Praise.Length; i++)
            {
                var (min, word, tint) = Praise[i];
                if (cells < min)
                    continue;
                GameAudio.PlayPraise(Praise.Length - 1 - i);   // the table runs biggest-first
                if (praiseLabel == null)
                    return;
                praiseLabel.text  = word;
                praiseLabel.color = new Color(tint.r, tint.g, tint.b, 0f);
                if (_praise != null)
                    StopCoroutine(_praise);
                _praise = StartCoroutine(PraisePop());
                return;
            }
        }

        // Big pop-in with overshoot, a beat held, then it drifts up and fades.
        private IEnumerator PraisePop()
        {
            var rt = praiseLabel.rectTransform;
            Vector2 rest = _praiseRest;
            const float dur = 0.9f;
            float t = 0f;
            while (t < dur)
            {
                float p = t / dur;
                float scale = p < 0.18f ? Mathf.Lerp(0.4f, 1.18f, p / 0.18f)
                            : p < 0.30f ? Mathf.Lerp(1.18f, 1f, (p - 0.18f) / 0.12f) : 1f;
                float alpha = p < 0.10f ? p / 0.10f : p < 0.55f ? 1f : 1f - (p - 0.55f) / 0.45f;
                rt.localScale       = Vector3.one * scale;
                rt.anchoredPosition = rest + new Vector2(0f, p > 0.55f ? 54f * (p - 0.55f) / 0.45f : 0f);
                Color c = praiseLabel.color;
                praiseLabel.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
                t += Time.deltaTime;
                yield return null;
            }
            rt.anchoredPosition = rest;
            rt.localScale = Vector3.one;
            Color end = praiseLabel.color;
            praiseLabel.color = new Color(end.r, end.g, end.b, 0f);
            _praise = null;
        }
    }
}
