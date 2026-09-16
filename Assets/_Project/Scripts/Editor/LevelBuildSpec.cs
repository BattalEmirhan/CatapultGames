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

    // Written recipe per band — the reset target. The Produce tab edits a live
    // copy (LevelProduceBandSet); "Reset band" comes back here.
    public static class LevelDifficultyBandPresets
    {
        public static LevelBuildSpec For(LevelDifficulty band)
        {
            var s = LevelBuildSpec.Default();
            // Casual ladder: squares only at first (1x1 / 3x3), runs from Normal,
            // the plus from Hard, and 5x5 only where the board is big enough to
            // need it. L and Diagonal are legacy shapes and never generated.
            switch (band)
            {
                case LevelDifficulty.Easy:
                    // The tutorial run: winning should be close to inevitable, so the
                    // slack is generous (a careless player mis-aims ~1 in 3 throws).
                    s.width = 8;  s.height = 8;  s.colorCount = 2; s.fillRatio = 0.55f;
                    s.iceCount = 0; s.stoneCount = 0; s.jokerCount = 0;
                    s.allowLine = false; s.allowColumn = false;
                    s.maxPower = 2; s.slackBalls = 6; s.shuffleWindow = 0;
                    break;
                case LevelDifficulty.Normal:
                    s.width = 9; s.height = 9; s.colorCount = 3; s.fillRatio = 0.60f;
                    s.iceCount = 2; s.stoneCount = 0; s.jokerCount = 0;
                    s.maxPower = 2; s.slackBalls = 2; s.shuffleWindow = 1;
                    break;
                case LevelDifficulty.Hard:
                    s.width = 10; s.height = 10; s.colorCount = 4; s.fillRatio = 0.65f;
                    s.iceCount = 4; s.stoneCount = 2; s.jokerCount = 0;
                    s.allowPlus = true;
                    s.maxPower = 3; s.slackBalls = 1; s.shuffleWindow = 2;
                    break;
                case LevelDifficulty.VeryHard:
                    s.width = 12; s.height = 12; s.colorCount = 4; s.fillRatio = 0.70f;
                    s.iceCount = 8; s.stoneCount = 4; s.jokerCount = 0;   // jokers are legacy: the Rainbow booster took their role
                    s.allowPlus = true;
                    s.maxPower = 3; s.slackBalls = 0; s.shuffleWindow = 3;
                    break;
            }
            return s.Clamped();
        }
    }

    // The live, override-able copy the Produce tab edits. The point of the
    // override is to find the right numbers BEFORE writing them into the presets.
    public sealed class LevelProduceBandSet
    {
        private readonly LevelBuildSpec[] _specs = new LevelBuildSpec[4];

        public LevelProduceBandSet() { ResetAll(); }

        public LevelBuildSpec For(LevelDifficulty band) => _specs[(int)band];
        public void Set(LevelDifficulty band, LevelBuildSpec spec) => _specs[(int)band] = spec.Clamped();
        public void Reset(LevelDifficulty band) => _specs[(int)band] = LevelDifficultyBandPresets.For(band);

        public void ResetAll()
        {
            for (int i = 0; i < _specs.Length; i++) Reset((LevelDifficulty)i);
        }
    }
}
