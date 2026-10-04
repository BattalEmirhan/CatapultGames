using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace CatapultGames
{
    // Player input — ONE gesture, no aiming skill:
    //   • tap a tray ball  → it becomes the selected ball (no shot)
    //   • tap a grid cell  → the selected ball flies there
    //   • press and slide  → the footprint + arc preview follows the finger;
    //                        release fires at the cell under the finger
    //   • slide back onto the tray and release → cancel, nothing fires
    // A gesture that starts on a UI element does nothing.
    //
    // There is no lift / hold ramp any more: the stamp is centred on the tapped
    // cell (GameConstants.GetPaintOffset) and the preview ghost is the whole
    // stamp, so what the finger covers is never the only thing on screen.
    //
    // Wire up in Inspector:
    //   gameCamera, grid, launcher, aimPreview (optional), gameManager (optional),
    //   queue + queueView (tray picking + launch origin), launchOrigin (fallback)
    public sealed class TapLaunchController : MonoBehaviour
    {
        private Vector3 Origin =>
            queueView != null ? queueView.CurrentLaunchOrigin
                               : (launchOrigin ? launchOrigin.position : transform.position);

        [SerializeField] private Camera        gameCamera;
        [SerializeField] private GridRenderer  grid;
        [SerializeField] private BallLauncher  launcher;
        [SerializeField] private AimPreview    aimPreview;   // optional
        [SerializeField] private Transform     launchOrigin; // fallback when there is no tray
        [SerializeField] private GameManager   gameManager;  // optional
        [SerializeField] private BallQueue     queue;        // optional; enables picking
        [SerializeField] private BallQueueView queueView;    // optional; enables picking

        [Tooltip("Launch angle above horizontal used to solve the arc to the tapped cell.")]
        [SerializeField] [Range(20f, 80f)] private float launchAngle = 50f;

        private bool    _pressed;
        private bool    _startedOverUI;   // gesture began on a UI element → ignore
        private bool    _startedOnTray;   // gesture picked a tray ball → not a shot
        private bool    _hasTarget;
        private int     _tx, _ty;
        private Vector2 _lastPos;

        private void Update()
        {
            if (gameManager != null && gameManager.IsOver)
            {
                if (_pressed)
                {
                    _pressed = false;
                    ClearTarget();
                }
                return;
            }

            GetPointerState(out bool pressing, out Vector2 pos);

            if (pressing)
            {
                if (!_pressed)
                {
                    _pressed       = true;
                    _hasTarget     = false;
                    _startedOverUI = IsPointerOverUI();
                    // Resolved once, at press time, so sliding on from a tray ball
                    // can never turn a pick into a shot.
                    _startedOnTray = !_startedOverUI && TrySelectTrayBall(pos);
                }
                _lastPos = pos;

                if (!_startedOverUI && !_startedOnTray)
                {
                    if (TryGetCell(pos, out int gx, out int gy))
                    {
                        _tx = gx; _ty = gy; _hasTarget = true;
                        aimPreview?.ShowArc(Origin, VelocityTo(gx, gy));
                    }
                    else
                        aimPreview?.Hide();
                }
            }
            else if (_pressed)
            {
                _pressed = false;
                if (!_startedOverUI && !_startedOnTray && _hasTarget)
                {
                    var cam = gameCamera != null ? gameCamera : Camera.main;
                    bool cancelled = queueView != null && queueView.IsOverTray(_lastPos, cam);
                    if (!cancelled)
                        LaunchAt(_tx, _ty);
                }
                ClearTarget();
            }
        }

        private bool TrySelectTrayBall(Vector2 screenPos)
        {
            if (queueView == null || queue == null)
                return false;
            var cam = gameCamera != null ? gameCamera : Camera.main;
            if (!queueView.TryPickSlot(screenPos, cam, out int slot))
                return false;
            if (!queueView.Select(slot))
                return false;
            Haptics.Light();
            aimPreview?.Hide();
            return true;
        }

        private bool TryGetCell(Vector2 screenPos, out int gx, out int gy)
        {
            gx = gy = -1;
            var cam = gameCamera != null ? gameCamera : Camera.main;
            if (cam == null || grid == null)
                return false;
            // Clamped: aiming past an edge snaps to the nearest cell, so a shot
            // can never leave the board.
            return grid.RaycastToGridClamped(PickRay(cam, screenPos), out gx, out gy);
        }

        // Screen→world ray with camera shake cancelled out. Shake only TRANSLATES
        // the camera, so subtracting its offset from the ray origin keeps the aim
        // steady while the view still shakes. See GameFX.CurrentShakeOffset.
        private static Ray PickRay(Camera cam, Vector2 screenPos)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);
            ray.origin -= GameFX.CurrentShakeOffset;
            return ray;
        }

        private Vector3 VelocityTo(int gx, int gy) =>
            LaunchSolver.SolveToCell(grid, Origin, gx, gy, launchAngle);

        private void LaunchAt(int gx, int gy)
        {
            Vector3 origin   = Origin;
            Vector3 velocity = LaunchSolver.SolveToCell(grid, origin, gx, gy, launchAngle);
            if (velocity == Vector3.zero || launcher == null)
                return;
            launcher.Launch(origin, velocity);
            Telemetry.RecordLaunch();
        }

        private void ClearTarget()
        {
            _hasTarget = false;
            _tx = _ty = -1;
            aimPreview?.Hide();
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
