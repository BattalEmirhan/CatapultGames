using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CatapultGames
{
    // The ball TRAY: three slots at the bottom of the screen, like the piece tray
    // of a block puzzle. The player taps a tray ball to select it, taps a cell to
    // throw it; the emptied slot refills from the queue with a pop.
    //
    // The queue itself (BallQueue) still models "current = offset 0", and picking
    // a tray ball is still BallQueue.SelectSlot (a reorder). What this view adds
    // is a STABLE mapping from tray slot → queue offset, so the two balls the
    // player did not pick stay where they are instead of shuffling every shot.
    //
    // Wire up in Inspector:
    //   _queue          — BallQueue
    //   _slots          — tray slot transforms, left → right (3)
    //   _remainingLabel — optional UI text showing "+N" balls beyond the tray
    public class BallQueueView : MonoBehaviour
    {
        [SerializeField] private BallQueue   _queue;
        [SerializeField] private Transform[] _slots;
        [SerializeField] private TMP_Text    _remainingLabel;   // optional

        [Header("Look")]
        [SerializeField] private float _slotScale      = 0.85f;
        [SerializeField] private float _selectedScale  = 1.10f;
        [SerializeField] private float _selectedLift   = 0.30f;
        [SerializeField] private float _refillDuration = 0.18f;

        [Tooltip("Tap radius around a tray ball, as a fraction of screen height.")]
        [SerializeField] private float _pickRadiusScreenFraction = 0.07f;

        private int[]        _slotOffset;   // queue offset shown in each slot, -1 = empty
        private BallData[]   _slotData;     // which BallData instance the slot's visual shows
        private BallVisual[] _slotBalls;
        private Coroutine[]  _slotAnims;
        private int          _selectedSlot = -1;

        // Set around our own queue calls (SelectSlot / Consume) so the resulting
        // OnChanged does not trigger a full rebuild that would reshuffle the tray.
        private bool _expectQueueChange;
        private bool _consumePending;

        private Transform _ring;
        private Material  _ringMat;

        // Number of tray slots — the player's freedom of choice. PlayoutBoard
        // mirrors this as SelectableSlots; keep them equal.
        public int SlotCount => _slots?.Length ?? 0;

        // Where the selected ball sits — the arc starts here (BallLauncher /
        // AimPreview / TapLaunchController all ask, so the shot leaves the tray).
        public Vector3 CurrentLaunchOrigin
        {
            get
            {
                if (_selectedSlot >= 0 && _selectedSlot < SlotCount && _slots[_selectedSlot])
                    return _slots[_selectedSlot].position + Vector3.up * _selectedLift;
                return transform.position;
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Awake()
        {
            int n = SlotCount;
            _slotOffset = new int[n];
            _slotData   = new BallData[n];
            _slotBalls  = new BallVisual[n];
            _slotAnims  = new Coroutine[n];
            for (int i = 0; i < n; i++) _slotOffset[i] = -1;
            BuildRing();
        }

        private void OnEnable()
        {
            if (_queue == null) return;
            _queue.OnBallConsumed += OnConsumed;
            _queue.OnChanged      += OnQueueChanged;
            _queue.OnColorCleared += OnColorCleared;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_queue == null) return;
            _queue.OnBallConsumed -= OnConsumed;
            _queue.OnChanged      -= OnQueueChanged;
            _queue.OnColorCleared -= OnColorCleared;
        }

        private void OnDestroy()
        {
            if (_ringMat) Destroy(_ringMat);
        }

        // Self-heal: if the selected slot has no ball while balls remain, the
        // mapping drifted (an event missed mid-animation) — rebuild it.
        private void LateUpdate()
        {
            if (_queue == null || _queue.IsEmpty) return;
            if (_selectedSlot < 0 || _slotOffset[_selectedSlot] != 0 || _slotBalls[_selectedSlot] == null)
                Rebuild();
        }

        // ── Queue events ──────────────────────────────────────────────────
        private void OnQueueChanged()
        {
            if (_expectQueueChange) return;          // our own SelectSlot — mapping already updated
            if (_consumePending) { _consumePending = false; AfterConsume(); return; }
            Rebuild();                                // Load / Restore / purge — start fresh
        }

        // Fired inside Consume(), before the cursor advances: the selected ball is
        // leaving (BallLauncher now owns the flying copy).
        private void OnConsumed(BallData _)
        {
            if (_selectedSlot >= 0 && _selectedSlot < SlotCount)
            {
                if (_slotBalls[_selectedSlot]) Destroy(_slotBalls[_selectedSlot].gameObject);
                _slotBalls[_selectedSlot] = null;
                _slotData[_selectedSlot]  = null;
            }
            _consumePending = true;
        }

        // Offsets shift down by one; the slot that held offset 0 empties and takes
        // the lowest offset no other slot shows. Whichever slot now holds offset 0
        // is the selection — the ring simply moves to it.
        private void AfterConsume()
        {
            int n = SlotCount;
            for (int i = 0; i < n; i++)
            {
                if (_slotOffset[i] == 0)     _slotOffset[i] = -1;
                else if (_slotOffset[i] > 0) _slotOffset[i]--;
            }
            for (int i = 0; i < n; i++)
                if (_slotOffset[i] < 0) _slotOffset[i] = NextFreeOffset();
            _selectedSlot = SlotWithOffset(0);
            RefreshLook();
        }

        private int NextFreeOffset()
        {
            for (int q = 0; q < _queue.Remaining; q++)
                if (SlotWithOffset(q) < 0) return q;
            return -1;
        }

        private int SlotWithOffset(int q)
        {
            for (int i = 0; i < SlotCount; i++) if (_slotOffset[i] == q) return i;
            return -1;
        }

        // A colour finished — rocket its leftover tray balls up and pop them as
        // fireworks. Fires BEFORE OnChanged (see BallQueue.RemoveColor), so the
        // visuals are still here; ownership is handed to each ball's own routine.
        private void OnColorCleared(CellColor color, int removed)
        {
            int popped = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                var bv = _slotBalls[i];
                if (bv == null || bv.BallColor != color) continue;
                bv.PlayFireworkAndDestroy(popped * 0.08f);
                _slotBalls[i] = null;
                _slotData[i]  = null;
                popped++;
            }

            // Balls queued beyond the tray have no visual; spawn stand-ins so the
            // whole batch pops as one volley, from the tray's far end.
            Vector3 spawnPos = SlotCount > 0 && _slots[SlotCount - 1] ? _slots[SlotCount - 1].position : transform.position;
            for (int i = popped; i < removed; i++)
            {
                var bv = BallVisual.Create(transform, color, 1, BallShape.Square, _slotScale);
                bv.transform.position = spawnPos;
                bv.PlayFireworkAndDestroy(i * 0.08f);
            }
        }

        // ── Selection ─────────────────────────────────────────────────────
        // Which tray slot, if any, is under a screen point. Screen-space distance
        // rather than a raycast: there are no colliders anywhere by design, and a
        // tap radius is a better touch target than a ball's silhouette anyway.
        public bool TryPickSlot(Vector2 screenPos, Camera cam, out int slot)
        {
            slot = -1;
            if (cam == null || _slots == null) return false;

            float radius  = Screen.height * Mathf.Max(0.01f, _pickRadiusScreenFraction);
            float bestSqr = radius * radius;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!_slots[i] || _slotBalls[i] == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(_slots[i].position);
                if (sp.z <= 0f) continue;
                float sqr = ((Vector2)sp - screenPos).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; slot = i; }
            }
            return slot >= 0;
        }

        // True when a screen point is over the tray band — used to cancel an aim
        // by dragging the finger back down onto the tray.
        public bool IsOverTray(Vector2 screenPos, Camera cam)
        {
            if (cam == null || SlotCount == 0) return false;
            float radius = Screen.height * Mathf.Max(0.01f, _pickRadiusScreenFraction) * 1.4f;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!_slots[i]) continue;
                Vector3 sp = cam.WorldToScreenPoint(_slots[i].position);
                if (sp.z > 0f && Mathf.Abs(sp.y - screenPos.y) < radius) return true;
            }
            return false;
        }

        // Make a tray slot the current ball. Reorders the queue underneath and
        // fixes up the other slots' offsets so they keep showing the same balls.
        public bool Select(int slot)
        {
            if (_queue == null || slot < 0 || slot >= SlotCount) return false;
            int q = _slotOffset[slot];
            if (q < 0) return false;
            if (q == 0) { _selectedSlot = slot; RefreshLook(); return true; }

            _expectQueueChange = true;
            bool ok = _queue.SelectSlot(q);
            _expectQueueChange = false;
            if (!ok) return false;

            for (int t = 0; t < SlotCount; t++)
                if (t != slot && _slotOffset[t] >= 0 && _slotOffset[t] < q) _slotOffset[t]++;
            _slotOffset[slot] = 0;
            _selectedSlot     = slot;
            RefreshLook();
            return true;
        }

        // ── Rebuild / refresh ─────────────────────────────────────────────
        // Fresh mapping: slot i shows queue offset i. Visuals are only recreated
        // where the ball behind a slot actually changed.
        private void Rebuild()
        {
            if (_queue == null || _slotOffset == null) return;
            for (int i = 0; i < SlotCount; i++)
                _slotOffset[i] = i < _queue.Remaining ? i : -1;
            _selectedSlot = _queue.IsEmpty ? -1 : 0;
            RefreshLook();
        }

        private void RefreshLook()
        {
            int shown = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                var data = _slotOffset[i] >= 0 ? _queue.Peek(_slotOffset[i]) : null;
                if (data != null) shown++;

                if (data != _slotData[i] || (data != null && _slotBalls[i] == null))
                {
                    if (_slotBalls[i]) Destroy(_slotBalls[i].gameObject);
                    _slotBalls[i] = null;
                    _slotData[i]  = data;
                    if (data != null && _slots[i])
                    {
                        _slotBalls[i] = BallVisual.Create(_slots[i], data.color, data.powerLevel, data.shape, 1f);
                        PopIn(i);
                    }
                }
                ApplySlotLook(i);
            }

            if (_ring) _ring.gameObject.SetActive(_selectedSlot >= 0 && _slotBalls[_selectedSlot] != null);
            if (_ring && _selectedSlot >= 0 && _slots[_selectedSlot])
                _ring.position = _slots[_selectedSlot].position + Vector3.up * 0.02f;

            if (_remainingLabel)
            {
                int hidden = Mathf.Max(0, _queue.Remaining - shown);
                _remainingLabel.text = hidden > 0 ? $"+{hidden}" : "";
            }
        }

        private void ApplySlotLook(int i)
        {
            var bv = _slotBalls[i];
            if (bv == null) return;
            if (_slotAnims[i] != null) return;   // pop-in owns the scale until it ends
            bool sel = i == _selectedSlot;
            bv.transform.localPosition = Vector3.up * (sel ? _selectedLift : 0f);
            bv.transform.localScale    = Vector3.one * (sel ? _selectedScale : _slotScale);
        }

        private void PopIn(int i)
        {
            if (_slotAnims[i] != null) StopCoroutine(_slotAnims[i]);
            _slotAnims[i] = StartCoroutine(PopInRoutine(i));
        }

        private IEnumerator PopInRoutine(int i)
        {
            float t = 0f;
            while (t < _refillDuration && _slotBalls[i])
            {
                float k = t / _refillDuration;
                float s = 1f + 0.35f * Mathf.Sin(k * Mathf.PI);   // overshoot then settle
                bool sel = i == _selectedSlot;
                _slotBalls[i].transform.localScale    = Vector3.one * (sel ? _selectedScale : _slotScale) * Mathf.Lerp(0.2f, 1f, k) * s;
                _slotBalls[i].transform.localPosition = Vector3.up * (sel ? _selectedLift : 0f);
                t += Time.deltaTime;
                yield return null;
            }
            _slotAnims[i] = null;
            ApplySlotLook(i);
        }

        // Pale disc under the selected ball — the only "you are here" marker.
        private void BuildRing()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "SelectRing";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(1.25f, 0.02f, 1.25f);
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _ringMat = new Material(sh) { color = new Color(1f, 1f, 1f, 0.9f) };
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial    = _ringMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            _ring = go.transform;
            go.SetActive(false);
        }
    }
}
