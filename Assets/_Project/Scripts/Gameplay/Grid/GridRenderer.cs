using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Builds and owns all CellViews for one level.
    // Attach to an empty GameObject in the Gameplay scene.
    public class GridRenderer : MonoBehaviour
    {
        // Fires whenever any cell's fill state changes (used by ProgressHUD)
        public event Action OnGridChanged;

        private LevelData _level;
        private readonly Dictionary<(int, int), CellView> _cells = new();

        public LevelData Level    => _level;
        public int       Width    => _level?.grid.width  ?? 0;
        public int       Height   => _level?.grid.height ?? 0;
        public float     CellSize => _level?.grid.cellSize ?? 1f;

        // ─── Build / Destroy ──────────────────────────────────────────────
        public void BuildGrid(LevelData level)
        {
            ClearGrid();
            _level = level;

            if (level?.cells == null) return;

            // Index cells by position for O(1) lookup
            var cellMap = new Dictionary<(int, int), CellData>();
            foreach (var c in level.cells)
                cellMap[(c.gridX, c.gridY)] = c;

            for (int y = 0; y < level.grid.height; y++)
            {
                for (int x = 0; x < level.grid.width; x++)
                {
                    cellMap.TryGetValue((x, y), out var data);
                    CellColor color  = data?.outlineColor ?? CellColor.None;
                    bool      filled = data?.isFilled     ?? false;

                    var view = CellView.Create(transform, x, y, level.grid.cellSize, color, filled);
                    _cells[(x, y)] = view;
                }
            }
        }

        public void ClearGrid()
        {
            foreach (var kv in _cells)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _cells.Clear();
            _level = null;
        }

        // ─── Cell access ──────────────────────────────────────────────────
        public bool TryGetCell(int x, int y, out CellView cell) =>
            _cells.TryGetValue((x, y), out cell);

        public CellView GetCell(int x, int y) =>
            _cells.TryGetValue((x, y), out var c) ? c : null;

        // ─── Fill state ───────────────────────────────────────────────────
        public void SetFilled(int x, int y, bool filled)
        {
            if (_cells.TryGetValue((x, y), out var cell))
            {
                cell.SetFilled(filled);
                OnGridChanged?.Invoke();
            }
        }

        public void SetFilledBatch(IEnumerable<Vector2Int> coords, bool filled)
        {
            foreach (var p in coords)
                SetFilled(p.x, p.y, filled);
        }

        // ─── Highlight + paint preview (aim) ──────────────────────────────
        private readonly HashSet<(int, int)> _highlighted = new();
        private readonly HashSet<(int, int)> _previewed   = new();

        // Clears BOTH aim visuals — the landing highlight and the paint-preview
        // ghosts — since they are always shown and hidden together while aiming.
        public void ClearHighlights()
        {
            foreach (var k in _highlighted)
                if (_cells.TryGetValue(k, out var c)) c.SetHighlight(false);
            _highlighted.Clear();

            foreach (var k in _previewed)
                if (_cells.TryGetValue(k, out var c)) c.SetPreview(false);
            _previewed.Clear();
        }

        public void SetHighlight(int x, int y, bool on)
        {
            if (!_cells.TryGetValue((x, y), out var cell)) return;
            cell.SetHighlight(on);
            if (on) _highlighted.Add((x, y));
            else    _highlighted.Remove((x, y));
        }

        // Ghost-raise a cell in its fill colour to preview that this shot would
        // paint it (see CellView.SetPreview). Used by AimPreview.
        public void SetPreview(int x, int y, bool on)
        {
            if (!_cells.TryGetValue((x, y), out var cell)) return;
            cell.SetPreview(on);
            if (on) _previewed.Add((x, y));
            else    _previewed.Remove((x, y));
        }

        // ─── Coordinate helpers ───────────────────────────────────────────

        // World-space centre of the whole grid (used for win celebration FX).
        public Vector3 WorldCenter
        {
            get
            {
                if (_level == null) return transform.position;
                float cx = (Width  - 1) * CellSize * 0.5f;
                float cz = (Height - 1) * CellSize * 0.5f;
                return transform.TransformPoint(new Vector3(cx, 0f, cz));
            }
        }

        // Celebratory bounce on every filled cell of a color — used when that color
        // is fully completed. Staggered outward from the grid centre for a ripple.
        public void PulseColor(CellColor color)
        {
            Vector3 center = WorldCenter;
            foreach (var kv in _cells)
            {
                var cell = kv.Value;
                if (cell == null || cell.OutlineColor != color || !cell.IsFilled) continue;
                float d = Vector3.Distance(cell.transform.position, center);
                cell.Pulse(d * 0.04f);   // ripple from centre outward
            }
        }

        // World-space centre of a grid cell.
        public Vector3 GridToWorld(int x, int y) =>
            transform.TransformPoint(new Vector3(x * CellSize, 0f, y * CellSize));

        // Convert a world position (on the Y=0 plane under this transform) to
        // grid coordinates. Returns false when outside the grid bounds.
        public bool WorldToGrid(Vector3 worldPos, out int gx, out int gy)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            gx = Mathf.RoundToInt(local.x / CellSize);
            gy = Mathf.RoundToInt(local.z / CellSize);
            return gx >= 0 && gx < Width && gy >= 0 && gy < Height;
        }

        // Raycast from a camera against the logical Y=0 grid plane and return
        // the hit grid cell. Useful for aim preview.
        public bool RaycastToGrid(Ray ray, out int gx, out int gy)
        {
            gx = gy = -1;
            // Intersect the ray with the plane Y=0 in world space
            var plane = new Plane(Vector3.up, transform.position);
            if (!plane.Raycast(ray, out float dist)) return false;
            return WorldToGrid(ray.GetPoint(dist), out gx, out gy);
        }

        // Like RaycastToGrid, but CLAMPS the hit to the nearest valid cell instead of
        // failing when the ray lands outside the grid — keeps aim on the board so a
        // shot can never be wasted off-grid. Returns false only if the ray misses the
        // grid plane entirely, or the grid is empty.
        public bool RaycastToGridClamped(Ray ray, out int gx, out int gy)
        {
            gx = gy = 0;
            if (Width <= 0 || Height <= 0) return false;

            var plane = new Plane(Vector3.up, transform.position);
            if (!plane.Raycast(ray, out float dist)) return false;

            Vector3 local = transform.InverseTransformPoint(ray.GetPoint(dist));
            gx = Mathf.Clamp(Mathf.RoundToInt(local.x / CellSize), 0, Width  - 1);
            gy = Mathf.Clamp(Mathf.RoundToInt(local.z / CellSize), 0, Height - 1);
            return true;
        }

        // ─── Progress helpers ─────────────────────────────────────────────
        public int CountTotalColored()
        {
            int n = 0;
            foreach (var kv in _cells)
                if (kv.Value.OutlineColor != CellColor.None) n++;
            return n;
        }

        public int CountUnfilledColored()
        {
            int n = 0;
            foreach (var kv in _cells)
            {
                var c = kv.Value;
                if (c.OutlineColor != CellColor.None && !c.IsFilled) n++;
            }
            return n;
        }

        // Per-color progress: how many cells of each color are filled vs. how
        // many that color needs in total. Ordered by the CellColor enum so the
        // HUD stays stable between refreshes. Used by ProgressHUD.
        public struct ColorProgress
        {
            public CellColor color;
            public int       filled;
            public int       total;
        }

        // Reused scratch buffers (indexed by CellColor) so per-frame refreshes —
        // ProgressHUD ticks once per cell during a paint wave — don't allocate two
        // dictionaries + sort every call. Iterating in enum order keeps the result
        // sorted for free (no Sort()).
        private int[] _ccTotal;
        private int[] _ccFilled;

        public List<ColorProgress> CountByColor()
        {
            int n = GameConstants.CellColorPalette.Length;
            if (_ccTotal == null) { _ccTotal = new int[n]; _ccFilled = new int[n]; }
            System.Array.Clear(_ccTotal,  0, n);
            System.Array.Clear(_ccFilled, 0, n);

            foreach (var kv in _cells)
            {
                var c  = kv.Value;
                int ci = (int)c.OutlineColor;
                if (ci <= 0 || ci >= n) continue;   // 0 = None → skipped
                _ccTotal[ci]++;
                if (c.IsFilled) _ccFilled[ci]++;
            }

            var result = new List<ColorProgress>();
            for (int ci = 1; ci < n; ci++)          // enum order → already sorted
            {
                if (_ccTotal[ci] == 0) continue;
                result.Add(new ColorProgress
                {
                    color  = (CellColor)ci,
                    filled = _ccFilled[ci],
                    total  = _ccTotal[ci]
                });
            }
            return result;
        }

        // ─── Win check ────────────────────────────────────────────────────
        public bool AllColoredCellsFilled()
        {
            foreach (var kv in _cells)
            {
                var cell = kv.Value;
                if (cell.OutlineColor != CellColor.None && !cell.IsFilled)
                    return false;
            }
            return true;
        }

        public bool HasUnfilledColoredCells() => !AllColoredCellsFilled();
    }
}
