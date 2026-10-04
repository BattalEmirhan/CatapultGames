using UnityEngine;
using UnityEngine.UIElements;

namespace CatapultGames.Editor
{
    // A level board painted into ONE element's generateVisualContent — not one
    // element per cell. 40 cards × 225 cells would otherwise be 9000 elements;
    // this is 40. Glyphs are skipped entirely (the type tint carries them).
    //
    // Colours come from LevelCellPalette, the same source the Editor grid reads,
    // so the card and the board it opens never disagree about a cell.
    public sealed class LevelThumbnailElement : VisualElement
    {
        private int _w, _h;
        private Color[] _fill = System.Array.Empty<Color>();
        private bool[]  _target = System.Array.Empty<bool>();

        public LevelThumbnailElement()
        {
            AddToClassList("cg-lvcard__thumb");
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetLevel(LevelData level)
        {
            _w = level?.grid?.width  ?? 0;
            _h = level?.grid?.height ?? 0;
            int n = Mathf.Max(0, _w * _h);
            _fill   = new Color[n];
            _target = new bool[n];
            for (int i = 0; i < n; i++)
                _fill[i] = LevelCellPalette.EmptyCell;

            if (level?.cells != null)
                foreach (var c in level.cells)
                {
                    if (c == null || c.gridX < 0 || c.gridY < 0 || c.gridX >= _w || c.gridY >= _h)
                        continue;
                    int i = c.gridY * _w + c.gridX;
                    _fill[i]   = LevelCellPalette.Resolve(c.outlineColor, c.cellType);
                    _target[i] = c.outlineColor != CellColor.None || c.cellType == CellType.Stone;
                }
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (_w == 0 || _h == 0)
                return;
            var r = contentRect;
            float cell = Mathf.Floor(Mathf.Min(r.width / _w, r.height / _h));
            if (cell < 1f)
                return;
            float ox = (r.width  - cell * _w) * 0.5f;
            float oy = (r.height - cell * _h) * 0.5f;
            float gap = cell >= 6f ? 1f : 0f;

            var p = ctx.painter2D;
            for (int y = 0; y < _h; y++)
            for (int x = 0; x < _w; x++)
            {
                int i = y * _w + x;
                p.fillColor = _fill[i];
                float px = ox + x * cell, py = oy + y * cell;
                p.BeginPath();
                p.MoveTo(new Vector2(px, py));
                p.LineTo(new Vector2(px + cell - gap, py));
                p.LineTo(new Vector2(px + cell - gap, py + cell - gap));
                p.LineTo(new Vector2(px, py + cell - gap));
                p.ClosePath();
                p.Fill();

                // Unfilled targets are hollow in the editor grid; echo that with an
                // inner board-coloured square when there is room, so the picture
                // reads as "outline to paint" rather than "already painted".
                if (_target[i] && cell >= 8f)
                {
                    float inset = Mathf.Max(1f, cell * 0.22f);
                    p.fillColor = LevelCellPalette.Board;
                    p.BeginPath();
                    p.MoveTo(new Vector2(px + inset, py + inset));
                    p.LineTo(new Vector2(px + cell - gap - inset, py + inset));
                    p.LineTo(new Vector2(px + cell - gap - inset, py + cell - gap - inset));
                    p.LineTo(new Vector2(px + inset, py + cell - gap - inset));
                    p.ClosePath();
                    p.Fill();
                }
            }
        }
    }
}
