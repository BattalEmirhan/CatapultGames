using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatapultGames.Editor
{
    public class LevelEditorWindow : EditorWindow
    {
        // ─── Tool Enum ────────────────────────────────────────────────────
        protected enum EditTool { Paint, Erase, Fill, Brush, RectSelect, MultiSelect }

        // ─── State ────────────────────────────────────────────────────────
        protected LevelData  _level    = new LevelData();
        protected string     _filePath;
        protected Dictionary<(int, int), CellData> _cellDict = new();

        // ─── Display ──────────────────────────────────────────────────────
        private int     _cellPx  = 40;
        private Vector2 _scrollPos;
        private int     _pendingW;
        private int     _pendingH;
        private int     _hoverX = -1;
        private int     _hoverY = -1;

        // ─── Import ───────────────────────────────────────────────────────
        private float _importThreshold = 0.35f;

        // ─── Tools ────────────────────────────────────────────────────────
        protected EditTool  _activeTool  = EditTool.Paint;
        protected CellColor _paintColor  = CellColor.Red;
        protected int       _brushRadius = 1;

        // ─── Selection ────────────────────────────────────────────────────
        protected HashSet<(int, int)> _selection = new();
        private bool _isRectDragging;
        private int  _rectX0, _rectY0, _rectX1, _rectY1;
        private bool _mouseWasDown;

        // ─── Color swatch textures ────────────────────────────────────────
        private Texture2D[] _swatches;

        // ─── Image import cache ───────────────────────────────────────────
        private string    _lastImportedImagePath;
        private Texture2D _cachedImportTex;

        // ─── Ball queue panel ─────────────────────────────────────────────
        private Vector2 _queueScroll;
        private const float QueuePanelW = 290f;
        // Cached main-area rect. GUILayoutUtility.GetRect returns a degenerate
        // rect during the Layout pass; we cache the real one from Repaint and
        // reuse it every pass so the GUILayout-based ball panel (inside
        // BeginArea) lays out at full size instead of collapsing to nothing.
        private Rect _mainAreaRect;
        private static readonly string[] BallColorNames =
            { "Red", "Green", "Blue", "Black", "White", "Pink", "Purple" };

        // ─── Batch-add state ──────────────────────────────────────────────
        private CellColor _batchColor = CellColor.Red;
        private int       _batchPower = 1;
        private int       _batchCount = 3;
        private bool      _showReqs   = false;  // closed by default — saves vertical space
        private bool      _showCam    = false;  // per-level camera framing foldout

        // ─── AI auto-play (solvability preview) ───────────────────────────
        // Plays the level greedily so the designer can watch and see if the
        // ball queue can clear it. Uses a display-only overlay so it never
        // mutates the authored cell fill state.
        private bool                       _isPlaying;        // stepping through moves
        private bool                       _simActive;        // overlay visible (incl. final frame)
        private LevelAutoSolver.Result     _playPlan;
        private int                        _playIndex;
        private double                     _nextStepAt;
        private float                      _playStepDelay = 0.30f;
        private readonly HashSet<(int, int)> _simFilled      = new();
        private readonly HashSet<(int, int)> _simCurrentArea = new();
        private int                        _simLandX = -1, _simLandY = -1;
        private string                     _playStatus = "";

        // ─── Level browser ────────────────────────────────────────────────
        private string[] _browserPaths  = System.Array.Empty<string>();
        private string[] _browserLabels = System.Array.Empty<string>();
        private int      _browserIndex  = -1;

        // ─── Open ─────────────────────────────────────────────────────────
        [MenuItem("Window/CatapultGames/Level Editor")]
        public static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

        // ─── Lifecycle ────────────────────────────────────────────────────
        protected virtual void OnEnable()
        {
            minSize = new Vector2(540, 460);
            if (_level == null) NewLevel();
            BuildSwatches();
        }

        private void OnDisable()
        {
            EditorApplication.update -= PlayTick;   // never leave the play loop running
            _isPlaying = _simActive = false;

            if (_swatches != null)
            {
                foreach (var t in _swatches)
                    if (t) DestroyImmediate(t);
                _swatches = null;
            }
            if (_cachedImportTex != null)
            {
                DestroyImmediate(_cachedImportTex);
                _cachedImportTex = null;
            }
        }

        private void BuildSwatches()
        {
            var values = System.Enum.GetValues(typeof(CellColor));
            _swatches  = new Texture2D[values.Length];
            foreach (CellColor c in values)
            {
                Color32 c32 = GameConstants.GetColor(c);
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f, c32.a / 255f));
                tex.Apply();
                _swatches[(int)c] = tex;
            }
        }

        private void OnGUI()
        {
            if (_swatches == null) BuildSwatches();
            DrawToolbar();
            GUILayout.Space(2);
            DrawGridConfigPanel();
            GUILayout.Space(2);
            DrawCameraPanel();
            GUILayout.Space(2);
            DrawToolPanel();
            GUILayout.Space(2);
            DrawMainArea();
            DrawStatusBar();
        }

        // ─── Toolbar ──────────────────────────────────────────────────────
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("New",       EditorStyles.toolbarButton, GUILayout.Width(36)))
                if (ConfirmDiscard()) NewLevel();
            if (GUILayout.Button("Open",      EditorStyles.toolbarButton, GUILayout.Width(40)))
                OpenFile();
            if (GUILayout.Button("Save",      EditorStyles.toolbarButton, GUILayout.Width(36)))
                SaveFile();
            if (GUILayout.Button("Save As",   EditorStyles.toolbarButton, GUILayout.Width(52)))
                SaveFileAs();
            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(62)))
                DuplicateLevel();
            if (GUILayout.Button("Export ▸",  EditorStyles.toolbarButton, GUILayout.Width(58)))
                ExportToResources();

            GUILayout.Space(4);
            if (GUILayout.Button("Import Image…", EditorStyles.toolbarButton, GUILayout.Width(90)))
                ImportImage();

            // ── Level browser dropdown ────────────────────────────────────
            GUILayout.Space(6);
            if (GUILayout.Button("Levels ▼", EditorStyles.toolbarButton, GUILayout.Width(62)))
                RefreshBrowser();

            if (_browserLabels.Length > 0)
            {
                EditorGUI.BeginChangeCheck();
                int pick = EditorGUILayout.Popup(_browserIndex, _browserLabels,
                    EditorStyles.toolbarDropDown, GUILayout.Width(130));
                if (EditorGUI.EndChangeCheck() && pick >= 0 && pick < _browserPaths.Length)
                    BrowserOpen(_browserPaths[pick]);
            }

            GUILayout.Space(6);
            GUILayout.Label("Name:", EditorStyles.miniLabel, GUILayout.Width(38));
            _level.metadata.levelName = EditorGUILayout.TextField(
                _level.metadata.levelName, EditorStyles.toolbarTextField, GUILayout.Width(110));

            GUILayout.FlexibleSpace();
            GUILayout.Label("Zoom:", EditorStyles.miniLabel, GUILayout.Width(34));
            _cellPx = (int)GUILayout.HorizontalSlider(_cellPx, 16, 80, GUILayout.Width(80));
            GUILayout.Label($"{_cellPx}px", EditorStyles.miniLabel, GUILayout.Width(28));

            EditorGUILayout.EndHorizontal();
        }

        // ─── Grid Config Panel ────────────────────────────────────────────
        private void DrawGridConfigPanel()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Label("Grid:", GUILayout.Width(30));
            GUILayout.Label("W", GUILayout.Width(12));
            _pendingW = EditorGUILayout.IntField(_pendingW, GUILayout.Width(36));
            GUILayout.Label("H", GUILayout.Width(12));
            _pendingH = EditorGUILayout.IntField(_pendingH, GUILayout.Width(36));
            _pendingW = Mathf.Clamp(_pendingW, 1, 50);
            _pendingH = Mathf.Clamp(_pendingH, 1, 50);
            if (GUILayout.Button("Apply", GUILayout.Width(50))) ApplyGridSize();

            GUILayout.Space(14);
            GUILayout.Label("Cell Size:", GUILayout.Width(62));
            _level.grid.cellSize = EditorGUILayout.FloatField(_level.grid.cellSize, GUILayout.Width(40));

            GUILayout.Space(14);
            bool hasImage = !string.IsNullOrEmpty(_lastImportedImagePath);
            GUI.enabled   = hasImage;
            GUILayout.Label("Threshold:", GUILayout.Width(62));
            EditorGUI.BeginChangeCheck();
            _importThreshold = GUILayout.HorizontalSlider(_importThreshold, 0.05f, 1f, GUILayout.Width(80));
            GUILayout.Label($"{_importThreshold:F2}", GUILayout.Width(30));
            if (EditorGUI.EndChangeCheck() && hasImage)
                ApplyImportThreshold();
            GUI.enabled = true;
            if (hasImage)
            {
                GUILayout.Label(Path.GetFileName(_lastImportedImagePath),
                    EditorStyles.miniLabel, GUILayout.Width(100));
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(18)))
                    ClearImportCache();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ─── Camera Framing Panel (per level) ─────────────────────────────
        // Edits LevelData.camera — saved into the level's JSON so each level
        // carries its own grid angle / zoom / position. A side-elevation diagram
        // shows the tilt + FOV cone so the angle is readable without pressing Play.
        private void DrawCameraPanel()
        {
            var cam = _level.camera ??= new CameraConfig();   // guard old in-memory levels

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _showCam = EditorGUILayout.Foldout(_showCam, "Camera Framing (this level)", true);
            if (_showCam)
            {
                EditorGUILayout.BeginHorizontal();

                // ── Sliders ───────────────────────────────────────────────
                EditorGUILayout.BeginVertical();
                cam.tiltAngle     = EditorGUILayout.Slider("Tilt Angle",    cam.tiltAngle,     30f, 85f);
                cam.fieldOfView   = EditorGUILayout.Slider("Field of View", cam.fieldOfView,   30f, 90f);
                cam.padding       = EditorGUILayout.Slider("Padding (zoom)", cam.padding,       1f,  1.5f);
                cam.gridScreenPos = EditorGUILayout.Slider("Grid Pos (0=bot,1=top)", cam.gridScreenPos, 0f, 1f);
                cam.offset        = EditorGUILayout.Vector3Field("Pos Offset", cam.offset);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset", GUILayout.Width(56)))
                {
                    cam.tiltAngle     = 50f;
                    cam.fieldOfView   = 60f;
                    cam.padding       = 1.08f;
                    cam.gridScreenPos = 0.5f;
                    cam.offset        = Vector3.zero;
                    GUI.FocusControl(null);
                    Repaint();
                }
                GUILayout.Label("Grid Pos 0.5 = centred.  Saved per level.",
                    EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                // ── Side-elevation diagram ────────────────────────────────
                Rect dia = GUILayoutUtility.GetRect(180, 118,
                    GUILayout.Width(180), GUILayout.Height(118));
                DrawCameraDiagram(dia, cam);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        // Side view (X = horizontal distance, Y = height) of the camera looking
        // down at the grid plane, so the tilt angle and FOV are visible at a glance.
        private void DrawCameraDiagram(Rect r, CameraConfig cam)
        {
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.14f));
            if (Event.current.type != EventType.Repaint) return;

            // Grid plane (seen edge-on) near the bottom of the box.
            float groundY = r.yMax - 22f;
            Vector3 gL = new Vector3(r.x + 18f,    groundY);
            Vector3 gR = new Vector3(r.xMax - 18f, groundY);
            Vector3 T  = new Vector3((gL.x + gR.x) * 0.5f, groundY);   // look target = grid centre

            Handles.color = new Color(0.45f, 0.70f, 1f);
            Handles.DrawAAPolyLine(3f, gL, gR);

            // Camera eye derived from the tilt angle (toward grid: forward + screen-down).
            float   tilt = cam.tiltAngle * Mathf.Deg2Rad;
            const float d = 66f;
            Vector3 dir  = new Vector3(Mathf.Cos(tilt), Mathf.Sin(tilt), 0f);
            Vector3 eye  = T - dir * d;

            // FOV cone around the line of sight.
            float   half = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            Vector3 a    = Rotate2D(dir, half);
            Vector3 b    = Rotate2D(dir, -half);
            Handles.color = new Color(1f, 0.85f, 0.2f, 0.45f);
            Handles.DrawAAPolyLine(1.5f, eye, eye + a * (d * 1.55f));
            Handles.DrawAAPolyLine(1.5f, eye, eye + b * (d * 1.55f));

            // Line of sight + eye marker.
            Handles.color = new Color(1f, 0.85f, 0.2f);
            Handles.DrawAAPolyLine(2f, eye, T);
            Handles.color = Color.white;
            Handles.DrawSolidDisc(eye, Vector3.forward, 4f);

            GUI.Label(new Rect(eye.x - 6f, eye.y - 17f, 60f, 16f), "cam", EditorStyles.miniLabel);
            GUI.Label(new Rect(r.x + 4f, r.y + 2f, 170f, 16f),
                $"tilt {cam.tiltAngle:0}°   fov {cam.fieldOfView:0}°", EditorStyles.miniLabel);
        }

        private static Vector3 Rotate2D(Vector3 v, float rad)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector3(v.x * c - v.y * s, v.x * s + v.y * c, 0f);
        }

        // ─── Tool Panel ───────────────────────────────────────────────────
        private void DrawToolPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Row 1: tool buttons + brush radius + selection actions
            EditorGUILayout.BeginHorizontal();

            DrawToolButton("Paint",     EditTool.Paint);
            DrawToolButton("Erase",     EditTool.Erase);
            DrawToolButton("Fill",      EditTool.Fill);
            DrawToolButton("Brush",     EditTool.Brush);
            DrawToolButton("Rect Sel",  EditTool.RectSelect);
            DrawToolButton("Multi Sel", EditTool.MultiSelect);

            if (_activeTool == EditTool.Brush)
            {
                GUILayout.Space(8);
                GUILayout.Label("r:", GUILayout.Width(14));
                _brushRadius = EditorGUILayout.IntSlider(_brushRadius, 1, 5, GUILayout.Width(100));
            }

            GUILayout.FlexibleSpace();

            if (_selection.Count > 0)
            {
                GUILayout.Label($"{_selection.Count} sel", EditorStyles.miniLabel, GUILayout.Width(46));
                if (GUILayout.Button("Paint",    EditorStyles.miniButton, GUILayout.Width(40))) PaintSelection();
                if (GUILayout.Button("Erase",    EditorStyles.miniButton, GUILayout.Width(40))) EraseSelection();
                if (GUILayout.Button("✕ Desel",  EditorStyles.miniButton, GUILayout.Width(52)))
                { _selection.Clear(); Repaint(); }
            }

            EditorGUILayout.EndHorizontal();

            // Row 2: color swatches
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Color:", GUILayout.Width(42));
            DrawColorSwatches();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawToolButton(string label, EditTool tool)
        {
            bool active = _activeTool == tool;
            GUI.backgroundColor = active ? new Color(0.45f, 0.75f, 1f) : Color.white;
            if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(62)))
            {
                _activeTool = tool;
                if (tool != EditTool.RectSelect && tool != EditTool.MultiSelect)
                    _selection.Clear();
                _isRectDragging = false;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawColorSwatches()
        {
            if (_swatches == null) return;

            foreach (CellColor c in System.Enum.GetValues(typeof(CellColor)))
            {
                int idx = (int)c;
                if (idx >= _swatches.Length) continue;

                bool selected = _paintColor == c;
                Rect r = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(24), GUILayout.Height(24));

                // outer border (white = selected, gray = not)
                EditorGUI.DrawRect(r, selected ? Color.white : new Color(0.35f, 0.35f, 0.35f));

                Rect inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);

                if (c == CellColor.None)
                    DrawCheckerboard(inner);
                else if (_swatches[idx] != null)
                    GUI.DrawTexture(inner, _swatches[idx], ScaleMode.StretchToFill);

                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                {
                    _paintColor = c;
                    Event.current.Use();
                    Repaint();
                }

                GUILayout.Space(2);
            }
        }

        private static void DrawCheckerboard(Rect r)
        {
            Color light = new Color(0.6f, 0.6f, 0.6f);
            Color dark  = new Color(0.25f, 0.25f, 0.25f);
            float h = r.height * 0.5f, w = r.width * 0.5f;
            EditorGUI.DrawRect(new Rect(r.x,     r.y,     w, h), light);
            EditorGUI.DrawRect(new Rect(r.x + w, r.y,     w, h), dark);
            EditorGUI.DrawRect(new Rect(r.x,     r.y + h, w, h), dark);
            EditorGUI.DrawRect(new Rect(r.x + w, r.y + h, w, h), light);
        }

        // ─── Main Area (grid left + queue right) ─────────────────────────
        private void DrawMainArea()
        {
            Rect reserved = GUILayoutUtility.GetRect(1, 1,
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Only the Repaint pass yields the true rect; cache it so the Layout
            // pass (which gets a degenerate rect) reuses the last good geometry.
            // Without this the ball panel below collapses and renders blank.
            if (Event.current.type == EventType.Repaint &&
                reserved.width > 1f && reserved.height > 1f)
            {
                bool first = _mainAreaRect.width <= 1f;
                _mainAreaRect = reserved;
                if (first) Repaint();   // re-run layout once now that we have a size
            }

            Rect area = _mainAreaRect.width > 1f ? _mainAreaRect : reserved;

            float gw       = Mathf.Max(100, area.width - QueuePanelW - 2);
            Rect gridRect  = new Rect(area.x, area.y, gw, area.height);
            Rect queueRect = new Rect(area.xMax - QueuePanelW, area.y, QueuePanelW, area.height);

            DrawGrid(gridRect);

            GUILayout.BeginArea(queueRect);
            DrawBallQueuePanel(new Rect(0, 0, QueuePanelW, area.height));
            GUILayout.EndArea();

            HandleGridMouseEvent(gridRect);
        }

        private void DrawGrid(Rect viewportRect)
        {
            int totalW = _level.grid.width  * _cellPx;
            int totalH = _level.grid.height * _cellPx;
            Rect contentRect = new Rect(0, 0, totalW + 4, totalH + 4);

            _scrollPos = GUI.BeginScrollView(viewportRect, _scrollPos, contentRect);

            EditorGUI.DrawRect(contentRect, new Color(0.13f, 0.13f, 0.13f));

            for (int y = 0; y < _level.grid.height; y++)
                for (int x = 0; x < _level.grid.width; x++)
                {
                    Rect cr = new Rect(x * _cellPx + 2, y * _cellPx + 2, _cellPx - 1, _cellPx - 1);
                    DrawCell(cr, x, y);
                }

            if (_isRectDragging)
            {
                int minX = Mathf.Min(_rectX0, _rectX1);
                int minY = Mathf.Min(_rectY0, _rectY1);
                int maxX = Mathf.Max(_rectX0, _rectX1);
                int maxY = Mathf.Max(_rectY0, _rectY1);
                Rect sel = new Rect(minX * _cellPx + 2, minY * _cellPx + 2,
                                    (maxX - minX + 1) * _cellPx - 1,
                                    (maxY - minY + 1) * _cellPx - 1);
                EditorGUI.DrawRect(sel, new Color(0.3f, 0.6f, 1f, 0.28f));
            }

            GUI.EndScrollView();
        }

        // ─── Ball Queue Panel ─────────────────────────────────────────────
        private void DrawBallQueuePanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.17f, 0.17f, 0.17f));

            // ── Header ────────────────────────────────────────────────────
            int ballCount = _level.balls?.Length ?? 0;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Balls  ({ballCount} total)", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Shuffle", EditorStyles.toolbarButton, GUILayout.Width(52)))
                ShuffleBalls();
            if (GUILayout.Button("+ Add 1", EditorStyles.toolbarButton, GUILayout.Width(52)))
                AddBall();
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(40)))
                if (EditorUtility.DisplayDialog("Clear Queue", "Remove all balls?", "Clear", "Cancel"))
                { StopPlay(); _level.balls = System.Array.Empty<BallData>(); Repaint(); }
            EditorGUILayout.EndHorizontal();

            // ── AI auto-play bar ──────────────────────────────────────────
            DrawAutoPlayBar();

            // ── Batch add (top of panel — always visible) ─────────────────
            DrawBatchAdd();

            // ── Color requirements (collapsible) ──────────────────────────
            DrawColorRequirements();

            // Scrollable ball list
            _queueScroll = EditorGUILayout.BeginScrollView(_queueScroll,
                GUILayout.ExpandHeight(true));

            var balls = _level.balls ?? System.Array.Empty<BallData>();
            int toRemove   = -1;
            int toMoveUp   = -1;
            int toMoveDown = -1;

            for (int i = 0; i < balls.Length; i++)
            {
                var ball = balls[i];

                // Alternating row background
                Rect rowRect = EditorGUILayout.BeginHorizontal(
                    i % 2 == 0 ? GUIStyle.none : EditorStyles.helpBox);

                // Color swatch
                int swatchIdx = (int)ball.color;
                Rect sr = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16), GUILayout.Height(16));
                if (_swatches != null && swatchIdx < _swatches.Length && _swatches[swatchIdx] != null)
                    GUI.DrawTexture(new Rect(sr.x, sr.y + 1, 14, 14), _swatches[swatchIdx]);

                // Color dropdown (None excluded → index 0 = Red)
                int colorIdx = Mathf.Clamp((int)ball.color - 1, 0, BallColorNames.Length - 1);
                colorIdx   = EditorGUILayout.Popup(colorIdx, BallColorNames, GUILayout.Width(68));
                ball.color = (CellColor)(colorIdx + 1);

                // Shape toggle: square sizes [1][2][3] (mutually exclusive with) [L]
                for (int p = 1; p <= 3; p++)
                {
                    GUI.backgroundColor = (ball.shape == BallShape.Square && ball.powerLevel == p)
                        ? new Color(0.4f, 0.85f, 0.4f)
                        : new Color(0.7f, 0.7f, 0.7f);
                    if (GUILayout.Button(p.ToString(), EditorStyles.miniButton, GUILayout.Width(18)))
                    {
                        ball.powerLevel = p;
                        ball.shape      = BallShape.Square;
                    }
                }
                GUI.backgroundColor = ball.shape == BallShape.L
                    ? new Color(0.95f, 0.7f, 0.3f)
                    : new Color(0.7f, 0.7f, 0.7f);
                if (GUILayout.Button("L", EditorStyles.miniButton, GUILayout.Width(18)))
                    ball.shape = BallShape.L;
                GUI.backgroundColor = Color.white;

                // Reorder
                GUI.enabled = i > 0;
                if (GUILayout.Button("↑", EditorStyles.miniButton, GUILayout.Width(18))) toMoveUp = i;
                GUI.enabled = i < balls.Length - 1;
                if (GUILayout.Button("↓", EditorStyles.miniButton, GUILayout.Width(18))) toMoveDown = i;
                GUI.enabled = true;

                // Delete
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(18))) toRemove = i;
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // Apply deferred mutations
            if (toRemove   >= 0)                              RemoveBall(toRemove);
            if (toMoveUp   > 0)                               SwapBalls(toMoveUp,   toMoveUp   - 1);
            if (toMoveDown >= 0 && toMoveDown < balls.Length - 1) SwapBalls(toMoveDown, toMoveDown + 1);

            // Footer: per-color paint coverage
            DrawQueueFooter(balls);
        }

        // ── Color Requirements ────────────────────────────────────────────
        // Shows grid cell count per color vs balls that cover each color.
        // P1 ≈ 1 cell, P2 ≈ 4 cells, P3 ≈ 9 cells coverage estimate.
        private void DrawColorRequirements()
        {
            _showReqs = EditorGUILayout.Foldout(_showReqs, "Color Requirements", true);
            if (!_showReqs) return;

            // Count grid cells per color
            var cellCount = new Dictionary<CellColor, int>();
            if (_level.cells != null)
                foreach (var c in _level.cells)
                    if (c.outlineColor != CellColor.None)
                    {
                        if (!cellCount.ContainsKey(c.outlineColor)) cellCount[c.outlineColor] = 0;
                        cellCount[c.outlineColor]++;
                    }

            // Count balls per (color, powerLevel)
            var ballCount = new Dictionary<(CellColor, int), int>();
            if (_level.balls != null)
                foreach (var b in _level.balls)
                {
                    var k = (b.color, b.powerLevel);
                    if (!ballCount.ContainsKey(k)) ballCount[k] = 0;
                    ballCount[k]++;
                }

            if (cellCount.Count == 0)
            {
                EditorGUILayout.LabelField("  (no colored cells)", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // Coverage estimates per power level (P1=1, P2=4, P3=9 cells)
            int[] coverage = { 0, 1, 4, 9 };

            // Reuse one style per call — mutate color per row to avoid per-row alloc
            var nameStyle = new GUIStyle(EditorStyles.miniLabel);
            var iconStyle = new GUIStyle(EditorStyles.miniLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Color", EditorStyles.miniLabel, GUILayout.Width(52));
            GUILayout.Label("Cells", EditorStyles.miniLabel, GUILayout.Width(34));
            GUILayout.Label("P1",    EditorStyles.miniLabel, GUILayout.Width(22));
            GUILayout.Label("P2",    EditorStyles.miniLabel, GUILayout.Width(22));
            GUILayout.Label("P3",    EditorStyles.miniLabel, GUILayout.Width(22));
            GUILayout.Label("Est.",  EditorStyles.miniLabel, GUILayout.Width(28));
            GUILayout.Label("",      EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            foreach (var kv in cellCount)
            {
                CellColor col   = kv.Key;
                int       cells = kv.Value;

                int n1  = ballCount.TryGetValue((col, 1), out var c1) ? c1 : 0;
                int n2  = ballCount.TryGetValue((col, 2), out var c2) ? c2 : 0;
                int n3  = ballCount.TryGetValue((col, 3), out var c3) ? c3 : 0;
                int est = n1 * coverage[1] + n2 * coverage[2] + n3 * coverage[3];

                bool   ok      = est >= cells;
                bool   warning = !ok && est >= cells * 0.5f;
                string icon    = ok ? "✓" : (warning ? "⚠" : "✕");

                Color32 c32  = GameConstants.GetColor(col);
                Color   tint = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);
                Color   iconC = ok ? new Color(0.4f, 0.9f, 0.4f)
                                   : (warning ? new Color(1f, 0.8f, 0.1f) : new Color(1f, 0.4f, 0.4f));

                nameStyle.normal.textColor = tint;
                iconStyle.normal.textColor = iconC;

                EditorGUILayout.BeginHorizontal();
                Rect sr = GUILayoutUtility.GetRect(10, 12, GUILayout.Width(10));
                EditorGUI.DrawRect(new Rect(sr.x, sr.y + 2, 10, 10), tint);
                GUILayout.Label(col.ToString(),  nameStyle, GUILayout.Width(42));
                GUILayout.Label(cells.ToString(), EditorStyles.miniLabel, GUILayout.Width(34));
                GUILayout.Label(n1.ToString(),    EditorStyles.miniLabel, GUILayout.Width(22));
                GUILayout.Label(n2.ToString(),    EditorStyles.miniLabel, GUILayout.Width(22));
                GUILayout.Label(n3.ToString(),    EditorStyles.miniLabel, GUILayout.Width(22));
                GUILayout.Label(est.ToString(),   EditorStyles.miniLabel, GUILayout.Width(28));
                GUILayout.Label(icon, iconStyle);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        // ── Batch Add ─────────────────────────────────────────────────────
        // Row 1: color + power selector
        // Row 2: count + Add + SaveExport shortcut
        private void DrawBatchAdd()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── Row 1: Color + Power ──────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Color:", EditorStyles.miniLabel, GUILayout.Width(36));

            int bIdx = Mathf.Clamp((int)_batchColor, 0, (_swatches?.Length ?? 0) - 1);
            if (_swatches != null && bIdx >= 0 && bIdx < _swatches.Length && _swatches[bIdx] != null)
            {
                Rect sr = GUILayoutUtility.GetRect(13, 13, GUILayout.Width(13), GUILayout.Height(13));
                GUI.DrawTexture(new Rect(sr.x, sr.y + 1, 13, 13), _swatches[bIdx]);
            }
            int colorIdx = Mathf.Clamp((int)_batchColor - 1, 0, BallColorNames.Length - 1);
            colorIdx    = EditorGUILayout.Popup(colorIdx, BallColorNames, GUILayout.Width(72));
            _batchColor = (CellColor)(colorIdx + 1);

            GUILayout.Space(8);
            GUILayout.Label("Power:", EditorStyles.miniLabel, GUILayout.Width(38));
            for (int p = 1; p <= 3; p++)
            {
                GUI.backgroundColor = _batchPower == p
                    ? new Color(0.3f, 0.8f, 0.4f)
                    : new Color(0.65f, 0.65f, 0.65f);
                if (GUILayout.Button($"P{p}", EditorStyles.miniButton, GUILayout.Width(26)))
                    _batchPower = p;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // ── Row 2: Count + Add ────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Count:", EditorStyles.miniLabel, GUILayout.Width(40));
            if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(20)))
                _batchCount = Mathf.Max(1, _batchCount - 1);
            GUILayout.Label(_batchCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(24));
            if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(20)))
                _batchCount = Mathf.Min(50, _batchCount + 1);
            _batchCount = EditorGUILayout.IntField(_batchCount, GUILayout.Width(32));
            _batchCount = Mathf.Clamp(_batchCount, 1, 50);

            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(0.35f, 0.65f, 1f);
            if (GUILayout.Button($"+ Add {_batchCount}", EditorStyles.miniButton, GUILayout.Width(70)))
            {
                StopPlay();
                var list = new List<BallData>(_level.balls ?? System.Array.Empty<BallData>());
                for (int i = 0; i < _batchCount; i++)
                    list.Add(new BallData(_batchColor, _batchPower));
                _level.balls = list.ToArray();
                Repaint();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            // ── Save & Export shortcut ────────────────────────────────────
            bool hasName = !string.IsNullOrEmpty(_level.metadata.levelName) &&
                           _level.metadata.levelName != "Untitled";
            EditorGUILayout.BeginHorizontal();
            if (!hasName)
            {
                var warnStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                    { normal = { textColor = new Color(1f, 0.7f, 0.2f) } };
                GUILayout.Label("Set level name (toolbar) to export", warnStyle);
            }
            else
            {
                GUILayout.Label($"→ {_level.metadata.levelName}.json",
                    EditorStyles.centeredGreyMiniLabel);
            }
            GUILayout.FlexibleSpace();
            GUI.enabled = hasName;
            GUI.backgroundColor = hasName ? new Color(0.4f, 0.9f, 0.5f) : Color.gray;
            if (GUILayout.Button("Save & Export", EditorStyles.miniButton, GUILayout.Width(90)))
            {
                SaveFile();
                ExportToResources();
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawQueueFooter(BallData[] balls)
        {
            var result = LevelValidator.Validate(_level);

            // ── header bar: overall status ────────────────────────────────
            Color headerBg = result.isValid
                ? new Color(0.15f, 0.35f, 0.15f)
                : new Color(0.38f, 0.15f, 0.15f);
            string headerLabel = result.isValid
                ? $"✓  Valid  —  {balls.Length} balls"
                : $"✕  Invalid  —  {balls.Length} balls";

            Rect hRect = EditorGUILayout.GetControlRect(GUILayout.Height(18));
            EditorGUI.DrawRect(hRect, headerBg);
            var headerStyle = new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = Color.white }, fontStyle = UnityEngine.FontStyle.Bold };
            EditorGUI.LabelField(hRect, "  " + headerLabel, headerStyle);

            // ── global errors ─────────────────────────────────────────────
            if (result.globalErrors.Length > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var errStyle = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(1f, 0.45f, 0.45f) }, wordWrap = true };
                foreach (var err in result.globalErrors)
                    GUILayout.Label("✕  " + err, errStyle);
                EditorGUILayout.EndVertical();
            }

            // ── per-color rows ────────────────────────────────────────────
            if (result.rows.Length > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                foreach (var row in result.rows)
                {
                    Color32 c32   = GameConstants.GetColor(row.color);
                    Color   tint  = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);

                    string icon = row.severity switch
                    {
                        LevelValidator.Severity.OK      => "✓",
                        LevelValidator.Severity.Warning => "⚠",
                        _                               => "✕"
                    };
                    Color iconColor = row.severity switch
                    {
                        LevelValidator.Severity.OK      => new Color(0.4f, 0.9f, 0.4f),
                        LevelValidator.Severity.Warning => new Color(1f,   0.8f, 0.1f),
                        _                               => new Color(1f,   0.4f, 0.4f)
                    };

                    EditorGUILayout.BeginHorizontal();

                    // Colored swatch square
                    Rect sr = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10), GUILayout.Height(10));
                    EditorGUI.DrawRect(new Rect(sr.x, sr.y + 3, 10, 10), tint);

                    // Icon
                    var iconStyle = new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = iconColor } };
                    GUILayout.Label(icon, iconStyle, GUILayout.Width(14));

                    // Color name + note
                    var nameStyle = new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = tint } };
                    GUILayout.Label(row.color.ToString(), nameStyle, GUILayout.Width(46));

                    var noteStyle = new GUIStyle(EditorStyles.miniLabel)
                        { normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }, wordWrap = true };
                    GUILayout.Label(row.note, noteStyle);

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }

        // ─── Ball Queue Helpers ───────────────────────────────────────────
        private void AddBall()
        {
            StopPlay();
            var list = new List<BallData>(_level.balls ?? System.Array.Empty<BallData>());
            list.Add(new BallData(_paintColor != CellColor.None ? _paintColor : CellColor.Red, 1));
            _level.balls = list.ToArray();
            Repaint();
        }

        private void RemoveBall(int i)
        {
            StopPlay();
            var list = new List<BallData>(_level.balls);
            list.RemoveAt(i);
            _level.balls = list.ToArray();
            Repaint();
        }

        private void SwapBalls(int a, int b)
        {
            StopPlay();
            var arr = _level.balls;
            (arr[a], arr[b]) = (arr[b], arr[a]);
            Repaint();
        }

        // ─── Shuffle ──────────────────────────────────────────────────────
        // Fisher-Yates shuffle of the whole ball queue.
        private void ShuffleBalls()
        {
            StopPlay();
            var arr = _level.balls;
            if (arr == null || arr.Length < 2) return;
            var rng = new System.Random();
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            Repaint();
        }

        // ─── AI Auto-play ─────────────────────────────────────────────────
        private void DrawAutoPlayBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            if (!_isPlaying)
            {
                GUI.backgroundColor = new Color(0.40f, 0.85f, 0.45f);
                if (GUILayout.Button("▶ Play (AI)", EditorStyles.miniButton, GUILayout.Width(80)))
                    StartPlay();
            }
            else
            {
                GUI.backgroundColor = new Color(1f, 0.55f, 0.40f);
                if (GUILayout.Button("■ Stop", EditorStyles.miniButton, GUILayout.Width(80)))
                    StopPlay();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Label("Speed", EditorStyles.miniLabel, GUILayout.Width(38));
            float speed = 1f - Mathf.InverseLerp(0.04f, 0.80f, _playStepDelay);   // 0 slow → 1 fast
            speed = GUILayout.HorizontalSlider(speed, 0f, 1f, GUILayout.Width(64));
            _playStepDelay = Mathf.Lerp(0.80f, 0.04f, speed);

            if (_simActive && !_isPlaying &&
                GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(46)))
                StopPlay();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_playStatus))
            {
                bool done = _simActive && !_isPlaying;
                Color c = !done            ? new Color(0.80f, 0.80f, 0.80f)
                        : _playPlan.solved ? new Color(0.45f, 0.90f, 0.45f)
                                           : new Color(1f,    0.50f, 0.50f);
                var st = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = c }, wordWrap = true };
                GUILayout.Label(_playStatus, st);
            }
        }

        private void StartPlay()
        {
            if (_isPlaying) return;

            SyncCells();   // make sure _level.cells reflects in-editor paint edits
            _playPlan = LevelAutoSolver.Solve(_level);

            _simFilled.Clear();
            _simCurrentArea.Clear();
            _simLandX = _simLandY = -1;
            _playIndex = 0;

            if (_playPlan.totalColored == 0)
            {
                _simActive  = false;
                _isPlaying  = false;
                _playStatus = "No colored cells to solve.";
                Repaint();
                return;
            }

            _simActive  = true;
            _isPlaying  = true;
            _playStatus = $"Playing…  0/{_playPlan.moves.Count}";
            _nextStepAt = EditorApplication.timeSinceStartup + _playStepDelay;
            EditorApplication.update -= PlayTick;   // guard against double-registration
            EditorApplication.update += PlayTick;
            Repaint();
        }

        // Cancels playback (if any) and clears the display-only overlay.
        private void StopPlay()
        {
            bool was = _isPlaying || _simActive;
            _isPlaying = false;
            _simActive = false;
            EditorApplication.update -= PlayTick;
            _simFilled.Clear();
            _simCurrentArea.Clear();
            _simLandX = _simLandY = -1;
            if (was) { _playStatus = ""; Repaint(); }
        }

        private void FinishPlay()
        {
            _isPlaying = false;
            EditorApplication.update -= PlayTick;
            _simCurrentArea.Clear();
            _simLandX = _simLandY = -1;

            var p = _playPlan;
            if (p.solved)
            {
                int used = p.totalBalls - p.leftoverTotal;
                _playStatus = p.leftoverTotal > 0
                    ? $"✓ SOLVED — used {used}/{p.totalBalls} balls.  Leftover ({p.leftoverTotal}): {FormatLeftover(p.leftover)}"
                    : $"✓ SOLVED — used all {p.totalBalls} balls, none left over.";
            }
            else
            {
                _playStatus = $"✗ FAILED — {p.remaining}/{p.totalColored} cells left after {p.totalBalls} balls (greedy AI).";
            }
            Repaint();
        }

        // "Red P1(2×2)×3, Blue P2(3×3)×2" — leftover balls by colour, power and paint size.
        private static string FormatLeftover(LevelAutoSolver.BallCount[] groups)
        {
            if (groups == null || groups.Length == 0) return "—";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < groups.Length; i++)
            {
                var g  = groups[i];
                int sz = GameConstants.GetPaintSize(g.power);
                if (i > 0) sb.Append(", ");
                sb.Append(g.color).Append(" P").Append(g.power)
                  .Append('(').Append(sz).Append('×').Append(sz).Append(")×").Append(g.count);
            }
            return sb.ToString();
        }

        // Driven by EditorApplication.update — advances one ball per step delay.
        private void PlayTick()
        {
            if (!_isPlaying) return;
            if (EditorApplication.timeSinceStartup < _nextStepAt) return;

            var moves = _playPlan.moves;
            if (moves == null || _playIndex >= moves.Count) { FinishPlay(); return; }

            var m = moves[_playIndex];
            _simCurrentArea.Clear();
            _simLandX = m.landX;
            _simLandY = m.landY;
            if (!m.wasted && m.filled != null)
                foreach (var pt in m.filled)
                {
                    _simFilled.Add((pt.x, pt.y));
                    _simCurrentArea.Add((pt.x, pt.y));
                }

            _playIndex++;
            _playStatus = $"Playing…  {_playIndex}/{moves.Count} balls   " +
                          $"({_simFilled.Count}/{_playPlan.totalColored} cells)";
            _nextStepAt = EditorApplication.timeSinceStartup + _playStepDelay;
            Repaint();
        }

        // ─── Cell Rendering ───────────────────────────────────────────────
        private void DrawCell(Rect rect, int x, int y)
        {
            _cellDict.TryGetValue((x, y), out var cell);
            CellColor color = cell?.outlineColor ?? CellColor.None;
            // During AI playback the overlay marks cells the AI has filled.
            bool filled     = (cell?.isFilled ?? false) ||
                              (_simActive && _simFilled.Contains((x, y)));

            if (color == CellColor.None)
            {
                EditorGUI.DrawRect(rect, new Color(0.22f, 0.22f, 0.22f));
                float cx = rect.x + rect.width  * 0.5f - 1;
                float cy = rect.y + rect.height * 0.5f - 1;
                EditorGUI.DrawRect(new Rect(cx, cy, 2, 2), new Color(0.3f, 0.3f, 0.3f));
            }
            else
            {
                Color32 c32 = GameConstants.GetColor(color);
                Color   col = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);

                if (filled)
                {
                    EditorGUI.DrawRect(rect, col);
                }
                else
                {
                    int border = Mathf.Max(2, _cellPx / 10);
                    EditorGUI.DrawRect(rect, col);
                    EditorGUI.DrawRect(new Rect(rect.x + border, rect.y + border,
                                                rect.width  - border * 2,
                                                rect.height - border * 2),
                                       new Color(0.17f, 0.17f, 0.17f));
                }
            }

            if (_selection.Contains((x, y)))
                EditorGUI.DrawRect(rect, new Color(0.3f, 0.6f, 1f, 0.35f));

            // AI playback overlays: flash the just-painted block + mark the landing cell.
            if (_simActive)
            {
                if (_simCurrentArea.Contains((x, y)))
                    EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.45f));
                if (x == _simLandX && y == _simLandY)
                    EditorGUI.DrawRect(rect, new Color(1f, 0.85f, 0.10f, 0.55f));
            }

            if (x == _hoverX && y == _hoverY)
                EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.10f));
        }

        // ─── Mouse / Tool Dispatch ────────────────────────────────────────
        protected virtual void HandleGridMouseEvent(Rect viewportRect)
        {
            Event e = Event.current;

            // Grid editing is locked while the AI is stepping through a level.
            if (_isPlaying) { _hoverX = _hoverY = -1; return; }

            // A click to edit clears a finished AI preview so the board is live again.
            if (_simActive && e.type == EventType.MouseDown && e.button == 0 &&
                viewportRect.Contains(e.mousePosition))
                StopPlay();

            // Outside the scroll view, e.mousePosition is in window space.
            // Convert to content space: subtract viewport origin, add scroll offset.
            Vector2 content = e.mousePosition - viewportRect.position + _scrollPos;
            int cx = Mathf.FloorToInt((content.x - 2) / _cellPx);
            int cy = Mathf.FloorToInt((content.y - 2) / _cellPx);
            bool inside = viewportRect.Contains(e.mousePosition) &&
                          cx >= 0 && cx < _level.grid.width &&
                          cy >= 0 && cy < _level.grid.height;

            _hoverX = inside ? cx : -1;
            _hoverY = inside ? cy : -1;

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    _mouseWasDown = true;
                    if (inside) { OnCellAction(cx, cy, first: true); e.Use(); }
                    break;

                case EventType.MouseDrag when e.button == 0 && _mouseWasDown:
                    if (inside) OnCellAction(cx, cy, first: false);
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseUp when e.button == 0:
                    if (_mouseWasDown)
                    {
                        if (_isRectDragging) CommitRectSelect();
                        _mouseWasDown   = false;
                        _isRectDragging = false;
                    }
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseMove:
                    Repaint();
                    break;
            }
        }

        private void OnCellAction(int x, int y, bool first)
        {
            switch (_activeTool)
            {
                case EditTool.Paint:
                    SetCell(x, y, _paintColor);
                    break;

                case EditTool.Erase:
                    SetCell(x, y, CellColor.None);
                    break;

                case EditTool.Fill:
                    if (first) FloodFill(x, y);
                    break;

                case EditTool.Brush:
                    ApplyBrush(x, y);
                    break;

                case EditTool.RectSelect:
                    if (first) { _rectX0 = _rectX1 = x; _rectY0 = _rectY1 = y; _isRectDragging = true; }
                    else        { _rectX1 = x; _rectY1 = y; }
                    break;

                case EditTool.MultiSelect:
                    if (first)
                    {
                        if (_selection.Contains((x, y))) _selection.Remove((x, y));
                        else _selection.Add((x, y));
                        Repaint();
                    }
                    break;
            }
        }

        // ─── Tool Implementations ─────────────────────────────────────────
        private void SetCell(int x, int y, CellColor color)
        {
            if (!_cellDict.TryGetValue((x, y), out var cell))
            {
                cell = new CellData(x, y, color);
                _cellDict[(x, y)] = cell;
            }
            else
            {
                cell.outlineColor = color;
            }
            SyncCells();
            Repaint();
        }

        private void ApplyBrush(int cx, int cy)
        {
            for (int dy = -_brushRadius; dy <= _brushRadius; dy++)
                for (int dx = -_brushRadius; dx <= _brushRadius; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || x >= _level.grid.width || y < 0 || y >= _level.grid.height) continue;
                    if (_cellDict.TryGetValue((x, y), out var cell))
                        cell.outlineColor = _paintColor;
                    else
                        _cellDict[(x, y)] = new CellData(x, y, _paintColor);
                }
            SyncCells();
            Repaint();
        }

        private void FloodFill(int startX, int startY)
        {
            if (!_cellDict.TryGetValue((startX, startY), out var start)) return;
            CellColor target = start.outlineColor;
            if (target == _paintColor) return;

            var queue   = new Queue<(int, int)>();
            var visited = new HashSet<(int, int)>();
            queue.Enqueue((startX, startY));
            visited.Add((startX, startY));

            int[] ddx = {  0,  0, -1, 1 };
            int[] ddy = { -1,  1,  0, 0 };

            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                if (_cellDict.TryGetValue((x, y), out var cell))
                    cell.outlineColor = _paintColor;

                for (int d = 0; d < 4; d++)
                {
                    int nx = x + ddx[d], ny = y + ddy[d];
                    if (nx < 0 || nx >= _level.grid.width || ny < 0 || ny >= _level.grid.height) continue;
                    if (visited.Contains((nx, ny))) continue;
                    if (!_cellDict.TryGetValue((nx, ny), out var nc) || nc.outlineColor != target) continue;
                    visited.Add((nx, ny));
                    queue.Enqueue((nx, ny));
                }
            }

            SyncCells();
            Repaint();
        }

        private void PaintSelection()
        {
            foreach (var (x, y) in _selection)
                if (_cellDict.TryGetValue((x, y), out var cell))
                    cell.outlineColor = _paintColor;
            SyncCells();
            Repaint();
        }

        private void EraseSelection()
        {
            foreach (var (x, y) in _selection)
                if (_cellDict.TryGetValue((x, y), out var cell))
                    cell.outlineColor = CellColor.None;
            SyncCells();
            Repaint();
        }

        private void CommitRectSelect()
        {
            int minX = Mathf.Clamp(Mathf.Min(_rectX0, _rectX1), 0, _level.grid.width  - 1);
            int maxX = Mathf.Clamp(Mathf.Max(_rectX0, _rectX1), 0, _level.grid.width  - 1);
            int minY = Mathf.Clamp(Mathf.Min(_rectY0, _rectY1), 0, _level.grid.height - 1);
            int maxY = Mathf.Clamp(Mathf.Max(_rectY0, _rectY1), 0, _level.grid.height - 1);
            _selection.Clear();
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    _selection.Add((x, y));
        }

        // Rebuilds _level.cells from _cellDict (needed when new cells are added mid-session).
        private void SyncCells()
        {
            var arr = new CellData[_cellDict.Count];
            int i   = 0;
            foreach (var kv in _cellDict) arr[i++] = kv.Value;
            _level.cells = arr;
        }

        // ─── Status Bar ───────────────────────────────────────────────────
        private void DrawStatusBar()
        {
            int colored = 0;
            if (_level.cells != null)
                foreach (var c in _level.cells)
                    if (c.outlineColor != CellColor.None) colored++;

            string hover    = _hoverX >= 0 ? $"  |  ({_hoverX},{_hoverY})" : "";
            string sel      = _selection.Count > 0 ? $"  |  {_selection.Count} selected" : "";
            string fileName = string.IsNullOrEmpty(_filePath) ? "unsaved" : Path.GetFileName(_filePath);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(
                $"{_level.grid.width}×{_level.grid.height}  |  {colored} colored  |  " +
                $"{_level.balls?.Length ?? 0} balls  |  {fileName}{hover}{sel}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ─── Level Operations ─────────────────────────────────────────────
        protected void NewLevel()
        {
            _level    = new LevelData();
            _filePath = null;
            _pendingW = _level.grid.width;
            _pendingH = _level.grid.height;
            _selection.Clear();
            RebuildCellDict();
            Repaint();
        }

        protected void ApplyGridSize()
        {
            var newCells = new List<CellData>();
            for (int y = 0; y < _pendingH; y++)
                for (int x = 0; x < _pendingW; x++)
                {
                    if (_cellDict.TryGetValue((x, y), out var existing))
                        newCells.Add(existing);
                    else
                        newCells.Add(new CellData(x, y, CellColor.None));
                }
            _level.grid.width  = _pendingW;
            _level.grid.height = _pendingH;
            _level.cells       = newCells.ToArray();
            RebuildCellDict();
            Repaint();
        }

        protected void RebuildCellDict()
        {
            StopPlay();   // any AI preview belongs to the old grid — drop it
            _level.camera ??= new CameraConfig();   // older levels predate this field
            _cellDict.Clear();
            if (_level.cells == null) return;
            foreach (var cell in _level.cells)
                _cellDict[(cell.gridX, cell.gridY)] = cell;
        }

        // ─── Image Import ─────────────────────────────────────────────────
        private void ImportImage()
        {
            string path = EditorUtility.OpenFilePanelWithFilters(
                "Import Image", "",
                new[] { "Image files", "png,jpg,jpeg", "All files", "*" });
            if (!string.IsNullOrEmpty(path)) ImportImageFromPath(path);
        }

        private void ImportImageFromPath(string path)
        {
            // Load and cache — only re-read disk when the path changes.
            if (_lastImportedImagePath != path || _cachedImportTex == null)
            {
                if (_cachedImportTex != null) DestroyImmediate(_cachedImportTex);
                _cachedImportTex = ImageImportUtility.LoadTexture(path);
                if (_cachedImportTex == null)
                {
                    EditorUtility.DisplayDialog("Import Failed", $"Could not read image:\n{path}", "OK");
                    return;
                }
                _lastImportedImagePath = path;
            }

            ApplyImportThreshold();
            Debug.Log($"[LevelEditor] Imported {Path.GetFileName(path)} → {_level.grid.width}×{_level.grid.height}");
        }

        // Re-applies the cached texture with the current threshold — called on every slider change.
        private void ApplyImportThreshold()
        {
            if (_cachedImportTex == null) return;
            if (_level.cells == null || _level.cells.Length != _level.grid.width * _level.grid.height)
                ApplyGridSize();
            ImageImportUtility.ImportImageToGrid(_level, _cachedImportTex, _importThreshold);
            RebuildCellDict();
            Repaint();
        }

        private void ClearImportCache()
        {
            if (_cachedImportTex != null) DestroyImmediate(_cachedImportTex);
            _cachedImportTex       = null;
            _lastImportedImagePath = null;
            Repaint();
        }

        private static bool IsImagePath(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        // ─── File I/O ─────────────────────────────────────────────────────
        private void OpenFile()
        {
            // Accept both level files and images in one picker.
            string path = EditorUtility.OpenFilePanelWithFilters(
                "Open Level or Import Image", LevelDirectory(),
                new[] { "Level & Image files", "json,png,jpg,jpeg",
                        "Level files", "json",
                        "Image files", "png,jpg,jpeg" });
            if (string.IsNullOrEmpty(path)) return;

            if (IsImagePath(path))
            {
                ImportImageFromPath(path);
                return;
            }

            var loaded = LevelSerializer.Load(path);
            if (loaded == null)
            {
                EditorUtility.DisplayDialog("Load Failed",
                    $"Could not parse level file:\n{path}", "OK");
                return;
            }
            _level    = loaded;
            _filePath = path;
            _pendingW = _level.grid.width;
            _pendingH = _level.grid.height;
            _selection.Clear();
            RebuildCellDict();
            Repaint();
        }

        private void SaveFile()
        {
            if (string.IsNullOrEmpty(_filePath)) { SaveFileAs(); return; }
            LevelSerializer.Save(_level, _filePath);
            Debug.Log($"[LevelEditor] Saved → {_filePath}");
        }

        private void SaveFileAs()
        {
            string def  = _level.metadata.levelName.Length > 0 ? _level.metadata.levelName : "level";
            string path = EditorUtility.SaveFilePanel("Save Level", LevelDirectory(), def, "json");
            if (string.IsNullOrEmpty(path)) return;
            _filePath = path;
            SaveFile();
        }

        private void DuplicateLevel()
        {
            string def  = _level.metadata.levelName.Length > 0
                ? _level.metadata.levelName + "_copy"
                : "level_copy";
            string path = EditorUtility.SaveFilePanel("Duplicate Level", LevelDirectory(), def, "json");
            if (string.IsNullOrEmpty(path)) return;

            // Save to new path but keep working on the original
            LevelSerializer.Save(_level, path);
            Debug.Log($"[LevelEditor] Duplicated → {path}");
            RefreshBrowser();
        }

        // Exports to Resources/Levels so the runtime (and Android builds) can load
        // it via Resources.Load. Resources works on every platform; StreamingAssets
        // file IO does not on Android.
        private void ExportToResources()
        {
            if (string.IsNullOrEmpty(_level.metadata.levelName) ||
                _level.metadata.levelName == "Untitled")
            {
                EditorUtility.DisplayDialog("Export Failed",
                    "Set a level name before exporting.", "OK");
                return;
            }

            string dir  = Path.Combine(Application.dataPath, "Resources", "Levels");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string dest = Path.Combine(dir, _level.metadata.levelName + ".json");
            LevelSerializer.Save(_level, dest);
            AssetDatabase.Refresh();
            Debug.Log($"[LevelEditor] Exported → {dest}");
            EditorUtility.DisplayDialog("Exported",
                $"Level saved to:\nResources/Levels/{_level.metadata.levelName}.json", "OK");
        }

        // ─── Level Browser ────────────────────────────────────────────────
        private void RefreshBrowser()
        {
            string dir = LevelDirectory();
            if (!Directory.Exists(dir))
            {
                _browserPaths  = System.Array.Empty<string>();
                _browserLabels = System.Array.Empty<string>();
                _browserIndex  = -1;
                return;
            }

            var files = Directory.GetFiles(dir, "*.json");
            System.Array.Sort(files);
            _browserPaths  = files;
            _browserLabels = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
                _browserLabels[i] = Path.GetFileNameWithoutExtension(files[i]);

            // Sync selection to current file
            _browserIndex = -1;
            if (!string.IsNullOrEmpty(_filePath))
                for (int i = 0; i < _browserPaths.Length; i++)
                    if (_browserPaths[i] == _filePath) { _browserIndex = i; break; }

            Repaint();
        }

        private void BrowserOpen(string path)
        {
            if (!ConfirmDiscard()) return;
            var loaded = LevelSerializer.Load(path);
            if (loaded == null)
            {
                EditorUtility.DisplayDialog("Load Failed",
                    $"Could not parse:\n{path}", "OK");
                return;
            }
            _level    = loaded;
            _filePath = path;
            _pendingW = _level.grid.width;
            _pendingH = _level.grid.height;
            _selection.Clear();
            RebuildCellDict();
            RefreshBrowser();
        }

        private bool ConfirmDiscard() =>
            EditorUtility.DisplayDialog("New Level", "Discard unsaved changes?", "Discard", "Cancel");

        private static string LevelDirectory()
        {
            string dir = Path.Combine(Application.dataPath, "_Project", "Levels");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
