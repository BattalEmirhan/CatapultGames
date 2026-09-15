using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // S-shaped conveyor visual for the ball queue.
    //
    // _catapultPivot: the catapult's BallPivot transform.
    //   Index-0 ball is parented here so it rides the catapult.
    // _waypoints[0]: world position to which the next ball slides when index-0 is consumed.
    //   Set it to the same world position as _catapultPivot so the slide looks right.
    // _waypoints[1..N]: the S-curve queue positions behind the catapult.
    public class BallQueueView : MonoBehaviour
    {
        [SerializeField] private BallQueue   _queue;
        [SerializeField] private Transform   _catapultPivot;   // BallPivot on the catapult
        [SerializeField] private Transform[] _waypoints;       // [0]=catapult rest pos, [1..N]=queue
        [SerializeField] private float       _advanceDuration = 0.12f;

        [Header("Scales")]
        [SerializeField] private float _currentBallScale = 0.70f;  // ball at catapult — bigger
        [SerializeField] private float _queueBallScale   = 0.42f;  // queue balls

        [Header("Selection")]
        [Tooltip("How many balls the player may choose between, counting the one on " +
                 "the catapult. 1 disables choosing.")]
        [SerializeField] private int _selectableSlots = 3;

        [Tooltip("Selectable queue balls are drawn slightly larger, so the choosable " +
                 "few read differently from the rest of the queue.")]
        [SerializeField] private float _selectableBallScale = 0.52f;

        [Tooltip("Tap radius around a selectable ball, as a fraction of screen height.")]
        [SerializeField] private float _pickRadiusScreenFraction = 0.055f;

        private readonly List<GameObject> _ballGos = new();
        private Coroutine _anim;
        private bool      _dirty;   // queue changed → view needs reconciling

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void OnEnable()
        {
            if (_queue == null) return;
            _queue.OnBallConsumed += OnConsumed;
            _queue.OnChanged      += OnQueueChanged;
            _queue.OnColorCleared += OnColorCleared;

            // Coroutines are killed when this component is disabled, so a slide could
            // have died mid-flight leaving _anim dangling. Reset it and force a
            // reconcile on (re)enable so the view always matches the queue.
            _anim  = null;
            _dirty = true;
        }

        private void OnDisable()
        {
            if (_queue == null) return;
            _queue.OnBallConsumed -= OnConsumed;
            _queue.OnChanged      -= OnQueueChanged;
            _queue.OnColorCleared -= OnColorCleared;
        }

        private void Start()
        {
            _dirty = false;
            Rebuild();
        }

        // Reconcile the view to the queue whenever it changed, and self-heal if the
        // catapult slot is empty while a ball is still queued. This makes the view
        // robust against interrupted slides / events missed during an animation —
        // the failure that previously left the catapult with no visible ball.
        private void LateUpdate()
        {
            if (_anim != null) return;   // let an in-progress slide finish first

            bool catapultMissing = _queue != null && !_queue.IsEmpty &&
                                   (_ballGos.Count == 0 || _ballGos[0] == null);
            if (_dirty || catapultMissing)
            {
                _dirty = false;
                Rebuild();
            }
        }

        // ── Queue events ──────────────────────────────────────────────────
        // Just flag the change; LateUpdate reconciles once any slide has finished.
        private void OnQueueChanged() => _dirty = true;

        private void OnConsumed(BallData _)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(SlideAndRebuild());
        }

        // A colour finished — rocket its leftover queue balls up and pop them as
        // fireworks. Fires BEFORE OnChanged (see BallQueue.RemoveColor), so the
        // to-be-removed visuals are still on screen here. We hand each one off to its
        // own self-destroying coroutine and null its slot so the upcoming Rebuild
        // doesn't just silently destroy it.
        private void OnColorCleared(CellColor color, int removed)
        {
            int popped = 0;
            for (int i = 0; i < _ballGos.Count; i++)
            {
                var go = _ballGos[i];
                if (go == null) continue;

                var bv = go.GetComponent<BallVisual>();
                if (bv == null || bv.BallColor != color) continue;

                bv.PlayFireworkAndDestroy(popped * 0.08f);   // staggered volley
                _ballGos[i] = null;                          // ownership handed off
                popped++;
            }

            // Leftovers queued beyond the visible waypoints have no on-screen visual.
            // Spawn a quick stand-in for each so the WHOLE batch pops, not just the
            // visible part. They launch from the back of the queue, continuing the
            // same stagger so the volley reads as one stream.
            Vector3 spawnPos = BackOfQueuePosition();
            for (int i = popped; i < removed; i++)
            {
                var bv = BallVisual.Create(transform, color, 1, BallShape.Square, _queueBallScale);
                bv.transform.position = spawnPos;
                bv.PlayFireworkAndDestroy(i * 0.08f);
            }
        }

        // Position of the rearmost wired queue waypoint (fallback: this transform).
        // Hidden-leftover fireworks launch from here.
        private Vector3 BackOfQueuePosition()
        {
            if (_waypoints != null)
                for (int i = _waypoints.Length - 1; i >= 0; i--)
                    if (_waypoints[i]) return _waypoints[i].position;
            return transform.position;
        }

        // ── Animation ─────────────────────────────────────────────────────
        private IEnumerator SlideAndRebuild()
        {
            // Index-0 ball is consumed — BallLauncher now owns the flying copy.
            // Destroy the catapult visual and clear the slot.
            if (_ballGos.Count > 0 && _ballGos[0])
            {
                Destroy(_ballGos[0]);
                _ballGos[0] = null;
            }

            // Slide remaining balls one slot forward
            if (_ballGos.Count > 1 && _waypoints != null && _waypoints.Length > 1)
            {
                var starts = new Vector3[_ballGos.Count];
                for (int i = 1; i < _ballGos.Count; i++)
                    starts[i] = _ballGos[i] ? _ballGos[i].transform.position : Vector3.zero;

                float elapsed = 0f;
                while (elapsed < _advanceDuration)
                {
                    float t = elapsed / _advanceDuration;
                    for (int i = 1; i < _ballGos.Count; i++)
                    {
                        if (!_ballGos[i]) continue;
                        int target = i - 1;
                        Vector3 dest = target == 0 && _catapultPivot
                            ? _catapultPivot.position
                            : (_waypoints != null && target < _waypoints.Length && _waypoints[target]
                               ? _waypoints[target].position : Vector3.zero);
                        _ballGos[i].transform.position = Vector3.Lerp(starts[i], dest, t);
                    }
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            Rebuild();
            _anim  = null;
            _dirty = false;
        }

        // ── Rebuild ───────────────────────────────────────────────────────
        private void Rebuild()
        {
            foreach (var go in _ballGos)
                if (go)
                    Destroy(go);
            _ballGos.Clear();

            if (_queue == null) return;

            // Index 0: current ball — parented to catapult pivot so it follows drag.
            // Fallback: if pivot not wired, place at waypoints[0] world position.
            {
                var ball = _queue.Peek(0);
                if (ball != null)
                {
                    if (_catapultPivot)
                    {
                        var bv = BallVisual.Create(_catapultPivot, ball.color, ball.powerLevel, ball.shape, _currentBallScale);
                        bv.transform.localPosition = Vector3.zero;
                        _ballGos.Add(bv.gameObject);
                    }
                    else
                    {
                        var bv = BallVisual.Create(transform, ball.color, ball.powerLevel, ball.shape, _currentBallScale);
                        if (_waypoints != null && _waypoints.Length > 0 && _waypoints[0])
                            bv.transform.position = _waypoints[0].position;
                        _ballGos.Add(bv.gameObject);
                    }
                }
                else
                {
                    _ballGos.Add(null);
                }
            }

            // Index 1+: queue balls along waypoints
            int count = _waypoints != null
                ? Mathf.Min(Mathf.Max(0, _queue.Remaining - 1), _waypoints.Length - 1)
                : 0;

            for (int i = 0; i < count; i++)
            {
                int qi = i + 1;
                int wi = i + 1;
                var ball = _queue.Peek(qi);
                if (ball == null || _waypoints == null || wi >= _waypoints.Length || !_waypoints[wi])
                {
                    _ballGos.Add(null);
                    continue;
                }
                float scale = qi < _selectableSlots ? _selectableBallScale : _queueBallScale;
                var bv2 = BallVisual.Create(transform, ball.color, ball.powerLevel, ball.shape, scale);
                bv2.transform.position = _waypoints[wi].position;
                _ballGos.Add(bv2.gameObject);
            }
        }

        // ── Selection picking ─────────────────────────────────────────────
        // Which selectable queue ball, if any, is under a screen point.
        //
        // Screen-space distance rather than a raycast: the game has no colliders by
        // design (no physics anywhere), and a tap radius is a better touch target
        // than the ball's silhouette anyway. Camera shake needs no correction here —
        // WorldToScreenPoint uses the shaken camera, which is exactly what is drawn.
        //
        // Slot 0 (the catapult ball) is excluded: it is already the current ball.
        public bool TryPickSlot(Vector2 screenPos, Camera cam, out int offset)
        {
            offset = -1;
            if (cam == null || _queue == null) return false;

            float radius  = Screen.height * Mathf.Max(0.01f, _pickRadiusScreenFraction);
            float bestSqr = radius * radius;

            int limit = Mathf.Min(_selectableSlots, _ballGos.Count);
            for (int i = 1; i < limit; i++)
            {
                var go = _ballGos[i];
                if (go == null) continue;

                Vector3 sp = cam.WorldToScreenPoint(go.transform.position);
                if (sp.z <= 0f) continue;   // behind the camera

                float sqr = ((Vector2)sp - screenPos).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; offset = i; }
            }

            return offset > 0;
        }
    }
}
