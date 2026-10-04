namespace CatapultGames
{
    public struct ColorCoverage
    {
        // Proven unreachable: no placement of these balls covers those cells.
        public bool Impossible => required > 0 && ceiling < required;

        // Completable, but with no margin for a wasted stamp.
        public bool Tight => required > 0 && !Impossible && ceiling < required * CoverageAnalyzer.TightHeadroom;

        // ceiling / required — 1.0 means "every stamp must be perfect".
        public float Headroom => required > 0 ? (float)ceiling / required : float.PositiveInfinity;

        public CellColor color;
        public bool      isWild;      // the Joker row — any colour pays for it
        public int       required;    // paint hits still needed (Ice counts twice)
        public int       ceiling;     // max hits this colour's balls could land
        public int       ballCount;
    }
}
