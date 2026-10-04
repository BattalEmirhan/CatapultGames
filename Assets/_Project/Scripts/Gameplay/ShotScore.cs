namespace CatapultGames
{
    // A shot pays per cell, multiplied by how dense the hit was and again by the
    // combo streak — consecutive shots that painted something. The rules live in
    // GameConstants (with the painting rules they follow from); this owns the
    // running total, because it is the only thing that sees the whole level.
    public readonly struct ShotScore
    {
        public int Multiplier => shotMultiplier * comboMultiplier;

        public readonly int cells;            // cells this shot put paint into
        public readonly int points;           // what it paid
        public readonly int shotMultiplier;   // from the cell count
        public readonly int comboMultiplier;  // from the streak
        public readonly int streak;           // scoring shots in a row, after this one

        public ShotScore(int cells, int points, int shotMultiplier, int comboMultiplier, int streak)
        {
            this.cells           = cells;
            this.points          = points;
            this.shotMultiplier  = shotMultiplier;
            this.comboMultiplier = comboMultiplier;
            this.streak          = streak;
        }
    }
}
