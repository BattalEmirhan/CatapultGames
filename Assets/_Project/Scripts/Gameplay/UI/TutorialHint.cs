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
    // step 2 means they don't need it). Stored as done (PlayerPrefs "TutorialDone")
    // when it runs to its end or the level ends — not when the app is closed
    // half-way, so an interrupted first session sees it again.
    //
    // Hint banner: LevelMetadata.hint, faded in when a level starts and out after
    // _hintDuration or on the first throw, whichever is first.
    //
    // LevelLoader calls BeginLevel after every load. Wire up in Inspector (the scene
    // builder does): references below, plus the pointer / caption / banner UI.
    public class TutorialHint : MonoBehaviour
    {
        [SerializeField] private Camera        _camera;
        [SerializeField] private GridRenderer  _grid;
        [SerializeField] private BallQueue     _queue;
        [SerializeField] private BallQueueView _queueView;
        [SerializeField] private BallLauncher  _launcher;
        [SerializeField] private GameManager   _gameManager;

        [Header("Pointer")]
        [SerializeField] private RectTransform   _pointer;       // disc Image, never a raycast target
        [SerializeField] private Image           _pointerRing;   // child of the pointer
        [SerializeField] private CanvasGroup     _captionGroup;
        [SerializeField] private TextMeshProUGUI _caption;
        [SerializeField] private float           _tapPeriod = 1.3f;

        [Header("Level hint banner")]
        [SerializeField] private CanvasGroup     _hintGroup;
        [SerializeField] private TextMeshProUGUI _hintLabel;
        [SerializeField] private float           _hintDuration = 4f;

        private const string DoneKey = "TutorialDone";

        private enum Step { None, TapCell, WaitForBoard, PickBall }

        private Step      _step;
        private int       _cellX, _cellY;   // TapCell target
        private int       _slot = -1;       // PickBall target
        private Coroutine _hint;
        private Image     _pointerImage;

        public static bool IsDone => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        // Dev / settings use: show the tutorial again on the next first-level start.
        public static void ResetDone() => PlayerPrefs.DeleteKey(DoneKey);

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            // Runtime-made sprites: a scene file cannot keep a reference to them.
            if (_pointer) _pointerImage = _pointer.GetComponent<Image>();
            if (_pointerImage) { _pointerImage.sprite = UiSprites.Disc(); _pointerImage.raycastTarget = false; }
            if (_pointerRing)  { _pointerRing.sprite  = UiSprites.Ring(); _pointerRing.raycastTarget  = false; }
            // Purely informative: none of it may swallow the tap it is asking for.
            foreach (var g in new[] { _captionGroup, _hintGroup })
                if (g) { g.blocksRaycasts = false; g.interactable = false; }
            HidePointer();
            SetAlpha(_hintGroup, 0f);
        }

        private void OnEnable()
        {
            if (_queue)     _queue.OnBallConsumed    += OnBallConsumed;
            if (_queue)     _queue.OnChanged         += OnQueueChanged;
            if (_queueView) _queueView.OnSlotSelected += OnSlotSelected;
        }

        private void OnDisable()
        {
            if (_queue)     _queue.OnBallConsumed    -= OnBallConsumed;
            if (_queue)     _queue.OnChanged         -= OnQueueChanged;
            if (_queueView) _queueView.OnSlotSelected -= OnSlotSelected;
        }

        // ── Entry point ───────────────────────────────────────────────────
        public void BeginLevel(string levelName, string hint)
        {
            _step = Step.None;
            HidePointer();

            if (_hint != null) { StopCoroutine(_hint); _hint = null; }
            SetAlpha(_hintGroup, 0f);
            if (!string.IsNullOrWhiteSpace(hint) && _hintGroup && _hintLabel)
            {
                _hintLabel.text = hint.Trim();
                _hint = StartCoroutine(ShowHint());
            }

            bool firstLevel = levelName != null && levelName == LevelOrder.First;
            if (firstLevel && !IsDone) StartTapCell();
        }

        // ── Steps ─────────────────────────────────────────────────────────
        private void StartTapCell()
        {
            if (!AimAtBestCell()) { Finish(); return; }
            _step = Step.TapCell;
            ShowCaption("Tap a square to throw");
        }

        // The cell where the current ball does the most, so the first throw the
        // tutorial asks for is also a good one.
        private bool AimAtBestCell()
        {
            var ball = _queue != null ? _queue.Current : null;
            if (ball == null || _grid == null) return false;
            int gain = CoverageAnalyzer.BestPlacement(CoverageAnalyzer.BuildTargets(_grid), ball,
                                                      out _cellX, out _cellY);
            return gain > 0;
        }

        // A tray ball worth switching to: another slot, preferably a different
        // colour than the one in hand (otherwise picking it teaches nothing).
        private bool FindOtherSlot()
        {
            _slot = -1;
            if (_queueView == null || _queue == null || _queue.Current == null) return false;

            var current = _queue.Current.color;
            int fallback = -1;
            for (int s = 0; s < _queueView.SlotCount; s++)
            {
                if (s == _queueView.SelectedSlot) continue;
                if (!_queueView.TryGetSlotBall(s, out var ball, out _)) continue;
                if (ball.color != current) { _slot = s; return true; }
                if (fallback < 0) fallback = s;
            }
            _slot = fallback;
            return _slot >= 0;
        }

        private void Finish()
        {
            _step = Step.None;
            HidePointer();
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }

        // ── Events ────────────────────────────────────────────────────────
        private void OnBallConsumed(BallData _)
        {
            // The first throw is the player getting on with it: the banner has done its job.
            if (_hint != null) { StopCoroutine(_hint); _hint = StartCoroutine(FadeOut(_hintGroup, 0.3f)); }

            if (_step == Step.TapCell)       { _step = Step.WaitForBoard; HidePointer(); }
            else if (_step == Step.PickBall) Finish();   // threw instead of picking — they've got it
        }

        // A booster or a pick changed the ball in hand: the best cell may have moved.
        private void OnQueueChanged()
        {
            if (_step == Step.TapCell && !AimAtBestCell()) Finish();
        }

        private void OnSlotSelected(int slot)
        {
            if (_step == Step.PickBall) Finish();
        }

        // ── Per frame: wait for the board, then animate the fingertip ─────
        private void Update()
        {
            if (_step == Step.None) return;

            if (_gameManager != null && _gameManager.IsOver) { Finish(); return; }

            if (_step == Step.WaitForBoard)
            {
                if (_launcher != null && _launcher.IsBusy) return;
                if (!FindOtherSlot()) { Finish(); return; }
                _step = Step.PickBall;
                ShowCaption("Tap a ball to pick it");
                return;
            }

            Vector3 world;
            if (_step == Step.TapCell)
                world = _grid.GridToWorld(_cellX, _cellY);
            else if (!_queueView.TryGetSlotBall(_slot, out _, out world)) { Finish(); return; }

            AnimatePointer(world);
        }

        // One "tap" per period: glide in from below-right, press, ring pulse, fade.
        private void AnimatePointer(Vector3 world)
        {
            if (_pointer == null || _camera == null) return;
            Vector3 sp = _camera.WorldToScreenPoint(world);
            var parent = _pointer.parent as RectTransform;
            if (sp.z <= 0f || parent == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, null, out var target))
            {
                HidePointer();
                return;
            }

            float p = Mathf.Repeat(Time.unscaledTime, _tapPeriod) / _tapPeriod;

            float glide  = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.35f, p));
            float press  = p < 0.35f ? 1f : p < 0.45f ? Mathf.Lerp(1f, 0.78f, (p - 0.35f) / 0.10f)
                                     : Mathf.Lerp(0.78f, 1f, Mathf.InverseLerp(0.45f, 0.60f, p));
            float alpha  = p < 0.10f ? p / 0.10f : p > 0.85f ? 1f - (p - 0.85f) / 0.15f : 1f;

            _pointer.anchoredPosition = target + Vector2.Lerp(new Vector2(90f, -120f), Vector2.zero, glide);
            _pointer.localScale       = Vector3.one * press;
            if (_pointerImage) _pointerImage.color = new Color(1f, 1f, 1f, 0.9f * alpha);

            if (_pointerRing)
            {
                float ring = Mathf.InverseLerp(0.40f, 0.95f, p);
                _pointerRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 2.2f, ring);
                _pointerRing.color = new Color(1f, 1f, 1f, ring > 0f && ring < 1f ? 0.85f * (1f - ring) : 0f);
            }
        }

        // ── UI helpers ────────────────────────────────────────────────────
        private void ShowCaption(string text)
        {
            if (_caption) _caption.text = text;
            SetAlpha(_captionGroup, 1f);
        }

        private void HidePointer()
        {
            if (_pointerImage) _pointerImage.color = new Color(1f, 1f, 1f, 0f);
            if (_pointerRing)  _pointerRing.color  = new Color(1f, 1f, 1f, 0f);
            SetAlpha(_captionGroup, 0f);
        }

        private IEnumerator ShowHint()
        {
            const float fadeIn = 0.25f;
            for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
            {
                SetAlpha(_hintGroup, t / fadeIn);
                yield return null;
            }
            SetAlpha(_hintGroup, 1f);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, _hintDuration));
            yield return FadeOut(_hintGroup, 0.4f);
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
            if (group) group.alpha = Mathf.Clamp01(a);
        }
    }
}
