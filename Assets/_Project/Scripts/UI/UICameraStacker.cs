using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CatapultGames
{
    // UIScene owns exactly one camera, a URP Overlay camera for the meta UI.
    // GameScene's Base camera lives in another scene and cannot be referenced at
    // author time, so the overlay is appended to its stack at startup.
    [RequireComponent(typeof(Camera))]
    public sealed class UICameraStacker : MonoBehaviour
    {
        private Camera _overlay;
        private Camera _base;

        private void Awake() => _overlay = GetComponent<Camera>();

        private void Start()
        {
            _base = Camera.main;
            if (_base == null)
            {
                Debug.LogError("[UICameraStacker] No MainCamera — load GameScene before UIScene (start from InitScene).");
                return;
            }
            var stack = _base.GetUniversalAdditionalCameraData().cameraStack;
            if (!stack.Contains(_overlay))
                stack.Add(_overlay);
        }

        private void OnDestroy()
        {
            if (_base != null)
                _base.GetUniversalAdditionalCameraData().cameraStack.Remove(_overlay);
        }
    }
}
