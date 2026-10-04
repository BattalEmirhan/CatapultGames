using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatapultGames.Editor
{
    // "Is this level really as hard as its number claims, and can I tell
    // without playing it by hand?" Runs the whole roster over a scope of levels
    // and shows, per level, one stacked outcome bar per bot, a band gauge for
    // the band bot, and a verdict against the authored band.
    //
    // Reads the tree the shell cloned; talks back only through the callbacks
    // given in the constructor. Knows nothing about the other tabs.
    public sealed class LevelSolvingTabController
    {
        private List<LevelCatalogEntry> Catalog => _catalog ??= LevelCatalog.Scan();

        private static readonly string[] ScopeNames = { "Open level", "All levels", "Range" };
        private static readonly string[] SortNames  = { "Easiest first", "Hardest first", "Number ↑", "Number ↓" };
        private const string HiddenClass = "cg-hidden";
        private const int MaxVisibleChips = 8;
        private readonly VisualElement _root;
        private readonly Func<LevelData> _openLevel;
        private readonly Func<string>    _openLevelName;
        private readonly Action<IReadOnlyList<BenchmarkLevelResult>> _onSweepFinished;
        private DropdownField _scopeDd, _sortDd;
        private IntegerField  _from, _to, _runs, _seed;
        private Label _cost, _summary, _detailHeader, _notSim;
        private VisualElement _botsRow, _levels, _detailCard, _detail, _rosterCard;
        private List<LevelCatalogEntry> _catalog;   // lazily read
        private List<BenchmarkLevelResult> _results = new List<BenchmarkLevelResult>();
        private readonly HashSet<string> _hiddenBots = new HashSet<string>();
        private string _selectedLevel;

        public LevelSolvingTabController(VisualElement root, Func<LevelData> openLevel, Func<string> openLevelName,
                                         Action<IReadOnlyList<BenchmarkLevelResult>> onSweepFinished)
        {
            _root            = root;
            _openLevel       = openLevel;
            _openLevelName   = openLevelName;
            _onSweepFinished = onSweepFinished;
        }

        public void Bind()
        {
            _scopeDd = Find<DropdownField>("solve-scope");
            if (_scopeDd != null)
            {
                _scopeDd.choices = new List<string>(ScopeNames);
                _scopeDd.index = 0;
                _scopeDd.RegisterValueChangedCallback(_ => RefreshCost());
            }
            _from = Find<IntegerField>("solve-from");
            _to   = Find<IntegerField>("solve-to");
            _runs = Find<IntegerField>("solve-runs");
            _seed = Find<IntegerField>("solve-seed");
            _from?.RegisterValueChangedCallback(_ => RefreshCost());
            _to?.RegisterValueChangedCallback(_ => RefreshCost());
            _runs?.RegisterValueChangedCallback(_ => RefreshCost());

            _cost = Find<Label>("solve-cost");
            var run = Find<Button>("solve-run");
            if (run != null)
                run.clicked += RunSweep;

            _summary   = Find<Label>("solve-summary");
            _sortDd    = Find<DropdownField>("solve-sort");
            if (_sortDd != null)
            {
                _sortDd.choices = new List<string>(SortNames);
                _sortDd.index = 0;
                _sortDd.RegisterValueChangedCallback(_ => RenderLevels());
            }
            _botsRow    = Find<VisualElement>("solve-bots");
            _levels     = Find<VisualElement>("solve-levels");
            _detailCard = Find<VisualElement>("solve-detail-card");
            _detailHeader = Find<Label>("solve-detail-header");
            _detail     = Find<VisualElement>("solve-detail");
            _rosterCard = Find<VisualElement>("solve-roster");
            _notSim     = Find<Label>("solve-notsim");

            BuildBotFilter();
            BuildRoster();
            RefreshCost();
        }

        public void Activate() => RefreshCost();

        // The disk changed (save / produce): whatever was measured belonged to
        // the old files. Drop the catalog cache; keep results on screen but say so.
        public void InvalidateCatalog()
        {
            _catalog = null;
            RefreshCost();
        }

        // Only the band bot's outcome bar + the verdict: the quick card the
        // Editor tab shows. Same data, same builders, so the two cannot disagree.
        public static VisualElement BuildQuickCard(BenchmarkLevelResult r)
        {
            var box = new VisualElement();
            box.style.marginTop = 6;
            if (r?.bandStat == null)
                return box;
            box.Add(SectionLabel($"LAST SWEEP — {SolverRoster.Find(r.bandStat.botId)?.DisplayName?.ToUpperInvariant()}, {r.bandStat.runs} RUNS"));
            box.Add(OutcomeRow(r.bandStat));
            box.Add(Legend());
            box.Add(Verdict(r));
            box.Add(Note("Gauge, headroom and lost runs are in the Solving tab."));
            return box;
        }

        private T Find<T>(string name) where T : VisualElement => _root.Q<T>(name);

        private List<(string name, LevelData level)> GatherLevels()
        {
            var list = new List<(string, LevelData)>();
            SolvingScope scope = (SolvingScope)Mathf.Clamp(_scopeDd?.index ?? 0, 0, 2);
            if (scope == SolvingScope.OpenLevel)
            {
                var l = _openLevel?.Invoke();
                if (l != null)
                    list.Add((_openLevelName?.Invoke() ?? "scratch", l));
                return list;
            }
            int from = Mathf.Min(_from?.value ?? 1, _to?.value ?? 1);
            int to   = Mathf.Max(_from?.value ?? 1, _to?.value ?? 1);
            foreach (var e in Catalog)
            {
                if (scope == SolvingScope.Range && (e.number < from || e.number > to))
                    continue;
                var l = LevelCatalog.Load(e);
                if (l != null)
                    list.Add((e.name, l));
            }
            return list;
        }

        private int CountLevels()
        {
            SolvingScope scope = (SolvingScope)Mathf.Clamp(_scopeDd?.index ?? 0, 0, 2);
            if (scope == SolvingScope.OpenLevel)
                return _openLevel?.Invoke() != null ? 1 : 0;
            if (scope == SolvingScope.AllLevels)
                return Catalog.Count;
            int from = Mathf.Min(_from?.value ?? 1, _to?.value ?? 1);
            int to   = Mathf.Max(_from?.value ?? 1, _to?.value ?? 1);
            int n = 0;
            foreach (var e in Catalog)
                if (e.number >= from && e.number <= to)
                    n++;
            return n;
        }

        // The sweep locks the editor (main thread), so the price is on screen
        // BEFORE the button is pressed.
        private void RefreshCost()
        {
            bool range = ((SolvingScope)Mathf.Clamp(_scopeDd?.index ?? 0, 0, 2)) == SolvingScope.Range;
            _from?.EnableInClassList(HiddenClass, !range);
            _to?.EnableInClassList(HiddenClass, !range);

            int levels = CountLevels();
            int runs   = Mathf.Clamp(_runs?.value ?? 1, 1, 1000);
            int bots   = SolverRoster.All.Count;
            int games  = LevelBenchmark.Cost(levels, bots, runs);
            bool expensive = false;
            foreach (var b in SolverRoster.All)
                if (b.IsExpensive)
                    expensive = true;
            string warn = expensive ? "  ·  includes a look-ahead bot — large boards take a while" : "";
            if (_cost != null)
                _cost.text = $"{levels} level × {bots} bots × {runs} runs = {games} games (editor is blocked while it runs){warn}";
        }

        private void RunSweep()
        {
            var levels = GatherLevels();
            if (levels.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing to sweep", "The chosen scope has no levels.", "OK");
                return;
            }
            int runs = Mathf.Clamp(_runs?.value ?? 30, 1, 1000);
            int seed = _seed?.value ?? 7;

            List<BenchmarkLevelResult> results;
            try
            {
                results = LevelBenchmark.RunSweep(levels, SolverRoster.All, runs, seed,
                    (p, info) => !EditorUtility.DisplayCancelableProgressBar("Solving sweep", info, p));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (results == null)
            {
                if (_summary != null)
                    _summary.text = "Sweep cancelled.";
                return;
            }

            _results = results;
            _selectedLevel = results.Count > 0 ? results[0].name : null;
            RenderLevels();
            RenderDetail();
            _onSweepFinished?.Invoke(results);
        }

        private void BuildBotFilter()
        {
            if (_botsRow == null)
                return;
            _botsRow.Clear();
            foreach (var bot in SolverRoster.All)
            {
                var id = bot.Id;
                var t = new Toggle(bot.DisplayName) { value = !_hiddenBots.Contains(id), tooltip = bot.Description };
                t.AddToClassList("cg-botchip");
                t.RegisterValueChangedCallback(e =>
                {
                    if (e.newValue)
                        _hiddenBots.Remove(id);
                    else
                        _hiddenBots.Add(id);
                    RenderDetail();
                });
                _botsRow.Add(t);
            }
        }

        private void BuildRoster()
        {
            if (_rosterCard == null)
                return;
            _rosterCard.Clear();
            foreach (var bot in SolverRoster.All)
            {
                string tag = bot.Id == SolverRoster.BandBotId ? " (band bot)" : bot.IsExpensive ? " (expensive)" : "";
                var l = new Label($"{bot.DisplayName}{tag} — {bot.Description}");
                l.AddToClassList("cg-note");
                _rosterCard.Add(l);
            }
            if (_notSim != null)
                _notSim.text = "Not simulated: the Keep Going extra-ball rescue, the undo button, scoring, and the physical arc (landing is exact). " +
                               "These are known gaps — read a win-rate anomaly with them in mind.";
        }

        private void RenderLevels()
        {
            if (_levels == null)
                return;
            _levels.Clear();

            var sorted = new List<BenchmarkLevelResult>(_results);
            switch ((SolvingSortMode)Mathf.Clamp(_sortDd?.index ?? 0, 0, 3))
            {
                case SolvingSortMode.EasiestFirst: sorted.Sort((a, b) => b.bandWinRate.CompareTo(a.bandWinRate)); break;
                case SolvingSortMode.HardestFirst: sorted.Sort((a, b) => a.bandWinRate.CompareTo(b.bandWinRate)); break;
                case SolvingSortMode.NumberAsc:    sorted.Sort((a, b) => a.number.CompareTo(b.number)); break;
                case SolvingSortMode.NumberDesc:   sorted.Sort((a, b) => b.number.CompareTo(a.number)); break;
            }

            int mismatches = 0, defects = 0;
            foreach (BenchmarkLevelResult r in _results)
            {
                if (!r.matches)
                    mismatches++;
                BotStat gate = r.Stat("gate-greedy");
                if (gate != null && gate.WinRate < 1f)
                    defects++;
            }
            if (_summary != null)
                _summary.text = _results.Count == 0 ? "No sweep yet."
                    : $"{_results.Count} level(s) · {mismatches} not at their authored band" +
                      (defects > 0 ? $" · ⚠ {defects} where the gate solver lost (level defect)" : "");

            foreach (BenchmarkLevelResult r in sorted)
            {
                BenchmarkLevelResult res = r;
                var row = new VisualElement();
                row.AddToClassList("cg-lvrow");
                row.EnableInClassList("cg-lvrow--mismatch", !r.matches);
                row.EnableInClassList("cg-lvrow--selected", r.name == _selectedLevel);
                row.RegisterCallback<ClickEvent>(_ => { _selectedLevel = res.name; RenderLevels(); RenderDetail(); });

                var name = new Label(r.name); name.AddToClassList("cg-lvrow__name"); row.Add(name);
                var band = new Label($"{LevelDifficultySchedule.Label(r.authored)} → {LevelDifficultySchedule.Label(r.measured)}{(r.matches ? "" : "  ✕")}");
                band.AddToClassList("cg-lvrow__band"); row.Add(band);

                var bar = new VisualElement(); bar.AddToClassList("cg-lvrow__bar");
                var fill = new VisualElement(); fill.AddToClassList("cg-lvrow__fill");
                fill.AddToClassList(r.bandWinRate >= 0.7f ? "cg-lvrow__fill--good" : r.bandWinRate >= 0.4f ? "cg-lvrow__fill--mid" : "cg-lvrow__fill--bad");
                fill.style.width = Length.Percent(r.bandWinRate * 100f);   // data → inline
                bar.Add(fill); row.Add(bar);

                var val = new Label($"{r.bandWinRate * 100f:0}%"); val.AddToClassList("cg-lvrow__value"); row.Add(val);
                _levels.Add(row);
            }
        }

        private void RenderDetail()
        {
            if (_detail == null || _detailCard == null)
                return;
            BenchmarkLevelResult r = _results.Find(x => x.name == _selectedLevel);
            _detailCard.EnableInClassList(HiddenClass, r == null);
            if (r == null)
                return;

            _detail.Clear();
            if (_detailHeader != null)
                _detailHeader.text = $"Bot playouts — {r.name}";

            // Status line
            _detail.Add(Note($"{r.name} · {r.targets} targets · {r.balls} balls · target band {LevelDifficultySchedule.Label(r.authored)} · band bot {SolverRoster.Find(SolverRoster.BandBotId)?.DisplayName}"));

            // Outcome rows
            _detail.Add(SectionLabel("OUTCOME PER RUN"));
            var head = new VisualElement(); head.AddToClassList("cg-outhead");
            head.Add(Fig("", "cg-outhead__name")); head.Add(Fig("", "cg-outhead__bar"));
            foreach (var h in new[] { "Won", "Out of balls", "Dead end", "Unplayable" })
                head.Add(Fig(h, "cg-outhead__fig"));
            _detail.Add(head);
            foreach (BotStat stat in r.bots)
            {
                if (_hiddenBots.Contains(stat.botId))
                    continue;
                _detail.Add(OutcomeRow(stat));
            }
            _detail.Add(Legend());

            // Band gauge — band bot only
            if (r.bandStat != null)
            {
                _detail.Add(SectionLabel($"BAND — {SolverRoster.Find(r.bandStat.botId)?.DisplayName?.ToUpperInvariant()}, WIN RATE"));
                _detail.Add(Gauge(r));
            }

            // Headroom
            if (r.bandStat != null)
            {
                _detail.Add(SectionLabel("HEADROOM"));
                _detail.Add(HeadroomRow("Balls spare on a win", r.bandStat.avgBallsLeft, r.balls, $"{r.bandStat.avgBallsLeft:0.0} of {r.balls}", "cg-lvrow__fill--good"));
                _detail.Add(HeadroomRow("Wasted throws / run",  r.bandStat.avgWasted,    r.balls, $"{r.bandStat.avgWasted:0.0}", "cg-lvrow__fill--bad"));
            }

            // Lost-run chips
            if (r.bandStat != null && r.bandStat.won < r.bandStat.runs)
            {
                _detail.Add(SectionLabel($"LOST RUNS — {r.bandStat.runs - r.bandStat.won} OF {r.bandStat.runs}"));
                _detail.Add(Chips(r.bandStat));
            }

            // Raw numbers
            var fold = new Foldout { text = "Raw numbers", value = false };
            fold.AddToClassList("cg-subfoldout");
            fold.Add(RawRow("bot", "won", "out of balls", "dead end", "unplayable", "avg shots", "avg spare", "avg wasted"));
            foreach (BotStat s in r.bots)
                fold.Add(RawRow(SolverRoster.Find(s.botId)?.DisplayName ?? s.botId,
                    Pct(s.WinRate), Pct(s.OutOfBallsRate), Pct(s.DeadEndRate), Pct(s.UnplayableRate),
                    s.avgShots.ToString("0.0"), s.avgBallsLeft.ToString("0.0"), s.avgWasted.ToString("0.0")));
            _detail.Add(fold);

            // Verdict — last, because everything above is its explanation.
            _detail.Add(Verdict(r));
        }

        private static string Pct(float v) => $"{v * 100f:0}%";

        private static Label Note(string text)
        {
            var l = new Label(text); l.AddToClassList("cg-note"); return l;
        }

        private static Label SectionLabel(string text)
        {
            var l = new Label(text); l.AddToClassList("cg-section-label"); return l;
        }

        private static Label Fig(string text, string cls)
        {
            var l = new Label(text); l.AddToClassList(cls); return l;
        }

        private static VisualElement OutcomeRow(BotStat stat)
        {
            var row = new VisualElement();
            row.AddToClassList("cg-outrow");
            bool band = stat.botId == SolverRoster.BandBotId;
            row.EnableInClassList("cg-outrow--band", band);

            var name = new Label((SolverRoster.Find(stat.botId)?.DisplayName ?? stat.botId) + (band ? "  (band)" : ""));
            name.AddToClassList("cg-outrow__name");
            row.Add(name);

            var bar = new VisualElement(); bar.AddToClassList("cg-outrow__bar");
            // The four segments must fill the track: won + outOfBalls + deadEnd + unplayable == runs.
            Debug.Assert(stat.won + stat.outOfBalls + stat.deadEnd + stat.unplayable == stat.runs, "outcomes do not sum to runs");
            bar.Add(Seg(stat.WinRate,        "cg-outrow__seg--won"));
            bar.Add(Seg(stat.OutOfBallsRate, "cg-outrow__seg--outof"));
            bar.Add(Seg(stat.DeadEndRate,    "cg-outrow__seg--deadend"));
            bar.Add(Seg(stat.UnplayableRate, "cg-outrow__seg--unplay"));
            row.Add(bar);

            row.Add(FigColored(Pct(stat.WinRate),        "cg-ok"));
            row.Add(FigColored(Pct(stat.OutOfBallsRate), "cg-warning"));
            row.Add(FigColored(Pct(stat.DeadEndRate),    "cg-danger"));
            row.Add(FigColored(Pct(stat.UnplayableRate), "cg-dim"));
            return row;
        }

        private static VisualElement Seg(float rate, string cls)
        {
            var s = new VisualElement();
            s.AddToClassList(cls);
            s.style.width = Length.Percent(rate * 100f);   // data → inline
            s.style.height = Length.Percent(100f);
            return s;
        }

        private static Label FigColored(string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList("cg-outrow__fig");
            l.AddToClassList(cls);
            return l;
        }

        private static VisualElement Legend()
        {
            var legend = new VisualElement(); legend.AddToClassList("cg-legend");
            void Item(string cls, string text)
            {
                var it = new VisualElement(); it.AddToClassList("cg-legend__item");
                var sw = new VisualElement(); sw.AddToClassList("cg-legend__swatch"); sw.AddToClassList(cls);
                var lb = new Label(text); lb.AddToClassList("cg-legend__text");
                it.Add(sw); it.Add(lb); legend.Add(it);
            }
            Item("cg-outrow__seg--won",     "won");
            Item("cg-outrow__seg--outof",   "ran out of balls → add slack or a bigger stamp");
            Item("cg-outrow__seg--deadend", "dead end → a colour became impossible; check coverage");
            Item("cg-outrow__seg--unplay",  "unplayable → no targets or no balls");
            return legend;
        }

        private static VisualElement Gauge(BenchmarkLevelResult r)
        {
            var g = new VisualElement(); g.AddToClassList("cg-gauge");
            float[] th = { LevelDifficultySchedule.HardAbove, LevelDifficultySchedule.NormalAbove, LevelDifficultySchedule.EasyAbove, LevelDifficultySchedule.VeryEasyAbove };
            string[] names = { "VERY HARD", "HARD", "NORMAL", "EASY", "VERY EASY" };
            string[] segCls = { "cg-gauge__seg--veryhard", "cg-gauge__seg--hard", "cg-gauge__seg--normal", "cg-gauge__seg--easy", "cg-gauge__seg--veryeasy" };
            var target = LevelDifficultySchedule.Target(r.authored);

            var namesRow = new VisualElement(); namesRow.AddToClassList("cg-gauge__names");
            var track    = new VisualElement(); track.AddToClassList("cg-gauge__track");
            float prev = 0f;
            for (int i = 0; i < 5; i++)
            {
                float end = i < 4 ? th[i] : 1f;
                float w = end - prev;
                var seg = new VisualElement(); seg.AddToClassList("cg-gauge__seg"); seg.AddToClassList(segCls[i]);
                seg.style.width = Length.Percent(w * 100f);
                track.Add(seg);

                // MeasuredDifficulty index: VeryEasy=0 … VeryHard=4; our i runs the other way.
                var measuredOfSeg = (MeasuredDifficulty)(4 - i);
                var nm = new Label(names[i] + (measuredOfSeg == target ? " ◂ target" : ""));
                nm.AddToClassList("cg-gauge__name");
                nm.EnableInClassList("cg-gauge__name--target", measuredOfSeg == target);
                nm.style.left  = Length.Percent(prev * 100f);
                nm.style.width = Length.Percent(w * 100f);
                namesRow.Add(nm);
                prev = end;
            }
            g.Add(namesRow);
            g.Add(track);

            var ticks = new VisualElement(); ticks.AddToClassList("cg-gauge__ticks");
            foreach (var t in th)
            {
                var tick = new VisualElement(); tick.AddToClassList("cg-gauge__tick"); tick.style.left = Length.Percent(t * 100f); ticks.Add(tick);
                var val  = new Label($"{t * 100f:0}"); val.AddToClassList("cg-gauge__tickval"); val.style.left = Length.Percent(t * 100f); ticks.Add(val);
            }
            var marker = new Label($"▼ {r.bandWinRate * 100f:0}%"); marker.AddToClassList("cg-gauge__marker");
            marker.style.left = Length.Percent(Mathf.Clamp01(r.bandWinRate) * 100f);
            ticks.Add(marker);
            g.Add(ticks);

            // "Measured Hard · 12 pts below Normal · 32 pts below the Easy target"
            float pts = r.bandWinRate * 100f;
            float targetLow = LowerBound(target) * 100f;
            string rel = pts >= targetLow ? $"{pts - targetLow:0} pts above the {LevelDifficultySchedule.Label(target)} floor"
                                          : $"{targetLow - pts:0} pts below the {LevelDifficultySchedule.Label(target)} target";
            g.Add(Note($"Measured {LevelDifficultySchedule.Label(r.measured)} · {rel}"));
            return g;
        }

        private static float LowerBound(MeasuredDifficulty d) => d switch
        {
            MeasuredDifficulty.VeryEasy => LevelDifficultySchedule.VeryEasyAbove,
            MeasuredDifficulty.Easy     => LevelDifficultySchedule.EasyAbove,
            MeasuredDifficulty.Normal   => LevelDifficultySchedule.NormalAbove,
            MeasuredDifficulty.Hard     => LevelDifficultySchedule.HardAbove,
            _                           => 0f
        };

        private static VisualElement HeadroomRow(string label, float value, float max, string figure, string fillCls)
        {
            var row = new VisualElement(); row.AddToClassList("cg-lvrow");
            var name = new Label(label); name.AddToClassList("cg-lvrow__name"); name.style.width = 148; row.Add(name);
            var bar = new VisualElement(); bar.AddToClassList("cg-lvrow__bar");
            var fill = new VisualElement(); fill.AddToClassList("cg-lvrow__fill"); fill.AddToClassList(fillCls);
            fill.style.width = Length.Percent(max > 0f ? Mathf.Clamp01(value / max) * 100f : 0f);
            bar.Add(fill); row.Add(bar);
            var fig = new Label(figure); fig.AddToClassList("cg-lvrow__value"); fig.style.width = 90; row.Add(fig);
            return row;
        }

        private static VisualElement Chips(BotStat stat)
        {
            var chips = new VisualElement(); chips.AddToClassList("cg-chips");
            int shown = 0, lost = 0;
            foreach (BenchmarkRunResult d in stat.details)
            {
                if (d.outcome == PlayoutOutcome.Won)
                    continue;
                lost++;
                if (shown >= MaxVisibleChips)
                    continue;
                var chip = new VisualElement(); chip.AddToClassList("cg-chip");
                var sw = new VisualElement(); sw.AddToClassList("cg-chip__swatch");
                sw.AddToClassList(d.outcome == PlayoutOutcome.DeadEnd ? "cg-outrow__seg--deadend" : d.outcome == PlayoutOutcome.OutOfBalls ? "cg-outrow__seg--outof" : "cg-outrow__seg--unplay");
                string reason = d.outcome == PlayoutOutcome.DeadEnd ? $"dead end at shot {d.shots}, {d.cellsLeft} cells left"
                              : d.outcome == PlayoutOutcome.OutOfBalls ? $"out of balls, {d.cellsLeft} cells left"
                              : "unplayable";
                var text = new Label($"seed {d.seed} · {reason}"); text.AddToClassList("cg-chip__text");
                chip.Add(sw); chip.Add(text); chips.Add(chip);
                shown++;
            }
            if (lost > shown)
            {
                var more = new VisualElement(); more.AddToClassList("cg-chip");
                var t = new Label($"+{lost - shown} more"); t.AddToClassList("cg-chip__text");
                more.Add(t); chips.Add(more);
            }
            return chips;
        }

        private static VisualElement RawRow(params string[] cells)
        {
            var row = new VisualElement(); row.AddToClassList("cg-raw");
            foreach (var c in cells)
                row.Add(new Label(c));
            return row;
        }

        private static Label Verdict(BenchmarkLevelResult r)
        {
            BotStat gate = r.Stat("gate-greedy");
            string text;
            string cls;
            if (gate != null && gate.WinRate < 1f)
            {
                text = $"⚠ The gate solver won only {Pct(gate.WinRate)} — this is a level defect (or an un-gated save), not difficulty. Fix the level before reading the band.";
                cls  = "cg-danger";
            }
            else if (r.matches)
            {
                text = $"Measured {LevelDifficultySchedule.Label(r.measured)} — matches the {LevelDifficultySchedule.Label(r.authored)} band level {r.number} asks for.";
                cls  = "cg-ok";
            }
            else
            {
                bool harder = (int)r.measured > (int)LevelDifficultySchedule.Target(r.authored);
                text = $"Measured {LevelDifficultySchedule.Label(r.measured)}, {(harder ? "harder" : "easier")} than the {LevelDifficultySchedule.Label(r.authored)} band this level number asks for. " +
                       (harder ? "Add slack balls, a bigger stamp, or fewer special cells." : "Remove slack, shrink stamps, or add ice / stone.");
                cls  = "cg-warning";
            }
            var l = new Label(text);
            l.AddToClassList("cg-verdict");
            l.AddToClassList(cls);
            return l;
        }
    }
}
