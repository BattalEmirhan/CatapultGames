using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames.Editor
{
    // The single source of truth for procedural generation. The Editor tab's
    // Generate card and the Produce runner both call TryBuild, so "auto
    // generate" can never mean two different things.
    //
    // Pipeline (load-bearing order — keep it):
    //   1. mask      — grow a contiguous blob of target cells to fillRatio
    //   2. colours   — multi-source growth over the mask, one source per colour
    //   3. specials  — stone on the rim of the mask, then ice, then joker
    //   4. queue     — plan a solution ball-by-ball with CoverageAnalyzer,
    //                  add slack, drift the order by shuffleWindow
    //   5. verify    — LevelValidator + LevelAutoSolver (the same gate Save uses)
    // Any step that cannot meet the spec fails the ATTEMPT; the next attempt
    // takes a new seed (seed + attempt * 7919). After `attempts` failures the
    // level is reported with the last reason. It never silently ships with
    // fewer specials or a thinner queue than asked.
    public static class LevelBuilder
    {
        private const int MaxPlannedBalls = 120;

        public static LevelData TryBuild(LevelBuildSpec spec, int seed, int attempts, out string error)
        {
            spec  = spec.Clamped();
            error = null;
            attempts = Mathf.Max(1, attempts);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                var rng   = new System.Random(seed + attempt * 7919);
                var level = BuildOnce(spec, rng, out string why);
                if (level != null) { error = null; return level; }
                error = $"attempt {attempt + 1}/{attempts}: {why}";
            }
            return null;
        }

        // ── One attempt ───────────────────────────────────────────────────
        private static LevelData BuildOnce(LevelBuildSpec spec, System.Random rng, out string why)
        {
            why = null;
            int w = spec.width, h = spec.height, n = w * h;

            // 1. Mask
            int targetCount = Mathf.Clamp(Mathf.RoundToInt(n * spec.fillRatio), 1, n);
            bool[] mask = GrowBlob(w, h, targetCount, rng);

            // 2. Colours
            var colors = PickColors(spec.colorCount, rng);
            CellColor[] cellColor = AssignColors(w, h, mask, colors, rng);
            if (cellColor == null) { why = "colour growth left a colour with no cells"; return null; }

            var perColor = new int[8];
            for (int i = 0; i < n; i++) if (cellColor[i] != CellColor.None) perColor[(int)cellColor[i]]++;
            foreach (var c in colors)
                if (perColor[(int)c] < spec.minCellsPerColor)
                { why = $"{c} got {perColor[(int)c]} cells (< {spec.minCellsPerColor})"; return null; }

            // 3. Specials
            var type = new CellType[n];
            if (!PlaceStone(w, h, mask, type, spec.stoneCount, rng))
            { why = $"could not place {spec.stoneCount} stone cells on the rim"; return null; }
            if (!ConvertTargets(w, h, mask, type, CellType.Ice,   spec.iceCount,   rng))
            { why = $"could not place {spec.iceCount} ice cells"; return null; }
            if (!ConvertTargets(w, h, mask, type, CellType.Joker, spec.jokerCount, rng))
            { why = $"could not place {spec.jokerCount} joker cells"; return null; }

            // Assemble the level
            var level = new LevelData
            {
                metadata = new LevelMetadata { levelName = "generated", author = "LevelBuilder", version = 1 },
                grid     = new GridConfig { width = w, height = h, cellSize = 1f },
                camera   = new CameraConfig(),
                cells    = new CellData[n],
                balls    = new BallData[0]
            };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                CellColor col = type[i] == CellType.Stone ? CellColor.None : cellColor[i];
                level.cells[i] = new CellData(x, y, col, type[i]);
            }

            // 4. Queue
            var plan = PlanQueue(level, spec, rng, out why);
            if (plan == null) return null;
            AddSlack(plan, spec.slackBalls, rng);
            Drift(plan, spec.shuffleWindow, rng);
            level.balls = plan.ToArray();

            // 5. Verify with the same gate Save uses
            var v = LevelValidator.Validate(level);
            if (!v.isValid) { why = "validator: " + FirstError(v); return null; }
            var s = LevelAutoSolver.Solve(level);
            if (!s.solved) { why = $"greedy solver left {s.remaining}/{s.totalColored} cells"; return null; }

            return level;
        }

        private static string FirstError(LevelValidator.Result v)
        {
            if (v.globalErrors.Length > 0) return v.globalErrors[0];
            foreach (var r in v.rows)
                if (r.severity == LevelValidator.Severity.Error) return $"{r.color}: {r.note}";
            return "unknown";
        }

        // ── 1. Mask ───────────────────────────────────────────────────────
        // Randomised BFS from one seed: pick a random frontier cell, add it, push
        // its neighbours. Organic, always connected, no isolated pixels.
        private static bool[] GrowBlob(int w, int h, int count, System.Random rng)
        {
            var mask     = new bool[w * h];
            var frontier = new List<int>();
            int start    = rng.Next(w * h);
            mask[start]  = true;
            PushNeighbours(w, h, start, mask, frontier);

            int placed = 1;
            while (placed < count && frontier.Count > 0)
            {
                int pick = rng.Next(frontier.Count);
                int idx  = frontier[pick];
                frontier[pick] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                if (mask[idx]) continue;
                mask[idx] = true;
                placed++;
                PushNeighbours(w, h, idx, mask, frontier);
            }
            return mask;
        }

        private static void PushNeighbours(int w, int h, int idx, bool[] mask, List<int> frontier)
        {
            int x = idx % w, y = idx / w;
            if (x > 0     && !mask[idx - 1]) frontier.Add(idx - 1);
            if (x < w - 1 && !mask[idx + 1]) frontier.Add(idx + 1);
            if (y > 0     && !mask[idx - w]) frontier.Add(idx - w);
            if (y < h - 1 && !mask[idx + w]) frontier.Add(idx + w);
        }

        // ── 2. Colours ────────────────────────────────────────────────────
        private static List<CellColor> PickColors(int count, System.Random rng)
        {
            var pool = new List<CellColor>
            {
                CellColor.Red, CellColor.Green, CellColor.Blue, CellColor.Black,
                CellColor.White, CellColor.Pink, CellColor.Purple
            };
            var picked = new List<CellColor>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int k = rng.Next(pool.Count);
                picked.Add(pool[k]);
                pool.RemoveAt(k);
            }
            return picked;
        }

        // Multi-source growth: each colour starts at a random mask cell and the
        // frontiers race. Produces contiguous, uneven regions — the shapes a
        // human would paint — rather than a checkerboard.
        private static CellColor[] AssignColors(int w, int h, bool[] mask, List<CellColor> colors, System.Random rng)
        {
            int n = w * h;
            var result  = new CellColor[n];
            var maskIdx = new List<int>();
            for (int i = 0; i < n; i++) if (mask[i]) maskIdx.Add(i);
            if (maskIdx.Count < colors.Count) return null;

            // Sources: distinct random mask cells.
            var frontier = new List<(int idx, CellColor col)>();
            var used     = new HashSet<int>();
            foreach (var c in colors)
            {
                int s;
                do { s = maskIdx[rng.Next(maskIdx.Count)]; } while (!used.Add(s));
                result[s] = c;
                PushColored(w, h, s, c, result, mask, frontier);
            }

            while (frontier.Count > 0)
            {
                int pick = rng.Next(frontier.Count);
                var (idx, col) = frontier[pick];
                frontier[pick] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                if (result[idx] != CellColor.None) continue;
                result[idx] = col;
                PushColored(w, h, idx, col, result, mask, frontier);
            }

            // The mask is connected, so every mask cell gets a colour; still guard.
            for (int i = 0; i < n; i++) if (mask[i] && result[i] == CellColor.None) return null;
            return result;
        }

        private static void PushColored(int w, int h, int idx, CellColor col, CellColor[] result, bool[] mask,
                                        List<(int, CellColor)> frontier)
        {
            int x = idx % w, y = idx / w;
            void Try(int j) { if (mask[j] && result[j] == CellColor.None) frontier.Add((j, col)); }
            if (x > 0)     Try(idx - 1);
            if (x < w - 1) Try(idx + 1);
            if (y > 0)     Try(idx - w);
            if (y < h - 1) Try(idx + w);
        }

        // ── 3. Specials ───────────────────────────────────────────────────
        // Stone goes on NON-target cells that touch the mask, so it shadows real
        // stamps instead of sitting in a corner as decoration. Falls back to any
        // free cell only when the rim is exhausted.
        private static bool PlaceStone(int w, int h, bool[] mask, CellType[] type, int count, System.Random rng)
        {
            if (count == 0) return true;
            var rim = new List<int>();
            var any = new List<int>();
            for (int i = 0; i < mask.Length; i++)
            {
                if (mask[i]) continue;
                any.Add(i);
                int x = i % w, y = i / w;
                bool touches = (x > 0 && mask[i - 1]) || (x < w - 1 && mask[i + 1]) ||
                               (y > 0 && mask[i - w]) || (y < h - 1 && mask[i + w]);
                if (touches) rim.Add(i);
            }
            Shuffle(rim, rng);
            Shuffle(any, rng);

            int placed = 0;
            foreach (var i in rim) { if (placed >= count) break; type[i] = CellType.Stone; placed++; }
            foreach (var i in any)
            {
                if (placed >= count) break;
                if (type[i] == CellType.Stone) continue;
                type[i] = CellType.Stone; placed++;
            }
            return placed >= count;
        }

        private static bool ConvertTargets(int w, int h, bool[] mask, CellType[] type, CellType to, int count, System.Random rng)
        {
            if (count == 0) return true;
            var candidates = new List<int>();
            for (int i = 0; i < mask.Length; i++)
                if (mask[i] && type[i] == CellType.Normal) candidates.Add(i);
            if (candidates.Count < count) return false;
            Shuffle(candidates, rng);
            for (int k = 0; k < count; k++) type[candidates[k]] = to;
            return true;
        }

        // ── 4. Queue ──────────────────────────────────────────────────────
        // Greedy plan by the game's own coverage rules: at each step, over every
        // (colour still open, allowed shape, power), take the kind that lands the
        // most hits; ties go to the smaller stamp so the plan does not overshoot.
        // Each ball is struck off a scratch board before the next is chosen, so
        // the queue is a PLAN, not a pile of independent guesses.
        private static List<BallData> PlanQueue(LevelData level, LevelBuildSpec spec, System.Random rng, out string why)
        {
            why = null;
            var board = CoverageAnalyzer.BuildTargets(level);
            var kinds = Kinds(spec);
            var plan  = new List<BallData>();

            if (board.TotalHits() == 0) { why = "no paint targets"; return null; }

            var open = new List<CellColor>();
            while (board.TotalHits() > 0)
            {
                if (plan.Count >= MaxPlannedBalls) { why = "plan exceeded the ball cap"; return null; }

                open.Clear();
                for (int i = 0; i < board.hits.Length; i++)
                    if (board.hits[i] > 0 && board.colors[i] != CellColor.None && !open.Contains(board.colors[i]))
                        open.Add(board.colors[i]);
                Shuffle(open, rng);   // vary which colour leads when gains tie

                BallData best = null; int bestGain = 0, bestSize = int.MaxValue, bx = -1, by = -1;
                foreach (var col in open)
                foreach (var (shape, power) in kinds)
                {
                    var ball = new BallData(col, power, shape);
                    int gain = CoverageAnalyzer.BestPlacement(board, ball, out int lx, out int ly);
                    if (gain == 0) continue;
                    int size = GameConstants.GetPaintCellCount(ball, board.width, board.height);
                    if (gain > bestGain || (gain == bestGain && size < bestSize))
                    { best = ball; bestGain = gain; bestSize = size; bx = lx; by = ly; }
                }

                if (best == null) { why = "a target cell is unreachable by every allowed shape"; return null; }
                CoverageAnalyzer.ApplyPlacement(board, best, bx, by);
                plan.Add(best);
            }
            return plan;
        }

        private static List<(BallShape, int)> Kinds(LevelBuildSpec spec)
        {
            var kinds = new List<(BallShape, int)>();
            void Add(BallShape s, bool allowed, bool scalesWithPower)
            {
                if (!allowed) return;
                if (!scalesWithPower) { kinds.Add((s, 1)); return; }
                for (int p = 1; p <= spec.maxPower; p++) kinds.Add((s, p));
            }
            Add(BallShape.Square,   spec.allowSquare,   true);
            Add(BallShape.L,        spec.allowL,        false);   // L ignores power
            Add(BallShape.Line,     spec.allowLine,     true);
            Add(BallShape.Column,   spec.allowColumn,   true);
            Add(BallShape.Plus,     spec.allowPlus,     true);
            Add(BallShape.Diagonal, spec.allowDiagonal, true);
            return kinds;
        }

        // Slack balls are copies of planned balls dropped at random slots. A
        // wasted ball costs the greedy solver nothing (it skips it), so slack can
        // only make the level easier — which is what "slack" means.
        private static void AddSlack(List<BallData> plan, int slack, System.Random rng)
        {
            if (plan.Count == 0) return;
            for (int i = 0; i < slack; i++)
            {
                var src = plan[rng.Next(plan.Count)];
                plan.Insert(rng.Next(plan.Count + 1), new BallData(src.color, src.powerLevel, src.shape));
            }
        }

        // Local disorder: each ball may swap with one up to `window` slots ahead.
        // Bounded on purpose — a full shuffle would routinely break the plan and
        // burn every attempt on the verify step.
        private static void Drift(List<BallData> plan, int window, System.Random rng)
        {
            if (window <= 0) return;
            for (int i = 0; i < plan.Count; i++)
            {
                int j = Mathf.Min(plan.Count - 1, i + rng.Next(window + 1));
                (plan[i], plan[j]) = (plan[j], plan[i]);
            }
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
