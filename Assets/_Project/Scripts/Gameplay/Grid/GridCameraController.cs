using UnityEngine;

namespace CatapultGames
{
    // Single perspective camera that frames the whole scene (grid + slingshot area).
    // FitToGrid() auto-positions based on fieldOfView + tiltAngle so the user
    // only has to tweak two Inspector sliders — no manual transform dragging.
    [RequireComponent(typeof(Camera))]
    public class GridCameraController : MonoBehaviour
    {
        [Tooltip("Vertical FOV in degrees.")]
        [Range(30f, 90f)] public float fieldOfView = 60f;

        [Tooltip("Camera tilt (X-rotation). 90 = top-down, 45 = classic diagonal.")]
        [Range(30f, 85f)] public float tiltAngle = 55f;

        [Tooltip("Extra breathing room around the scene.")]
        [Range(1f, 1.5f)] public float padding = 1.10f;

        [Tooltip("Where the GRID CENTRE sits on screen vertically: " +
                 "0 = bottom, 0.5 = centred, 1 = top.")]
        [Range(0f, 1f)] public float gridScreenPos = 0.5f;

        private Camera  _cam;
        private Vector3 _offset;   // per-level manual nudge, applied after fit
        private void Awake() => _cam = GetComponent<Camera>();

        // ── Public ────────────────────────────────────────────────────────

        // Apply a level's per-level camera settings, then frame the grid.
        public void FitToGrid(GridConfig grid, CameraConfig camCfg)
        {
            if (camCfg != null)
            {
                fieldOfView   = camCfg.fieldOfView;
                tiltAngle     = camCfg.tiltAngle;
                padding       = camCfg.padding;
                gridScreenPos = camCfg.gridScreenPos;
                _offset       = camCfg.offset;
            }
            FitToGrid(grid);
        }

        public void FitToGrid(GridConfig grid)
        {
            _cam.orthographic = false;
            _cam.fieldOfView  = fieldOfView;

            float cs       = grid.cellSize;
            float gw       = grid.width  * cs;
            float gh       = grid.height * cs;
            float midX     = (grid.width  - 1) * cs * 0.5f;
            float gridMidZ = (grid.height - 1) * cs * 0.5f;   // world Z of the grid's centre

            // Scene the zoom must fit: grid (Z 0..gh) + slingshot/aim band.
            // FrontZ must reach past the catapult (placed at Z=-8 by the scene builder)
            // so it never clips off the bottom on wider aspect ratios.
            const float FrontZ = -9f;
            float sceneMinZ = FrontZ;
            float sceneMaxZ = gh + 1f;

            float tiltRad    = tiltAngle * Mathf.Deg2Rad;
            float halfFovRad = fieldOfView * Mathf.Deg2Rad * 0.5f;

            // Project world extents onto the camera plane, then pick the zoom that
            // fits both the scene's projected height and its width.
            float projH  = (sceneMaxZ - sceneMinZ) * Mathf.Sin(tiltRad);  // foreshortened Z span
            float projW  = gw;
            float aspect = _cam.aspect > 0.001f ? _cam.aspect : 1f;
            float framed = Mathf.Max(projH, projW / aspect) * padding;    // vertical world span in view
            float dist   = (framed * 0.5f) / Mathf.Tan(halfFovRad);

            var     rot      = Quaternion.Euler(tiltAngle, 0f, 0f);
            Vector3 screenUp = rot * Vector3.up;

            // Aim the screen-centre ground ray at the GRID centre, so the board sits
            // in the middle of the screen by default (not the whole-scene mid).
            float camY = dist * Mathf.Sin(tiltRad);
            float camZ = gridMidZ - dist * Mathf.Cos(tiltRad);
            Vector3 camPos = new Vector3(midX, camY, camZ);

            // gridScreenPos slides the grid centre to a chosen vertical screen fraction
            // (0 = bottom, 0.5 = centre, 1 = top). Pushing the camera down screenUp
            // moves the content up, so a higher fraction needs a larger up-shift.
            float shift = (gridScreenPos - 0.5f) * framed;
            camPos -= screenUp * shift;

            transform.SetPositionAndRotation(camPos + _offset, rot);
        }

        public void FitToGrid(GridRenderer gr) => FitToGrid(gr.Level.grid, gr.Level.camera);
    }
}
