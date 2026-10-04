using UnityEngine;

namespace CatapultGames
{
    // Canvas conventions shared by every screen.
    public static class UiLayout
    {
        // iPhone 17 Pro portrait; every CanvasScaler uses it, matching 0.5.
        public static readonly Vector2 ReferenceResolution = new Vector2(1320f, 2868f);

        // Unity's built-in "UI" layer: only UIScene's overlay camera draws it, and the
        // gameplay camera excludes it, or the meta UI would be drawn twice.
        public const int UiLayer = 5;
    }
}
