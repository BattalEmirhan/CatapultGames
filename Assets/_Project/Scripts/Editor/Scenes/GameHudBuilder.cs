using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames.Editor
{
    // Canvas_Game's children: everything the player reads or taps while a level
    // is running. Sizes are the 1080×1920 layout scaled ×1.35 for the 1320×2868
    // reference resolution, so the HUD keeps its old on-screen size.
    internal static class GameHudBuilder
    {
        private static readonly Color Neutral    = new Color(0.2f, 0.2f, 0.2f);
        private static readonly Color HintBacker = new Color(0.10f, 0.12f, 0.22f, 0.55f);
        private static readonly Color Hidden     = new Color(1f, 1f, 1f, 0f);

        internal static void AddResultPanel(GameSceneParts p)
        {
            var panel = SceneKit.MakeBackdrop(p.Canvas, "ResultPanel", new Color(0f, 0f, 0f, 0.8f));
            panel.SetActive(false);
            var ui = p.Canvas.gameObject.AddComponent<ResultScreenUI>();
            SceneKit.SetRef(ui, "panel",        panel);
            SceneKit.SetRef(ui, "titleText",    SceneKit.MakeText(panel.transform, "TitleText", "", 97f, new Vector2(0.1f, 0.60f), new Vector2(0.9f, 0.78f)));
            SceneKit.SetRef(ui, "subtitleText", SceneKit.MakeText(panel.transform, "SubText",   "", 54f, new Vector2(0.1f, 0.46f), new Vector2(0.9f, 0.58f)));
            SceneKit.SetRef(ui, "retryButton",  SceneKit.MakeButton(panel.transform, "Retry", new Vector2(0.18f, 0.22f), new Vector2(0.46f, 0.40f), Neutral));
            SceneKit.SetRef(ui, "menuButton",   SceneKit.MakeButton(panel.transform, "Menu",  new Vector2(0.54f, 0.22f), new Vector2(0.82f, 0.40f), Neutral));
            SceneKit.SetRef(ui, "extraBallsButton", AddWideAction(panel.transform, "Keep Going", new Color(0.16f, 0.42f, 0.22f)));
            SceneKit.SetRef(ui, "nextButton",       AddWideAction(panel.transform, "Next Level", new Color(0.20f, 0.45f, 0.80f)));
            SceneKit.SetRef(ui, "gameManager",  p.GameManager);
            SceneKit.SetRef(p.GameManager, "resultScreen", ui);
        }

        // Clear of the tray band and the top HUDs. Gestures that start on UI never
        // fire a shot, so it may sit over the play area. The view lives on the
        // canvas: it hides the button by deactivating it, which would stop its own
        // updates if it lived on the button.
        internal static void AddUndo(GameSceneParts p)
        {
            var button = SceneKit.MakeButton(p.Canvas, "Undo", new Vector2(0.74f, 0.10f), new Vector2(0.96f, 0.17f), Neutral);
            button.gameObject.SetActive(false);
            var ui = p.Canvas.gameObject.AddComponent<UndoButtonUI>();
            SceneKit.SetRef(ui, "button",      button);
            SceneKit.SetRef(ui, "gameManager", p.GameManager);
        }

        // A short column left of the tray, bottom-up. Kept off the tray band: a
        // button over a tray ball would eat the tap that selects it.
        internal static void AddBoosters(GameSceneParts p)
        {
            p.Boosters = new GameObject("BoosterSystem").AddComponent<BoosterSystem>();
            SceneKit.SetRef(p.Boosters, "queue",       p.Queue);
            SceneKit.SetRef(p.Boosters, "grid",        p.Grid);
            SceneKit.SetRef(p.Boosters, "gameManager", p.GameManager);
            var bar = p.Canvas.gameObject.AddComponent<BoosterBarUI>();
            SceneKit.SetRef(bar, "boosters", p.Boosters);
            SceneKit.SetRef(bar, "queue",    p.Queue);
            AddBoosterButton(p.Canvas, bar, "Rainbow", "rainbow", 0, new Color(0.62f, 0.42f, 0.86f));
            AddBoosterButton(p.Canvas, bar, "Recolor", "recolor", 1, new Color(0.24f, 0.60f, 0.86f));
            AddBoosterButton(p.Canvas, bar, "Bomb",    "bomb",    2, new Color(0.90f, 0.42f, 0.32f));
        }

        // GameManager fades it in when a colour can no longer be finished; the
        // colour name inside is tinted, hence rich text.
        internal static void AddWarning(GameSceneParts p)
        {
            var label = SceneKit.MakeText(p.Canvas, "WarningLabel", "", 62f, new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.77f));
            label.fontStyle = FontStyles.Bold;
            label.richText  = true;
            label.color     = Hidden;
            SceneKit.SetRef(p.GameManager, "warningLabel", label);
        }

        // Both live inside one SafeAreaFitter rect so they clear the notch.
        internal static void AddProgressAndScore(GameSceneParts p)
        {
            var safe = SceneKit.NewRect(p.Canvas, "HudSafeArea", Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            AddProgress(p, safe);
            AddScore(p, safe);
        }

        // Built last so the fingertip draws over the buttons it points at.
        internal static void AddTutorial(GameSceneParts p)
        {
            p.Tutorial = new GameObject("TutorialHint").AddComponent<TutorialHint>();
            var t = p.Tutorial;
            SceneKit.SetRef(t, "camera",      p.Camera);
            SceneKit.SetRef(t, "grid",        p.Grid);
            SceneKit.SetRef(t, "queue",       p.Queue);
            SceneKit.SetRef(t, "queueView",   p.QueueView);
            SceneKit.SetRef(t, "launcher",    p.Launcher);
            SceneKit.SetRef(t, "gameManager", p.GameManager);
            SceneKit.SetRef(t, "hintGroup",    AddFadePanel(p.Canvas, "LevelHint",       new Vector2(0.08f, 0.63f), new Vector2(0.92f, 0.69f),  out var hint));
            SceneKit.SetRef(t, "hintLabel",    hint);
            SceneKit.SetRef(t, "captionGroup", AddFadePanel(p.Canvas, "TutorialCaption", new Vector2(0.25f, 0.19f), new Vector2(0.75f, 0.245f), out var caption));
            SceneKit.SetRef(t, "caption",      caption);
            AddPointer(p.Canvas, t);
        }

        // "Keep Going" (after a loss) and "Next Level" (after a win) share the wide
        // slot under Retry/Menu — never both at once. ResultScreenUI picks one.
        private static Button AddWideAction(Transform panel, string label, Color color)
        {
            var button = SceneKit.MakeButton(panel, label, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.19f), color);
            button.gameObject.SetActive(false);
            return button;
        }

        // Row 0 shares the undo button's band so the two read as a pair. The name
        // sits low in the button, the "×N" count in its top-right corner.
        private static void AddBoosterButton(Transform canvas, BoosterBarUI bar, string label, string field, int row, Color color)
        {
            const float Bottom = 0.10f, Height = 0.065f, Gap = 0.01f;
            float y0     = Bottom + row * (Height + Gap);
            var   button = SceneKit.MakeButton(canvas, label, new Vector2(0.03f, y0), new Vector2(0.21f, y0 + Height), color);
            var   name   = button.GetComponentInChildren<TextMeshProUGUI>();
            name.fontSize  = 41f;
            name.fontStyle = FontStyles.Bold;
            SceneKit.SetAnchors(name.rectTransform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.60f));
            var count = SceneKit.MakeText(button.transform, "Count", "×1", 38f, new Vector2(0.50f, 0.58f), new Vector2(0.96f, 0.98f));
            count.alignment = TextAlignmentOptions.TopRight;
            count.fontStyle = FontStyles.Bold;
            SceneKit.SetRef(bar, field + "Button", button);
            SceneKit.SetRef(bar, field + "Count",  count);
        }

        private static void AddProgress(GameSceneParts p, Transform safe)
        {
            var root  = SceneKit.NewRect(safe, "ProgressHUD", Vector2.zero, Vector2.one);
            p.Progress = root.gameObject.AddComponent<ProgressHUD>();
            var label = SceneKit.MakeText(root, "ProgressLabel", "0 / 0", 86f, new Vector2(0f, 0.92f), new Vector2(0.50f, 1.00f));
            label.alignment = TextAlignmentOptions.TopLeft;
            label.fontStyle = FontStyles.Bold;
            label.margin    = new Vector4(32f, 22f, 0f, 0f);
            SceneKit.SetRef(p.Progress, "label", label);
            SceneKit.SetRef(p.Progress, "grid",  p.Grid);
        }

        // Top-right, below the dev level dropdown that owns the very corner.
        private static void AddScore(GameSceneParts p, Transform safe)
        {
            var root  = SceneKit.NewRect(safe, "ScoreHUD", Vector2.zero, Vector2.one);
            var score = root.gameObject.AddComponent<ScoreHUD>();
            SceneKit.SetRef(score, "gameManager", p.GameManager);
            SceneKit.SetRef(score, "scoreLabel",  AddCornerText(root, "ScoreLabel", "0", 81f, new Vector2(0.50f, 0.845f), new Vector2(1.00f, 0.925f)));
            SceneKit.SetRef(score, "comboLabel",  AddCornerText(root, "ComboLabel", "",  57f, new Vector2(0.42f, 0.775f), new Vector2(1.00f, 0.845f)));
            var praise = SceneKit.MakeText(p.Canvas, "PraiseLabel", "", 130f, new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.62f));
            praise.fontStyle = FontStyles.Bold;
            SceneKit.SetRef(score, "praiseLabel", praise);
            SceneKit.SetFloat(score, "burstDuration", 0.95f);
        }

        private static TextMeshProUGUI AddCornerText(Transform parent, string name, string text, float size, Vector2 min, Vector2 max)
        {
            var label = SceneKit.MakeText(parent, name, text, size, min, max);
            label.alignment = TextAlignmentOptions.TopRight;
            label.fontStyle = FontStyles.Bold;
            label.margin    = new Vector4(0f, 0f, 32f, 0f);
            return label;
        }

        // The disc and ring sprites are generated at runtime (UiSprites).
        private static void AddPointer(Transform canvas, TutorialHint tutorial)
        {
            var pointer = SceneKit.NewRect(canvas, "TutorialPointer", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            pointer.sizeDelta = new Vector2(149f, 149f);
            AddHiddenImage(pointer);
            var ring = SceneKit.NewRect(pointer, "Ring", Vector2.zero, Vector2.one);
            SceneKit.SetRef(tutorial, "pointer",     pointer);
            SceneKit.SetRef(tutorial, "pointerRing", AddHiddenImage(ring));
        }

        private static Image AddHiddenImage(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color         = Hidden;
            return image;
        }

        // A hidden text strip on a translucent dark backing, faded by its group.
        private static CanvasGroup AddFadePanel(Transform parent, string name, Vector2 min, Vector2 max, out TextMeshProUGUI label)
        {
            var rect = SceneKit.NewRect(parent, name, min, max);
            var bg   = rect.gameObject.AddComponent<Image>();
            bg.color         = HintBacker;
            bg.raycastTarget = false;
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha          = 0f;
            group.blocksRaycasts = false;
            group.interactable   = false;
            label = SceneKit.MakeText(rect, "Text", "", 54f, Vector2.zero, Vector2.one);
            label.fontStyle = FontStyles.Bold;
            return group;
        }
    }
}
