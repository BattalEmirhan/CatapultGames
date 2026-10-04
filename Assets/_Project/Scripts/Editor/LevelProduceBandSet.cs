namespace CatapultGames.Editor
{
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
            for (int i = 0; i < _specs.Length; i++)
                Reset((LevelDifficulty)i);
        }
    }
}
