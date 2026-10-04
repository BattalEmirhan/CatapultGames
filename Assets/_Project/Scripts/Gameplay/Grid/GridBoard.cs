using UnityEngine;

namespace CatapultGames
{
    // The dark slate panel the cells sit on — one 9-sliced sprite (TileArt.Panel)
    // lying under the grid, a little larger than it, drawn before every socket.
    // Call Rebuild() from LevelLoader.Apply() after BuildGrid().
    public class GridBoard : MonoBehaviour
    {
        private const float Padding = 0.30f;   // panel margin around the grid, in cells

        private SpriteRenderer _panel;

        // ── Public ────────────────────────────────────────────────────────
        public void Rebuild(GridConfig grid)
        {
            if (!_panel)
            {
                int gridLayer = LayerMask.NameToLayer("CG_Grid");
                if (gridLayer < 0) gridLayer = 0;

                var go = new GameObject("BoardPanel") { layer = gridLayer };
                go.transform.SetParent(transform, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // flat, top edge up the screen
                _panel = go.AddComponent<SpriteRenderer>();
                _panel.sprite       = TileArt.Panel();
                _panel.drawMode     = SpriteDrawMode.Sliced;
                _panel.sortingOrder = CellView.BoardOrder;
                _panel.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            float cs   = grid.cellSize;
            float midX = (grid.width  - 1) * cs * 0.5f;
            float midZ = (grid.height - 1) * cs * 0.5f;

            // A hair under the cells' plane, so nothing z-fights with the sockets.
            _panel.transform.localPosition = new Vector3(midX, -0.01f, midZ);
            _panel.size = new Vector2(grid.width * cs + 2f * Padding * cs, grid.height * cs + 2f * Padding * cs);
        }
    }
}
