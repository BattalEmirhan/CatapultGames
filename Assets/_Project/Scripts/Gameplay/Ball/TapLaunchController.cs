using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace CatapultGames
{
    // Player input — TAP TO TARGET.
    //
    // The player picks a grid cell directly:
    //   • a quick tap fires the catapult ball at that cell, and
    //   • pressing and sliding the finger moves the target across cells (the arc
    //     follows the finger); releasing fires at the cell under the finger.
    // A gesture that started on a UI element does not fire.
    //
    // Reuses the existing systems: BallLauncher (flight + paint + queue advance),
    // AimPreview (arc + cell highlight), LaunchSolver (velocity to the cell).
    //
    // Wire up in Inspector:
    //   _camera       — the GridCamera
    //   _grid         — GridRenderer
    //   _launcher     — BallLauncher
    //   _aimPreview   — AimPreview (optional; arc preview while holding)
    //   _launchOrigin — Transform the ball launches from (catapult ball pivot)
    //   _gameManager  — GameManager (optional; stops input once the level ends)
    public class TapLaunchController : MonoBehaviour
    {
        [SerializeField] private Camera        _camera;
        [SerializeField] private GridRenderer  _grid;
        [SerializeField] private BallLauncher  _launcher;
        [SerializeField] private AimPreview    _aimPreview;   // optional
        [SerializeField] private Transform     _launchOrigin;
        [SerializeField] private GameManager   _gameManager;  // optional
        [SerializeField] private BallQueue     _queue;        // optional; enables picking
        [SerializeField] private BallQueueView _queueView;    // optional; enables picking

        [Tooltip("Launch angle above horizontal used to solve the arc to the tapped cell.")]
        [SerializeField] [Range(20f, 80f)] private float _launchAngle = 50f;

        [Tooltip("Aim assist: MAX lift of the target above the finger, measured in GRID " +
                 "ROWS, when holding. Quick taps use no lift. 0 disables the lift.")]
        [SerializeField] [Range(0f, 5f)] private float _aimLiftCells = 2f;

        [Tooltip("A press shorter than this (sec) is a quick tap → fires straight under " +
                 "the finger with no lift.")]
        [SerializeField] private float _tapMaxTime = 0.12f;

        [Tooltip("After the tap window, the lift ramps to full over this long (sec) while held.")]
        [SerializeField] private float _aimLiftRampTime = 0.18f;

        // ── State ─────────────────────────────────────────────────────────
        private bool    _pressed;
        private bool    _startedOverUI;  // gesture began on a UI element → ignore
        private bool    _startedOnQueue; // gesture picked a queued ball → not a shot
        private Vector2 _lastPos;        // last pressed pointer pos (release reads this)
        private int     _hoverX = -1, _hoverY = -1;
        private bool    _hasHover;       // finger is / was over a valid cell this gesture
        private float   _pressStartTime; // when the current press began (for tap-vs-hold)
        private float   _currentLift;    // lift applied this frame, in grid rows (0 on a fresh tap)

        // ── Update ────────────────────────────────────────────────────────
        private void Update()
        {
            if (_gameManager != null && _gameManager.IsOver)
            {
                if (_pressed) { _pressed = false; ClearHover(); }
                return;
            }

            GetPointerState(out bool pressing, out Vector2 pos);

            if (pressing)
            {
                if (!_pressed)
                {
                    _pressed        = true;
                    _hasHover       = false;
                    _startedOverUI  = IsPointerOverUI();
                    _pressStartTime = Time.time;
                    _currentLift    = 0f;

                    // A press that lands on one of the next queued balls picks it
                    // instead of aiming. Resolved once, at press time, so sliding on
                    // from there can't turn a pick into a shot.
                    _startedOnQueue = !_startedOverUI && TrySelectQueueBall(pos);
                }

                _lastPos = pos;

                if (!_startedOverUI && !_startedOnQueue)
                {
                    // Quick tap → aim straight under the finger (no lift). Hold → the
                    // lift ramps in after the tap window, raising the target above the
                    // finger so it isn't hidden. _currentLift (in grid rows) drives
                    // preview AND launch. SmoothStep eases the tap→hold transition.
                    float held = Time.time - _pressStartTime;
                    float ramp = Mathf.InverseLerp(_tapMaxTime, _tapMaxTime + _aimLiftRampTime, held);
                    _currentLift = _aimLiftCells * Mathf.SmoothStep(0f, 1f, ramp);

                    // While held, the target follows the finger; the arc previews it.
                    if (TryGetCell(pos, _currentLift, out int gx, out int gy))
                    {
                        _hoverX = gx; _hoverY = gy; _hasHover = true;
                        _aimPreview?.ShowArc(VelocityTo(gx, gy));
                    }
                    else
                    {
                        _aimPreview?.Hide();   // finger off the grid right now — no target
                    }
                }
            }
            else if (_pressed)
            {
                _pressed = false;

                // Fire at the last previewed target (same lift), so what you saw fires.
                if (!_startedOverUI && !_startedOnQueue)
                {
                    if (TryGetCell(_lastPos, _currentLift, out int gx, out int gy))
                        LaunchAt(gx, gy);
                    else if (_hasHover)
                        LaunchAt(_hoverX, _hoverY);
                }

                ClearHover();
            }
        }

        // ── Queue picking ─────────────────────────────────────────────────
        // Pull one of the next queued balls to the front. Selection is modelled as
        // reordering the queue, so nothing else in the shot chain has to know it
        // happened: AimPreview redraws off BallQueue.OnChanged (including the
        // board's active-colour marking) and BallLauncher still just fires Current.
        private bool TrySelectQueueBall(Vector2 screenPos)
        {
            if (_queueView == null || _queue == null) return false;

            var cam = _camera != null ? _camera : Camera.main;
            if (!_queueView.TryPickSlot(screenPos, cam, out int offset)) return false;
            if (!_queue.SelectSlot(offset)) return false;

            Haptics.Light();
            _aimPreview?.Hide();
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private bool TryGetCell(Vector2 screenPos, float liftCells, out int gx, out int gy)
        {
            gx = gy = -1;
            var cam = _camera != null ? _camera : Camera.main;
            if (cam == null || _grid == null) return false;

            // Lift the aim above the finger by this many grid ROWS so the fingertip
            // doesn't cover the target (0 = straight under the finger). Converting to
            // an on-screen offset keeps the lift exactly N cells at any zoom/screen.
            if (liftCells > 0f)
                screenPos.y += CellsToScreenLift(cam, screenPos, liftCells);

            // Clamp aim to the grid — aiming past an edge (incl. the lift overshooting
            // the top rows) snaps to the nearest cell, so you can't fire off the board.
            return _grid.RaycastToGridClamped(PickRay(cam, screenPos), out gx, out gy);
        }

        // Screen→world ray for aim picking, with camera shake cancelled out. Shake only
        // TRANSLATES the camera (no rotation), so a screen point's ray keeps its direction
        // and just gains the shake offset on its origin — subtract it and the aim stays
        // rock-steady on the grid while the view still shakes. See GameFX.ShakeOffset.
        private static Ray PickRay(Camera cam, Vector2 screenPos)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            ray.origin -= GameFX.CurrentShakeOffset;
            return ray;
        }

        // Screen-space Y offset (pixels) that corresponds to moving `cells` grid rows
        // "up the board" from the point currently under the finger. Used so the aim
        // lift is measured in real cells rather than a flat fraction of screen height.
        private float CellsToScreenLift(Camera cam, Vector2 screenPos, float cells)
        {
            var plane = new Plane(Vector3.up, _grid.transform.position);
            Ray ray = PickRay(cam, screenPos);
            if (!plane.Raycast(ray, out float dist)) return 0f;

            Vector3 p0 = ray.GetPoint(dist);
            // Rows increase along the grid's local +Z (see GridRenderer.GridToWorld).
            Vector3 up = _grid.transform.TransformDirection(Vector3.forward);
            Vector3 p1 = p0 + up * (cells * _grid.CellSize);

            return cam.WorldToScreenPoint(p1).y - cam.WorldToScreenPoint(p0).y;
        }

        private Vector3 VelocityTo(int gx, int gy)
        {
            Vector3 origin = _launchOrigin ? _launchOrigin.position : transform.position;
            return LaunchSolver.SolveToCell(_grid, origin, gx, gy, _launchAngle);
        }

        private void LaunchAt(int gx, int gy)
        {
            Vector3 velocity = VelocityTo(gx, gy);
            if (velocity == Vector3.zero || _launcher == null) return;
            _launcher.Launch(velocity);
            Telemetry.RecordLaunch();
        }

        private void ClearHover()
        {
            _hasHover = false;
            _hoverX = _hoverY = -1;
            _aimPreview?.Hide();
        }

        private static void GetPointerState(out bool pressing, out Vector2 pos)
        {
            var touch = Touchscreen.current?.primaryTouch;
            if (touch != null && touch.press.isPressed)
            {
                pressing = true;
                pos      = touch.position.ReadValue();
                return;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                pressing = true;
                pos      = mouse.position.ReadValue();
                return;
            }

            pressing = false;
            pos      = Vector2.zero;
        }

        private static bool IsPointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
