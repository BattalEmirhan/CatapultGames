using UnityEngine;

namespace CatapultGames
{
    // The dark, rounded plate the cells sit on — soft-edged, receiving the cubes'
    // shadows so they read as objects standing on it rather than floating over a
    // void. Dark (2026-10-04, as before the light-board pass) so the coloured
    // cubes are the brightest things on screen.
    // Call Rebuild() from LevelLoader.Apply() after BuildGrid().
    public sealed class GridBoard : MonoBehaviour
    {
        private static readonly Color PlateColor = new Color(0.06f, 0.07f, 0.12f);
        private const float PlateH = 0.22f;
        private Transform _plate;
        private Material  _mat;

        private void OnDestroy()
        {
            if (_mat)
                Destroy(_mat);
        }

        public void Rebuild(GridConfig grid)
        {
            if (!_plate)
            {
                int gridLayer = LayerMask.NameToLayer("CG_Grid");
                if (gridLayer < 0)
                    gridLayer = 0;

                var go = new GameObject("BoardPlate") { layer = gridLayer };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = RoundedCubeMesh.Get(0.10f, 4);

                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows    = true;

                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _mat = new Material(shader) { color = PlateColor };
                if (_mat.HasProperty("_Smoothness"))
                    _mat.SetFloat("_Smoothness", 0.15f);
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
    }
}
