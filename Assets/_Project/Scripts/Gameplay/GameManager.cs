using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatapultGames
{
    // Task 15 — Win / fail detection.
    // Orchestrates all gameplay systems and triggers the result screen.
    //
    // Wire up in Inspector:
    //   grid         — GridRenderer
    //   queue        — BallQueue
    //   launcher     — BallLauncher
    //   aimPreview   — AimPreview
    //   resultScreen — ResultScreenUI
    //   warningLabel — optional TMP label for the dead-end warning
    public sealed class GameManager : MonoBehaviour
    {
        // Raised for every landed shot, including a wasted one (cells == 0), which
        // is what the HUD needs to show a broken combo.
        public event Action<ShotScore> OnShotScored;

        // Running total. Also raised on undo, so a HUD only has to watch one event.
        public event Action<int> OnScoreChanged;

        public string LevelName => _levelName;
        public int Score       => _score;
        public int ComboStreak => _comboStreak;

        // True once the level has been won or lost — other systems (e.g. GameScene's
        // tap input) check this to stop accepting launches.
        public bool IsOver => _gameOver;

        // Undo is only offered with the board at rest. Mid-flight the phrase "the
        // last shot" is ambiguous (shots may overlap), and unwinding a queue while
        // a paint wave is still rising would fight BallLauncher's flight counter.
        public bool CanUndo =>
            !_gameOver && launcher != null && !launcher.IsBusy && launcher.LastShot != null;

        public bool HasNextLevel => LevelOrder.Next(_levelName) != null;

        [SerializeField] private GridRenderer   grid;
        [SerializeField] private BallQueue      queue;
        [SerializeField] private BallLauncher   launcher;
        [SerializeField] private AimPreview     aimPreview;
        [SerializeField] private ResultScreenUI resultScreen;

        [Header("Keep going offer")]
        [Tooltip("Balls handed out when the player takes the offer after a loss. " +
                 "Offered once per level — a level must stay beatable on its own.")]
        [SerializeField] private int extraBallCount = 3;

        [Tooltip("Largest power the rescue may hand out (1-3). It picks the smallest " +
                 "ball that does the job, so this is a ceiling, not the size used.")]
        [SerializeField] [Range(1, 3)] private int extraBallMaxPower = 3;

        [Header("Dead-end warning")]
        [Tooltip("Optional. Says which colour can no longer be finished. The level " +
                 "goes on — undo or a booster can still save it — and is only lost " +
                 "once the queue runs dry.")]
        [SerializeField] private TextMeshProUGUI warningLabel;
        [SerializeField] private float warningDuration = 2.5f;

        private bool _gameOver;
        private bool _extraBallsSpent;                              // offer is once per level
        private readonly List<BallData> _remainingBalls = new();    // reused by the solvability check

        // The dead end last warned about, so the player is told once rather than
        // after every shot. Cleared when the dead end goes away (undo, booster,
        // rescue balls), so walking back into it warns again.
        private const int NoWarning = -1;
        private const int WildWarning = 1000;                       // the Joker row has no colour
        private int       _warnedKey = NoWarning;
        private Coroutine _warning;

        // The level this run is playing, for progress and "next level". Null when
        // a level came from no file (test harness).
        private string _levelName;
        private int   _score;
        private int   _comboStreak;
        private ShotAward _lastAward;

        // Shapes the rescue may hand out — the casual set only. L is left out on
        // purpose (its arms run to the grid edges, so it is a different power
        // class — handing one out would not rescue the level, it would erase it)
        // and Diagonal is a legacy shape the game no longer teaches.
        private static readonly BallShape[] RescueShapes =
        {
            BallShape.Square, BallShape.Line, BallShape.Column, BallShape.Plus
        };

        private void OnEnable()
        {
            if (!launcher)
                return;
            launcher.OnBallLanded  += OnBallLanded;
            launcher.OnShotPainted += OnShotPainted;
        }

        private void OnDisable()
        {
            if (!launcher)
                return;
            launcher.OnBallLanded  -= OnBallLanded;
            launcher.OnShotPainted -= OnShotPainted;
        }

        // Called by LevelLoader once the grid and queue hold the new level. The dev
        // level picker swaps levels in place instead of reloading the scene, so this
        // has to put EVERYTHING back to a fresh run — including a run that had
        // already ended, or the next board would start behind a result panel.
        public void BeginRun(string levelName)
        {
            _levelName       = levelName;
            _extraBallsSpent = false;

            if (_gameOver)
            {
                _gameOver = false;
                if (aimPreview)
                    aimPreview.enabled = true;
                resultScreen?.Hide();
            }

            ResetScore();
        }

        // One step back, and an exact one: painting only ever ADDS hits, so removing
        // the hits that shot landed — and restoring the queue as it stood before the
        // ball was consumed — puts the board back where it was. Ice comes along for
        // free: a cell that only cracked goes back to intact.
        //
        // Restoring the whole queue also undoes any colour purge the shot set off,
        // which is why BallQueue snapshots rather than pushing a single ball back.
        public void UndoLastShot()
        {
            if (!CanUndo)
                return;

            ShotRecord shot = launcher.LastShot;

            if (grid != null && shot.painted != null)
                foreach (var c in shot.painted)
                    grid.UndoHit(c.x, c.y);

            queue?.Restore(shot.queue);
            RevertLastAward();     // the score goes back too, or undo prints points

            // One step only — the record is spent.
            launcher.ClearLastShot();
            _warnedKey = NoWarning;   // the shot that walked into a dead end may be the one undone
            HideWarning();
            Haptics.Medium();
            GameAudio.Play(Sfx.Undo);
        }

        // Called by the result screen's third button. Hands out a few balls aimed
        // at whatever is still missing and resumes the level in place.
        //
        // Deliberately once per level: a level has to be beatable on its own, and
        // an endless top-up would erase the difficulty curve it was authored to.
        public void GrantExtraBalls()
        {
            if (_extraBallsSpent || queue == null || grid == null)
                return;

            var rescue = BuildRescueBalls(extraBallCount);
            if (rescue.Count == 0)
                return;

            _extraBallsSpent = true;
            queue.Append(rescue);

            // Resume: unwind exactly what EndGame did.
            _gameOver = false;
            if (aimPreview)
                aimPreview.enabled = true;
            resultScreen?.Hide();

            GameFX.Instance.Flash(new Color(0.35f, 0.85f, 0.45f), 0.25f, 0.35f);
            Haptics.Medium();
            GameAudio.Play(Sfx.Booster);   // same sparkle: the rescue is a booster too
        }

        // Levels reload in place (LevelLoader → BeginRun resets the run).
        public void RestartLevel() => GameFlow.RequestLevel(_levelName);

        // After the last level there is nowhere further: back to the level list.
        public void NextLevel()
        {
            string next = LevelOrder.Next(_levelName);
            if (next == null)
                GameFlow.RequestLevelSelect();
            else
                GameFlow.RequestLevel(next);
        }

        public void GoToMainMenu() => GameFlow.RequestMenu();

        // Evaluated after every ball lands. Because shots can overlap, a loss is
        // only declared once the queue is empty AND no balls are still in flight.
        private void OnBallLanded(Vector3 _)
        {
            if (_gameOver)
                return;

            // Once a colour is fully painted its leftover balls are useless — drop
            // them from the queue. Done before the win/lose check so an empty queue
            // afterwards is evaluated correctly.
            PurgeCompletedColors();

            if (grid != null && grid.AllColoredCellsFilled())
            {
                EndGame(ResultReason.Won);
                return;
            }

            if (launcher != null && launcher.IsBusy)
                return;   // let the volley finish

            if (queue != null && queue.IsEmpty)
            {
                EndGame(ResultReason.OutOfBalls);
                return;
            }

            // Balls left, but not enough of them. This used to end the level on the
            // spot; with undo and boosters in hand that is no longer a fact, only a
            // warning — the player is told now instead of finding out forty shots
            // later, and the loss itself waits for an empty queue.
            CheckDeadEnd();
        }

        // Can what is left in the queue still cover what is left on the grid?
        // Same measurement the level editor validates with (CoverageAnalyzer), which
        // is why that lives in Shared/ — a colour whose ceiling has fallen below its
        // remaining cells can never be completed by these balls alone.
        private void CheckDeadEnd()
        {
            if (grid == null || queue == null)
                return;

            queue.CopyRemaining(_remainingBalls);
            if (_remainingBalls.Count == 0)
                return;   // the empty-queue path handles this

            var rows = CoverageAnalyzer.Analyze(CoverageAnalyzer.BuildTargets(grid), _remainingBalls);

            foreach (ColorCoverage row in rows)
            {
                if (!row.Impossible)
                    continue;

                int key = row.isWild ? WildWarning : (int)row.color;
                if (key == _warnedKey)
                    return;   // already said
                _warnedKey = key;
                ShowWarning(row);
                return;
            }

            _warnedKey = NoWarning;   // solvable again — the next dead end is news
        }

        private void ShowWarning(ColorCoverage row)
        {
            GameFX.Instance.Shake(0.10f, 0.22f);
            Haptics.Medium();
            GameAudio.Play(Sfx.Warning);

            if (warningLabel == null)
                return;

            string what = row.isWild
                ? "The joker cells"
                : $"<color=#{ColorUtility.ToHtmlStringRGB(GameConstants.GetColorF(row.color))}>" +
                  $"{GameConstants.GetColorDisplayName(row.color)}</color>";
            warningLabel.text = $"{what} can't be finished\n<size=70%>Undo or use a booster</size>";

            if (_warning != null)
                StopCoroutine(_warning);
            _warning = StartCoroutine(FadeWarning());
        }

        // Quick fade in, hold, fade out. Unscaled time: a hit-stop on the same shot
        // must not freeze the message.
        private IEnumerator FadeWarning()
        {
            const float fade = 0.2f;
            float hold = Mathf.Max(0f, warningDuration - 2f * fade);
            float total = 2f * fade + hold;
            var c = warningLabel.color;

            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                float a = t < fade ? t / fade : t < fade + hold ? 1f : 1f - (t - fade - hold) / fade;
                warningLabel.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
                yield return null;
            }
            HideWarning();
        }

        private void HideWarning()
        {
            if (_warning != null)
            {
                StopCoroutine(_warning);
                _warning = null;
            }
            if (warningLabel == null)
                return;
            var c = warningLabel.color;
            warningLabel.color = new Color(c.r, c.g, c.b, 0f);
        }

        // One landed shot. A shot that painted nothing pays nothing and breaks the
        // streak — that is the only thing keeping the multiplier meaningful.
        private void OnShotPainted(int cells, Vector3 _)
        {
            // A shot still in the air when the level ended does not move the total the
            // result screen is already showing. The winning shot itself is scored,
            // because this runs before OnBallLanded decides the level is over.
            if (_gameOver)
                return;

            int streak    = cells > 0 ? _comboStreak + 1 : 0;
            int shotMult  = GameConstants.GetShotMultiplier(cells);
            int comboMult = GameConstants.GetComboMultiplier(streak);
            int points    = cells * GameConstants.PointsPerCell * shotMult * comboMult;

            _lastAward   = new ShotAward { valid = true, points = points, streakBefore = _comboStreak };
            _comboStreak = streak;
            _score      += points;

            OnShotScored?.Invoke(new ShotScore(cells, points, shotMult, comboMult, streak));
            OnScoreChanged?.Invoke(_score);
        }

        // Part of BeginRun: without it the score from the previous board would keep
        // counting across an in-place level switch.
        private void ResetScore()
        {
            _score       = 0;
            _comboStreak = 0;
            _lastAward   = default;
            _warnedKey   = NoWarning;
            HideWarning();
            OnScoreChanged?.Invoke(_score);
        }

        // Undo gives the points back as well as the cells. One step only, matching
        // the single-step undo record it is paired with.
        private void RevertLastAward()
        {
            if (!_lastAward.valid)
                return;

            _score       = Mathf.Max(0, _score - _lastAward.points);
            _comboStreak = _lastAward.streakBefore;
            _lastAward   = default;

            OnScoreChanged?.Invoke(_score);
        }

        // Remove queued balls whose colour is already complete (all its cells filled).
        // Colour-agnostic and level-agnostic: it reads live per-colour progress, and is
        // idempotent (re-running finds nothing to remove) so it needs no reset on reload.
        private void PurgeCompletedColors()
        {
            if (grid == null || queue == null)
                return;

            // A Joker cell takes any colour, so while one is still empty every ball
            // in the queue is potentially the ball that finishes the level — purging
            // a "finished" colour would throw away the only thing that could.
            if (grid.HasUnfilledWildCells())
                return;

            foreach (ColorProgress cp in grid.CountByColor())
            {
                if (cp.total <= 0 || cp.filled < cp.total)
                    continue;

                int removed = queue.RemoveColor(cp.color);
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
            if (grid != null)
                grid.PulseColor(color);
            if (!_gameOver)
                GameAudio.Play(Sfx.ColorFanfare);   // the win fanfare owns the last one
            // Confetti over the board + a tiny zoom punch: "a whole colour is done"
            // is the mid-level payoff, and it should look like one.
            if (grid != null)
                GameFX.Instance.Confetti(grid.WorldCenter, GameConstants.GetColorF(color));
            GameFX.Instance.ZoomPunch(1.5f);
            GameFX.Instance.Shake(0.22f, 0.30f);
        }

        // Pick balls that actually fit what is left on the board.
        //
        // The obvious version — "hand out the biggest stamp in the colour that is
        // furthest from done" — is the same mistake the level validator used to
        // make: a stamp's AREA is not what it paints. A 4x4 dropped on a one-row
        // stripe covers four cells, so the rescue would arrive useless in exactly
        // the situation that produced the dead end.
        //
        // So each ball is chosen by measuring every (colour, shape, power) against
        // the real board, taking the best, and striking that placement off before
        // choosing the next one — the balls are a plan, not three independent picks.
        // On a tie the smaller stamp wins, so the rescue is enough to finish and no
        // more. Runs once, on a button press; a few hundred thousand cheap
        // comparisons is well inside a frame.
        private List<BallData> BuildRescueBalls(int count)
        {
            var result = new List<BallData>(Mathf.Max(0, count));
            if (count <= 0 || grid == null)
                return result;

            var colors = new List<CellColor>();
            foreach (ColorProgress cp in grid.CountByColor())
                if (cp.total > cp.filled)
                    colors.Add(cp.color);
            if (colors.Count == 0)
                return result;

            TargetBoard board    = CoverageAnalyzer.BuildTargets(grid);
            int w        = grid.Width;
            int h        = grid.Height;
            int maxPower = Mathf.Clamp(extraBallMaxPower, 1, 3);
            var probe    = new BallData();

            for (int n = 0; n < count; n++)
            {
                BallData best = null;
                int bestGain = 0, bestStamp = int.MaxValue, bestX = 0, bestY = 0;

                foreach (var color in colors)
                foreach (var shape in RescueShapes)
                for (int power = 1; power <= maxPower; power++)
                {
                    probe.color      = color;
                    probe.shape      = shape;
                    probe.powerLevel = power;

                    int gain = CoverageAnalyzer.BestPlacement(board, probe, out int lx, out int ly);
                    if (gain <= 0)
                        continue;

                    int stamp = GameConstants.GetPaintCellCount(probe, w, h);
                    if (gain < bestGain || (gain == bestGain && stamp >= bestStamp))
                        continue;

                    bestGain  = gain;
                    bestStamp = stamp;
                    bestX     = lx;
                    bestY     = ly;
                    best      = new BallData(color, power, shape);
                }

                if (best == null)
                    break;   // nothing left that any ball could paint

                result.Add(best);
                CoverageAnalyzer.ApplyPlacement(board, best, bestX, bestY);
            }

            return result;
        }

        private void EndGame(ResultReason reason)
        {
            bool won = reason == ResultReason.Won;

            _gameOver = true;
            if (aimPreview)
                aimPreview.enabled = false;
            HideWarning();
            GameAudio.Play(won ? Sfx.Win : Sfx.Lose);

            if (won)
            {
                if (grid != null)
                    GameFX.Instance.Win(grid.WorldCenter);
                PlayerProgress.MarkWon(_levelName);   // opens the next level
            }
            else
                GameFX.Instance.Shake(0.12f, 0.25f);

            // The offer only makes sense on a loss, and only if it hasn't been taken
            // yet — and only if there is actually something left for the balls to do.
            bool offerExtra = !won && !_extraBallsSpent && extraBallCount > 0;

            // Brief delay before the panel covers the screen so the win burst is seen.
            float delay = won ? 0.5f : 0.2f;
            StartCoroutine(ShowResultDelayed(reason, offerExtra, delay));
        }

        private IEnumerator ShowResultDelayed(ResultReason reason, bool offerExtra, float delay)
        {
            float elapsed = 0f;
            while (elapsed < delay)
            {
                if (AnyPointerPressedThisFrame())
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // The player may have taken an offer during the delay on a previous
            // frame; don't cover a resumed level with a result panel.
            if (!_gameOver)
                yield break;

            if (resultScreen)
                resultScreen.Show(reason, offerExtra, _score, HasNextLevel);
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
    }
}
