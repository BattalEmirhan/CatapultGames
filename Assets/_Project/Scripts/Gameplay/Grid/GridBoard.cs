using UnityEngine;

namespace CatapultGames
{
    // Dark background quad placed slightly below the grid cells.
    // Gives the grid a "board" look and helps cells contrast against the sky.
    // Call Rebuild() from LevelLoader.Apply() after BuildGrid().
    public class GridBoard : MonoBehaviour
    {
        private Transform    _quad;
        private Material     _mat;

        // ── Public ────────────────────────────────────────────────────────
        public void Rebuild(GridConfig grid)
        {
            if (!_quad)
            {
                int gridLayer = LayerMask.NameToLayer("CG_Grid");
                if (gridLayer < 0) gridLayer = 0;

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name  = "BoardQuad";
                go.layer = gridLayer;
                Destroy(go.GetComponent<MeshCollider>());
                go.transform.SetParent(transform, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var mr = go.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows    = false;

                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                          ?? Shader.Find("Unlit/Color");
                _mat = new Material(shader) { color = new Color(0.06f, 0.07f, 0.12f) };
                mr.sharedMaterial = _mat;
                _quad = go.transform;
            }

            float cs   = grid.cellSize;
            float pad  = cs * 0.45f;
            float midX = (grid.width  - 1) * cs * 0.5f;
            float midZ = (grid.height - 1) * cs * 0.5f;

            _quad.localPosition = new Vector3(midX, -0.06f, midZ);  // base under cube cells
            _quad.localScale    = new Vector3(grid.width * cs + pad,
                                              grid.height * cs + pad, 1f);
        }

        private void OnDestroy()
        {
            if (_mat) Destroy(_mat);
        }
    }
}
