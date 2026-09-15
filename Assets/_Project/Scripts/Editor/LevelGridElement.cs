using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatapultGames.Editor
{
    // The board, as a UI Toolkit element. Every cell is its own VisualElement
    // because every cell has to be clickable and restyled on its own; the
    // element loads its own stylesheet so it is self-contained.
    //
    // It NEVER mutates level data. It only reports intent — CellPressed,
    // CellDragged, StrokeCommitted, CellHovered — and the host window decides
    // what the brush does. Overlays (selection, AI playback) are classes on a
    // lazily-created child per cell, so a 15x15 board is ~225 elements plus
    // whatever is actually highlighted.
    //
    // Colours come from LevelCellPalette, shared with the Gallery thumbnails, so
    // a card and the board it opens never disagree about what a cell is.
    public sealed class LevelGridElement : VisualElement
    {
        private const string RootClass    = "cg-grid";
        private const string RowClass     = "cg-grid__row";
        private const string CellClass    = "cg-grid__cell";
        private const string TargetClass  = "cg-grid__cell--target";
        private const string FilledClass  = "cg-grid__cell--filled";
        private const string GlyphClass   = "cg-grid__glyph";
        private const string FxClass      = "cg-grid__fx";
        private const string FxSelected   = "cg-grid__fx--selected";
        private const string FxHover      = "cg-grid__fx--hover";
        private const string FxHit        = "cg-grid__fx--hit";
        private const string FxLand       = "cg-grid__fx--land";
        private const string RectClass    = "cg-grid__rect";

        private const int Gap = 1;
        private const int GlyphMinPx = 22;

        public event Action<int, int, int> CellPressed;     // x, y, button
        public event Action<int, int>      CellDragged;     // x, y (only when the cell changes)
        public event Action                StrokeCommitted; // pointer up after a press
        public event Action<int, int>      CellHovered;     // (-1,-1) when the pointer leaves

        private LevelData _level;
        private int _cellPx = 32;
        private int _w, _h;
        private VisualElement[] _cells = Array.Empty<VisualElement>();
        private readonly VisualElement _rect;

        private bool _pressed;
        private int  _lastX = -1, _lastY = -1;
        private int  _hoverX = -1, _hoverY = -1;

        // Overlay state, kept so a rebuild (zoom) can re-apply it.
        private readonly HashSet<(int, int)> _selected  = new HashSet<(int, int)>();
        private readonly HashSet<(int, int)> _simFilled = new HashSet<(int, int)>();
        private readonly HashSet<(int, int)> _simHit    = new HashSet<(int, int)>();
        private int  _simLandX = -1, _simLandY = -1;
        private bool _simActive;

        public int CellSize => _cellPx;

        public LevelGridElement()
        {
            AddToClassList(RootClass);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(EditorConstants.LevelGridStylePath);
            if (sheet != null) styleSheets.Add(sheet);

            _rect = new VisualElement { pickingMode = PickingMode.Ignore };
            _rect.AddToClassList(RectClass);
            _rect.style.display = DisplayStyle.None;

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            RegisterCallback<PointerCaptureOutEvent>(_ => EndStroke());
        }

        // ── Data in ───────────────────────────────────────────────────────
        public void SetLevel(LevelData level)
        {
            _level = level;
            int w = level?.grid?.width  ?? 0;
            int h = level?.grid?.height ?? 0;
            if (w != _w || h != _h) Rebuild(w, h);
            else Refresh();
        }

        public void SetCellSize(int px)
        {
            px = Mathf.Clamp(px, 10, 96);
            if (px == _cellPx) return;
            _cellPx = px;
            Rebuild(_w, _h);
        }

        // Repaints every cell from the level. Cheap (no allocation beyond glyph
        // labels), so the host calls it after every edit.
        public void Refresh()
        {
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
                RefreshCell(x, y);
        }

        public void RefreshCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h || _level == null) return;
            var ve   = _cells[y * _w + x];
            var cell = LevelEditOps.Cell(_level, x, y);
            CellColor color = cell?.outlineColor ?? CellColor.None;
            CellType  type  = cell?.cellType     ?? CellType.Normal;
            bool target = color != CellColor.None || type == CellType.Stone;
            bool filled = (cell?.isFilled ?? false) || (_simActive && _simFilled.Contains((x, y)));

            Color body = LevelCellPalette.Resolve(color, type);
            ve.EnableInClassList(TargetClass, target);
            ve.EnableInClassList(FilledClass, filled || type == CellType.Stone);

            // Data-driven colour is inline by design (see LevelEditorWindow.uss);
            // everything geometric that is constant stays in the stylesheet.
            if (target)
            {
                int border = Mathf.Max(2, _cellPx / 10);
                ve.style.borderTopWidth = ve.style.borderBottomWidth =
                ve.style.borderLeftWidth = ve.style.borderRightWidth = border;
                ve.style.borderTopColor = ve.style.borderBottomColor =
                ve.style.borderLeftColor = ve.style.borderRightColor =
                    type == CellType.Stone ? LevelCellPalette.StoneEdge : body;
                ve.style.backgroundColor = filled || type == CellType.Stone ? body : LevelCellPalette.Board;
            }
            else
            {
                ve.style.borderTopWidth = ve.style.borderBottomWidth =
                ve.style.borderLeftWidth = ve.style.borderRightWidth = 0;
                ve.style.backgroundColor = LevelCellPalette.EmptyCell;
            }

            // Glyph: Ice ❄ / Joker ◆ when there is room; the tint carries it below that.
            var glyph = ve.Q<Label>(className: GlyphClass);
            string text = _cellPx >= GlyphMinPx ? LevelCellPalette.TypeGlyph(type) : "";
            if (text.Length == 0)
            {
                if (glyph != null) glyph.style.display = DisplayStyle.None;
            }
            else
            {
                if (glyph == null)
                {
                    glyph = new Label { pickingMode = PickingMode.Ignore };
                    glyph.AddToClassList(GlyphClass);
                    ve.Add(glyph);
                }
                glyph.text = text;
                glyph.style.display = DisplayStyle.Flex;
                glyph.style.fontSize = Mathf.RoundToInt(_cellPx * 0.5f);
                glyph.style.color = type == CellType.Ice ? LevelCellPalette.IceGlyph : LevelCellPalette.JokerGlyph;
            }

            ApplyFx(x, y);
        }

        // ── Overlays ──────────────────────────────────────────────────────
        public void SetSelection(IEnumerable<(int, int)> cells)
        {
            var old = new List<(int, int)>(_selected);
            _selected.Clear();
            if (cells != null) foreach (var c in cells) _selected.Add(c);
            foreach (var c in old)      ApplyFx(c.Item1, c.Item2);
            foreach (var c in _selected) ApplyFx(c.Item1, c.Item2);
        }

        public void SetPlayback(bool active, IEnumerable<(int, int)> filled, IEnumerable<(int, int)> hit, int landX, int landY)
        {
            _simActive = active;
            _simFilled.Clear();
            _simHit.Clear();
            if (active)
            {
                if (filled != null) foreach (var c in filled) _simFilled.Add(c);
                if (hit    != null) foreach (var c in hit)    _simHit.Add(c);
            }
            _simLandX = active ? landX : -1;
            _simLandY = active ? landY : -1;
            Refresh();
        }

        public void ShowRect(int x0, int y0, int x1, int y1)
        {
            int minX = Mathf.Min(x0, x1), minY = Mathf.Min(y0, y1);
            int maxX = Mathf.Max(x0, x1), maxY = Mathf.Max(y0, y1);
            int step = _cellPx + Gap;
            _rect.style.left   = minX * step;
            _rect.style.top    = minY * step;
            _rect.style.width  = (maxX - minX + 1) * step - Gap;
            _rect.style.height = (maxY - minY + 1) * step - Gap;
            _rect.style.display = DisplayStyle.Flex;
        }

        public void HideRect() => _rect.style.display = DisplayStyle.None;

        private void ApplyFx(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            var ve = _cells[y * _w + x];
            bool sel   = _selected.Contains((x, y));
            bool hover = x == _hoverX && y == _hoverY;
            bool hit   = _simActive && _simHit.Contains((x, y));
            bool land  = _simActive && x == _simLandX && y == _simLandY;

            var fx = ve.Q<VisualElement>(className: FxClass);
            if (!(sel || hover || hit || land))
            {
                if (fx != null) fx.style.display = DisplayStyle.None;
                return;
            }
            if (fx == null)
            {
                fx = new VisualElement { pickingMode = PickingMode.Ignore };
                fx.AddToClassList(FxClass);
                ve.Add(fx);
            }
            fx.style.display = DisplayStyle.Flex;
            fx.EnableInClassList(FxSelected, sel);
            fx.EnableInClassList(FxHover,    hover && !sel && !hit && !land);
            fx.EnableInClassList(FxHit,      hit);
            fx.EnableInClassList(FxLand,     land);
        }

        // ── Build ─────────────────────────────────────────────────────────
        private void Rebuild(int w, int h)
        {
            Clear();
            _w = w; _h = h;
            _cells = new VisualElement[Mathf.Max(0, w * h)];

            for (int y = 0; y < h; y++)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList(RowClass);
                for (int x = 0; x < w; x++)
                {
                    var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                    cell.AddToClassList(CellClass);
                    cell.style.width  = _cellPx;
                    cell.style.height = _cellPx;
                    cell.style.marginRight  = Gap;
                    cell.style.marginBottom = Gap;
                    row.Add(cell);
                    _cells[y * w + x] = cell;
                }
                Add(row);
            }

            Add(_rect);
            HideRect();
            style.width  = w * (_cellPx + Gap) + 2;
            style.height = h * (_cellPx + Gap) + 2;
            Refresh();
        }

        // ── Pointer → cell ────────────────────────────────────────────────
        private bool TryCellAt(Vector2 local, out int x, out int y)
        {
            int step = _cellPx + Gap;
            x = Mathf.FloorToInt((local.x - 1) / step);
            y = Mathf.FloorToInt((local.y - 1) / step);
            return x >= 0 && y >= 0 && x < _w && y < _h;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!TryCellAt(evt.localPosition, out int x, out int y)) return;
            _pressed = true;
            _lastX = x; _lastY = y;
            this.CapturePointer(evt.pointerId);
            CellPressed?.Invoke(x, y, evt.button);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            bool inside = TryCellAt(evt.localPosition, out int x, out int y);
            SetHover(inside ? x : -1, inside ? y : -1);
            if (!_pressed || !inside) return;
            if (x == _lastX && y == _lastY) return;
            _lastX = x; _lastY = y;
            CellDragged?.Invoke(x, y);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (this.HasPointerCapture(evt.pointerId)) this.ReleasePointer(evt.pointerId);
            EndStroke();
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (!_pressed) SetHover(-1, -1);
        }

        private void EndStroke()
        {
            if (!_pressed) return;
            _pressed = false;
            _lastX = _lastY = -1;
            StrokeCommitted?.Invoke();
        }

        private void SetHover(int x, int y)
        {
            if (x == _hoverX && y == _hoverY) return;
            int ox = _hoverX, oy = _hoverY;
            _hoverX = x; _hoverY = y;
            ApplyFx(ox, oy);
            ApplyFx(x, y);
            CellHovered?.Invoke(x, y);
        }
    }
}
