namespace CatapultGames.Editor
{
    public struct ValidationColorRow
    {
        public CellColor color;
        public bool      isWild;     // the Joker row — any colour pays for it
        public int       required;   // paint hits still needed (an Ice cell counts twice)
        public int       coverage;   // max hits balls of this color could actually land
        public float     headroom;   // coverage / required
        public ValidationSeverity  severity;
        public string    note;
    }
}
