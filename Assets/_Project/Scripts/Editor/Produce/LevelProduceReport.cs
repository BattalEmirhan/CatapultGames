using System.Collections.Generic;

namespace CatapultGames.Editor
{
    public sealed class LevelProduceReport
    {
        public readonly List<LevelProduceRow> rows = new List<LevelProduceRow>();
        public int  written, skipped, failed;
        public bool cancelled;
    }
}
