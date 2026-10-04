namespace CatapultGames.Editor
{
    // Level number → difficulty band, and win-rate → measured band. The ONLY seam
    // for both: the Produce runner asks For(n) per level, the Gallery badges read
    // it, and the Solving verdict reads Classify / MatchesAuthored. Changing the
    // curve is an edit to For() and nothing else follows.
    //
    // Note the two axes kept apart on purpose: this is the difficulty band; the
    // ball budget (slack balls) lives in the band's LevelBuildSpec. Derive neither
    // from the other.
    public static class LevelDifficultySchedule
    {
        // The first few levels are the tutorial run and stay easy no matter what.
        public const int FtueEndLevel = 5;

        // Win-rate thresholds of the band bot (see SolverRoster.BandBotId).
        // Ordered high → low; the array is what the gauge draws its ticks from.
        public const float VeryEasyAbove = 0.85f;
        public const float EasyAbove     = 0.65f;
        public const float NormalAbove   = 0.40f;
        public const float HardAbove     = 0.20f;

        public static LevelDifficulty For(int levelNumber)
        {
            if (levelNumber <= FtueEndLevel)
                return LevelDifficulty.Easy;
            if (levelNumber % 10 == 0)
                return LevelDifficulty.VeryHard;
            if (levelNumber % 5  == 0)
                return LevelDifficulty.Hard;
            return LevelDifficulty.Normal;
        }

        public static MeasuredDifficulty Classify(float winRate)
        {
            if (winRate >= VeryEasyAbove)
                return MeasuredDifficulty.VeryEasy;
            if (winRate >= EasyAbove)
                return MeasuredDifficulty.Easy;
            if (winRate >= NormalAbove)
                return MeasuredDifficulty.Normal;
            return winRate >= HardAbove ? MeasuredDifficulty.Hard : MeasuredDifficulty.VeryHard;
        }

        // Authored band expressed on the measured scale (Easy → Easy, …).
        public static MeasuredDifficulty Target(LevelDifficulty authored) =>
            (MeasuredDifficulty)((int)authored + 1);

        // One step of tolerance in BOTH directions. A Hard level measuring Normal
        // is tuning noise; measuring Very Easy is a defect. Normal's window is
        // two-sided too: a Normal level the population wins 99% of the time does
        // NOT match — that is the "this level is too easy" signal.
        public static bool MatchesAuthored(MeasuredDifficulty measured, LevelDifficulty authored)
        {
            int m = (int)measured;
            int a = (int)Target(authored);
            return m >= a - 1 && m <= a + 1;
        }

        public static string Label(LevelDifficulty d) => d switch
        {
            LevelDifficulty.Easy     => "Easy",
            LevelDifficulty.Normal   => "Normal",
            LevelDifficulty.Hard     => "Hard",
            _                        => "Very Hard"
        };

        public static string Label(MeasuredDifficulty d) => d switch
        {
            MeasuredDifficulty.VeryEasy => "Very Easy",
            MeasuredDifficulty.Easy     => "Easy",
            MeasuredDifficulty.Normal   => "Normal",
            MeasuredDifficulty.Hard     => "Hard",
            _                           => "Very Hard"
        };
    }
}
