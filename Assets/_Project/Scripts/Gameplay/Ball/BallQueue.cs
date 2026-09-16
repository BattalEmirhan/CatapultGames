using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Task 11 — Ordered ball queue with auto-advance.
    // Pure logic — no visuals. Attach to any persistent GameObject.
    public class BallQueue : MonoBehaviour
    {
        private readonly List<BallData> _balls = new();
        private int _index;

        // ── Events ────────────────────────────────────────────────────────
        public event Action<BallData>  OnBallConsumed;   // fired before advancing
        public event Action            OnChanged;        // fired after any state change
        public event Action            OnEmpty;          // fired when last ball consumed
        public event Action<CellColor, int> OnColorCleared; // (color, count removed) when leftovers are purged

        // ── State ─────────────────────────────────────────────────────────
        public bool     IsEmpty   => _index >= _balls.Count;
        public int      Remaining => Mathf.Max(0, _balls.Count - _index);
        public BallData Current   => IsEmpty ? null : _balls[_index];

        // Peek ahead: offset=0 → current, 1 → next, etc.
        public BallData Peek(int offset)
        {
            int i = _index + offset;
            return i >= 0 && i < _balls.Count ? _balls[i] : null;
        }

        // Every ball not yet fired, current one first. Fills a caller-owned list so
        // the mid-run solvability check (GameManager) can run per landing without
        // allocating. BallData instances are shared, not copied — read only.
        public void CopyRemaining(List<BallData> into)
        {
            if (into == null) return;
            into.Clear();
            for (int i = _index; i < _balls.Count; i++)
                if (_balls[i] != null) into.Add(_balls[i]);
        }

        // Bring a queued ball to the front so it becomes Current — the player picking
        // one of the next few instead of always firing whatever is loaded.
        //
        // A move, not a swap: the balls it jumps keep their relative order, so the
        // authored sequence still plays out and the choice costs nothing but position.
        // Everything downstream is untouched by this — AimPreview reads Current,
        // BallLauncher consumes Current — which is why selection is modelled as
        // reordering rather than as a second "which ball" concept.
        public bool SelectSlot(int offset)
        {
            if (offset <= 0) return false;          // slot 0 is already the current ball

            int i = _index + offset;
            if (i < 0 || i >= _balls.Count) return false;

            var ball = _balls[i];
            if (ball == null) return false;

            _balls.RemoveAt(i);
            _balls.Insert(_index, ball);
            OnChanged?.Invoke();
            return true;
        }

        // A booster changed the current ball IN PLACE (colour / shape); the data
        // reference is the same, so listeners that compare references (the tray)
        // need an explicit nudge to look again.
        public void NotifyCurrentChanged() => OnChanged?.Invoke();

        // ── Undo support ──────────────────────────────────────────────────
        // A whole-queue snapshot rather than a "put the ball back" call, because
        // undoing one shot can also have to undo a RemoveColor purge that the shot
        // triggered. Restoring the entire state covers both without special cases.
        public readonly struct Snapshot
        {
            internal readonly BallData[] balls;
            internal readonly int        index;
            internal Snapshot(BallData[] balls, int index) { this.balls = balls; this.index = index; }
            public bool IsValid => balls != null;
        }

        public Snapshot Capture() => new Snapshot(_balls.ToArray(), _index);

        public void Restore(Snapshot snapshot)
        {
            if (!snapshot.IsValid) return;
            _balls.Clear();
            _balls.AddRange(snapshot.balls);
            _index = Mathf.Clamp(snapshot.index, 0, _balls.Count);
            OnChanged?.Invoke();
        }

        // Extra balls handed out mid-run (the "keep going" offer). They join the
        // back of the queue, so the authored order plays out first.
        public void Append(IEnumerable<BallData> extra)
        {
            if (extra == null) return;

            int before = _balls.Count;
            foreach (var b in extra)
                if (b != null) _balls.Add(b);

            if (_balls.Count != before) OnChanged?.Invoke();
        }

        // ── API ───────────────────────────────────────────────────────────
        public void Load(BallData[] balls)
        {
            _balls.Clear();
            if (balls != null) _balls.AddRange(balls);
            _index = 0;
            OnChanged?.Invoke();
        }

        // Advance to next ball; returns the one that was consumed.
        public BallData Consume()
        {
            if (IsEmpty) return null;
            var ball = _balls[_index];
            OnBallConsumed?.Invoke(ball);
            _index++;
            OnChanged?.Invoke();
            if (IsEmpty) OnEmpty?.Invoke();
            return ball;
        }

        // Drop every not-yet-fired ball of this colour — used once a colour is fully
        // painted, since its leftovers can no longer help. Already-consumed balls
        // (index below the cursor) are left alone. Returns how many were removed.
        // Iterates back-to-front so removals never shift the live cursor (_index).
        public int RemoveColor(CellColor color)
        {
            int removed = 0;
            for (int i = _balls.Count - 1; i >= _index; i--)
            {
                if (_balls[i] != null && _balls[i].color == color)
                {
                    _balls.RemoveAt(i);
                    removed++;
                }
            }
            if (removed > 0)
            {
                // Fire before OnChanged so the view can grab the to-be-removed
                // visuals (for the firework) before it reconciles to the new state.
                // `removed` lets the view also pop balls that were queued beyond the
                // visible waypoints (no on-screen visual of their own).
                OnColorCleared?.Invoke(color, removed);
                OnChanged?.Invoke();
                if (IsEmpty) OnEmpty?.Invoke();
            }
            return removed;
        }
    }
}
