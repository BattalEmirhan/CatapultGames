using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames.Editor
{
    // The dev level picker's static frame in UIScene: a top-right toggle button and
    // the scrolling list it opens. LevelPickerHUD fills the rows at runtime. Built
    // after the menu panels so it draws over them.
    internal static class LevelPickerFrame
    {
        private const float Margin = 32f;

        private static readonly Color ToggleColor = new Color(0.10f, 0.10f, 0.16f, 0.92f);
        private static readonly Color ListColor   = new Color(0.07f, 0.07f, 0.12f, 0.96f);

        internal static void Add(Transform canvas)
        {
            var safe = SceneKit.NewRect(canvas, "LevelPicker", Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var toggle = AddToggle(safe, out var label);
            var list   = AddList(safe, out var content);
            var hud    = safe.gameObject.AddComponent<LevelPickerHUD>();
            SceneKit.SetRef(hud, "toggleButton", toggle);
            SceneKit.SetRef(hud, "toggleLabel",  label);
            SceneKit.SetRef(hud, "listPanel",    list);
            SceneKit.SetRef(hud, "content",      content);
        }

        private static Button AddToggle(Transform safe, out TextMeshProUGUI label)
        {
            var button = SceneKit.MakeButton(safe, "Levels", Vector2.one, Vector2.one, ToggleColor);
            PinTopRight((RectTransform)button.transform, new Vector2(405f, 122f), -Margin);
            var colors = button.colors;
            colors.highlightedColor = new Color(0.20f, 0.30f, 0.55f, 0.97f);
            colors.pressedColor     = new Color(0.25f, 0.40f, 0.70f, 1.00f);
            button.colors = colors;
            label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize  = 57f;
            label.fontStyle = FontStyles.Bold;
            return button;
        }

        // Below the toggle with a small gap. Clipped by RectMask2D, not Mask: a Mask
        // whose graphic is fully transparent writes no stencil and hides every row.
        private static GameObject AddList(Transform safe, out RectTransform content)
        {
            var panel = SceneKit.NewRect(safe, "LevelList", Vector2.one, Vector2.one);
            PinTopRight(panel, new Vector2(459f, 1080f), -Margin - 122f - 14f);
            panel.gameObject.AddComponent<Image>().color = ListColor;
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal        = false;
            scroll.scrollSensitivity = 40f;
            scroll.movementType      = ScrollRect.MovementType.Clamped;
            var viewport = SceneKit.NewRect(panel, "Viewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(5f, 5f);
            viewport.offsetMax = new Vector2(-5f, -5f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = AddRowColumn(viewport);
            scroll.viewport = viewport;
            scroll.content  = content;
            return panel.gameObject;
        }

        // Rows size themselves from their LayoutElement (see LevelPickerHUD).
        private static RectTransform AddRowColumn(Transform viewport)
        {
            var content = SceneKit.NewRect(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0f, 1f);
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing                = 8f;
            column.padding                = new RectOffset(8, 8, 8, 8);
            column.childControlWidth      = true;
            column.childControlHeight     = true;
            column.childForceExpandWidth  = true;
            column.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return content;
        }

        private static void PinTopRight(RectTransform r, Vector2 size, float y)
        {
            r.anchorMin = r.anchorMax = r.pivot = Vector2.one;
            r.sizeDelta        = size;
            r.anchoredPosition = new Vector2(-Margin, y);
        }
    }
}
