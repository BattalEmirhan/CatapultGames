using UnityEngine;

namespace CatapultGames
{
    // Rubber-band stretch visual for the slingshot.
    //
    // Attach to the catapult root.  Wire up in Inspector:
    //   _controller  — the SlingshotController
    //   _anchorLeft  — left fork tip Transform
    //   _anchorRight — right fork tip Transform
    //   _ballPivot   — the ball Transform (moves with drag)
    //
    // A LineRenderer draws: anchorLeft → ballPivot → anchorRight
    [RequireComponent(typeof(LineRenderer))]
    public class SlingshotVisual : MonoBehaviour
    {
        [SerializeField] private SlingshotController _controller;
        [SerializeField] private Transform           _anchorLeft;
        [SerializeField] private Transform           _anchorRight;
        [SerializeField] private Transform           _ballPivot;   // ball moves here while dragging

        [Header("Stretch")]
        [Tooltip("How far the ball moves in world units at full drag.")]
        [SerializeField] private float _maxStretchWorld = 1.2f;

        private LineRenderer _lr;
        private Vector3      _ballRestLocal;   // original local position of ball

        // ── Lifecycle ────────────────────────────────────────────────────
        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.positionCount = 3;
            _lr.useWorldSpace = true;
            _lr.enabled       = false;

            if (_ballPivot)
                _ballRestLocal = _ballPivot.localPosition;
        }

        // ── Update ───────────────────────────────────────────────────────
        private void LateUpdate()
        {
            if (_controller == null) return;

            if (_controller.IsDragging)
            {
                UpdateStretch();
            }
            else
            {
                ResetVisual();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────
        private void UpdateStretch()
        {
            Vector2 delta = _controller.DragDelta;

            // Convert screen-pixel delta to a local world offset
            // (normalise by screen height so it's resolution-independent)
            float h  = Screen.height > 0 ? Screen.height : 1;
            float nx = delta.x / h;               // normalise aspect-neutral
            float ny = delta.y / h;

            Vector3 localOffset = new Vector3(nx, -ny, 0f) * _maxStretchWorld;

            if (_ballPivot)
                _ballPivot.localPosition = _ballRestLocal + localOffset;

            // Draw rubber band
            if (_anchorLeft && _anchorRight && _ballPivot)
            {
                _lr.enabled = true;
                _lr.SetPosition(0, _anchorLeft.position);
                _lr.SetPosition(1, _ballPivot.position);
                _lr.SetPosition(2, _anchorRight.position);
            }
        }

        private void ResetVisual()
        {
            _lr.enabled = false;
            if (_ballPivot)
                _ballPivot.localPosition = _ballRestLocal;
        }
    }
}
