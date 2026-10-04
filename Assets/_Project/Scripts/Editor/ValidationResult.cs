namespace CatapultGames.Editor
{
    public struct ValidationResult
    {
        public ValidationColorRow[] rows;
        public string[]   globalErrors;
        public string[]   globalWarnings;
        public bool       isValid;
    }
}
