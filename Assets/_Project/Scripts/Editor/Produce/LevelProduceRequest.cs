using UnityEngine;

namespace CatapultGames.Editor
{
    // What the user asked for, normalised ONCE. The summary, the confirmation,
    // the runner and the report all read Normalized() — never the raw fields —
    // so the dialog can never count one range while the runner walks another.
    public struct LevelProduceRequest
    {
        public int Count => Normalized().to - Normalized().from + 1;
        public bool WasClamped => Normalized().from != from || Normalized().to != to;

        public int  from;
        public int  to;
        public int  baseSeed;
        public int  attempts;
        public bool overwrite;

        public const int MaxCountedLevel = 999;   // caps every loop, so a pasted huge number cannot freeze the editor
        public const int MaxSpan         = 300;

        public LevelProduceRequest Normalized()
        {
            var r = this;
            r.from = Mathf.Clamp(r.from, 1, MaxCountedLevel);
            r.to   = Mathf.Clamp(r.to,   1, MaxCountedLevel);
            if (r.to < r.from)
                (r.from, r.to) = (r.to, r.from);
            if (r.to - r.from + 1 > MaxSpan)
                r.to = r.from + MaxSpan - 1;
            r.attempts = Mathf.Clamp(r.attempts, 1, 100);
            return r;
        }
    }
}
