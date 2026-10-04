using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatapultGames.Editor
{
    // "What do I have?" — every level in Resources/Levels as a painted card,
    // filtered by band and name, paged, click-to-open. Its edge over the
    // Project window is the badge row: authored band vs measured band (after a
    // sweep), and whether the save gate would pass it.
    //
    // Lazy: nothing is read until Activate(). MarkStale() reloads only if the
    // tab has loaded before; otherwise the next Activate reads fresh anyway.
    public sealed class LevelGalleryTabController
    {
        private const int PageSize = 40;
        private const string HiddenClass = "cg-hidden";
        private readonly VisualElement _root;
        private readonly Action<LevelCatalogEntry> _openLevel;
        private TextField _search;
        private DropdownField _bandDd;
        private Label _info, _pageLabel;
        private VisualElement _cards;
        private Button _pagePrev, _pageNext;
        private bool _loaded;
        private readonly List<GalleryEntry> _entries = new List<GalleryEntry>();
        private readonly Dictionary<string, BenchmarkLevelResult> _sweep = new Dictionary<string, BenchmarkLevelResult>();
        private string _currentPath;
        private int _page;
        private static readonly string[] BandFilterNames = { "All bands", "Easy", "Normal", "Hard", "Very Hard" };

        public LevelGalleryTabController(VisualElement root, Action<LevelCatalogEntry> openLevel)
        {
            _root      = root;
            _openLevel = openLevel;
        }

        public void Bind()
        {
            _search = Find<TextField>("gallery-search");
            _search?.RegisterValueChangedCallback(_ => { _page = 0; RenderPage(); });
            _bandDd = Find<DropdownField>("gallery-band");
            if (_bandDd != null)
            {
                _bandDd.choices = new List<string>(BandFilterNames);
                _bandDd.index = 0;
                _bandDd.RegisterValueChangedCallback(_ => { _page = 0; RenderPage(); });
            }
            var refresh = Find<Button>("gallery-refresh");
            if (refresh != null)
                refresh.clicked += Reload;
            _info      = Find<Label>("gallery-info");
            _pageLabel = Find<Label>("gallery-page-label");
            _cards     = Find<VisualElement>("gallery-cards");
            _pagePrev  = Find<Button>("gallery-page-prev");
            _pageNext  = Find<Button>("gallery-page-next");
            if (_pagePrev != null)
                _pagePrev.clicked += () => { _page = Mathf.Max(0, _page - 1); RenderPage(); };
            if (_pageNext != null)
                _pageNext.clicked += () => { _page++; RenderPage(); };
        }

        // First time the tab is shown — the expensive read happens here.
        public void Activate()
        {
            if (!_loaded)
                Reload();
            else
                RenderPage();
        }

        public void MarkStale()
        {
            if (!_loaded)
                return;
            Reload();
        }

        public void SetCurrentPath(string path)
        {
            _currentPath = path;
            if (_loaded)
                RenderPage();
        }

        // Measured bands arrive from the Solving tab via the shell. They are
        // dropped on Reload: they belonged to the boards that were just rewritten,
        // and a confident "measured: Very Hard" on an unplayed level is worse
        // than no measurement.
        public void SetSweepStats(IReadOnlyList<BenchmarkLevelResult> results)
        {
            foreach (BenchmarkLevelResult r in results)
                _sweep[r.name] = r;
            if (_loaded)
                RenderPage();
        }

        private T Find<T>(string name) where T : VisualElement => _root.Q<T>(name);

        private void Reload()
        {
            _entries.Clear();
            _sweep.Clear();
            foreach (var c in LevelCatalog.Scan())
            {
                var level = LevelCatalog.Load(c);
                if (level == null)
                    continue;
                ValidationResult v = LevelValidator.Validate(level);
                AutoSolverResult s = LevelAutoSolver.Solve(level);
                _entries.Add(new GalleryEntry
                {
                    catalog  = c,
                    level    = level,
                    authored = LevelDifficultySchedule.For(c.number),
                    valid    = v.isValid,
                    solvable = s.solved,
                    targets  = LevelEditOps.CountTargets(level),
                    balls    = level.balls?.Length ?? 0,
                    ice      = LevelEditOps.CountType(level, CellType.Ice),
                    stone    = LevelEditOps.CountType(level, CellType.Stone),
                    joker    = LevelEditOps.CountType(level, CellType.Joker)
                });
            }
            _loaded = true;
            _page = 0;
            RenderPage();
        }

        private List<GalleryEntry> Filtered()
        {
            string q = (_search?.value ?? "").Trim().ToLowerInvariant();
            int band = (_bandDd?.index ?? 0) - 1;   // -1 = all
            var list = new List<GalleryEntry>();
            foreach (GalleryEntry e in _entries)
            {
                if (band >= 0 && (int)e.authored != band)
                    continue;
                if (q.Length > 0 && !e.catalog.name.ToLowerInvariant().Contains(q))
                    continue;
                list.Add(e);
            }
            return list;
        }

        // Only one page of cards is ever built. No pooling: a page rebuild is a
        // per-click cost, not a per-frame one.
        private void RenderPage()
        {
            if (_cards == null)
                return;
            _cards.Clear();
            var list  = Filtered();
            int pages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)PageSize));
            _page = Mathf.Clamp(_page, 0, pages - 1);

            if (_info != null)
                _info.text = $"{list.Count} of {_entries.Count} levels";
            if (_pageLabel != null)
                _pageLabel.text = $"{_page + 1} / {pages}";
            _pagePrev?.SetEnabled(_page > 0);
            _pageNext?.SetEnabled(_page < pages - 1);

            int start = _page * PageSize;
            for (int i = start; i < Mathf.Min(list.Count, start + PageSize); i++)
                _cards.Add(BuildCard(list[i]));
        }

        private VisualElement BuildCard(GalleryEntry e)
        {
            var card = new VisualElement();
            card.AddToClassList("cg-lvcard");
            bool current = !string.IsNullOrEmpty(_currentPath) &&
                           System.IO.Path.GetFullPath(_currentPath) == System.IO.Path.GetFullPath(e.catalog.path);
            card.EnableInClassList("cg-lvcard--current", current);
            _sweep.TryGetValue(e.catalog.name, out BenchmarkLevelResult sweep);
            card.EnableInClassList("cg-lvcard--mismatch", sweep != null && !sweep.matches);
            card.EnableInClassList("cg-lvcard--invalid",  sweep == null && !(e.valid && e.solvable));
            card.RegisterCallback<ClickEvent>(_ => _openLevel?.Invoke(e.catalog));

            var thumb = new LevelThumbnailElement();
            thumb.SetLevel(e.level);
            card.Add(thumb);

            var name = new Label(e.catalog.name); name.AddToClassList("cg-lvcard__name"); card.Add(name);

            string specials = "";
            if (e.ice > 0)
                specials += $" · {e.ice} ice";
            if (e.stone > 0)
                specials += $" · {e.stone} stone";
            if (e.joker > 0)
                specials += $" · {e.joker} joker";
            var meta = new Label($"{e.level.grid.width}×{e.level.grid.height} · {e.targets} targets · {e.balls} balls{specials}");
            meta.AddToClassList("cg-lvcard__meta"); card.Add(meta);

            var badges = new VisualElement(); badges.AddToClassList("cg-badges");
            badges.Add(Badge(LevelDifficultySchedule.Label(e.authored), "cg-badge--accent"));
            badges.Add(e.valid && e.solvable ? Badge("gate ✓", "cg-badge--ok")
                     : e.valid ? Badge("solver ✕", "cg-badge--warn") : Badge("invalid", "cg-badge--danger"));
            if (sweep != null)
                badges.Add(Badge($"measured {LevelDifficultySchedule.Label(sweep.measured)} {sweep.bandWinRate * 100f:0}%",
                                 sweep.matches ? "cg-badge--ok" : "cg-badge--danger"));
            card.Add(badges);
            return card;
        }

        private static Label Badge(string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList("cg-badge");
            l.AddToClassList(cls);
            return l;
        }
    }
}
