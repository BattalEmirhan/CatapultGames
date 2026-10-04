using System;

namespace CatapultGames.Editor
{
    // The batch writer. UI-less: the Produce tab feeds it a request and a band
    // set and renders the report. Per level it
    //   1. resolves the band from the number (LevelDifficultySchedule — the one seam)
    //   2. names the file (EditorConstants.LevelFileName — the one naming rule)
    //   3. skips if present and overwrite is off
    //   4. builds with LevelBuilder — same builder as the Editor's Generate button
    //   5. reports a failure with its reason; never silently ships a thinner level
    //   6. writes IN PLACE, so the .meta (GUID) of an existing file survives
    // Writing straight into Resources/Levels is the whole point: there is no
    // second format to forget to export.
    public static class LevelProduceRunner
    {
        public static LevelProduceReport Run(in LevelProduceRequest request, LevelProduceBandSet bands,
                                             Func<float, string, bool> onProgress)
        {
            var req    = request.Normalized();
            var report = new LevelProduceReport();
            int total  = req.to - req.from + 1;

            for (int n = req.from; n <= req.to; n++)
            {
                int done = n - req.from;
                if (onProgress != null && !onProgress((float)done / total, $"level{n} ({done + 1}/{total})"))
                {
                    report.cancelled = true;
                    break;
                }

                var band = LevelDifficultySchedule.For(n);
                LevelProduceRow row  = new LevelProduceRow { number = n, name = EditorConstants.LevelFileName(n), band = band };
                report.rows.Add(row);

                string path = EditorConstants.LevelPath(n);
                if (System.IO.File.Exists(path) && !req.overwrite)
                {
                    row.status = ProduceStatus.Skipped;
                    row.note   = "exists — overwrite is off";
                    report.skipped++;
                    continue;
                }

                var spec  = bands.For(band);
                var level = LevelBuilder.TryBuild(spec, req.baseSeed + n * 1000, req.attempts, out string error);
                if (level == null)
                {
                    row.status = ProduceStatus.Failed;
                    row.note   = error ?? "builder returned nothing";
                    report.failed++;
                    continue;
                }

                level.metadata.levelName = row.name;   // file stem = level identity
                // The hint belongs to the level SLOT (what level n teaches), not to
                // the board that happened to be generated there — keep it.
                if (System.IO.File.Exists(path))
                    level.metadata.hint = LevelSerializer.Load(path)?.metadata?.hint ?? "";
                LevelSerializer.Save(level, path);     // in place → GUID kept
                row.status = ProduceStatus.Written;
                row.note   = $"{LevelDifficultySchedule.Label(band)} · {level.grid.width}×{level.grid.height} · " +
                             $"{LevelEditOps.CountTargets(level)} targets · {level.balls.Length} balls";
                report.written++;
            }

            return report;
        }

        // How many files in the range already exist — for the destructive-op
        // confirmation. Reads the normalised range like everything else.
        public static int CountExisting(in LevelProduceRequest request)
        {
            var req = request.Normalized();
            int n = 0;
            for (int i = req.from; i <= req.to; i++)
                if (LevelCatalog.Exists(i))
                    n++;
            return n;
        }
    }
}
