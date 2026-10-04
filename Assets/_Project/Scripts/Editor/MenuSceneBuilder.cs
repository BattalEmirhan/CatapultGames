using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames.Editor
{
    // Creates the MainMenu and LevelSelect scenes with one menu click, the same way
    // GameplaySceneBuilder makes the gameplay scene: everything built and wired in
    // code, nothing placed by hand, so a rebuild can never lose a reference.
    // Menu: CatapultGames → Build Menu Scenes
    //
    // Build Settings afterwards: MainMenu (index 0 — the app boots into it),
    // LevelSelect, then every other scene in its previous order.
    public static class MenuSceneBuilder
    {
        internal const string MainMenuSceneName    = "MainMenu";
        internal const string LevelSelectSceneName = "LevelSelect";

        private const string SceneFolder     = "Assets/Scenes";
        private static string MainMenuPath    => $"{SceneFolder}/{MainMenuSceneName}.unity";
        private static string LevelSelectPath => $"{SceneFolder}/{LevelSelectSceneName}.unity";

        // The gameplay background's violet, so the menus and the board read as one
        // game; white ink on it, like the in-game HUD.
        private static readonly Color Background = new Color(0.20f, 0.10f, 0.30f);
        private static readonly Color Ink        = Color.white;

        [MenuItem("CatapultGames/Build Menu Scenes", priority = 21)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);

            BuildMainMenu();
            BuildLevelSelect();
            AssetDatabase.Refresh();
            PutMenusFirstInBuildSettings();

            EditorUtility.DisplayDialog("Menu scenes built",
                $"Saved:\n{MainMenuPath}\n{LevelSelectPath}\n\n" +
                "Build Settings now start at MainMenu.\n" +
                "Run CatapultGames/Build Gameplay Scene too if it predates this, so its " +
                "Menu / Next Level buttons know these scene names.",
                "OK");
        }

        // ── MainMenu ──────────────────────────────────────────────────────
        private static void BuildMainMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera();
            GameplaySceneBuilder.AddEventSystem();

            var canvasGo = GameplaySceneBuilder.MakeCanvas("UICanvas");
            var safe     = MakeSafeArea(canvasGo.transform);

            var title = GameplaySceneBuilder.MakeText(safe, "Title", "Catapult\nGames", 140,
                                                      new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.85f));
            StyleInk(title, FontStyles.Bold);

            var progress = GameplaySceneBuilder.MakeText(safe, "Progress", "", 44,
                                                         new Vector2(0.10f, 0.53f), new Vector2(0.90f, 0.59f));
            StyleInk(progress, FontStyles.Normal);

            var play = GameplaySceneBuilder.MakeButton(safe, "Play",
                                                       new Vector2(0.15f, 0.36f), new Vector2(0.85f, 0.47f));
            play.GetComponent<Image>().color = new Color(0.30f, 0.72f, 0.48f);
            var playLabel = play.GetComponentInChildren<TextMeshProUGUI>();
            playLabel.fontSize  = 64;
            playLabel.fontStyle = FontStyles.Bold;

            var select = GameplaySceneBuilder.MakeButton(safe, "Levels",
                                                         new Vector2(0.25f, 0.24f), new Vector2(0.75f, 0.32f));
            select.GetComponent<Image>().color = new Color(0.34f, 0.62f, 0.95f);
            select.GetComponentInChildren<TextMeshProUGUI>().fontSize = 48;

            var ui = canvasGo.AddComponent<MainMenuUI>();
            GameplaySceneBuilder.SetRef(ui, "_playButton",        play);
            GameplaySceneBuilder.SetRef(ui, "_playLabel",         playLabel);
            GameplaySceneBuilder.SetRef(ui, "_levelSelectButton", select);
            GameplaySceneBuilder.SetRef(ui, "_progressLabel",     progress.GetComponent<TextMeshProUGUI>());
            GameplaySceneBuilder.SetStr(ui, "_gameplayScene",     GameplaySceneBuilder.SceneName);
            GameplaySceneBuilder.SetStr(ui, "_levelSelectScene",  LevelSelectSceneName);

            EditorSceneManager.SaveScene(scene, MainMenuPath);
        }

        // ── LevelSelect ───────────────────────────────────────────────────
        // Title, a vertically scrolling grid of level tiles (LevelSelectUI fills it
        // at runtime), and a Back button.
        private static void BuildLevelSelect()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera();
            GameplaySceneBuilder.AddEventSystem();

            var canvasGo = GameplaySceneBuilder.MakeCanvas("UICanvas");
            var safe     = MakeSafeArea(canvasGo.transform);

            var title = GameplaySceneBuilder.MakeText(safe, "Title", "Levels", 96,
                                                      new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f));
            StyleInk(title, FontStyles.Bold);

            // Scroll view: viewport clips, content grows with the tiles.
            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(safe, false);
            SetAnchors((RectTransform)scrollGo.transform, new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.86f));
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal   = false;
            scroll.vertical     = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            GameplaySceneBuilder.StretchToParent((RectTransform)viewportGo.transform);
            viewportGo.AddComponent<RectMask2D>();
            // A clear Image so drags that start between tiles still scroll.
            viewportGo.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot     = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;

            var grid = contentGo.AddComponent<GridLayoutGroup>();
            grid.cellSize        = new Vector2(200f, 200f);
            grid.spacing         = new Vector2(36f, 36f);
            grid.padding         = new RectOffset(12, 12, 12, 12);
            grid.childAlignment  = TextAnchor.UpperCenter;
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = (RectTransform)viewportGo.transform;
            scroll.content  = content;

            var back = GameplaySceneBuilder.MakeButton(safe, "Back",
                                                       new Vector2(0.30f, 0.03f), new Vector2(0.70f, 0.11f));
            back.GetComponent<Image>().color = new Color(0.45f, 0.48f, 0.58f);
            back.GetComponentInChildren<TextMeshProUGUI>().fontSize = 48;

            var ui = canvasGo.AddComponent<LevelSelectUI>();
            GameplaySceneBuilder.SetRef(ui, "_buttonContainer", content);
            GameplaySceneBuilder.SetRef(ui, "_backButton",      back);
            GameplaySceneBuilder.SetStr(ui, "_gameplayScene",   GameplaySceneBuilder.SceneName);
            GameplaySceneBuilder.SetStr(ui, "_mainMenuScene",   MainMenuSceneName);

            EditorSceneManager.SaveScene(scene, LevelSelectPath);
        }

        // ── Helpers ───────────────────────────────────────────────────────
        // A plain camera only to clear the screen; the menus are all overlay UI.
        private static void MakeCamera()
        {
            var camGo = new GameObject("MenuCamera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.cullingMask     = 0;   // nothing in the world to draw
            camGo.AddComponent<AudioListener>();
        }

        private static Transform MakeSafeArea(Transform canvas)
        {
            var go = new GameObject("SafeArea", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            GameplaySceneBuilder.StretchToParent((RectTransform)go.transform);
            go.AddComponent<SafeAreaFitter>();
            return go.transform;
        }

        // Menu text: the same white as the gameplay HUD, on the violet background.
        private static void StyleInk(GameObject textGo, FontStyles style)
        {
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.color         = Ink;
            tmp.fontStyle     = style;
            tmp.raycastTarget = false;
        }

        private static void SetAnchors(RectTransform r, Vector2 min, Vector2 max)
        {
            r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        // MainMenu must be index 0: that is the scene a build boots into.
        private static void PutMenusFirstInBuildSettings()
        {
            var ordered = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(LevelSelectPath, true),
            };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != MainMenuPath && s.path != LevelSelectPath) ordered.Add(s);
            EditorBuildSettings.scenes = ordered.ToArray();
        }
    }
}
