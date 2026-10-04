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
        public static int Cost(int levels, int bots, int runs) => levels * bots * runs;

        // Returns null when the progress callback asked to cancel. Polled between
        // RUNS, not levels — one expensive bot on one level is a unit worth
        // cancelling in.
        public static List<BenchmarkLevelResult> RunSweep(IList<(string name, LevelData level)> levels,
                                                 IReadOnlyList<ISolverBot> roster, int runs, int baseSeed,
                                                 Func<float, string, bool> onProgress)
        {
            var results = new List<BenchmarkLevelResult>(levels.Count);
            int total = Cost(levels.Count, roster.Count, runs);
            int done  = 0;

            for (int li = 0; li < levels.Count; li++)
            {
                var (name, level) = levels[li];
                BenchmarkLevelResult res = new BenchmarkLevelResult
                {
                    name     = name,
                    number   = EditorConstants.LevelNumberOf(name),
                    targets  = LevelEditOps.CountTargets(level),
                    balls    = level?.balls?.Length ?? 0
                };
                res.authored = LevelDifficultySchedule.For(res.number);

                foreach (var bot in roster)
                {
                    BotStat stat = new BotStat { botId = bot.Id, runs = runs };
                    float shotsSum = 0, leftSum = 0, wastedSum = 0;

                    for (int run = 0; run < runs; run++)
                    {
                        int seed = baseSeed + li * 100003 + run * 7919;
                        BenchmarkRunResult rr = PlayOne(level, bot, seed);
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
        public static BenchmarkRunResult PlayOne(LevelData level, ISolverBot bot, int seed)
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

            return new BenchmarkRunResult
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
