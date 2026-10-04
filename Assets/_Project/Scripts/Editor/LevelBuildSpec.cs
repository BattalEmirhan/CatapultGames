using UnityEngine;

namespace CatapultGames.Editor
{
    // Flat parameter block for procedural generation. One per difficulty band
    // (LevelDifficultyBandPresets), edited live in the Produce tab, and the only
    // input LevelBuilder takes — so the Editor tab's Generate button and the
    // Produce runner can never generate by different rules.
    [System.Serializable]
    public struct LevelBuildSpec
    {
        // Board
        public int   width;
        public int   height;
        public int   colorCount;        // distinct target colours, 1..7
        public float fillRatio;         // fraction of cells that are paint targets
        public int   minCellsPerColor;  // below this a colour is a stray pixel

        // Special cells (exact counts; the builder retries with a new seed if
        // they do not fit rather than silently placing fewer)
        public int iceCount;
        public int stoneCount;
        public int jokerCount;

        // Ball queue
        public bool allowSquare, allowL, allowLine, allowColumn, allowPlus, allowDiagonal;
        public int  maxPower;           // 1..3
        public int  slackBalls;         // extra balls beyond the planned solution
        public int  shuffleWindow;      // how far a planned ball may drift from its slot (0 = exact plan order)

        public static LevelBuildSpec Default() => new LevelBuildSpec
        {
            width = 10, height = 10, colorCount = 3, fillRatio = 0.6f, minCellsPerColor = 4,
            iceCount = 0, stoneCount = 0, jokerCount = 0,
            allowSquare = true, allowL = false, allowLine = true, allowColumn = true,
            allowPlus = false, allowDiagonal = false,
            maxPower = 2, slackBalls = 2, shuffleWindow = 1
        };

        public LevelBuildSpec Clamped()
        {
            var s = this;
            s.width            = Mathf.Clamp(s.width,  4, 30);
            s.height           = Mathf.Clamp(s.height, 4, 30);
            s.colorCount       = Mathf.Clamp(s.colorCount, 1, 7);
            s.fillRatio        = Mathf.Clamp(s.fillRatio, 0.1f, 1f);
            s.minCellsPerColor = Mathf.Clamp(s.minCellsPerColor, 1, 64);
            s.iceCount         = Mathf.Max(0, s.iceCount);
            s.stoneCount       = Mathf.Max(0, s.stoneCount);
            s.jokerCount       = Mathf.Max(0, s.jokerCount);
            s.maxPower         = Mathf.Clamp(s.maxPower, 1, 3);
            s.slackBalls       = Mathf.Clamp(s.slackBalls, 0, 50);
            s.shuffleWindow    = Mathf.Clamp(s.shuffleWindow, 0, 10);
            if (!(s.allowSquare || s.allowL || s.allowLine || s.allowColumn || s.allowPlus || s.allowDiagonal))
                s.allowSquare = true;   // a queue with no allowed shape cannot exist
            return s;
        }

        public string Summary() =>
            $"{width}×{height} · {colorCount} colours · fill {fillRatio:0.00} · " +
            $"ice {iceCount} · stone {stoneCount} · joker {jokerCount} · " +
            $"P≤{maxPower} · +{slackBalls} slack · shuffle {shuffleWindow}";
    }
}
