namespace CatapultGames.Editor
{
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
}
