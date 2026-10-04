using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CatapultGames.Editor
{
    // UIScene: the meta UI drawn over GameScene — main menu, level select and the
    // dev level picker — on the UI layer, rendered by an Overlay camera that
    // UICameraStacker appends to the gameplay camera's stack at startup. It also
    // owns the only EventSystem, which Canvas_Game in GameScene relies on too.
    internal static class UiSceneBuilder
    {
        private static readonly Color Background = new Color(0.07f, 0.09f, 0.15f, 0.97f);
        private static readonly Color PlayColor  = new Color(0.30f, 0.72f, 0.48f);
        private static readonly Color SkyColor   = new Color(0.34f, 0.62f, 0.95f);
        private static readonly Color BackColor  = new Color(0.45f, 0.48f, 0.58f);

        internal static void Build(string path)
        {
            var scene  = SceneKit.NewScene();
            var camera = AddCamera();
            SceneKit.AddEventSystem();
            var canvas = SceneKit.MakeCanvas("Canvas_Meta", camera, UiLayout.UiLayer, 20).transform;
            AddMainMenu(canvas);
            AddLevelSelect(canvas);
            LevelPickerFrame.Add(canvas);
            SceneKit.SetLayerRecursively(canvas.gameObject, UiLayout.UiLayer);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static Camera AddCamera()
        {
            var go  = new GameObject("UICamera") { layer = UiLayout.UiLayer };
            var cam = go.AddComponent<Camera>();
            cam.cullingMask = 1 << UiLayout.UiLayer;
            cam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            go.AddComponent<UICameraStacker>();
            return cam;
        }

        // The panel covers the whole screen; its contents keep to the safe area.
        private static Transform AddPanel(Transform canvas, string name, out GameObject panel)
        {
            panel = SceneKit.MakeBackdrop(canvas, name, Background);
            var safe = SceneKit.NewRect(panel.transform, "SafeArea", Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return safe;
        }

        private static void AddMainMenu(Transform canvas)
        {
            var safe = AddPanel(canvas, "MainMenu", out var panel);
            SceneKit.MakeText(safe, "Title", "Catapult\nGames", 189f, new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.85f)).fontStyle = FontStyles.Bold;
            var progress = SceneKit.MakeText(safe, "Progress", "", 59f, new Vector2(0.10f, 0.53f), new Vector2(0.90f, 0.59f));
            var play     = SceneKit.MakeButton(safe, "Play", new Vector2(0.15f, 0.36f), new Vector2(0.85f, 0.47f), PlayColor);
            var label    = play.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize  = 86f;
            label.fontStyle = FontStyles.Bold;
            var levels = SceneKit.MakeButton(safe, "Levels", new Vector2(0.25f, 0.24f), new Vector2(0.75f, 0.32f), SkyColor);
            levels.GetComponentInChildren<TextMeshProUGUI>().fontSize = 65f;

            var ui = canvas.gameObject.AddComponent<MainMenuUI>();
            SceneKit.SetRef(ui, "panel",             panel);
            SceneKit.SetRef(ui, "playButton",        play);
            SceneKit.SetRef(ui, "playLabel",         label);
            SceneKit.SetRef(ui, "levelSelectButton", levels);
            SceneKit.SetRef(ui, "progressLabel",     progress);
        }

        // Title, a vertically scrolling grid of tiles (LevelSelectUI fills it each
        // time the panel opens) and a Back button.
        private static void AddLevelSelect(Transform canvas)
        {
            var safe = AddPanel(canvas, "LevelSelect", out var panel);
            SceneKit.MakeText(safe, "Title", "Levels", 130f, new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f)).fontStyle = FontStyles.Bold;
            var content = AddTileScroll(safe);
            var back    = SceneKit.MakeButton(safe, "Back", new Vector2(0.30f, 0.03f), new Vector2(0.70f, 0.11f), BackColor);
            back.GetComponentInChildren<TextMeshProUGUI>().fontSize = 65f;

            var ui = canvas.gameObject.AddComponent<LevelSelectUI>();
            SceneKit.SetRef(ui, "panel",           panel);
            SceneKit.SetRef(ui, "buttonContainer", content);
            SceneKit.SetRef(ui, "backButton",      back);
        }

        private static RectTransform AddTileScroll(Transform safe)
        {
            var scroll = SceneKit.NewRect(safe, "Scroll", new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.86f)).gameObject.AddComponent<ScrollRect>();
            scroll.horizontal   = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = SceneKit.NewRect(scroll.transform, "Viewport", Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            // A clear Image so drags that start between tiles still scroll.
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            var content = SceneKit.NewRect(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f));
            content.pivot = new Vector2(0.5f, 1f);
            AddTileGrid(content.gameObject);
            scroll.viewport = viewport;
            scroll.content  = content;
            return content;
        }

        private static void AddTileGrid(GameObject content)
        {
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize        = new Vector2(270f, 270f);
            grid.spacing         = new Vector2(49f, 49f);
            grid.padding         = new RectOffset(16, 16, 16, 16);
            grid.childAlignment  = TextAnchor.UpperCenter;
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
