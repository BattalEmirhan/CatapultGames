using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames.Editor
{
    // Runs the roster over a set of levels and turns the outcomes into a
    // measured difficulty. UI-less; the Solving tab only renders what comes back.
    //
    // Seeds: baseSeed + levelIndex * 100003 + run * 7919 — no two (level, run)
    // cells share a stream, every bot sees the same stream for the same cell,
    // and a fixed base seed replays the whole sweep exactly.
    public static class LevelBenchmark
    {
        public sealed class RunResult
        {
            public int            seed;
            public PlayoutOutcome outcome;
            public int            shots;
            public int            wasted;
            public int            ballsLeft;
            public int            cellsLeft;
        }

        public sealed class BotStat
        {
            public string botId;
            public int    runs;
            public int    won, outOfBalls, deadEnd, unplayable;
            public float  avgShots;       // over wins
            public float  avgBallsLeft;   // over wins — the headroom the level gives
            public float  avgWasted;      // over all runs
            public readonly List<RunResult> details = new List<RunResult>();

            public float WinRate        => runs > 0 ? (float)won        / runs : 0f;
            public float OutOfBallsRate => runs > 0 ? (float)outOfBalls / runs : 0f;
            public float DeadEndRate    => runs > 0 ? (float)deadEnd    / runs : 0f;
            public float UnplayableRate => runs > 0 ? (float)unplayable / runs : 0f;
        }

        public sealed class LevelResult
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
                foreach (var b in bots) if (b.botId == botId) return b;
                return null;
            }
        }

        public static int Cost(int levels, int bots, int runs) => levels * bots * runs;

        // Returns null when the progress callback asked to cancel. Polled between
        // RUNS, not levels — one expensive bot on one level is a unit worth
        // cancelling in.
        public static List<LevelResult> RunSweep(IList<(string name, LevelData level)> levels,
                                                 IReadOnlyList<ISolverBot> roster, int runs, int baseSeed,
                                                 Func<float, string, bool> onProgress)
        {
            var results = new List<LevelResult>(levels.Count);
            int total = Cost(levels.Count, roster.Count, runs);
            int done  = 0;

            for (int li = 0; li < levels.Count; li++)
            {
                var (name, level) = levels[li];
                var res = new LevelResult
                {
                    name     = name,
                    number   = EditorConstants.LevelNumberOf(name),
                    targets  = LevelEditOps.CountTargets(level),
                    balls    = level?.balls?.Length ?? 0
                };
                res.authored = LevelDifficultySchedule.For(res.number);

                foreach (var bot in roster)
                {
                    var stat = new BotStat { botId = bot.Id, runs = runs };
                    float shotsSum = 0, leftSum = 0, wastedSum = 0;

                    for (int run = 0; run < runs; run++)
                    {
                        int seed = baseSeed + li * 100003 + run * 7919;
                        var rr = PlayOne(level, bot, seed);
                        stat.details.Add(rr);
                        switch (rr.outcome)
                        {
                            case PlayoutOutcome.Won:        stat.won++; shotsSum += rr.shots; leftSum += rr.ballsLeft; break;
                            case PlayoutOutcome.OutOfBalls: stat.outOfBalls++; break;
                            case PlayoutOutcome.DeadEnd:    stat.deadEnd++;    break;
                            default:                        stat.unplayable++; break;
                        }
                        wastedSum += rr.wasted;

                        done++;
                        if (onProgress != null && !onProgress((float)done / Mathf.Max(1, total), $"{name} · {bot.DisplayName} · run {run + 1}/{runs}"))
                            return null;
                    }

                    stat.avgShots     = stat.won > 0 ? shotsSum / stat.won : 0f;
                    stat.avgBallsLeft = stat.won > 0 ? leftSum  / stat.won : 0f;
                    stat.avgWasted    = runs > 0 ? wastedSum / runs : 0f;
                    res.bots.Add(stat);
                }

                res.bandStat    = res.Stat(SolverRoster.BandBotId) ?? (res.bots.Count > 0 ? res.bots[0] : null);
                res.bandWinRate = res.bandStat?.WinRate ?? 0f;
                res.measured    = LevelDifficultySchedule.Classify(res.bandWinRate);
                res.matches     = LevelDifficultySchedule.MatchesAuthored(res.measured, res.authored);
                results.Add(res);
            }
            return results;
        }

        // One full game: bot throws until the board says the run is over. The
        // queue is finite so the loop always terminates.
        public static RunResult PlayOne(LevelData level, ISolverBot bot, int seed)
        {
            var rng   = new System.Random(seed);
            var board = PlayoutBoard.From(level);
            int queued = board.Queue.Count;   // purge empties the queue on a win, so "spare" is queued − thrown
            bot.BeginEpisode(board, rng);

            PlayoutOutcome? outcome = board.Evaluate();
            int guard = 0;
            while (outcome == null && guard++ < 1000)
            {
                var move = bot.ChooseMove(board, rng);
                board.Apply(move);
                outcome = board.Evaluate();
            }

            return new RunResult
            {
                seed      = seed,
                outcome   = outcome ?? PlayoutOutcome.OutOfBalls,
                shots     = board.Shots,
                wasted    = board.Wasted,
                ballsLeft = Mathf.Max(0, queued - board.Shots),
                cellsLeft = board.RemainingCells
            };
        }
    }
}
