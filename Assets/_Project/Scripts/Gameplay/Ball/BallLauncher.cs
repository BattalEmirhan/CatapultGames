using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Task 14 — Ball launch and landing.
    //
    // Listens for SlingshotController.OnLaunch, spawns a flying ball visual
    // (BallVisual: Lit sphere + power rings), tweens it along the simulated arc,
    // then raises the painted cells as an outward wave and advances the BallQueue.
    //
    // Wire up in Inspector:
    //   _slingshot     — SlingshotController
    //   _queue         — BallQueue
    //   _grid          — GridRenderer
    //   _launchOrigin  — Transform where ball spawns (catapult ball pivot)
    public class BallLauncher : MonoBehaviour
    {
        [SerializeField] private SlingshotController _slingshot;
        [SerializeField] private BallQueue           _queue;
        [SerializeField] private GridRenderer        _grid;
        [SerializeField] private Transform           _launchOrigin;

        [Header("Flight")]
        [SerializeField] private float _flightDuration  = 0.45f;   // snappier travel (was 0.65)
        [SerializeField] private float _ballVisualScale = 0.42f;

        [Header("Paint wave")]
        [SerializeField] private float _riseStagger = 0.025f;  // delay between each cell rising (was 0.04)

        // Consumed by GameManager for win/lose checks
        public event Action<Vector3> OnBallLanded;

        // Number of balls currently in flight. Multiple may overlap so the player
        // can fire back-to-back; GameManager checks this before declaring a loss.
        private int _activeFlights;
        public bool IsBusy => _activeFlights > 0;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void OnEnable()  { if (_slingshot) _slingshot.OnLaunch += Launch; }
        private void OnDisable() { if (_slingshot) _slingshot.OnLaunch -= Launch; }

        // ── Launch entry point ────────────────────────────────────────────
        // Public so alternative inputs (e.g. Gameplay2's tap-to-target controller)
        // can drive a launch directly without going through the slingshot event.
        public void Launch(Vector3 velocity, float strength)
        {
            if (_queue == null || _queue.IsEmpty || velocity == Vector3.zero || _grid == null) return;

            Vector3 origin = _launchOrigin ? _launchOrigin.position : transform.position;

            // Snap the shot to the aimed cell (aim assist) — always inside the grid.
            // Same resolution as AimPreview → preview == reality.
            Vector3 snappedVel = LaunchSolver.SnapToCell(_grid, origin, velocity);
            List<Vector3> arc = TrajectorySimulator.Simulate(
                origin, snappedVel, GameConstants.TrajectorySteps,
                GameConstants.TrajectoryTimeStep, out Vector3 landPos);

            // Safety: bail only if the grid is degenerate and clamping couldn't land us on it.
            if (!_grid.WorldToGrid(landPos, out _, out _))
                return;

            // Consume the ball now so the next shot uses the next ball — the
            // player can fire again without waiting for this one to land.
            var ballData = _queue.Consume();
            if (ballData == null) return;

            _activeFlights++;
            StartCoroutine(DoLaunch(arc, landPos, origin, ballData));
        }

        // ── Arc tween coroutine ───────────────────────────────────────────
        private IEnumerator DoLaunch(List<Vector3> arc, Vector3 landPos, Vector3 origin, BallData ballData)
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
            if (_grid.WorldToGrid(landPos, out int gx, out int gy))
                yield return PaintWave(gx, gy, ballData, landPos);

            // Decrement only now (after the fills land) so an overlapping shot's
            // landing can't declare a premature loss while this wave is still rising.
            _activeFlights = Mathf.Max(0, _activeFlights - 1);
            OnBallLanded?.Invoke(landPos);
        }

        // ── Outward rising wave ───────────────────────────────────────────
        // Fills the matched cells one at a time, nearest-to-the-hit first, so the
        // cubes rise in a ripple. Once the last one is up, fires a big bloom.
        private IEnumerator PaintWave(int gx, int gy, BallData ball, Vector3 landPos)
        {
            var targets = PaintingSystem.PaintTargetsOrdered(_grid, gx, gy, ball);
            if (targets.Count == 0) yield break;

            var wait = _riseStagger > 0f ? new WaitForSeconds(_riseStagger) : null;
            foreach (var c in targets)
            {
                _grid.SetFilled(c.x, c.y, true);
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
