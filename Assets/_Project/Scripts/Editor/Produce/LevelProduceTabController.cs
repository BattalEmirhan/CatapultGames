using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatapultGames.Editor
{
    // "Produce levels N…M in bulk." Difficulty is not a user input: the runner
    // asks LevelDifficultySchedule.For(n) per level, and this tab only edits the
    // per-band RECIPE (LevelBuildSpec) — live, override-able, resettable to the
    // written preset. The point of the override is to find the right numbers
    // before writing them into LevelDifficultyBandPresets.
    //
    // Reads the tree the shell cloned; reports back only via onCatalogProduced.
    public sealed class LevelProduceTabController
    {
        private static readonly string[] BandNames = { "Easy", "Normal", "Hard", "Very Hard" };

        private readonly VisualElement _root;
        private readonly LevelProduceBandSet _bands;
        private readonly Action _onCatalogProduced;

        private IntegerField _from, _to, _seed, _attempts;
        private Toggle _overwrite;
        private Label _summary, _schedule, _bandSummary, _reportHeader;
        private DropdownField _bandDd;
        private VisualElement _report;
        private LevelDifficulty _editedBand = LevelDifficulty.Normal;
        private bool _syncing;   // guard: writing spec → fields must not fire field → spec

        // One handler per field TYPE; which spec field it is comes from the element name.
        private static readonly string[] BandIntFieldNames =
        {
            "band-width", "band-height", "band-colors", "band-min-cells",
            "band-ice", "band-stone", "band-joker", "band-slack", "band-shuffle"
        };
        private static readonly string[] BandToggleNames =
        {
            "band-shape-square", "band-shape-l", "band-shape-line",
            "band-shape-column", "band-shape-plus", "band-shape-diagonal"
        };

        public LevelProduceTabController(VisualElement root, LevelProduceBandSet bands, Action onCatalogProduced)
        {
            _root  = root;
            _bands = bands;
            _onCatalogProduced = onCatalogProduced;
        }

        private T Find<T>(string name) where T : VisualElement => _root.Q<T>(name);

        public void Bind()
        {
            _from     = Find<IntegerField>("produce-from");
            _to       = Find<IntegerField>("produce-to");
            _seed     = Find<IntegerField>("produce-seed");
            _attempts = Find<IntegerField>("produce-attempts");
            _overwrite = Find<Toggle>("produce-overwrite");
            _from?.RegisterValueChangedCallback(_ => RefreshSummary());
            _to?.RegisterValueChangedCallback(_ => RefreshSummary());
            _overwrite?.RegisterValueChangedCallback(_ => RefreshSummary());
            _summary  = Find<Label>("produce-summary");
            _schedule = Find<Label>("produce-schedule");
            var run = Find<Button>("produce-run");
            if (run != null) run.clicked += RunProduce;

            _bandDd = Find<DropdownField>("band-select");
            if (_bandDd != null)
            {
                _bandDd.choices = new List<string>(BandNames);
                _bandDd.index = (int)_editedBand;
                _bandDd.RegisterValueChangedCallback(_ => { _editedBand = (LevelDifficulty)Mathf.Clamp(_bandDd.index, 0, 3); RefreshBandUI(); });
            }
            _bandSummary = Find<Label>("band-summary");
            var reset = Find<Button>("band-reset");
            if (reset != null) reset.clicked += () => { _bands.Reset(_editedBand); RefreshBandUI(); };

            foreach (var name in BandIntFieldNames)
            {
                var n = name;
                var f = Find<IntegerField>(n);
                f?.RegisterValueChangedCallback(e => { if (!_syncing) OnBandIntChanged(n, e.newValue); });
            }
            foreach (var name in BandToggleNames)
            {
                var n = name;
                var t = Find<Toggle>(n);
                t?.RegisterValueChangedCallback(e => { if (!_syncing) OnBandToggleChanged(n, e.newValue); });
            }
            Find<Slider>("band-fill")?.RegisterValueChangedCallback(e =>
            {
                if (_syncing) return;
                var s = _bands.For(_editedBand); s.fillRatio = e.newValue; _bands.Set(_editedBand, s); RefreshBandSummary();
            });
            Find<SliderInt>("band-maxpower")?.RegisterValueChangedCallback(e =>
            {
                if (_syncing) return;
                var s = _bands.For(_editedBand); s.maxPower = e.newValue; _bands.Set(_editedBand, s); RefreshBandSummary();
            });

            _reportHeader = Find<Label>("produce-report-header");
            _report = Find<VisualElement>("produce-report");

            RefreshBandUI();
            RefreshSummary();
        }

        public void Activate() => RefreshSummary();

        // ── Band recipe editing ───────────────────────────────────────────
        // Clamp rules and "which band" are the two things worth seeing here;
        // Clamped() is the single place the ranges live.
        private void OnBandIntChanged(string name, int value)
        {
            var s = _bands.For(_editedBand);
            switch (name)
            {
                case "band-width":     s.width = value; break;
                case "band-height":    s.height = value; break;
                case "band-colors":    s.colorCount = value; break;
                case "band-min-cells": s.minCellsPerColor = value; break;
                case "band-ice":       s.iceCount = value; break;
                case "band-stone":     s.stoneCount = value; break;
                case "band-joker":     s.jokerCount = value; break;
                case "band-slack":     s.slackBalls = value; break;
                case "band-shuffle":   s.shuffleWindow = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(name), name, "No spec field mapped for this element.");
            }
            _bands.Set(_editedBand, s);
            RefreshBandSummary();
        }

        private void OnBandToggleChanged(string name, bool value)
        {
            var s = _bands.For(_editedBand);
            switch (name)
            {
                case "band-shape-square":   s.allowSquare = value; break;
                case "band-shape-l":        s.allowL = value; break;
                case "band-shape-line":     s.allowLine = value; break;
                case "band-shape-column":   s.allowColumn = value; break;
                case "band-shape-plus":     s.allowPlus = value; break;
                case "band-shape-diagonal": s.allowDiagonal = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(name), name, "No spec toggle mapped for this element.");
            }
            _bands.Set(_editedBand, s);
            RefreshBandUI();   // Clamped() may have re-enabled Square
        }

        public void RefreshBandUI()
        {
            var s = _bands.For(_editedBand);
            _syncing = true;
            try
            {
                Find<IntegerField>("band-width")?.SetValueWithoutNotify(s.width);
                Find<IntegerField>("band-height")?.SetValueWithoutNotify(s.height);
                Find<IntegerField>("band-colors")?.SetValueWithoutNotify(s.colorCount);
                Find<IntegerField>("band-min-cells")?.SetValueWithoutNotify(s.minCellsPerColor);
                Find<IntegerField>("band-ice")?.SetValueWithoutNotify(s.iceCount);
                Find<IntegerField>("band-stone")?.SetValueWithoutNotify(s.stoneCount);
                Find<IntegerField>("band-joker")?.SetValueWithoutNotify(s.jokerCount);
                Find<IntegerField>("band-slack")?.SetValueWithoutNotify(s.slackBalls);
                Find<IntegerField>("band-shuffle")?.SetValueWithoutNotify(s.shuffleWindow);
                Find<Slider>("band-fill")?.SetValueWithoutNotify(s.fillRatio);
                Find<SliderInt>("band-maxpower")?.SetValueWithoutNotify(s.maxPower);
                Find<Toggle>("band-shape-square")?.SetValueWithoutNotify(s.allowSquare);
                Find<Toggle>("band-shape-l")?.SetValueWithoutNotify(s.allowL);
                Find<Toggle>("band-shape-line")?.SetValueWithoutNotify(s.allowLine);
                Find<Toggle>("band-shape-column")?.SetValueWithoutNotify(s.allowColumn);
                Find<Toggle>("band-shape-plus")?.SetValueWithoutNotify(s.allowPlus);
                Find<Toggle>("band-shape-diagonal")?.SetValueWithoutNotify(s.allowDiagonal);
            }
            finally { _syncing = false; }
            RefreshBandSummary();
        }

        private void RefreshBandSummary()
        {
            if (_bandSummary != null)
                _bandSummary.text = $"{LevelDifficultySchedule.Label(_editedBand)}: {_bands.For(_editedBand).Summary()}";
        }

        // ── Range summary ─────────────────────────────────────────────────
        private LevelProduceRequest Request() => new LevelProduceRequest
        {
            from      = _from?.value ?? 1,
            to        = _to?.value ?? 1,
            baseSeed  = _seed?.value ?? 0,
            attempts  = _attempts?.value ?? 12,
            overwrite = _overwrite?.value ?? false
        };

        private void RefreshSummary()
        {
            var raw = Request();
            var req = raw.Normalized();
            int existing = LevelProduceRunner.CountExisting(req);
            string clamped = raw.WasClamped ? $"  (range clamped to {req.from}–{req.to})" : "";
            if (_summary != null)
                _summary.text = $"{req.Count} level(s) · {existing} already exist → {(req.overwrite ? "overwritten" : "skipped")}{clamped}";

            // Band per number, run-length encoded so a 100-level range stays one line.
            if (_schedule != null)
            {
                var sb = new StringBuilder();
                int runStart = req.from;
                var runBand  = LevelDifficultySchedule.For(req.from);
                for (int n = req.from + 1; n <= req.to + 1; n++)
                {
                    var b = n <= req.to ? LevelDifficultySchedule.For(n) : (LevelDifficulty)(-1);
                    if (n <= req.to && b == runBand) continue;
                    if (sb.Length > 0) sb.Append(" · ");
                    sb.Append(runStart == n - 1 ? $"{runStart}" : $"{runStart}–{n - 1}").Append(' ').Append(LevelDifficultySchedule.Label(runBand));
                    runStart = n; runBand = b;
                    if (sb.Length > 400) { sb.Append(" …"); break; }
                }
                _schedule.text = sb.ToString();
            }
        }

        // ── Run ───────────────────────────────────────────────────────────
        private void RunProduce()
        {
            var req = Request().Normalized();
            int existing = LevelProduceRunner.CountExisting(req);
            if (existing > 0 && req.overwrite)
            {
                if (!EditorUtility.DisplayDialog("Overwrite levels?",
                        $"{existing} existing level file(s) in {req.from}–{req.to} will be overwritten. This cannot be undone.",
                        "Overwrite", "Cancel"))
                    return;
            }

            LevelProduceReport report;
            try
            {
                report = LevelProduceRunner.Run(req, _bands,
                    (p, info) => !EditorUtility.DisplayCancelableProgressBar("Producing levels", info, p));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();   // the JSONs must be imported as TextAssets or Resources.Load will not see them
            RenderReport(report);
            RefreshSummary();
            _onCatalogProduced?.Invoke();
        }

        private void RenderReport(LevelProduceReport report)
        {
            if (_report == null) return;
            _report.Clear();
            if (_reportHeader != null)
                _reportHeader.text = $"Report — {report.written} written · {report.skipped} skipped · {report.failed} failed{(report.cancelled ? " · cancelled" : "")}";

            foreach (var row in report.rows)
            {
                var r = new VisualElement();
                r.AddToClassList("cg-report-row");
                r.AddToClassList(row.status == ProduceStatus.Written ? "cg-report-row--written"
                               : row.status == ProduceStatus.Skipped ? "cg-report-row--skipped" : "cg-report-row--failed");
                var name = new Label(row.name); name.AddToClassList("cg-report-row__name"); r.Add(name);
                var st = new Label(row.status.ToString()); st.AddToClassList("cg-report-row__status"); r.Add(st);
                var note = new Label(row.note ?? ""); note.AddToClassList("cg-report-row__note"); r.Add(note);
                _report.Add(r);
            }
        }
    }
}
