using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames
{
    // First-run tutorial and per-level hint banner (BACKLOG Faz 5).
    //
    // Tutorial: shown once, on the first level, the first time it is played. Show,
    // don't tell — at most two steps, each a fingertip tapping the exact spot:
    //   1. TapCell  — the cell where the current ball paints the most
    //                 (CoverageAnalyzer.BestPlacement), "Tap to throw"
    //   2. PickBall — once the board is at rest, a tray ball of another colour,
    //                 "Tap a ball to pick it". Skipped when the tray has no choice.
    // Either step also ends on whatever the player does instead (throwing during
    // step 2 means they don't need it). Stored as done (save key "TutorialDone")
    // when it runs to its end or the level ends — not when the app is closed
    // half-way, so an interrupted first session sees it again.
    //
    // Hint banner: LevelMetadata.hint, faded in when a level starts and out after
    // hintDuration or on the first throw, whichever is first.
    //
    // LevelLoader calls BeginLevel after every load. Wire up in Inspector (the scene
    // builder does): references below, plus the pointer / caption / banner UI.
    public sealed class TutorialHint : MonoBehaviour
    {
        public static bool IsDone => SaveStore.Current.GetInt(DoneKey, 0) == 1;

        [SerializeField] private Camera        camera;
        [SerializeField] private GridRenderer  grid;
        [SerializeField] private BallQueue     queue;
        [SerializeField] private BallQueueView queueView;
        [SerializeField] private BallLauncher  launcher;
        [SerializeField] private GameManager   gameManager;

        [Header("Pointer")]
        [SerializeField] private RectTransform   pointer;       // disc Image, never a raycast target
        [SerializeField] private Image           pointerRing;   // child of the pointer
        [SerializeField] private CanvasGroup     captionGroup;
        [SerializeField] private TextMeshProUGUI caption;
        [SerializeField] private float           tapPeriod = 1.3f;

        [Header("Level hint banner")]
        [SerializeField] private CanvasGroup     hintGroup;
        [SerializeField] private TextMeshProUGUI hintLabel;
        [SerializeField] private float           hintDuration = 4f;

        private const string DoneKey = "TutorialDone";
        private TutorialStep      _step;
        private int       _cellX, _cellY;   // TapCell target
        private int       _slot = -1;       // PickBall target
        private Coroutine _hint;
        private Image     _pointerImage;

        private void Awake()
        {
            // Runtime-made sprites: a scene file cannot keep a reference to them.
            if (pointer)
                _pointerImage = pointer.GetComponent<Image>();
            if (_pointerImage)
            {
                _pointerImage.sprite = UiSprites.Disc();
                _pointerImage.raycastTarget = false;
            }
            if (pointerRing)
            {
                pointerRing.sprite  = UiSprites.Ring();
                pointerRing.raycastTarget  = false;
            }
            // Purely informative: none of it may swallow the tap it is asking for.
            foreach (var g in new[] { captionGroup, hintGroup })
                if (g)
                {
                    g.blocksRaycasts = false;
                    g.interactable = false;
                }
            HidePointer();
            SetAlpha(hintGroup, 0f);
        }

        private void OnEnable()
        {
            if (queue)
                queue.OnBallConsumed    += OnBallConsumed;
            if (queue)
                queue.OnChanged         += OnQueueChanged;
            if (queueView)
                queueView.OnSlotSelected += OnSlotSelected;
        }

        private void Update()
        {
            if (_step == TutorialStep.None)
                return;

            if (gameManager != null && gameManager.IsOver)
            {
                Finish();
                return;
            }

            if (_step == TutorialStep.WaitForBoard)
            {
                if (launcher != null && launcher.IsBusy)
                    return;
                if (!FindOtherSlot())
                {
                    Finish();
                    return;
                }
                _step = TutorialStep.PickBall;
                ShowCaption("Tap a ball to pick it");
                return;
            }

            Vector3 world;
            if (_step == TutorialStep.TapCell)
                world = grid.GridToWorld(_cellX, _cellY);
            else if (!queueView.TryGetSlotBall(_slot, out _, out world))
            {
                Finish();
                return;
            }

            AnimatePointer(world);
        }

        private void OnDisable()
        {
            if (queue)
                queue.OnBallConsumed    -= OnBallConsumed;
            if (queue)
                queue.OnChanged         -= OnQueueChanged;
            if (queueView)
                queueView.OnSlotSelected -= OnSlotSelected;
        }

        // Dev / settings use: show the tutorial again on the next first-level start.
        public static void ResetDone() => SaveStore.Current.Delete(DoneKey);

        public void BeginLevel(string levelName, string hint)
        {
            _step = TutorialStep.None;
            HidePointer();

            if (_hint != null)
            {
                StopCoroutine(_hint);
                _hint = null;
            }
            SetAlpha(hintGroup, 0f);
            if (!string.IsNullOrWhiteSpace(hint) && hintGroup && hintLabel)
            {
                hintLabel.text = hint.Trim();
                _hint = StartCoroutine(ShowHint());
            }

            bool firstLevel = levelName != null && levelName == LevelOrder.First;
            if (firstLevel && !IsDone)
                StartTapCell();
        }

        private void StartTapCell()
        {
            if (!AimAtBestCell())
            {
                Finish();
                return;
            }
            _step = TutorialStep.TapCell;
            ShowCaption("Tap a square to throw");
        }

        // The cell where the current ball does the most, so the first throw the
        // tutorial asks for is also a good one.
        private bool AimAtBestCell()
        {
            var ball = queue != null ? queue.Current : null;
            if (ball == null || grid == null)
                return false;
            int gain = CoverageAnalyzer.BestPlacement(CoverageAnalyzer.BuildTargets(grid), ball,
                                                      out _cellX, out _cellY);
            return gain > 0;
        }

        // A tray ball worth switching to: another slot, preferably a different
        // colour than the one in hand (otherwise picking it teaches nothing).
        private bool FindOtherSlot()
        {
            _slot = -1;
            if (queueView == null || queue == null || queue.Current == null)
                return false;

            var current = queue.Current.color;
            int fallback = -1;
            for (int s = 0; s < queueView.SlotCount; s++)
            {
                if (s == queueView.SelectedSlot)
                    continue;
                if (!queueView.TryGetSlotBall(s, out var ball, out _))
                    continue;
                if (ball.color != current)
                {
                    _slot = s;
                    return true;
                }
                if (fallback < 0)
                    fallback = s;
            }
            _slot = fallback;
            return _slot >= 0;
        }

        private void Finish()
        {
            _step = TutorialStep.None;
            HidePointer();
            SaveStore.Current.SetInt(DoneKey, 1);
            SaveStore.Current.Flush();
        }

        private void OnBallConsumed(BallData _)
        {
            // The first throw is the player getting on with it: the banner has done its job.
            if (_hint != null)
            {
                StopCoroutine(_hint);
                _hint = StartCoroutine(FadeOut(hintGroup, 0.3f));
            }

            if (_step == TutorialStep.TapCell)
            {
                _step = TutorialStep.WaitForBoard;
                HidePointer();
            }
            else if (_step == TutorialStep.PickBall)
                Finish();   // threw instead of picking — they've got it
        }

        // A booster or a pick changed the ball in hand: the best cell may have moved.
        private void OnQueueChanged()
        {
            if (_step == TutorialStep.TapCell && !AimAtBestCell())
                Finish();
        }

        private void OnSlotSelected(int slot)
        {
            if (_step == TutorialStep.PickBall)
                Finish();
        }

        // One "tap" per period: glide in from below-right, press, ring pulse, fade.
        private void AnimatePointer(Vector3 world)
        {
            if (pointer == null || camera == null)
                return;
            Vector3 sp = camera.WorldToScreenPoint(world);
            var parent = pointer.parent as RectTransform;
            if (sp.z <= 0f || parent == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, camera, out var target))
            {
                HidePointer();
                return;
            }

            float p = Mathf.Repeat(Time.unscaledTime, tapPeriod) / tapPeriod;

            float glide  = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.35f, p));
            float press  = p < 0.35f ? 1f : p < 0.45f ? Mathf.Lerp(1f, 0.78f, (p - 0.35f) / 0.10f)
                                     : Mathf.Lerp(0.78f, 1f, Mathf.InverseLerp(0.45f, 0.60f, p));
            float alpha  = p < 0.10f ? p / 0.10f : p > 0.85f ? 1f - (p - 0.85f) / 0.15f : 1f;

            pointer.anchoredPosition = target + Vector2.Lerp(new Vector2(122f, -162f), Vector2.zero, glide);
            pointer.localScale       = Vector3.one * press;
            if (_pointerImage)
                _pointerImage.color = new Color(1f, 1f, 1f, 0.9f * alpha);

            if (pointerRing)
            {
                float ring = Mathf.InverseLerp(0.40f, 0.95f, p);
                pointerRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 2.2f, ring);
                pointerRing.color = new Color(1f, 1f, 1f, ring > 0f && ring < 1f ? 0.85f * (1f - ring) : 0f);
            }
        }

        private void ShowCaption(string text)
        {
            if (caption)
                caption.text = text;
            SetAlpha(captionGroup, 1f);
        }

        private void HidePointer()
        {
            if (_pointerImage)
                _pointerImage.color = new Color(1f, 1f, 1f, 0f);
            if (pointerRing)
                pointerRing.color  = new Color(1f, 1f, 1f, 0f);
            SetAlpha(captionGroup, 0f);
        }

        private IEnumerator ShowHint()
        {
            const float fadeIn = 0.25f;
            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                SetAlpha(hintGroup, t / fadeIn);
                yield return null;
            }
            SetAlpha(hintGroup, 1f);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, hintDuration));
            yield return FadeOut(hintGroup, 0.4f);
        }

        private IEnumerator FadeOut(CanvasGroup group, float duration)
        {
            float from = group ? group.alpha : 0f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(group, Mathf.Lerp(from, 0f, t / duration));
                yield return null;
            }
            SetAlpha(group, 0f);
            _hint = null;
        }

        private static void SetAlpha(CanvasGroup group, float a)
        {
            if (group)
                group.alpha = Mathf.Clamp01(a);
        }
    }
}
