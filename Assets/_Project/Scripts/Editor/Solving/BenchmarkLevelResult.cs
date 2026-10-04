using System.Collections.Generic;

namespace CatapultGames.Editor
{
    public sealed class BenchmarkLevelResult
    {
        public string name;
        public int    number;
        public int    targets;
        public int    balls;
        public LevelDifficulty    authored;
        public MeasuredDifficulty measured;
        public float  bandWinRate;
        public bool   matches;
        public BotStat bandStat;
        public readonly List<BotStat> bots = new List<BotStat>();

        public BotStat Stat(string botId)
        {
            foreach (BotStat b in bots)
                if (b.botId == botId)
                    return b;
            return null;
        }
    }
}
