using System.Collections.Generic;

namespace CatapultGames.Editor
{
    public sealed class BotStat
    {
        public float WinRate        => runs > 0 ? (float)won        / runs : 0f;
        public float OutOfBallsRate => runs > 0 ? (float)outOfBalls / runs : 0f;
        public float DeadEndRate    => runs > 0 ? (float)deadEnd    / runs : 0f;
        public float UnplayableRate => runs > 0 ? (float)unplayable / runs : 0f;

        public string botId;
        public int    runs;
        public int    won, outOfBalls, deadEnd, unplayable;
        public float  avgShots;       // over wins
        public float  avgBallsLeft;   // over wins — the headroom the level gives
        public float  avgWasted;      // over all runs
        public readonly List<BenchmarkRunResult> details = new List<BenchmarkRunResult>();
    }
}
