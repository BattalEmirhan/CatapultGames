using UnityEngine;
using UnityEngine.InputSystem;

namespace CatapultGames
{
    // Task 12 — Slingshot input controller.
    //
    // Reads touch/mouse input from the bottom panel (input zone).
    // Aim is a CUMULATIVE, re-anchored value: each frame's finger movement is added
    // to an aim vector that is clamped to _maxDragPixels. Because over-pulling isn't
    // "banked", reversing the drag adjusts the aim immediately (no long swipe to undo),
    // and where you first touch / hitting the screen edge doesn't matter.
    // The aim is INVERTED so dragging away from the grid launches toward it.
    // Fires OnLaunch with a world-space velocity vector; other systems (Task 14)
    // consume it to animate the ball and (Task 13) to preview the arc.
    public class SlingshotController : MonoBehaviour
    {
        [Header("Drag thresholds (screen pixels)")]
        [SerializeField] private float _maxDragPixels = 130f;
        [SerializeField] private float _minDragPixels = 14f;

        [Header("Launch")]
        [Tooltip("Speed of ball at full drag.")]
        [SerializeField] private float _maxLaunchSpeed = 20f;
        [Tooltip("Upward launch angle in degrees above horizontal.")]
        [SerializeField] [Range(20f, 80f)] private float _launchAngle = 52f;

        [Header("Input zone")]
        [Tooltip("Bottom fraction of screen that accepts drag.  0.42 = bottom 42%.")]
        [SerializeField] [Range(0.1f, 0.9f)] private float _inputZoneHeight = 0.42f;

        [Header("Feel")]
        [Tooltip("Aim smoothing. Higher = snappier, lower = smoother / less twitchy. 0 = off.")]
        [SerializeField] [Range(0f, 30f)] private float _aimSmoothing = 1f;

        [Tooltip("Ignore a sudden one-frame pointer jump bigger than this (pixels) when it's " +
                 "far larger than recent movement — that's the finger smearing as it lifts off, " +
                 "which would otherwise slip the aim right as you release.")]
        [SerializeField] private float _releaseJitterPixels = 35f;
        [Header("Comfort")]
        [Tooltip("If true, a quick tap (no drag) will fire a short-strength shot.")]
        [SerializeField] private bool _tapToLaunch = true;

        // ── State ─────────────────────────────────────────────────────────
        private bool    _dragging;
        private Vector2 _aim;         // accumulated aim (px), re-anchored to the finger and CLAMPED
        private Vector2 _aimSmoothed; // low-passed _aim — the value actually used to launch
        private Vector2 _lastPos;     // last raw pointer pos, to measure per-frame movement
        private float   _avgStep;     // running average step size (px) of real movement

        // ── Public read-only output ────────────────────────────────────────
        public bool    IsDragging   => _dragging;
        public Vector2 DragDelta    => _aimSmoothed;

        // 0–1 based on how far the player has dragged
        public float DragStrength =>
            Mathf.Clamp01(DragDelta.magnitude / _maxDragPixels);

        // World-space velocity to apply at launch.
        // Returns zero if not dragging or drag is below minimum.
        public Vector3 LaunchVelocity
        {
            get
            {
                if (!_dragging || DragDelta.magnitude < _minDragPixels)
                    return Vector3.zero;

                // Invert: drag away from grid → launch toward grid
                Vector2 invFlat = -DragDelta.normalized;

                // Screen X  → world X
                // Screen Y  → world Z (because camera is tilted at 45°,
                //             moving up on screen == moving deeper into grid)
                Vector3 worldFlat = new Vector3(invFlat.x, 0f, invFlat.y);

                // Tilt upward by launchAngle
                float rad = _launchAngle * Mathf.Deg2Rad;
                Vector3 dir = (worldFlat * Mathf.Cos(rad)
                              + Vector3.up * Mathf.Sin(rad)).normalized;

                return dir * (DragStrength * _maxLaunchSpeed);
            }
        }

        // ── Events ────────────────────────────────────────────────────────

        // Fires every frame while dragging (gives current drag delta for aim preview).
        public event System.Action<Vector2> OnDragUpdated;

        // Fires once on release when drag meets minimum threshold.
        // args: world-space velocity, drag strength 0–1
        public event System.Action<Vector3, float> OnLaunch;

        // ── Update ────────────────────────────────────────────────────────
        private void Update()
        {
            GetPointerState(out bool pressing, out Vector2 pos);

            if (pressing && !_dragging && InInputZone(pos))
            {
                _dragging    = true;
                _aim         = Vector2.zero;
                _aimSmoothed = Vector2.zero;
                _lastPos     = pos;
                _avgStep     = 0f;
            }
            else if (pressing && _dragging)
            {
                Vector2 frameStep = pos - _lastPos;
                _lastPos = pos;
                float stepMag = frameStep.magnitude;

                // A lone jump that dwarfs recent movement is the finger smearing as it
                // lifts off the glass — not a real aim change. Skip that frame so the
                // shot fires from the aim you actually held. Sustained fast drags pass
                // because their running-average step is already large.
                bool smear = stepMag > _releaseJitterPixels && stepMag > _avgStep * 2.5f + 6f;
                if (!smear)
                {
                    // Re-anchoring / cumulative aim: accumulate finger movement and CLAMP
                    // to the max. Over-pulling is NOT banked, so reversing adjusts the aim
                    // immediately — no long swipe to undo a pull, and finger start point /
                    // screen edge don't matter (you never run out of room).
                    _aim += frameStep;
                    if (_aim.magnitude > _maxDragPixels)
                        _aim = _aim.normalized * _maxDragPixels;

                    _avgStep = Mathf.Lerp(_avgStep, stepMag, 0.4f);   // learn only from real movement
                }

                // Low-pass the aim so it isn't twitchy.
                _aimSmoothed = _aimSmoothing > 0f
                    ? Vector2.Lerp(_aimSmoothed, _aim, 1f - Mathf.Exp(-_aimSmoothing * Time.deltaTime))
                    : _aim;

                OnDragUpdated?.Invoke(DragDelta);
            }
            else if (!pressing && _dragging)
            {
                bool valid = DragDelta.magnitude >= _minDragPixels;
                bool isTap = DragDelta.magnitude < _minDragPixels && _tapToLaunch;
                if (valid)
                {
                    OnLaunch?.Invoke(LaunchVelocity, DragStrength);
                    Telemetry.RecordLaunch();
                }
                else if (isTap)
                {
                    // Short quick tap: fire a small-strength shot in camera-forward
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        Vector3 dir = (cam.transform.forward + Vector3.up * 0.2f).normalized;
                        OnLaunch?.Invoke(dir * (_maxLaunchSpeed * 0.45f), 0.45f);
                        Telemetry.RecordLaunch();
                    }
                }

                _dragging    = false;
                _aim         = Vector2.zero;
                _aimSmoothed = Vector2.zero;
                OnDragUpdated?.Invoke(Vector2.zero);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private static void GetPointerState(out bool pressing, out Vector2 pos)
        {
            // Touch takes priority on mobile; fall back to mouse in editor/desktop
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

        private bool InInputZone(Vector2 screenPos) =>
            Screen.height > 0 && screenPos.y / Screen.height <= _inputZoneHeight;
    }
}
