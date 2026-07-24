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
