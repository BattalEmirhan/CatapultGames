using UnityEngine;

namespace CatapultGames
{
    // Attach to a full-screen RectTransform inside a Canvas (any render mode —
    // the anchors are screen fractions).
    // Resizes the rect every frame (if changed) to match the device safe area
    // so child UI elements avoid notches, home indicators, and rounded corners.
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rt;
        private Rect          _lastSafe;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafe)
                Apply();
        }

        private void Apply()
        {
            _lastSafe = Screen.safeArea;
            var screen        = new Vector2(Screen.width, Screen.height);
            _rt.anchorMin     = _lastSafe.position / screen;
            _rt.anchorMax     = (_lastSafe.position + _lastSafe.size) / screen;
            _rt.offsetMin     = Vector2.zero;
            _rt.offsetMax     = Vector2.zero;
        }
    }
}
