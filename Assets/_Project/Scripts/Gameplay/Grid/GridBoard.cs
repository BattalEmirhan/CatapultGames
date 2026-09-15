using UnityEngine;

namespace CatapultGames
{
    // The light, rounded plate the cells sit on — a "board" in the casual sense:
    // pale, soft-edged, receiving the cubes' shadows so they read as objects
    // standing on it rather than floating over a void.
    // Call Rebuild() from LevelLoader.Apply() after BuildGrid().
    public class GridBoard : MonoBehaviour
    {
        private static readonly Color PlateColor = new Color(0.96f, 0.95f, 0.93f);
        private const float PlateH = 0.22f;

        private Transform _plate;
        private Material  _mat;

        // ── Public ────────────────────────────────────────────────────────
        public void Rebuild(GridConfig grid)
        {
            if (!_plate)
            {
                int gridLayer = LayerMask.NameToLayer("CG_Grid");
                if (gridLayer < 0) gridLayer = 0;

                var go = new GameObject("BoardPlate") { layer = gridLayer };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = RoundedCubeMesh.Get(0.10f, 4);

                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows    = true;

                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(shader) { color = PlateColor };
                if (_mat.HasProperty("_Smoothness")) _mat.SetFloat("_Smoothness", 0.15f);
                mr.sharedMaterial = _mat;
                _plate = go.transform;
            }

            float cs   = grid.cellSize;
            float pad  = cs * 0.55f;
            float midX = (grid.width  - 1) * cs * 0.5f;
            float midZ = (grid.height - 1) * cs * 0.5f;

            // Top face at Y = 0 (the cells' floor), body hanging below it.
            _plate.localPosition = new Vector3(midX, -PlateH * 0.5f, midZ);
            _plate.localScale    = new Vector3(grid.width * cs + pad, PlateH, grid.height * cs + pad);
        }

        private void OnDestroy()
        {
            if (_mat) Destroy(_mat);
        }
    }
}
