using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatapultGames.Editor
{
    // The level editor window is a SHELL: it owns the level state, the tab
    // switching and the bridges between tabs, and nothing else. The Editor tab
    // is the window's own code (it edits the state the shell holds); Gallery,
    // Produce and Solving are controllers that query the same UXML tree and
    // talk back only through callbacks handed to them here. No controller knows
    // another controller exists.
    //
    //   LevelEditorWindow
    //   ├── Editor   — this file (brushes, balls, camera, generate, AI preview, save)
    //   ├── Gallery  — Gallery/LevelGalleryTabController
    //   ├── Produce  — Produce/LevelProduceTabController
    //   └── Solving  — Solving/LevelSolvingTabController
    //
    // The whole window tree lives in UI/LevelEditorWindow.uxml; CreateGUI only
    // clones it and binds. Element names are the contract — every lookup below
    // is null-safe, so the window still opens if a control goes missing.
    public sealed class LevelEditorWindow : EditorWindow
    {
        // ── Class names used from code ────────────────────────────────────
        private const string HiddenClass       = "cg-hidden";
        private const string ActiveTabClass    = "cg-tab--active";
        private const string ActiveToolClass   = "cg-tool--active";
        private const string ActiveTypeClass   = "cg-type--active";
        private const string ActiveSwatchClass = "cg-swatch--active";
        private const string DirtyTitleClass   = "cg-titlebar__subtitle--dirty";

        private const int MaxUndo = 60;

        private static readonly string[] BallColorNames = { "Red", "Green", "Blue", "Black", "White", "Pink", "Purple" };
        private static readonly string[] BallShapeNames = { "Square", "L", "Line", "Column", "Plus", "Diagonal" };
        private static readonly string[] BandNames      = { "Easy", "Normal", "Hard", "Very Hard" };

        // ── Serialized so a domain reload gives it back ───────────────────
        [SerializeField] private string _scratchJson;   // the open level, parked as JSON across reloads
        [SerializeField] private string _savedJson;     // JSON as it was at the last load/save (dirty baseline)
        [SerializeField] private string _filePath;      // null = scratch
        [SerializeField] private int    _activeTabInt   = (int)LevelEditorTab.Editor;
        [SerializeField] private int    _cellPx         = 32;
        [SerializeField] private int    _genSeed        = 1;
        [SerializeField] private int    _genBand        = (int)LevelDifficulty.Normal;

        // ── Level state ───────────────────────────────────────────────────
        private LevelData _level = new LevelData();
        private readonly LevelCatalogBrowser _browser = new LevelCatalogBrowser();
        private readonly LevelProduceBandSet _bands   = new LevelProduceBandSet();   // shared by Generate + Produce

        private readonly List<string> _undo = new List<string>();
        private readonly List<string> _redo = new List<string>();

        // ── Tabs / controllers ────────────────────────────────────────────
        private LevelEditorTab _activeTab;
        private VisualElement _editorPanel, _galleryPanel, _producePanel, _solvingPanel;
        private Button _tabEditorButton, _tabGalleryButton, _tabProduceButton, _tabSolvingButton;
        private LevelGalleryTabController _galleryTab;
        private LevelProduceTabController _produceTab;
        private LevelSolvingTabController _solvingTab;
        private readonly Dictionary<string, LevelBenchmark.LevelResult> _lastSweep = new Dictionary<string, LevelBenchmark.LevelResult>();

        // ── Editor tab: brushes ───────────────────────────────────────────
        private LevelPaintTool _tool       = LevelPaintTool.Paint;
        private CellColor      _paintColor = CellColor.Red;
        private CellType       _paintType  = CellType.Normal;
        private int            _brushRadius = 1;
        private readonly HashSet<(int, int)> _selection = new HashSet<(int, int)>();
        private bool _rectDragging;
        private int  _rectX0, _rectY0, _rectX1, _rectY1;
        private bool _strokeDirty;      // a stroke changed something → refresh status once on commit
        private string _strokeSnapshot; // level as it was at pointer-down; pushed to undo on the first real change

        // ── Editor tab: elements ──────────────────────────────────────────
        private LevelGridElement _grid;
        private Label _targetInfo, _browserLabel, _hoverInfo, _boardInfo, _typeCounts, _selectionInfo,
                      _brushWarning, _ballCount, _ballWarning, _genRecipe, _genStatus, _playStatus,
                      _statusHeader, _savePath, _saveFresh, _importFile;
        private Button _browserPrev, _browserNext, _playBtn, _stopBtn, _resetBtn, _importClear, _undoBtn, _redoBtn;
        private VisualElement _colorSwatches, _ballList, _statusErrors, _statusWarnings, _statusRows, _statusQuick,
                              _colorRow, _typeRow, _selectionRow, _cameraDiagram;
        private SliderInt _brushRadiusSlider;
        private TextField _levelName;
        private IntegerField _gridW, _gridH, _batchCount, _genSeedField;
        private FloatField _cellSize;
        private DropdownField _batchColor, _batchShape, _genBandDd;
        private SliderInt _batchPower;
        private Slider _camTilt, _camFov, _camPadding, _camGridPos, _importThreshold, _playSpeed;
        private Vector3Field _camOffset;
        private readonly Dictionary<LevelPaintTool, Button> _toolButtons = new Dictionary<LevelPaintTool, Button>();
        private readonly Dictionary<CellType, Button> _typeButtons = new Dictionary<CellType, Button>();
        private readonly Dictionary<CellColor, VisualElement> _swatches = new Dictionary<CellColor, VisualElement>();
        private IVisualElementScheduledItem _statusItem;

        // ── AI playback ───────────────────────────────────────────────────
        private LevelAutoSolver.Result _playPlan;
        private bool _hasPlan;
        private int  _playIndex;
        private bool _isPlaying;      // stepping
        private bool _simActive;      // overlay visible (paused or finished)
        private float _playStepDelay = 0.30f;
        private readonly HashSet<(int, int)> _simFilled = new HashSet<(int, int)>();
        private readonly HashSet<(int, int)> _simHit    = new HashSet<(int, int)>();
        private int _simLandX = -1, _simLandY = -1;
        private IVisualElementScheduledItem _playItem;

        // ── Image import ──────────────────────────────────────────────────
        private string    _importPath;
        private Texture2D _importTex;
        private float     _importThresholdValue = 0.35f;

        // ═══════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═══════════════════════════════════════════════════════════════════
        // Priority 0 keeps this at the top of the CatapultGames menu, away from
        // the destructive scene rebuild (20).
        [MenuItem("CatapultGames/Level Editor", priority = 0)]
        public static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

        public void CreateGUI()
        {
            minSize = new Vector2(760, 520);
            rootVisualElement.Clear();

            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EditorConstants.LevelEditorLayoutPath);
            if (layout == null)
            {
                rootVisualElement.Add(new HelpBox($"Layout asset not found: {EditorConstants.LevelEditorLayoutPath}", HelpBoxMessageType.Error));
                return;
            }
            layout.CloneTree(rootVisualElement);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(EditorConstants.LevelEditorStylePath);
            if (sheet != null) rootVisualElement.styleSheets.Add(sheet);
            rootVisualElement.Q<VisualElement>("window-root")?.StretchToParentSize();

            RestoreScratchLevel();     // domain reload recovery, before anything reads _level

            BindTabs();                // shell first
            BindTitlebar();
            BindLevelCard();
            BindGrid();
            BindBrushCard();
            BindBallCard();
            BindGenerateCard();
            BindPlayCard();
            BindStatusCard();
            BindFileCard();

            rootVisualElement.focusable = true;   // key events (Ctrl+Z/Y/S) need a focusable target
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            _browser.Refresh(_filePath);
            RefreshEverything();
        }

        private void OnDisable()
        {
            // A domain reload disables the window too: park the scratch level as
            // JSON so CreateGUI can rebuild it. No dialog here — every recompile
            // would otherwise ask "save?".
            StopPlay();
            _scratchJson = _level != null ? LevelSerializer.ToJson(_level) : null;
            if (_importTex != null) { DestroyImmediate(_importTex); _importTex = null; }
        }

        // Unity gives a window no way to veto its own close, so this offers
        // Save / Don't save — never a Cancel it could not honour.
        private void OnDestroy()
        {
            if (_level == null || !IsDirty()) return;
            string name = string.IsNullOrEmpty(_filePath) ? "the scratch level" : Path.GetFileName(_filePath);
            if (EditorUtility.DisplayDialog("Unsaved changes", $"Save {name} before closing?", "Save", "Don't save"))
                SaveFile();
        }

        private void RestoreScratchLevel()
        {
            LevelData restored = null;
            if (!string.IsNullOrEmpty(_scratchJson))
            {
                restored = LevelSerializer.FromJson(_scratchJson);
                _scratchJson = null;
            }
            _level = restored ?? new LevelData();
            LevelEditOps.Normalize(_level);
            if (_savedJson == null) _savedJson = LevelSerializer.ToJson(_level);
            _activeTab = (LevelEditorTab)_activeTabInt;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Bind helpers
        // ═══════════════════════════════════════════════════════════════════
        private T Find<T>(string name) where T : VisualElement => rootVisualElement.Q<T>(name);

        private Button BindButton(string name, Action action)
        {
            var b = Find<Button>(name);
            if (b != null) b.clicked += action;
            return b;
        }

        private static void SetDisplayed(VisualElement e, bool shown) => e?.EnableInClassList(HiddenClass, !shown);

        private static void SetText(Label l, string text) { if (l != null) l.text = text ?? ""; }

        // ═══════════════════════════════════════════════════════════════════
        // Tab shell
        // ═══════════════════════════════════════════════════════════════════
        private void BindTabs()
        {
            _editorPanel  = Find<VisualElement>("tab-panel-editor");
            _galleryPanel = Find<VisualElement>("tab-panel-gallery");
            _producePanel = Find<VisualElement>("tab-panel-produce");
            _solvingPanel = Find<VisualElement>("tab-panel-solving");

            _tabEditorButton  = BindButton("tab-editor",  () => RequestTab(LevelEditorTab.Editor));
            _tabGalleryButton = BindButton("tab-gallery", () => RequestTab(LevelEditorTab.Gallery));
            _tabProduceButton = BindButton("tab-produce", () => RequestTab(LevelEditorTab.Produce));
            _tabSolvingButton = BindButton("tab-solving", () => RequestTab(LevelEditorTab.Solving));

            // Controllers query the same tree; they do not build their own.
            _galleryTab = new LevelGalleryTabController(rootVisualElement, OpenLevelFromGallery);
            _galleryTab.Bind();

            _produceTab = new LevelProduceTabController(rootVisualElement, _bands, OnCatalogProduced);
            _produceTab.Bind();

            _solvingTab = new LevelSolvingTabController(rootVisualElement, () => _level, () => CurrentLevelName(), OnSweepFinished);
            _solvingTab.Bind();

            SetActiveTab(_activeTab);
        }

        // The user's click — gated. Leaving the Editor tab with unsaved work is
        // the number-one way an edit silently disappears, so the question lives here.
        private void RequestTab(LevelEditorTab tab)
        {
            if (tab == _activeTab) return;
            if (_activeTab == LevelEditorTab.Editor && tab != LevelEditorTab.Editor && IsDirty())
            {
                // Switching tabs does not lose the level (it stays in memory), but the
                // Solving/Gallery views read DISK — say so once instead of blocking.
                if (!EditorUtility.DisplayDialog("Unsaved changes",
                        "The open level has unsaved changes. Other tabs read the saved files, so they will not see these edits until you save.",
                        "Switch anyway", "Stay"))
                    return;
            }
            SetActiveTab(tab);
        }

        // The mechanical move — no gate. Used at startup and after a confirmation
        // has already been taken (e.g. opening from the Gallery).
        private void SetActiveTab(LevelEditorTab tab)
        {
            _activeTab    = tab;
            _activeTabInt = (int)tab;

            SetDisplayed(_editorPanel,  tab == LevelEditorTab.Editor);
            SetDisplayed(_galleryPanel, tab == LevelEditorTab.Gallery);
            SetDisplayed(_producePanel, tab == LevelEditorTab.Produce);
            SetDisplayed(_solvingPanel, tab == LevelEditorTab.Solving);

            _tabEditorButton?.EnableInClassList(ActiveTabClass,  tab == LevelEditorTab.Editor);
            _tabGalleryButton?.EnableInClassList(ActiveTabClass, tab == LevelEditorTab.Gallery);
            _tabProduceButton?.EnableInClassList(ActiveTabClass, tab == LevelEditorTab.Produce);
            _tabSolvingButton?.EnableInClassList(ActiveTabClass, tab == LevelEditorTab.Solving);

            // Lazy: the catalog is only read when somebody looks at it.
            if (tab == LevelEditorTab.Gallery) { _galleryTab?.SetCurrentPath(_filePath); _galleryTab?.Activate(); }
            if (tab == LevelEditorTab.Solving) _solvingTab?.Activate();
            if (tab == LevelEditorTab.Produce) _produceTab?.Activate();
        }

        // ── Bridges (the window is the only thing that knows all tabs) ─────
        private void OpenLevelFromGallery(LevelCatalogEntry entry)
        {
            if (entry == null) return;
            if (!ConfirmLeavingLevel("You are opening another level from the Gallery.")) return;
            if (!LoadFromPath(entry.path)) return;
            SetActiveTab(LevelEditorTab.Editor);   // not Request: the confirmation was already taken
        }

        private void OnCatalogProduced()
        {
            _browser.Refresh(_filePath);
            _galleryTab?.MarkStale();
            _solvingTab?.InvalidateCatalog();

            // The Editor may be holding a file the run just overwrote.
            if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath) && !IsDirty())
                LoadFromPath(_filePath);
            RefreshTitlebar();
        }

        private void OnSweepFinished(IReadOnlyList<LevelBenchmark.LevelResult> results)
        {
            foreach (var r in results) _lastSweep[r.name] = r;
            _galleryTab?.SetSweepStats(results);
            RefreshStatusNow();
        }

        // ═══════════════════════════════════════════════════════════════════
        // Title bar + browser
        // ═══════════════════════════════════════════════════════════════════
        private void BindTitlebar()
        {
            _targetInfo   = Find<Label>("target-info");
            _browserLabel = Find<Label>("browser-label");
            _browserPrev  = BindButton("browser-prev", () => BrowseTo(-1));
            _browserNext  = BindButton("browser-next", () => BrowseTo(+1));
        }

        private void BrowseTo(int delta)
        {
            _browser.Refresh(_filePath);
            var target = delta < 0 ? (_browser.CanPrev ? _browser.Entries[_browser.Index - 1] : null)
                                   : (_browser.Index < 0 && _browser.Count > 0 ? _browser.Entries[0]
                                      : _browser.CanNext ? _browser.Entries[_browser.Index + 1] : null);
            if (target == null) return;
            if (!ConfirmLeavingLevel("You are moving to another level.")) return;
            LoadFromPath(target.path);
        }

        private void RefreshTitlebar()
        {
            bool dirty = IsDirty();
            string state = string.IsNullOrEmpty(_filePath)
                ? "Unsaved scratch level"
                : $"Editing {Path.GetFileNameWithoutExtension(_filePath)}";
            if (dirty) state += "  •  unsaved changes";
            SetText(_targetInfo, state);
            _targetInfo?.EnableInClassList(DirtyTitleClass, dirty);

            int idx = _browser.IndexOfPath(_filePath);
            SetText(_browserLabel, idx >= 0 ? $"{_browser.Entries[idx].name}  {idx + 1} / {_browser.Count}"
                                            : $"—  / {_browser.Count}");
            _browserPrev?.SetEnabled(idx > 0);
            _browserNext?.SetEnabled(_browser.Count > 0 && idx < _browser.Count - 1);
        }

        private string CurrentLevelName() =>
            !string.IsNullOrEmpty(_filePath) ? Path.GetFileNameWithoutExtension(_filePath)
                                             : (_level?.metadata?.levelName ?? "scratch");

        // ═══════════════════════════════════════════════════════════════════
        // Dirty tracking, gates, undo
        // ═══════════════════════════════════════════════════════════════════
        private static string Lf(string s) => s?.Replace("\r\n", "\n");

        // "Is the game playing what I am looking at?" — one format, so this is a
        // straight compare of the in-memory level with its last saved JSON.
        private bool IsDirty() => _level != null && Lf(LevelSerializer.ToJson(_level)) != Lf(_savedJson);

        // Whether the file on disk still matches what was loaded (a Produce run
        // or an external edit can change it under us).
        private bool IsDiskStale()
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath)) return false;
            var disk = LevelSerializer.Load(_filePath);
            if (disk == null) return true;
            LevelEditOps.Normalize(disk);
            return Lf(LevelSerializer.ToJson(disk)) != Lf(_savedJson);
        }

        private bool ConfirmLeavingLevel(string context)
        {
            if (!IsDirty()) return true;
            int choice = EditorUtility.DisplayDialogComplex("Unsaved changes",
                context + "\n\nThe open level has unsaved changes.", "Save", "Cancel", "Discard");
            if (choice == 1) return false;
            if (choice == 0) return SaveFile();
            return true;
        }

        private void PushUndo() => PushUndoSnapshot(LevelSerializer.ToJson(_level));

        private void PushUndoSnapshot(string json)
        {
            _undo.Add(json);
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
            _redo.Clear();
            RefreshUndoButtons();
        }

        // A stroke is one undo step, but only if it changed something: the
        // snapshot is taken at pointer-down and pushed on the first real write,
        // so clicking an already-red cell red does not eat an undo slot.
        private void BeginStroke() => _strokeSnapshot = LevelSerializer.ToJson(_level);

        private void MarkStrokeChanged()
        {
            if (!_strokeDirty && _strokeSnapshot != null) PushUndoSnapshot(_strokeSnapshot);
            _strokeDirty = true;
        }

        private void UndoEdit()
        {
            if (_undo.Count == 0) return;
            _redo.Add(LevelSerializer.ToJson(_level));
            RestoreSnapshot(_undo[_undo.Count - 1]);
            _undo.RemoveAt(_undo.Count - 1);
        }

        private void RedoEdit()
        {
            if (_redo.Count == 0) return;
            _undo.Add(LevelSerializer.ToJson(_level));
            RestoreSnapshot(_redo[_redo.Count - 1]);
            _redo.RemoveAt(_redo.Count - 1);
        }

        private void RestoreSnapshot(string json)
        {
            var l = LevelSerializer.FromJson(json);
            if (l == null) return;
            _level = l;
            LevelEditOps.Normalize(_level);
            ResetPlayback();
            RefreshEverything();
        }

        private void RefreshUndoButtons()
        {
            _undoBtn?.SetEnabled(_undo.Count > 0);
            _redoBtn?.SetEnabled(_redo.Count > 0);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_activeTab != LevelEditorTab.Editor) return;
            if (!evt.ctrlKey && !evt.commandKey) return;
            if (evt.keyCode == KeyCode.Z) { if (evt.shiftKey) RedoEdit(); else UndoEdit(); evt.StopPropagation(); }
            else if (evt.keyCode == KeyCode.Y) { RedoEdit(); evt.StopPropagation(); }
            else if (evt.keyCode == KeyCode.S) { SaveFile(); evt.StopPropagation(); }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Level card (name, size, camera)
        // ═══════════════════════════════════════════════════════════════════
        private void BindLevelCard()
        {
            _levelName = Find<TextField>("level-name");
            _levelName?.RegisterValueChangedCallback(e =>
            {
                _level.metadata.levelName = e.newValue;
                RefreshTitlebar();
                RefreshFileCard();
            });

            _gridW = Find<IntegerField>("grid-width");
            _gridH = Find<IntegerField>("grid-height");
            BindButton("grid-apply", () =>
            {
                int w = Mathf.Clamp(_gridW?.value ?? _level.grid.width, 1, 50);
                int h = Mathf.Clamp(_gridH?.value ?? _level.grid.height, 1, 50);
                if (w == _level.grid.width && h == _level.grid.height) return;
                PushUndo();
                LevelEditOps.Resize(_level, w, h);
                _selection.Clear();
                ResetPlayback();
                RefreshEverything();
            });

            _cellSize = Find<FloatField>("cell-size");
            _cellSize?.RegisterValueChangedCallback(e =>
            {
                _level.grid.cellSize = Mathf.Max(0.1f, e.newValue);
                RefreshTitlebar();
            });

            _camTilt    = BindCamSlider("cam-tilt",    v => _level.camera.tiltAngle     = v);
            _camFov     = BindCamSlider("cam-fov",     v => _level.camera.fieldOfView   = v);
            _camPadding = BindCamSlider("cam-padding", v => _level.camera.padding       = v);
            _camGridPos = BindCamSlider("cam-gridpos", v => _level.camera.gridScreenPos = v);
            _camOffset  = Find<Vector3Field>("cam-offset");
            _camOffset?.RegisterValueChangedCallback(e => { _level.camera.offset = e.newValue; RefreshTitlebar(); });
            BindButton("cam-reset", () =>
            {
                PushUndo();
                _level.camera = new CameraConfig();
                RefreshCameraCard();
                RefreshTitlebar();
            });

            _cameraDiagram = Find<VisualElement>("camera-diagram");
            if (_cameraDiagram != null) _cameraDiagram.generateVisualContent += DrawCameraDiagram;
        }

        private Slider BindCamSlider(string name, Action<float> setter)
        {
            var s = Find<Slider>(name);
            s?.RegisterValueChangedCallback(e =>
            {
                setter(e.newValue);
                _cameraDiagram?.MarkDirtyRepaint();
                RefreshTitlebar();
            });
            return s;
        }

        private void RefreshLevelCard()
        {
            _levelName?.SetValueWithoutNotify(_level.metadata.levelName ?? "");
            _gridW?.SetValueWithoutNotify(_level.grid.width);
            _gridH?.SetValueWithoutNotify(_level.grid.height);
            _cellSize?.SetValueWithoutNotify(_level.grid.cellSize);
            RefreshCameraCard();
        }

        private void RefreshCameraCard()
        {
            var cam = _level.camera ??= new CameraConfig();
            _camTilt?.SetValueWithoutNotify(cam.tiltAngle);
            _camFov?.SetValueWithoutNotify(cam.fieldOfView);
            _camPadding?.SetValueWithoutNotify(cam.padding);
            _camGridPos?.SetValueWithoutNotify(cam.gridScreenPos);
            _camOffset?.SetValueWithoutNotify(cam.offset);
            _cameraDiagram?.MarkDirtyRepaint();
        }

        // Side elevation (X = distance, Y = height) of the camera looking down at
        // the grid plane, so tilt and FOV read at a glance without pressing Play.
        private void DrawCameraDiagram(MeshGenerationContext ctx)
        {
            var cam = _level?.camera;
            if (cam == null) return;
            var p = ctx.painter2D;
            var r = _cameraDiagram.contentRect;

            float groundY = r.height - 18f;
            var gL = new Vector2(16f, groundY);
            var gR = new Vector2(r.width - 16f, groundY);
            var T  = new Vector2((gL.x + gR.x) * 0.5f, groundY);

            p.lineWidth = 3f;
            p.strokeColor = new Color(0.45f, 0.70f, 1f);
            p.BeginPath(); p.MoveTo(gL); p.LineTo(gR); p.Stroke();

            float tilt = cam.tiltAngle * Mathf.Deg2Rad;
            const float d = 62f;
            var dir = new Vector2(Mathf.Cos(tilt), -Mathf.Sin(tilt));   // screen Y is down
            var eye = T - dir * d;

            float half = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            var a = Rotate(dir, half);
            var b = Rotate(dir, -half);
            p.lineWidth = 1.5f;
            p.strokeColor = new Color(1f, 0.85f, 0.2f, 0.45f);
            p.BeginPath(); p.MoveTo(eye); p.LineTo(eye + a * (d * 1.55f)); p.Stroke();
            p.BeginPath(); p.MoveTo(eye); p.LineTo(eye + b * (d * 1.55f)); p.Stroke();

            p.lineWidth = 2f;
            p.strokeColor = new Color(1f, 0.85f, 0.2f);
            p.BeginPath(); p.MoveTo(eye); p.LineTo(T); p.Stroke();

            p.fillColor = Color.white;
            p.BeginPath(); p.Arc(eye, 4f, 0f, 360f); p.Fill();

            // Label lives in a child element so the painter stays geometry-only.
            var label = _cameraDiagram.Q<Label>("camera-diagram-label");
            if (label == null)
            {
                label = new Label { name = "camera-diagram-label", pickingMode = PickingMode.Ignore };
                label.AddToClassList("cg-note");
                label.style.position = Position.Absolute;
                label.style.left = 4; label.style.top = 2;
                _cameraDiagram.Add(label);
            }
            label.text = $"tilt {cam.tiltAngle:0}°   fov {cam.fieldOfView:0}°";
        }

        private static Vector2 Rotate(Vector2 v, float rad)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Grid
        // ═══════════════════════════════════════════════════════════════════
        private void BindGrid()
        {
            var host = Find<VisualElement>("grid-host");
            _grid = new LevelGridElement();
            host?.Add(_grid);
            _grid.SetCellSize(_cellPx);

            _grid.CellPressed     += OnCellPressed;
            _grid.CellDragged     += OnCellDragged;
            _grid.StrokeCommitted += OnStrokeCommitted;
            _grid.CellHovered     += OnCellHovered;

            var zoom = Find<SliderInt>("zoom-slider");
            zoom?.SetValueWithoutNotify(_cellPx);
            zoom?.RegisterValueChangedCallback(e => { _cellPx = e.newValue; _grid.SetCellSize(_cellPx); });

            _hoverInfo = Find<Label>("hover-info");
            _boardInfo = Find<Label>("board-info");
        }

        private void OnCellPressed(int x, int y, int button)
        {
            if (_isPlaying) return;                     // board is locked while the AI steps
            if (_simActive) ResetPlayback();            // a click to edit drops a finished preview

            BeginStroke();
            if (button == 1)
            {
                var c = LevelEditOps.Cell(_level, x, y);
                if (c != null && (c.outlineColor != CellColor.None || c.cellType != CellType.Normal))
                {
                    LevelEditOps.Erase(_level, x, y);
                    _grid.RefreshCell(x, y);
                    MarkStrokeChanged();
                }
                return;
            }
            if (button != 0) return;

            switch (_tool)
            {
                case LevelPaintTool.Paint:
                case LevelPaintTool.Erase:
                case LevelPaintTool.Brush:
                    ApplyBrushAt(x, y);
                    break;
                case LevelPaintTool.Fill:
                    int n = LevelEditOps.FloodFill(_level, x, y, _paintColor, _paintType, out string why);
                    ShowBrushWarning(why);
                    if (n > 0) { _grid.Refresh(); MarkStrokeChanged(); }
                    break;
                case LevelPaintTool.RectSelect:
                    _rectDragging = true;
                    _rectX0 = _rectX1 = x; _rectY0 = _rectY1 = y;
                    _grid.ShowRect(x, y, x, y);
                    break;
                case LevelPaintTool.MultiSelect:
                    if (!_selection.Remove((x, y))) _selection.Add((x, y));
                    _grid.SetSelection(_selection);
                    RefreshSelectionRow();
                    break;
            }
        }

        private void OnCellDragged(int x, int y)
        {
            if (_isPlaying) return;
            switch (_tool)
            {
                case LevelPaintTool.Paint:
                case LevelPaintTool.Erase:
                case LevelPaintTool.Brush:
                    ApplyBrushAt(x, y);
                    break;
                case LevelPaintTool.RectSelect:
                    if (!_rectDragging) return;
                    _rectX1 = x; _rectY1 = y;
                    _grid.ShowRect(_rectX0, _rectY0, _rectX1, _rectY1);
                    break;
            }
        }

        // Pointer-up: one stroke = one undo step (pushed at pointer-down) and one
        // status refresh, not one per dragged cell.
        private void OnStrokeCommitted()
        {
            if (_rectDragging)
            {
                _rectDragging = false;
                _grid.HideRect();
                _selection.Clear();
                int minX = Mathf.Min(_rectX0, _rectX1), maxX = Mathf.Max(_rectX0, _rectX1);
                int minY = Mathf.Min(_rectY0, _rectY1), maxY = Mathf.Max(_rectY0, _rectY1);
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (LevelEditOps.InBounds(_level, x, y)) _selection.Add((x, y));
                _grid.SetSelection(_selection);
                RefreshSelectionRow();
            }
            if (_strokeDirty)
            {
                _strokeDirty = false;
                AfterEdit();
            }
            _strokeSnapshot = null;
        }

        private void ApplyBrushAt(int x, int y)
        {
            string why = null;
            int n;
            if (_tool == LevelPaintTool.Erase)
            {
                var c = LevelEditOps.Cell(_level, x, y);
                bool had = c != null && (c.outlineColor != CellColor.None || c.cellType != CellType.Normal);
                LevelEditOps.Erase(_level, x, y);
                n = had ? 1 : 0;
            }
            else if (_tool == LevelPaintTool.Brush)
            {
                n = LevelEditOps.Brush(_level, x, y, _brushRadius - 1, _paintColor, _paintType, out why);
            }
            else
            {
                var c = LevelEditOps.Cell(_level, x, y);
                bool same = c != null && c.outlineColor == _paintColor && c.cellType == _paintType;
                n = !same && LevelEditOps.TryPaint(_level, x, y, _paintColor, _paintType, out why) ? 1 : 0;
            }
            ShowBrushWarning(why);
            if (n == 0) return;
            MarkStrokeChanged();
            if (_tool == LevelPaintTool.Brush) _grid.Refresh(); else _grid.RefreshCell(x, y);
        }

        private void OnCellHovered(int x, int y)
        {
            if (x < 0) { SetText(_hoverInfo, ""); return; }
            var c = LevelEditOps.Cell(_level, x, y);
            string t = c != null && c.cellType != CellType.Normal ? $" {c.cellType}" : "";
            string col = c != null && c.outlineColor != CellColor.None ? $" {c.outlineColor}" : "";
            SetText(_hoverInfo, $"({x},{y}){col}{t}");
        }

        // Everything that must follow a data change: title (dirty), counts,
        // validation (debounced), and dropping any AI plan built on the old board.
        private void AfterEdit()
        {
            ResetPlayback();
            RefreshTitlebar();
            RefreshBoardInfo();
            QueueStatus();
        }

        private void RefreshBoardInfo()
        {
            int targets = LevelEditOps.CountTargets(_level);
            SetText(_boardInfo, $"{_level.grid.width}×{_level.grid.height}  ·  {targets} targets  ·  {LevelEditOps.CountColors(_level)} colours  ·  {_level.balls?.Length ?? 0} balls");
            SetText(_typeCounts, TypeCountsLabel());
        }

        private string TypeCountsLabel()
        {
            int ice = LevelEditOps.CountType(_level, CellType.Ice);
            int stone = LevelEditOps.CountType(_level, CellType.Stone);
            int joker = LevelEditOps.CountType(_level, CellType.Joker);
            if (ice + stone + joker == 0) return "";
            var sb = new StringBuilder();
            if (ice > 0) sb.Append(ice).Append(" ice");
            if (stone > 0) { if (sb.Length > 0) sb.Append(" · "); sb.Append(stone).Append(" stone"); }
            if (joker > 0) { if (sb.Length > 0) sb.Append(" · "); sb.Append(joker).Append(" joker"); }
            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════════
        // Brush card
        // ═══════════════════════════════════════════════════════════════════
        private void BindBrushCard()
        {
            foreach (var tool in LevelPaintTools.All)
            {
                var t = tool;
                var b = BindButton(LevelPaintTools.ElementName(t), () => SetTool(t));
                if (b != null) _toolButtons[t] = b;
            }

            _brushRadiusSlider = Find<SliderInt>("brush-radius");
            _brushRadiusSlider?.SetValueWithoutNotify(_brushRadius);
            _brushRadiusSlider?.RegisterValueChangedCallback(e => _brushRadius = e.newValue);

            _colorRow = Find<VisualElement>("color-row");
            _typeRow  = Find<VisualElement>("type-row");
            _colorSwatches = Find<VisualElement>("color-swatches");
            BuildSwatches();

            _typeButtons[CellType.Normal] = BindButton("type-normal", () => SetType(CellType.Normal));
            _typeButtons[CellType.Ice]    = BindButton("type-ice",    () => SetType(CellType.Ice));
            _typeButtons[CellType.Stone]  = BindButton("type-stone",  () => SetType(CellType.Stone));
            _typeButtons[CellType.Joker]  = BindButton("type-joker",  () => SetType(CellType.Joker));
            _typeCounts = Find<Label>("type-counts");

            _selectionRow  = Find<VisualElement>("selection-row");
            _selectionInfo = Find<Label>("selection-info");
            BindButton("sel-paint", () =>
            {
                if (_selection.Count == 0) return;
                PushUndo();
                LevelEditOps.PaintMany(_level, _selection, _paintColor, _paintType, out string why);
                ShowBrushWarning(why);
                _grid.Refresh();
                AfterEdit();
            });
            BindButton("sel-erase", () =>
            {
                if (_selection.Count == 0) return;
                PushUndo();
                LevelEditOps.EraseMany(_level, _selection);
                _grid.Refresh();
                AfterEdit();
            });
            BindButton("sel-clear", () => { _selection.Clear(); _grid.SetSelection(_selection); RefreshSelectionRow(); });

            _brushWarning = Find<Label>("brush-warning");
            _undoBtn = BindButton("undo-btn", UndoEdit);
            _redoBtn = BindButton("redo-btn", RedoEdit);

            SetTool(_tool);
            SetType(_paintType);
            RefreshSelectionRow();
            RefreshUndoButtons();
        }

        private void BuildSwatches()
        {
            if (_colorSwatches == null) return;
            _colorSwatches.Clear();
            _swatches.Clear();
            foreach (CellColor c in Enum.GetValues(typeof(CellColor)))
            {
                if (c == CellColor.Any) continue;   // a ball-only colour (Rainbow booster), never authored
                var col = c;
                var sw = new VisualElement { tooltip = col.ToString() };
                sw.AddToClassList("cg-swatch");
                sw.style.backgroundColor = LevelCellPalette.Swatch(col);   // colour is data → inline
                sw.RegisterCallback<ClickEvent>(_ => SetColor(col));
                _colorSwatches.Add(sw);
                _swatches[col] = sw;
            }
            RefreshSwatches();
        }

        private void RefreshSwatches()
        {
            foreach (var kv in _swatches) kv.Value.EnableInClassList(ActiveSwatchClass, kv.Key == _paintColor);
        }

        private void SetTool(LevelPaintTool tool)
        {
            _tool = tool;
            if (!LevelPaintTools.IsSelection(tool)) { _selection.Clear(); _grid?.SetSelection(_selection); RefreshSelectionRow(); }
            _rectDragging = false;
            _grid?.HideRect();
            foreach (var kv in _toolButtons) kv.Value.EnableInClassList(ActiveToolClass, kv.Key == tool);
            SetDisplayed(_brushRadiusSlider, LevelPaintTools.UsesRadius(tool));
            SetDisplayed(_colorRow, LevelPaintTools.UsesColor(tool));
            SetDisplayed(_typeRow,  LevelPaintTools.UsesColor(tool));
            ShowBrushWarning(null);
        }

        private void SetColor(CellColor c)
        {
            _paintColor = c;
            RefreshSwatches();
            ShowBrushWarning(null);
        }

        private void SetType(CellType t)
        {
            _paintType = t;
            foreach (var kv in _typeButtons)
            {
                if (kv.Value == null) continue;
                bool active = kv.Key == t;
                kv.Value.EnableInClassList(ActiveTypeClass, active);
                kv.Value.style.backgroundColor = active ? LevelCellPalette.TypeTint(t) : StyleKeyword.Null;
            }
            // Stone takes no colour; grey the swatches out so the board does not lie.
            _colorSwatches?.SetEnabled(t != CellType.Stone);
            ShowBrushWarning(null);
        }

        private void ShowBrushWarning(string text)
        {
            SetText(_brushWarning, text);
            SetDisplayed(_brushWarning, !string.IsNullOrEmpty(text));
        }

        private void RefreshSelectionRow()
        {
            SetDisplayed(_selectionRow, _selection.Count > 0);
            SetText(_selectionInfo, $"{_selection.Count} selected");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Ball card
        // ═══════════════════════════════════════════════════════════════════
        private void BindBallCard()
        {
            _ballCount   = Find<Label>("ball-count");
            _ballWarning = Find<Label>("ball-warning");
            _ballList    = Find<VisualElement>("ball-list");

            BindButton("ball-shuffle", () => { PushUndo(); LevelEditOps.ShuffleBalls(_level, new System.Random()); RefreshBallList(); AfterEdit(); });
            BindButton("ball-clear", () =>
            {
                if ((_level.balls?.Length ?? 0) == 0) return;
                if (!EditorUtility.DisplayDialog("Clear queue", "Remove all balls?", "Clear", "Cancel")) return;
                PushUndo(); LevelEditOps.ClearBalls(_level); RefreshBallList(); AfterEdit();
            });

            _batchColor = Find<DropdownField>("batch-color");
            if (_batchColor != null) { _batchColor.choices = new List<string>(BallColorNames); _batchColor.index = 0; }
            _batchShape = Find<DropdownField>("batch-shape");
            if (_batchShape != null) { _batchShape.choices = new List<string>(BallShapeNames); _batchShape.index = 0; }
            _batchPower = Find<SliderInt>("batch-power");
            _batchCount = Find<IntegerField>("batch-count");
            BindButton("batch-add", () =>
            {
                var color = (CellColor)(Mathf.Max(0, _batchColor?.index ?? 0) + 1);
                var shape = (BallShape)Mathf.Max(0, _batchShape?.index ?? 0);
                int power = _batchPower?.value ?? 1;
                int count = Mathf.Clamp(_batchCount?.value ?? 1, 1, 50);
                PushUndo();
                if (!LevelEditOps.TryAddBalls(_level, color, power, shape, count, out string why)) { ShowBallWarning(why); return; }
                ShowBallWarning(null);
                RefreshBallList();
                AfterEdit();
            });
        }

        private void ShowBallWarning(string text)
        {
            SetText(_ballWarning, text);
            SetDisplayed(_ballWarning, !string.IsNullOrEmpty(text));
        }

        // Rows are DATA, so they are built in code; the static frame around them
        // is in the UXML. Rebuilt whole on every queue change (≤ 50 rows).
        private void RefreshBallList()
        {
            if (_ballList == null) return;
            _ballList.Clear();
            var balls = _level.balls ?? new BallData[0];
            SetText(_ballCount, $"Balls ({balls.Length})");

            for (int i = 0; i < balls.Length; i++)
            {
                int idx = i;
                var ball = balls[i];
                var row = new VisualElement();
                row.AddToClassList("cg-ballrow");
                if (i % 2 == 1) row.AddToClassList("cg-ballrow--alt");

                var num = new Label((i + 1).ToString());
                num.AddToClassList("cg-ballrow__idx");
                row.Add(num);

                var sw = new VisualElement();
                sw.AddToClassList("cg-ballrow__swatch");
                sw.style.backgroundColor = LevelCellPalette.Swatch(ball.color);
                row.Add(sw);

                var colorDd = new DropdownField(new List<string>(BallColorNames), Mathf.Clamp((int)ball.color - 1, 0, BallColorNames.Length - 1));
                colorDd.AddToClassList("cg-dd--color");
                colorDd.RegisterValueChangedCallback(_ =>
                {
                    PushUndo();
                    ball.color = (CellColor)(colorDd.index + 1);
                    sw.style.backgroundColor = LevelCellPalette.Swatch(ball.color);
                    AfterEdit();
                });
                row.Add(colorDd);

                var shapeDd = new DropdownField(new List<string>(BallShapeNames), Mathf.Clamp((int)ball.shape, 0, BallShapeNames.Length - 1));
                shapeDd.AddToClassList("cg-dd--shape");
                shapeDd.RegisterValueChangedCallback(_ =>
                {
                    PushUndo();
                    ball.shape = (BallShape)shapeDd.index;
                    RefreshBallList();   // power buttons enable/disable with L
                    AfterEdit();
                });
                row.Add(shapeDd);

                // Power scales every shape except L, whose arms run to the edges.
                for (int p = 1; p <= 3; p++)
                {
                    int power = p;
                    var pb = new Button(() =>
                    {
                        if (ball.powerLevel == power) return;
                        PushUndo();
                        ball.powerLevel = power;
                        RefreshBallList();
                        AfterEdit();
                    }) { text = p.ToString() };
                    pb.AddToClassList("cg-ballrow__power");
                    pb.EnableInClassList("cg-ballrow__power--active", ball.powerLevel == p);
                    pb.EnableInClassList("cg-ballrow__power--off", ball.shape == BallShape.L);
                    pb.SetEnabled(ball.shape != BallShape.L);
                    row.Add(pb);
                }

                var up = new Button(() => { PushUndo(); LevelEditOps.MoveBall(_level, idx, -1); RefreshBallList(); AfterEdit(); }) { text = "↑" };
                up.AddToClassList("cg-ballrow__btn"); up.SetEnabled(i > 0); row.Add(up);
                var down = new Button(() => { PushUndo(); LevelEditOps.MoveBall(_level, idx, +1); RefreshBallList(); AfterEdit(); }) { text = "↓" };
                down.AddToClassList("cg-ballrow__btn"); down.SetEnabled(i < balls.Length - 1); row.Add(down);
                var del = new Button(() => { PushUndo(); LevelEditOps.RemoveBall(_level, idx); RefreshBallList(); AfterEdit(); }) { text = "✕" };
                del.AddToClassList("cg-ballrow__btn"); del.AddToClassList("cg-btn--danger"); row.Add(del);

                _ballList.Add(row);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // Generate card — the same LevelBuilder the Produce tab runs
        // ═══════════════════════════════════════════════════════════════════
        private void BindGenerateCard()
        {
            _genBandDd = Find<DropdownField>("gen-band");
            if (_genBandDd != null)
            {
                _genBandDd.choices = new List<string>(BandNames);
                _genBandDd.index = Mathf.Clamp(_genBand, 0, 3);
                _genBandDd.RegisterValueChangedCallback(_ => { _genBand = _genBandDd.index; RefreshGenerateCard(); });
            }
            _genSeedField = Find<IntegerField>("gen-seed");
            _genSeedField?.SetValueWithoutNotify(_genSeed);
            _genSeedField?.RegisterValueChangedCallback(e => _genSeed = e.newValue);
            BindButton("gen-random", () => { _genSeed = UnityEngine.Random.Range(1, 999999); _genSeedField?.SetValueWithoutNotify(_genSeed); });
            _genRecipe = Find<Label>("gen-recipe");
            _genStatus = Find<Label>("gen-status");
            BindButton("gen-build", GenerateIntoEditor);
        }

        private void RefreshGenerateCard()
        {
            var band = (LevelDifficulty)Mathf.Clamp(_genBand, 0, 3);
            SetText(_genRecipe, _bands.For(band).Summary());
        }

        private void GenerateIntoEditor()
        {
            if (!ConfirmLeavingLevel("Generating replaces the open board and queue.")) return;
            var band = (LevelDifficulty)Mathf.Clamp(_genBand, 0, 3);
            var spec = _bands.For(band);
            var built = LevelBuilder.TryBuild(spec, _genSeed, 12, out string error);
            if (built == null)
            {
                SetText(_genStatus, "✕ " + error);
                _genStatus?.AddToClassList("cg-danger");
                return;
            }
            _genStatus?.RemoveFromClassList("cg-danger");
            PushUndo();
            string keepName = _level.metadata?.levelName;
            _level = built;
            LevelEditOps.Normalize(_level);
            if (!string.IsNullOrEmpty(keepName) && keepName != "Untitled") _level.metadata.levelName = keepName;
            _selection.Clear();
            ResetPlayback();
            RefreshEverything();
            SetText(_genStatus, $"✓ Generated {band} (seed {_genSeed}) — {LevelEditOps.CountTargets(_level)} targets, {_level.balls.Length} balls. Unsaved until you save.");
        }

        // ═══════════════════════════════════════════════════════════════════
        // AI preview card
        // ═══════════════════════════════════════════════════════════════════
        private void BindPlayCard()
        {
            _playBtn    = BindButton("play-btn",  StartOrResumePlay);
            _stopBtn    = BindButton("stop-btn",  PausePlay);
            _resetBtn   = BindButton("reset-btn", ResetPlayback);
            _playSpeed  = Find<Slider>("play-speed");
            _playSpeed?.RegisterValueChangedCallback(e => _playStepDelay = Mathf.Lerp(0.80f, 0.04f, e.newValue));
            _playStatus = Find<Label>("play-status");
            if (_playSpeed != null) _playStepDelay = Mathf.Lerp(0.80f, 0.04f, _playSpeed.value);
        }

        // The plan is computed once and cached; Stop pauses (overlay + cache
        // stay, Play resumes); Reset drops both. Every edit resets. This is a
        // purely visual preview of the greedy solver — it runs no runtime code.
        private void StartOrResumePlay()
        {
            if (_isPlaying) return;
            if (!_hasPlan)
            {
                _playPlan = LevelAutoSolver.Solve(_level);
                _hasPlan  = true;
                _playIndex = 0;
                _simFilled.Clear(); _simHit.Clear();
                _simLandX = _simLandY = -1;
                if (_playPlan.totalColored == 0)
                {
                    SetText(_playStatus, "No paint targets to solve.");
                    _hasPlan = false;
                    return;
                }
            }
            _simActive = true;
            _isPlaying = true;
            RefreshPlayButtons();
            _playItem?.Pause();
            _playItem = rootVisualElement.schedule.Execute(PlayTick).Every(Mathf.RoundToInt(_playStepDelay * 1000f));
        }

        private void PausePlay()
        {
            _isPlaying = false;
            _playItem?.Pause();
            RefreshPlayButtons();
            SetText(_playStatus, $"Paused at {_playIndex}/{_playPlan.moves?.Count ?? 0} balls.");
        }

        private void StopPlay()
        {
            _isPlaying = false;
            _playItem?.Pause();
        }

        private void ResetPlayback()
        {
            bool was = _simActive || _hasPlan;
            StopPlay();
            _simActive = false;
            _hasPlan   = false;
            _simFilled.Clear(); _simHit.Clear();
            _simLandX = _simLandY = -1;
            _grid?.SetPlayback(false, null, null, -1, -1);
            RefreshPlayButtons();
            if (was) SetText(_playStatus, "");
        }

        private void PlayTick()
        {
            if (!_isPlaying) return;
            var moves = _playPlan.moves;
            if (moves == null || _playIndex >= moves.Count) { FinishPlay(); return; }

            var m = moves[_playIndex];
            _simHit.Clear();
            _simLandX = m.landX; _simLandY = m.landY;
            if (!m.wasted)
            {
                if (m.hit    != null) foreach (var p in m.hit)    _simHit.Add((p.x, p.y));
                if (m.filled != null) foreach (var p in m.filled) _simFilled.Add((p.x, p.y));
            }
            _playIndex++;
            _grid.SetPlayback(true, _simFilled, _simHit, _simLandX, _simLandY);
            SetText(_playStatus, $"Playing…  {_playIndex}/{moves.Count} balls   ({_simFilled.Count}/{_playPlan.totalColored} cells)");

            // Speed slider moved mid-run → re-arm at the new cadence.
            int ms = Mathf.RoundToInt(_playStepDelay * 1000f);
            _playItem?.Pause();
            _playItem = rootVisualElement.schedule.Execute(PlayTick).Every(ms);
        }

        private void FinishPlay()
        {
            _isPlaying = false;
            _playItem?.Pause();
            _simHit.Clear(); _simLandX = _simLandY = -1;
            _grid.SetPlayback(true, _simFilled, null, -1, -1);
            RefreshPlayButtons();

            var p = _playPlan;
            if (p.solved)
            {
                int used = p.totalBalls - p.leftoverTotal;
                SetText(_playStatus, p.leftoverTotal > 0
                    ? $"✓ SOLVED — used {used}/{p.totalBalls} balls, {p.leftoverTotal} left over."
                    : $"✓ SOLVED — used all {p.totalBalls} balls.");
            }
            else
                SetText(_playStatus, $"✗ FAILED — {p.remaining}/{p.totalColored} cells left after {p.totalBalls} balls (greedy, authored order).");
        }

        private void RefreshPlayButtons()
        {
            SetDisplayed(_playBtn,  !_isPlaying);
            SetDisplayed(_stopBtn,  _isPlaying);
            SetDisplayed(_resetBtn, _simActive && !_isPlaying);
            if (_playBtn != null) _playBtn.text = _hasPlan && !_isPlaying && _simActive ? "▶ Resume" : "▶ Play";
        }

        // ═══════════════════════════════════════════════════════════════════
        // Status card (validator + solver + quick sweep pointer)
        // ═══════════════════════════════════════════════════════════════════
        private void BindStatusCard()
        {
            _statusHeader   = Find<Label>("status-header");
            _statusErrors   = Find<VisualElement>("status-errors");
            _statusWarnings = Find<VisualElement>("status-warnings");
            _statusRows     = Find<VisualElement>("status-rows");
            _statusQuick    = Find<VisualElement>("status-quick");
        }

        // Validation walks the board per ball kind; fine per click, wasteful per
        // dragged cell — so edits queue one refresh a beat later.
        private void QueueStatus()
        {
            _statusItem?.Pause();
            _statusItem = rootVisualElement.schedule.Execute(RefreshStatusNow).StartingIn(120);
        }

        private void RefreshStatusNow()
        {
            if (_statusRows == null || _statusErrors == null || _statusWarnings == null || _statusQuick == null) return;
            var v = LevelValidator.Validate(_level);
            var s = LevelAutoSolver.Solve(_level);

            bool ok = v.isValid && s.solved;
            SetText(_statusHeader, ok ? "✓ Valid — greedy solver clears it" : v.isValid ? "⚠ Valid, but the greedy solver could not clear it" : "✕ Invalid");
            _statusHeader?.EnableInClassList("cg-status__header--ok", ok);
            _statusHeader?.EnableInClassList("cg-status__header--bad", !v.isValid);

            _statusErrors.Clear();
            foreach (var e in v.globalErrors) _statusErrors.Add(Msg("✕  " + e, "cg-msg--error"));
            if (!s.solved && s.totalColored > 0)
                _statusErrors.Add(Msg($"⚠  Greedy solver left {s.remaining}/{s.totalColored} cells in the authored order.", "cg-msg--warn"));

            _statusWarnings.Clear();
            foreach (var w in v.globalWarnings) _statusWarnings.Add(Msg("⚠  " + w, "cg-msg--warn"));

            _statusRows.Clear();
            foreach (var row in v.rows)
            {
                var r = new VisualElement();
                r.AddToClassList("cg-status");
                var dot = new VisualElement();
                dot.AddToClassList("cg-status__dot");
                dot.style.backgroundColor = row.isWild ? new Color(1f, 0.95f, 0.62f) : LevelCellPalette.Swatch(row.color);
                r.Add(dot);
                var icon = new Label(row.severity == LevelValidator.Severity.OK ? "✓" : row.severity == LevelValidator.Severity.Warning ? "⚠" : "✕");
                icon.AddToClassList("cg-status__icon");
                icon.AddToClassList(row.severity == LevelValidator.Severity.OK ? "cg-ok" : row.severity == LevelValidator.Severity.Warning ? "cg-warning" : "cg-danger");
                r.Add(icon);
                var name = new Label(row.isWild ? "Joker" : row.color.ToString());
                name.AddToClassList("cg-status__name");
                r.Add(name);
                var note = new Label(row.note);
                note.AddToClassList("cg-status__note");
                r.Add(note);
                _statusRows.Add(r);
            }

            // Quick card: only the band bot's outcome bar + verdict from the last
            // sweep of THIS level; the full explanation lives in the Solving tab.
            _statusQuick.Clear();
            if (_lastSweep.TryGetValue(CurrentLevelName(), out var res))
            {
                _statusQuick.Add(LevelSolvingTabController.BuildQuickCard(res));
            }
            else
            {
                var hint = new Label("No sweep yet for this level — run one in the Solving tab to measure its difficulty.");
                hint.AddToClassList("cg-note"); hint.AddToClassList("cg-dim");
                hint.style.marginTop = 6;
                _statusQuick.Add(hint);
            }
        }

        private static Label Msg(string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList("cg-msg");
            l.AddToClassList(cls);
            return l;
        }

        // ═══════════════════════════════════════════════════════════════════
        // File card
        // ═══════════════════════════════════════════════════════════════════
        private void BindFileCard()
        {
            _savePath  = Find<Label>("save-path");
            _saveFresh = Find<Label>("save-fresh");
            BindButton("save-btn",      () => SaveFile());
            BindButton("save-as-btn",   SaveFileAs);
            BindButton("duplicate-btn", DuplicateLevel);
            BindButton("new-btn",       () => { if (ConfirmLeavingLevel("You are starting a new level.")) NewLevel(); });
            BindButton("open-btn",      OpenFile);
            BindButton("import-btn",    ImportImage);
            _importThreshold = Find<Slider>("import-threshold");
            _importThreshold?.SetValueWithoutNotify(_importThresholdValue);
            _importThreshold?.RegisterValueChangedCallback(e => { _importThresholdValue = e.newValue; if (_importTex != null) ApplyImport(); });
            _importFile  = Find<Label>("import-file");
            _importClear = BindButton("import-clear", ClearImport);
        }

        private void RefreshFileCard()
        {
            string name = _level.metadata?.levelName;
            bool hasName = !string.IsNullOrEmpty(name) && name != "Untitled";
            SetText(_savePath, !string.IsNullOrEmpty(_filePath)
                ? "→ " + Path.Combine("Resources/Levels", Path.GetFileName(_filePath))
                : hasName ? $"→ Resources/Levels/{name}.json (not saved yet)" : "Set a level name to save.");

            bool dirty = IsDirty();
            bool stale = IsDiskStale();
            string fresh = dirty ? "● Unsaved changes — the game is still loading the last saved version."
                         : stale ? "● The file on disk changed since it was opened (Produce run or external edit). Re-open to see it."
                         : string.IsNullOrEmpty(_filePath) ? "" : "✓ Saved — the game loads exactly this.";
            SetText(_saveFresh, fresh);
            _saveFresh?.EnableInClassList("cg-warning", dirty || stale);
            _saveFresh?.EnableInClassList("cg-ok", !dirty && !stale);

            SetText(_importFile, string.IsNullOrEmpty(_importPath) ? "" : Path.GetFileName(_importPath));
            SetDisplayed(_importClear, !string.IsNullOrEmpty(_importPath));
            SetDisplayed(_importThreshold, !string.IsNullOrEmpty(_importPath));
        }

        private void NewLevel()
        {
            _level = new LevelData();
            LevelEditOps.Normalize(_level);
            _filePath = null;
            _savedJson = LevelSerializer.ToJson(_level);
            _undo.Clear(); _redo.Clear();
            _selection.Clear();
            ClearImport();
            ResetPlayback();
            RefreshEverything();
        }

        private bool LoadFromPath(string path)
        {
            var loaded = LevelSerializer.Load(path);
            if (loaded == null)
            {
                EditorUtility.DisplayDialog("Load failed", $"Could not parse level file:\n{path}", "OK");
                return false;
            }
            LevelEditOps.Normalize(loaded);
            _level     = loaded;
            _filePath  = path;
            _savedJson = LevelSerializer.ToJson(_level);
            _undo.Clear(); _redo.Clear();
            _selection.Clear();
            ClearImport();
            ResetPlayback();
            _browser.Refresh(_filePath);
            RefreshEverything();
            return true;
        }

        private void OpenFile()
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Open level or import image", EditorConstants.LevelsAbsoluteFolder,
                new[] { "Level & image files", "json,png,jpg,jpeg", "Level files", "json", "Image files", "png,jpg,jpeg" });
            if (string.IsNullOrEmpty(path)) return;
            if (IsImagePath(path)) { ImportImageFromPath(path); return; }
            if (!ConfirmLeavingLevel("You are opening another level.")) return;
            LoadFromPath(path);
        }

        // Saving IS publishing (Resources/Levels is what the game loads), so the
        // validator and the greedy solver both run here as a confirmation — not a
        // hard block, since saving work in progress is legitimate.
        private bool SaveFile()
        {
            if (string.IsNullOrEmpty(_filePath)) { SaveFileAs(); return !string.IsNullOrEmpty(_filePath) && !IsDirty(); }
            if (!ConfirmSaveIfUnsound()) return false;

            // The file stem is the level's identity (LevelLoader, PlayerPrefs) —
            // keep the metadata name in step so the two never disagree.
            _level.metadata.levelName = Path.GetFileNameWithoutExtension(_filePath);
            LevelSerializer.Save(_level, _filePath);
            AssetDatabase.Refresh();   // re-import so Resources.Load sees the change
            _savedJson = LevelSerializer.ToJson(_level);
            _browser.Refresh(_filePath);
            _galleryTab?.MarkStale();
            _solvingTab?.InvalidateCatalog();
            RefreshEverything();
            Debug.Log($"[LevelEditor] Saved → {_filePath}");
            return true;
        }

        private void SaveFileAs()
        {
            string def  = !string.IsNullOrEmpty(_level.metadata.levelName) && _level.metadata.levelName != "Untitled"
                ? _level.metadata.levelName : NextFreeLevelName();
            string path = EditorUtility.SaveFilePanel("Save level", EditorConstants.LevelsAbsoluteFolder, def, "json");
            if (string.IsNullOrEmpty(path)) return;

            string prev = _filePath;
            _filePath = path;
            if (!SaveFile()) _filePath = prev;   // backed out at the gate — don't adopt the new path
        }

        private static string NextFreeLevelName()
        {
            int n = 1;
            while (LevelCatalog.Exists(n)) n++;
            return EditorConstants.LevelFileName(n);
        }

        private bool ConfirmSaveIfUnsound()
        {
            var result = LevelValidator.Validate(_level);
            var solve  = LevelAutoSolver.Solve(_level);
            if (result.isValid && solve.solved) return true;

            var sb = new StringBuilder();
            sb.AppendLine($"\"{Path.GetFileNameWithoutExtension(_filePath)}\" did not pass validation:").AppendLine();
            foreach (var err in result.globalErrors) sb.AppendLine($"  ✕  {err}");
            foreach (var row in result.rows)
                if (row.severity == LevelValidator.Severity.Error) sb.AppendLine($"  ✕  {row.color}: {row.note}");
            if (!solve.solved)
                sb.AppendLine($"  ✕  Auto-solver could not clear it — {solve.remaining} of {solve.totalColored} cells left unpainted");
            foreach (var warn in result.globalWarnings) sb.AppendLine($"  ⚠  {warn}");
            foreach (var row in result.rows)
                if (row.severity == LevelValidator.Severity.Warning) sb.AppendLine($"  ⚠  {row.color}: {row.note}");
            sb.AppendLine().Append("Save anyway?");
            return EditorUtility.DisplayDialog("Level did not validate", sb.ToString(), "Save anyway", "Cancel");
        }

        private void DuplicateLevel()
        {
            string def  = NextFreeLevelName();
            string path = EditorUtility.SaveFilePanel("Duplicate level", EditorConstants.LevelsAbsoluteFolder, def, "json");
            if (string.IsNullOrEmpty(path)) return;
            var copy = LevelSerializer.FromJson(LevelSerializer.ToJson(_level));
            copy.metadata.levelName = Path.GetFileNameWithoutExtension(path);
            LevelSerializer.Save(copy, path);
            AssetDatabase.Refresh();
            _browser.Refresh(_filePath);
            _galleryTab?.MarkStale();
            _solvingTab?.InvalidateCatalog();
            RefreshTitlebar();
            Debug.Log($"[LevelEditor] Duplicated → {path}");
        }

        // ── Image import ──────────────────────────────────────────────────
        private static bool IsImagePath(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        private void ImportImage()
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Import image", "", new[] { "Image files", "png,jpg,jpeg", "All files", "*" });
            if (!string.IsNullOrEmpty(path)) ImportImageFromPath(path);
        }

        private void ImportImageFromPath(string path)
        {
            if (_importPath != path || _importTex == null)
            {
                if (_importTex != null) DestroyImmediate(_importTex);
                _importTex = ImageImportUtility.LoadTexture(path);
                if (_importTex == null)
                {
                    EditorUtility.DisplayDialog("Import failed", $"Could not read image:\n{path}", "OK");
                    return;
                }
                _importPath = path;
            }
            PushUndo();
            ApplyImport();
        }

        // Re-applied on every threshold change — the texture is cached.
        private void ApplyImport()
        {
            if (_importTex == null) return;
            ImageImportUtility.ImportImageToGrid(_level, _importTex, _importThresholdValue);
            LevelEditOps.Normalize(_level);
            _selection.Clear();
            ResetPlayback();
            RefreshEverything();
        }

        private void ClearImport()
        {
            if (_importTex != null) DestroyImmediate(_importTex);
            _importTex  = null;
            _importPath = null;
            RefreshFileCard();
        }

        // ═══════════════════════════════════════════════════════════════════
        // Full refresh
        // ═══════════════════════════════════════════════════════════════════
        private void RefreshEverything()
        {
            if (_grid == null) return;
            _grid.SetLevel(_level);
            _grid.SetSelection(_selection);
            RefreshLevelCard();
            RefreshBallList();
            RefreshGenerateCard();
            RefreshBoardInfo();
            RefreshSelectionRow();
            RefreshTitlebar();
            RefreshFileCard();
            RefreshUndoButtons();
            RefreshPlayButtons();
            RefreshStatusNow();
        }
    }
}
