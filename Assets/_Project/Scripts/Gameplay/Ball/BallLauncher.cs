using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Ball launch and landing.
    //
    // Launch() is called by TapLaunchController. It spawns a flying ball visual
    // (BallVisual), tweens it along the simulated arc, then raises the painted
    // cells as an outward wave and advances the BallQueue.
    //
    // The ball is NOT a physics body: it follows the exact arc AimPreview drew, in
    // a fixed time regardless of distance. That keeps the shot snappy at any range
    // and makes the previewed cell the painted cell by construction.
    //
    // Wire up in Inspector:
    //   _queue         — BallQueue
    //   _grid          — GridRenderer
    //   _launchOrigin  — Transform where ball spawns (catapult ball pivot)
    public class BallLauncher : MonoBehaviour
    {
        [SerializeField] private BallQueue    _queue;
        [SerializeField] private GridRenderer _grid;
        [SerializeField] private Transform    _launchOrigin;

        [Header("Flight")]
        [SerializeField] private float _flightDuration  = 0.45f;   // snappier travel (was 0.65)
        [SerializeField] private float _ballVisualScale = 0.42f;

        [Header("Paint wave")]
        [SerializeField] private float _riseStagger = 0.025f;  // delay between each cell rising (was 0.04)

        // Consumed by GameManager for win/lose checks
        public event Action<Vector3> OnBallLanded;

        // How many cells this shot put paint into, and where it hit. Raised just
        // BEFORE OnBallLanded so GameManager can score the shot before the same
        // landing decides the level is over — otherwise the winning shot would be
        // missing from the total on the result screen. A count of 0 is a wasted
        // ball, which is exactly what breaks a combo (see GameManager).
        public event Action<int, Vector3> OnShotPainted;

        // Number of balls currently in flight. Multiple may overlap so the player
        // can fire back-to-back; GameManager checks this before declaring a loss.
        private int _activeFlights;
        public bool IsBusy => _activeFlights > 0;

        // ── Undo record ───────────────────────────────────────────────────
        // Everything needed to put the world back the way it was before the last
        // shot: the queue as it stood before the ball was consumed, and the cells
        // this shot put paint into. Painting only ever ADDS hits (an Ice cell may
        // have merely cracked), so removing exactly these hits is an exact inverse.
        //
        // Set at landing, so it always describes the most recently LANDED shot.
        // GameManager only offers undo while nothing is in flight, which keeps
        // "the last shot" unambiguous when the player fires overlapping shots.
        public sealed class ShotRecord
        {
            public BallQueue.Snapshot  queue;
            public List<Vector2Int>    painted;   // cells this shot hit (one hit each)
            public BallData            ball;
        }

        public ShotRecord LastShot { get; private set; }
        public void ClearLastShot() => LastShot = null;

        // ── Launch entry point ────────────────────────────────────────────
        // Called by TapLaunchController with a velocity already solved to the
        // aimed cell centre (LaunchSolver.SolveToCell). The origin is wherever
        // the selected ball sits (its tray slot), so the flying copy takes off
        // from the ball the player just saw.
        public void Launch(Vector3 velocity) =>
            Launch(_launchOrigin ? _launchOrigin.position : transform.position, velocity);

        public void Launch(Vector3 origin, Vector3 velocity)
        {
            if (_queue == null || _queue.IsEmpty || velocity == Vector3.zero || _grid == null) return;

            // Same simulation resolution as AimPreview → preview == reality.
            List<Vector3> arc = TrajectorySimulator.Simulate(
                origin, velocity, GameConstants.TrajectorySteps,
                GameConstants.TrajectoryTimeStep, out Vector3 landPos);

            // Safety: bail only if the grid is degenerate and clamping couldn't land us on it.
            if (!_grid.WorldToGrid(landPos, out _, out _))
                return;

            // Snapshot the queue BEFORE consuming, so undo can restore it exactly —
            // including any RemoveColor purge this shot goes on to trigger.
            var queueBefore = _queue.Capture();

            // Consume the ball now so the next shot uses the next ball — the
            // player can fire again without waiting for this one to land.
            var ballData = _queue.Consume();
            if (ballData == null) return;

            _activeFlights++;
            StartCoroutine(DoLaunch(arc, landPos, origin, ballData, queueBefore));
        }

        // ── Arc tween coroutine ───────────────────────────────────────────
        private IEnumerator DoLaunch(List<Vector3> arc, Vector3 landPos, Vector3 origin,
                                     BallData ballData, BallQueue.Snapshot queueBefore)
        {
            // Spawn flying ball — BallVisual.Create handles material cleanup on Destroy
            var bv  = BallVisual.Create(null, ballData.color, ballData.powerLevel, ballData.shape, _ballVisualScale);
            bv.transform.position = origin;
            bv.EnableTrail(_ballVisualScale * 0.6f);

            GameFX.Instance.LaunchPuff(origin);

            // Tween ball along arc — with a quick pop-in scale as it leaves.
            const float popDur = 0.12f;
            float elapsed = 0f;
            while (elapsed < _flightDuration)
            {
                float t = elapsed / _flightDuration;
                bv.transform.position  = TrajectorySimulator.SamplePath(arc, t);
                float pop = elapsed < popDur ? Mathf.Lerp(0.3f, 1f, elapsed / popDur) : 1f;
                bv.transform.localScale = Vector3.one * pop;
                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(bv.gameObject);

            // Landing splash + shockwave ring in the ball's color
            Color ballCol = GameConstants.GetColorF(ballData.color);
            GameFX.Instance.Impact(landPos, ballCol);
            GameFX.Instance.ImpactRing(landPos, ballCol);

            // Raise the painted cells as a wave rippling outward from the hit cell,
            // then a big bloom once they have all risen. Awaited (not fire-and-forget)
            // so the cells are actually filled BEFORE OnBallLanded runs the win /
            // completed-colour checks below. The player can still fire again during
            // the wave — Launch() isn't gated on this coroutine.
            var painted = new List<Vector2Int>();
            if (_grid.WorldToGrid(landPos, out int gx, out int gy))
                yield return PaintWave(gx, gy, ballData, landPos, painted);

            LastShot = new ShotRecord { queue = queueBefore, painted = painted, ball = ballData };

            // Decrement only now (after the fills land) so an overlapping shot's
            // landing can't declare a premature loss while this wave is still rising.
            _activeFlights = Mathf.Max(0, _activeFlights - 1);

            OnShotPainted?.Invoke(painted.Count, landPos);   // score first (see the event)
            OnBallLanded?.Invoke(landPos);
        }

        // ── Outward rising wave ───────────────────────────────────────────
        // Paints the matched cells one at a time, nearest-to-the-hit first, so the
        // cubes rise in a ripple. Once the last one is up, fires a big bloom.
        // `paintedInto` collects the cells this shot hit, for the undo record.
        //
        // ApplyHit rather than SetFilled: an Ice cell takes two hits, and only the
        // cell itself knows whether this one filled it or merely cracked it.
        private IEnumerator PaintWave(int gx, int gy, BallData ball, Vector3 landPos,
                                      List<Vector2Int> paintedInto)
        {
            var targets = PaintingSystem.PaintTargetsOrdered(_grid, gx, gy, ball);
            if (targets.Count == 0) yield break;

            var wait = _riseStagger > 0f ? new WaitForSeconds(_riseStagger) : null;
            foreach (var c in targets)
            {
                _grid.ApplyHit(c.x, c.y);
                paintedInto?.Add(c);
                Haptics.Light();                 // tick as each cube starts rising
                if (wait != null) yield return wait;
            }

            // Stronger pulse once every cube of this shot has risen.
            Haptics.Heavy();

            // Bloom grows with how many cells this hit lit up — bigger paint, bigger pop.
            float scale = Mathf.Lerp(0.8f, 2.0f, Mathf.InverseLerp(1f, 12f, targets.Count));
            GameFX.Instance.Bloom(landPos, GameConstants.GetColorF(ball.color), scale);
        }
    }
}
