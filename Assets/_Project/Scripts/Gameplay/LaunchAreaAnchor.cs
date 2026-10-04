using UnityEngine;

namespace CatapultGames
{
    // Pins the launch area (catapult + ball queue) to a fixed band at the BOTTOM of
    // the screen, independent of the grid camera's per-level framing.
    //
    // The catapult and queue are real world objects so the launch arc stays
    // continuous with the grid camera. Without this, changing a level's camera
    // (tilt / zoom / offset) re-frames the whole scene and slides the balls around
    // the screen. Reanchor() reprojects the area onto the screen-bottom ground point
    // after the camera has been fitted, so the balls always sit in the same place.
    //
    // The whole launch area is one child hierarchy under this transform, so moving
    // this root preserves the internal layout (catapult centred, queue spread left).
    public sealed class LaunchAreaAnchor : MonoBehaviour
    {
        [SerializeField] private Camera gameCamera;

        [Tooltip("Vertical screen fraction the launch area's ground point is pinned to. " +
                 "0 = very bottom, 0.2 = top of the bottom fifth.")]
        [Range(0f, 0.4f)] [SerializeField] private float screenY = 0.08f;

        [Tooltip("Horizontal screen fraction (0.5 = centred).")]
        [Range(0f, 1f)] [SerializeField] private float screenX = 0.5f;

        [Tooltip("Ground-plane height the launch area sits on.")]
        [SerializeField] private float groundY = 0f;

        private int _lastW, _lastH;

        private void LateUpdate()
        {
            // Re-pin only when the viewport actually changes (rotation / resolution).
            // Camera shake leaves Screen size untouched, so the area rides the shake
            // with the rest of the scene instead of jittering against it.
            if (Screen.width != _lastW || Screen.height != _lastH)
                Reanchor();
        }

        // Reproject the launch area onto the screen-bottom ground point. Call this
        // right after the camera is fitted (LevelLoader does, before loading balls).
        public void Reanchor()
        {
            if (gameCamera == null)
                gameCamera = Camera.main;
            if (gameCamera == null || Screen.height <= 0)
                return;

            Vector3 sp  = new Vector3(Screen.width * screenX, Screen.height * screenY, 0f);
            Ray     ray = gameCamera.ScreenPointToRay(sp);
            var     ground = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            if (ground.Raycast(ray, out float dist))
                transform.position = ray.GetPoint(dist);

            _lastW = Screen.width;
            _lastH = Screen.height;
        }
    }
}
