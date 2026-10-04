using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CatapultGames.Editor
{
    // Creates a ready-to-play Gameplay scene with one menu click.
    // Menu: CatapultGames → Build Gameplay Scene
    //
    // Controls: tap a grid cell and the catapult ball launches there
    // (hold and slide to move the target, release to fire).
    public static class GameplaySceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Gameplay2.unity";
        internal const string SceneName = "Gameplay2";   // what the menus load

        // The gameplay post-processing profile, created once under Assets/Settings
        // and reused on every rebuild (a Volume must reference an asset, or the
        // saved scene loses it).
        private const string PostProfilePath = "Assets/Settings/GameplayPostFX.asset";

        // Priority 20 leaves a >10 gap below the Level Editor (0), which Unity
        // renders as a separator — this rebuilds the scene from scratch, so it
        // should not sit flush against the tool used all day.
        [MenuItem("CatapultGames/Build Gameplay Scene", priority = 20)]
        public static void Build() => BuildScene(ScenePath);

        internal static void AddEventSystem()
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Reflection avoids a hard assembly reference — works with old and new Input System
            var inputModuleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
                esGo.AddComponent(inputModuleType);
            else
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // A full-screen Screen Space Overlay canvas at the project's reference
        // resolution (portrait 1080x1920), the same setup every screen uses.
        internal static GameObject MakeCanvas(string name, int sortingOrder = 10)
        {
            var canvasGo = new GameObject(name);
            var canvas   = canvasGo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler                 = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            return canvasGo;
        }

        internal static void SetRef(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
                Debug.LogWarning($"[SceneBuilder] Field not found: {fieldName} on {target.GetType().Name}");
        }

        internal static void SetStr(Object target, string fieldName, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.stringValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        internal static GameObject MakeText(Transform parent, string name, string text,
                                           int fontSize, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.offsetMin = r.offsetMax = Vector2.zero;
            return go;
        }

        internal static Button MakeButton(Transform parent, string label,
                                         Vector2 anchorMin, Vector2 anchorMax)
        {
            var go  = new GameObject(label + "Btn");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f);
            var btn = go.AddComponent<Button>();
            var r   = go.GetComponent<RectTransform>();
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.offsetMin = r.offsetMax = Vector2.zero;

            var textGo = MakeText(go.transform, "Label", label, 34, Vector2.zero, Vector2.one);
            return btn;
        }

        internal static void StretchToParent(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax  = Vector2.zero;
        }

        internal static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            bool exists = scenes.Exists(s => s.path == scenePath);
            if (!exists)
            {
                scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static void BuildScene(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string sceneFolder = Path.GetDirectoryName(scenePath);
            if (!string.IsNullOrEmpty(sceneFolder) && !Directory.Exists(sceneFolder))
                Directory.CreateDirectory(sceneFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Register the CG_Grid layer so CellView and GridBoard can find it by name.
            EnsureLayer("CG_Grid");

            // Warm key light with soft shadows over a dim ambient — the dark theme:
            // the cubes are lit, the board around them stays in shadow.
            var lightGo = new GameObject("DirectionalLight");
            var light   = lightGo.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.color     = new Color(1f, 0.96f, 0.90f);
            light.intensity = 1.15f;
            light.shadows   = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            light.transform.rotation = Quaternion.Euler(58f, -28f, 0f);
            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
            // Dim, cool ambient (the dark theme's): bright ambient lifts every face
            // toward grey and is what made the cubes look washed out.
            RenderSettings.ambientLight = new Color(0.26f, 0.28f, 0.40f);

            // Renders grid cubes, catapult, trajectory, and queue together.
            // GridCameraController.FitToGrid() auto-positions at runtime so that
            // both the grid and the launch area are always in frame.
            // Inspector sliders: fieldOfView (60°) and tiltAngle (55°) to taste.
            var camGo  = new GameObject("GridCamera");
            camGo.tag  = "MainCamera";   // so Camera.main resolves — GameFX shake/zoom/firework use it
            var cam    = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView  = 60f;
            cam.clearFlags   = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.15f);   // fallback behind the gradient quad
            cam.cullingMask  = -1;  // render everything
            cam.depth        = 0;
            camGo.AddComponent<BackgroundGradient>();   // dark night-blue gradient behind everything
            camGo.AddComponent<AudioListener>();        // GameAudio plays 2D one-shots; something has to hear them

            // Post-processing: a faint bloom on the brightest highlights and a soft
            // vignette. The profile is a real asset so the saved scene keeps its
            // reference; rebuilding reuses it and rewrites its values.
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            var postGo = new GameObject("PostFX");
            var volume = postGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = EnsurePostProfile();
            // Placeholder position — FitToGrid() overwrites this at runtime
            cam.transform.SetPositionAndRotation(
                new Vector3(5.5f, 14f, -5f),
                Quaternion.Euler(55f, 0f, 0f));
            var camCtrl = camGo.AddComponent<GridCameraController>();
            camCtrl.fieldOfView   = 60f;
            camCtrl.tiltAngle     = 66f;    // steep: the board reads flat, like a block puzzle
            camCtrl.padding       = 1.12f;
            camCtrl.gridScreenPos = 0.60f;  // board in the upper two thirds, tray band below (per-level override via LevelData.camera)

            var gridGo = new GameObject("GridRoot");
            var grid   = gridGo.AddComponent<GridRenderer>();
            var board  = gridGo.AddComponent<GridBoard>();  // background board, same GO

            var queueGo = new GameObject("BallQueue");
            var queue   = queueGo.AddComponent<BallQueue>();

            // The ball TRAY (three slots on a plate) and a small decorative
            // catapult live under one root so LaunchAreaAnchor can pin the whole
            // group to a fixed band at the bottom of the screen — it never slides
            // when a level's grid camera changes.
            const float catX = 5.5f;
            const float catZ = -8f;
            var launchAreaGo = new GameObject("LaunchArea");
            launchAreaGo.transform.position = new Vector3(catX, 0f, catZ);

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (!litShader)
                litShader = Shader.Find("Standard");

            // Tray plate — the light slab the three balls sit on.
            AddDeco(launchAreaGo.transform, "TrayPlate", PrimitiveType.Cube,
                    new Vector3(0f, 0.12f, 0f), new Vector3(5.4f, 0.24f, 1.9f),
                    litShader, new Color(0.13f, 0.14f, 0.21f));   // dark, like the board

            // Slots: left → right. The selected ball is lifted above its slot by
            // BallQueueView, and the arc starts from wherever that ball is.
            var slotsParent = new GameObject("TraySlots").transform;
            slotsParent.SetParent(launchAreaGo.transform, false);
            var slots = new Transform[3];
            for (int i = 0; i < slots.Length; i++)
            {
                var s = new GameObject($"Slot_{i}").transform;
                s.SetParent(slotsParent, false);
                s.localPosition = new Vector3((i - 1) * 1.65f, 0.62f, 0f);
                slots[i] = s;
            }

            var queueView = queueGo.AddComponent<BallQueueView>();
            SetRef(queueView, "queue", queue);
            SetRefArray(queueView, "slots", slots);
            // _remainingLabel is wired once the UI canvas exists (below).

            // Launch origin FALLBACK at the centre slot. TapLaunchController,
            // BallLauncher and AimPreview normally take the selected tray ball's
            // position (BallQueueView.CurrentLaunchOrigin) instead.
            var originGo = new GameObject("LaunchOrigin").transform;
            originGo.SetParent(launchAreaGo.transform, false);
            originGo.localPosition = new Vector3(0f, 0.92f, 0f);

            // Decorative catapult behind the tray — the theme, not the mechanism.
            var catapultGo = new GameObject("Catapult");
            catapultGo.transform.SetParent(launchAreaGo.transform, false);
            catapultGo.transform.localPosition = new Vector3(0f, 0f, -1.35f);
            catapultGo.transform.localScale    = Vector3.one * 0.7f;
            var wood = new Color(0.60f, 0.46f, 0.34f);
            AddDeco(catapultGo.transform, "CatapultBase", PrimitiveType.Cube,   new Vector3(0f, 0.2f, 0f),  new Vector3(1.2f, 0.4f, 0.6f),  litShader, new Color(0.52f, 0.40f, 0.30f));
            AddDeco(catapultGo.transform, "CatapultArm",  PrimitiveType.Cube,   new Vector3(0f, 0.6f, 0f),  new Vector3(0.15f, 0.9f, 0.15f), litShader, wood);
            AddDeco(catapultGo.transform, "ForkL",        PrimitiveType.Sphere, new Vector3(-0.3f, 1.1f, 0f), Vector3.one * 0.18f,           litShader, wood);
            AddDeco(catapultGo.transform, "ForkR",        PrimitiveType.Sphere, new Vector3( 0.3f, 1.1f, 0f), Vector3.one * 0.18f,           litShader, wood);

            var aimGo  = new GameObject("AimPreview");
            var aimLR  = aimGo.AddComponent<LineRenderer>();
            aimLR.startWidth   = 0.22f;   // thick start
            aimLR.endWidth     = 0.08f;   // tapers toward landing
            var aimShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!aimShader)
                aimShader = Shader.Find("Unlit/Color");
            aimLR.sharedMaterial = new Material(aimShader) { color = new Color(1f, 1f, 0.3f) };  // bright yellow
            aimLR.textureMode    = LineTextureMode.Tile;

            var aimPreview = aimGo.AddComponent<AimPreview>();
            SetRef(aimPreview, "queue",        queue);
            SetRef(aimPreview, "grid",         grid);
            SetRef(aimPreview, "launchOrigin", originGo);
            // Arc resolution is shared via GameConstants so preview == real landing.

            var launcherGo = new GameObject("BallLauncher");
            var launcher   = launcherGo.AddComponent<BallLauncher>();
            SetRef(launcher, "queue",        queue);
            SetRef(launcher, "grid",         grid);
            SetRef(launcher, "launchOrigin", originGo);
            SetFloat(launcher, "flightDuration",   0.55f);  // snappy travel from the Z=-8 catapult
            SetFloat(launcher, "ballVisualScale",  0.45f);

            AddEventSystem();

            var canvasGo = new GameObject("UICanvas");
            var canvas   = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            // Scale with screen so 72pt buttons stay touch-friendly on any device
            var scaler                 = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Tray counter — "+N" balls still waiting beyond the three slots. Sits
            // at the right end of the tray band; never a raycast target.
            var remainGo  = MakeText(canvasGo.transform, "TrayRemaining", "", 46,
                                     new Vector2(0.78f, 0.03f), new Vector2(0.98f, 0.10f));
            var remainTMP = remainGo.GetComponent<TextMeshProUGUI>();
            remainTMP.alignment     = TextAlignmentOptions.MidlineRight;
            remainTMP.fontStyle     = FontStyles.Bold;
            remainTMP.raycastTarget = false;
            SetRef(queueView, "remainingLabel", remainTMP);

            var panel = new GameObject("ResultPanel");
            panel.transform.SetParent(canvasGo.transform, false);
            var panelImg   = panel.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.8f);
            StretchToParent(panel.GetComponent<RectTransform>());
            panel.SetActive(false);

            // Title
            var titleGo = MakeText(panel.transform, "TitleText", "",   72, new Vector2(0.1f,0.6f), new Vector2(0.9f,0.78f));
            // Subtitle
            var subGo   = MakeText(panel.transform, "SubText",   "",   40, new Vector2(0.1f,0.46f), new Vector2(0.9f,0.58f));
            // Buttons
            var retryBtn = MakeButton(panel.transform, "Retry", new Vector2(0.18f,0.22f), new Vector2(0.46f,0.40f));
            var menuBtn  = MakeButton(panel.transform, "Menu",  new Vector2(0.54f,0.22f), new Vector2(0.82f,0.40f));

            // The "keep going" offer sits below and spans both — it is the action
            // we want the thumb to find first after a loss. ResultScreenUI shows or
            // hides it per result, so it starts inactive.
            var keepGoingBtn = MakeButton(panel.transform, "Keep Going",
                                          new Vector2(0.18f,0.08f), new Vector2(0.82f,0.19f));
            keepGoingBtn.GetComponent<Image>().color = new Color(0.16f, 0.42f, 0.22f);
            keepGoingBtn.gameObject.SetActive(false);

            // "Next level" takes the same slot: shown only after a win, when the
            // offer never is.
            var nextBtn = MakeButton(panel.transform, "Next Level",
                                     new Vector2(0.18f,0.08f), new Vector2(0.82f,0.19f));
            nextBtn.GetComponent<Image>().color = new Color(0.20f, 0.45f, 0.80f);
            nextBtn.gameObject.SetActive(false);

            var gmGo = new GameObject("GameManager");
            var gm   = gmGo.AddComponent<GameManager>();
            SetRef(gm, "grid",         grid);
            SetRef(gm, "queue",        queue);
            SetRef(gm, "launcher",     launcher);
            SetRef(gm, "aimPreview",   aimPreview);
            SetStr(gm, "mainMenuScene",    MenuSceneBuilder.MainMenuSceneName);
            SetStr(gm, "levelSelectScene", MenuSceneBuilder.LevelSelectSceneName);

            // ResultScreenUI
            var resultUI = canvasGo.AddComponent<ResultScreenUI>();
            SetRef(resultUI, "panel",        panel);
            SetRef(resultUI, "titleText",    titleGo.GetComponent<TextMeshProUGUI>());
            SetRef(resultUI, "subtitleText", subGo.GetComponent<TextMeshProUGUI>());
            SetRef(resultUI, "retryButton",      retryBtn);
            SetRef(resultUI, "menuButton",       menuBtn);
            SetRef(resultUI, "extraBallsButton", keepGoingBtn);
            SetRef(resultUI, "nextButton",       nextBtn);
            SetRef(resultUI, "gameManager",  gm);
            SetRef(gm, "resultScreen",  resultUI);
            // Button listeners are wired in ResultScreenUI.Awake — no need to add them here.

            // Clear of the catapult/queue strip at the bottom and of the HUDs at the
            // top. Gestures starting on UI never fire a shot (TapLaunchController
            // checks IsPointerOverUI), so it is safe to sit over the play area.
            var undoBtn = MakeButton(canvasGo.transform, "Undo",
                                     new Vector2(0.74f,0.10f), new Vector2(0.96f,0.17f));
            undoBtn.gameObject.SetActive(false);   // UndoButtonUI shows it once a shot lands

            // Lives on the canvas, NOT on the button: it hides the button by
            // deactivating it, and an inactive object stops updating.
            var undoUI = canvasGo.AddComponent<UndoButtonUI>();
            SetRef(undoUI, "button",      undoBtn);
            SetRef(undoUI, "gameManager", gm);

            // A short column left of the tray, bottom-up: Rainbow, Recolor, Bomb.
            // Kept off the tray band itself — a button over a tray ball would eat
            // the tap that selects it (UI gestures never reach TapLaunchController).
            var boosterGo = new GameObject("BoosterSystem");
            var boosters  = boosterGo.AddComponent<BoosterSystem>();
            SetRef(boosters, "queue",       queue);
            SetRef(boosters, "grid",        grid);
            SetRef(boosters, "gameManager", gm);

            var rainbow = MakeBoosterButton(canvasGo.transform, "Rainbow", 0, new Color(0.62f, 0.42f, 0.86f), out var rainbowCount);
            var recolor = MakeBoosterButton(canvasGo.transform, "Recolor", 1, new Color(0.24f, 0.60f, 0.86f), out var recolorCount);
            var bomb    = MakeBoosterButton(canvasGo.transform, "Bomb",    2, new Color(0.90f, 0.42f, 0.32f), out var bombCount);

            // One view drives all three buttons, so it sits on the canvas (next to
            // UndoButtonUI) rather than on any one of them.
            var boosterBar = canvasGo.AddComponent<BoosterBarUI>();
            SetRef(boosterBar, "boosters",      boosters);
            SetRef(boosterBar, "queue",         queue);
            SetRef(boosterBar, "rainbowButton", rainbow);
            SetRef(boosterBar, "recolorButton", recolor);
            SetRef(boosterBar, "bombButton",    bomb);
            SetRef(boosterBar, "rainbowCount",  rainbowCount);
            SetRef(boosterBar, "recolorCount",  recolorCount);
            SetRef(boosterBar, "bombCount",     bombCount);

            // GameManager fades it in when a colour can no longer be finished.
            var warningGo  = MakeText(canvasGo.transform, "WarningLabel", "", 46,
                                      new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.77f));
            var warningTMP = warningGo.GetComponent<TextMeshProUGUI>();
            warningTMP.fontStyle     = FontStyles.Bold;
            warningTMP.raycastTarget = false;
            warningTMP.richText      = true;               // the colour name is tinted
            warningTMP.color         = new Color(1f, 1f, 1f, 0f);   // starts hidden
            SetRef(gm, "warningLabel", warningTMP);

            // Wrap in a SafeAreaFitter panel so it respects notch / home indicator
            var safeAreaGo = new GameObject("ProgressSafeArea");
            safeAreaGo.transform.SetParent(canvasGo.transform, false);
            safeAreaGo.AddComponent<SafeAreaFitter>();  // fills safe area at runtime

            var progressGo  = new GameObject("ProgressHUD");
            progressGo.transform.SetParent(safeAreaGo.transform, false);
            var progressHUD = progressGo.AddComponent<ProgressHUD>();

            // Anchored to top-left corner of the safe area panel, not the full screen
            var progressLabel = MakeText(
                progressGo.transform, "ProgressLabel", "0 / 0",
                64, new Vector2(0f, 0.92f), new Vector2(0.50f, 1.00f));
            var pTMP = progressLabel.GetComponent<TextMeshProUGUI>();
            pTMP.alignment  = TextAlignmentOptions.TopLeft;
            pTMP.fontStyle  = FontStyles.Bold;
            pTMP.margin     = new Vector4(24f, 16f, 0f, 0f);
            SetRef(progressHUD, "label", pTMP);
            SetRef(progressHUD, "grid",  grid);

            // Shares the safe-area panel with the progress HUD (top-left), sitting
            // clear of the dev level dropdown that owns the very top-right corner.
            var scoreGo  = new GameObject("ScoreHUD");
            scoreGo.transform.SetParent(safeAreaGo.transform, false);
            var scoreHUD = scoreGo.AddComponent<ScoreHUD>();

            var scoreLabelGo = MakeText(scoreGo.transform, "ScoreLabel", "0",
                                        60, new Vector2(0.50f, 0.845f), new Vector2(1.00f, 0.925f));
            var scoreTMP = scoreLabelGo.GetComponent<TextMeshProUGUI>();
            scoreTMP.alignment = TextAlignmentOptions.TopRight;
            scoreTMP.fontStyle = FontStyles.Bold;
            scoreTMP.margin    = new Vector4(0f, 0f, 24f, 0f);
            scoreTMP.raycastTarget = false;   // must never eat an aim gesture

            var comboLabelGo = MakeText(scoreGo.transform, "ComboLabel", "",
                                        42, new Vector2(0.42f, 0.775f), new Vector2(1.00f, 0.845f));
            var comboTMP = comboLabelGo.GetComponent<TextMeshProUGUI>();
            comboTMP.alignment = TextAlignmentOptions.TopRight;
            comboTMP.fontStyle = FontStyles.Bold;
            comboTMP.margin    = new Vector4(0f, 0f, 24f, 0f);
            comboTMP.raycastTarget = false;

            // "GREAT!" praise — big, centred over the board, never a raycast target.
            var praiseGo  = MakeText(canvasGo.transform, "PraiseLabel", "", 96,
                                     new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.62f));
            var praiseTMP = praiseGo.GetComponent<TextMeshProUGUI>();
            praiseTMP.fontStyle     = FontStyles.Bold;
            praiseTMP.raycastTarget = false;

            SetRef(scoreHUD, "gameManager", gm);
            SetRef(scoreHUD, "scoreLabel",  scoreTMP);
            SetRef(scoreHUD, "comboLabel",  comboTMP);
            SetRef(scoreHUD, "praiseLabel", praiseTMP);
            SetFloat(scoreHUD, "burstDuration", 0.95f);

            // Built after the other canvas children so the fingertip draws on top
            // of the buttons it points at. Nothing here is a raycast target.
            var tutorialGo = new GameObject("TutorialHint");
            var tutorial   = tutorialGo.AddComponent<TutorialHint>();

            var hintGroup = MakePanel(canvasGo.transform, "LevelHint",
                                      new Vector2(0.08f, 0.63f), new Vector2(0.92f, 0.69f), out var hintTMP, 40);
            var captionGroup = MakePanel(canvasGo.transform, "TutorialCaption",
                                         new Vector2(0.25f, 0.19f), new Vector2(0.75f, 0.245f), out var captionTMP, 40);

            var pointerGo = new GameObject("TutorialPointer", typeof(RectTransform));
            pointerGo.transform.SetParent(canvasGo.transform, false);
            var pointerRect = (RectTransform)pointerGo.transform;
            pointerRect.anchorMin = pointerRect.anchorMax = new Vector2(0.5f, 0.5f);
            pointerRect.sizeDelta = new Vector2(110f, 110f);
            var pointerImg = pointerGo.AddComponent<Image>();   // sprite is made at runtime (UiSprites)
            pointerImg.raycastTarget = false;
            pointerImg.color = new Color(1f, 1f, 1f, 0f);
            var ringGo = new GameObject("Ring", typeof(RectTransform));
            ringGo.transform.SetParent(pointerGo.transform, false);
            StretchToParent((RectTransform)ringGo.transform);
            var ringImg = ringGo.AddComponent<Image>();
            ringImg.raycastTarget = false;
            ringImg.color = new Color(1f, 1f, 1f, 0f);

            SetRef(tutorial, "camera",       cam);
            SetRef(tutorial, "grid",         grid);
            SetRef(tutorial, "queue",        queue);
            SetRef(tutorial, "queueView",    queueView);
            SetRef(tutorial, "launcher",     launcher);
            SetRef(tutorial, "gameManager",  gm);
            SetRef(tutorial, "pointer",      pointerRect);
            SetRef(tutorial, "pointerRing",  ringImg);
            SetRef(tutorial, "captionGroup", captionGroup);
            SetRef(tutorial, "caption",      captionTMP);
            SetRef(tutorial, "hintGroup",    hintGroup);
            SetRef(tutorial, "hintLabel",    hintTMP);

            var pickerGo = new GameObject("LevelPickerHUD");
            var picker   = pickerGo.AddComponent<LevelPickerHUD>();

            // Keeps the balls in a fixed bottom band regardless of the level's grid
            // camera (tilt / zoom / offset), so they never slide around the screen.
            var launchAnchor = launchAreaGo.AddComponent<LaunchAreaAnchor>();
            SetRef(launchAnchor,   "camera",  cam);
            SetFloat(launchAnchor, "screenY", 0.10f);   // tray band at the bottom tenth

            var loaderGo = new GameObject("LevelLoader");
            var loader   = loaderGo.AddComponent<LevelLoader>();
            SetRef(loader,  "grid",             grid);
            SetRef(loader,  "queue",            queue);
            SetRef(loader,  "cam",              camCtrl);
            SetRef(loader,  "board",            board);
            SetRef(loader,  "progressHUD",      progressHUD);
            SetRef(loader,  "levelPicker",      picker);
            SetRef(loader,  "launchAnchor",     launchAnchor);
            SetRef(loader,  "gameManager",      gm);      // resets the score per level
            SetRef(loader,  "boosters",         boosters); // refills the booster counts per level
            SetRef(loader,  "tutorial",         tutorial); // first-run tutorial + level hint banner
            SetStr(loader,  "defaultLevelName", "level1");
            SetRef(picker,  "loader",           loader);

            // Tap a tray ball to select it, tap a grid cell to throw it. Drives the
            // BallLauncher (flight/paint/queue) and AimPreview (arc) built above.
            var tapGo = new GameObject("TapLaunchController");
            var tap   = tapGo.AddComponent<TapLaunchController>();
            SetRef(tap, "camera",       cam);
            SetRef(tap, "grid",         grid);
            SetRef(tap, "launcher",     launcher);
            SetRef(tap, "aimPreview",   aimPreview);
            SetRef(tap, "launchOrigin", originGo);    // fallback only — the tray supplies the origin
            SetRef(tap, "gameManager",  gm);
            SetRef(tap, "queue",        queue);       // tapping a tray ball picks it
            SetRef(tap, "queueView",    queueView);
            SetFloat(tap, "launchAngle", 50f);

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            // Add to build settings
            AddSceneToBuildSettings(scenePath);

            EditorUtility.DisplayDialog("Scene built",
                $"Gameplay scene saved to:\n{scenePath}\n\n" +
                "NEXT STEPS:\n" +
                "1. Level Editor → Save (writes straight to Resources/Levels)\n" +
                $"2. Open {Path.GetFileName(scenePath)} scene\n" +
                "3. Press Play\n\n" +
                "Controls: TAP a tray ball to select it, TAP a grid cell to throw it.\n" +
                "(Press and slide to preview the stamp; slide back onto the tray to cancel.)",
                "OK");
        }

        private static VolumeProfile EnsurePostProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProfilePath);
            }

            // Written on every rebuild, not only on creation: the look is code, and a
            // profile left over from an earlier build would otherwise keep old values.
            if (!profile.TryGet(out Bloom bloom))
                bloom = profile.Add<Bloom>(true);
            // Faint: cubes no longer emit, and a strong bloom haloed them into
            // each other. Only the brightest highlights catch it.
            bloom.intensity.Override(0.20f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.65f);

            if (!profile.TryGet(out Vignette vignette))
                vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // A collider-less lit primitive — scenery only (the game has no physics).
        private static void AddDeco(Transform parent, string name, PrimitiveType type,
                                    Vector3 localPos, Vector3 localScale, Shader shader, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader) { color = color };
        }

        // Adds a named layer to the project if it doesn't already exist.
        // Returns the layer index, or 0 (Default) if no free slot was found.
        private static int EnsureLayer(string layerName)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 0; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (el.stringValue == layerName)
                    return i;
            }
            // Built-in layers 0-7 are reserved; find first empty user slot
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[SceneBuilder] Added layer '{layerName}' at index {i}");
                    return i;
                }
            }
            Debug.LogWarning($"[SceneBuilder] No free layer slot for '{layerName}'. Using Default.");
            return 0;
        }

        private static void SetRefArray(Object target, string fieldName, Object[] values)
        {
            var so   = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[SceneBuilder] Array field not found: {fieldName}");
                return;
            }
            prop.ClearArray();
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string fieldName, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // One booster button in the left column (row 0 at the bottom), with its
        // "×N" count in the top-right corner. Same anchor band as the undo button
        // for row 0, so the two read as a pair.
        private static Button MakeBoosterButton(Transform parent, string label, int row, Color color,
                                                out TextMeshProUGUI countLabel)
        {
            const float bottom = 0.10f, height = 0.065f, gap = 0.01f;
            float y0 = bottom + row * (height + gap);
            var btn = MakeButton(parent, label, new Vector2(0.03f, y0), new Vector2(0.21f, y0 + height));
            btn.GetComponent<Image>().color = color;
            var text = btn.GetComponentInChildren<TextMeshProUGUI>();
            text.fontSize  = 30;
            text.fontStyle = FontStyles.Bold;
            var textRect = text.rectTransform;          // name in the lower part, count above it
            textRect.anchorMin = new Vector2(0.04f, 0.04f);
            textRect.anchorMax = new Vector2(0.96f, 0.60f);

            var countGo = MakeText(btn.transform, "Count", "×1", 28,
                                   new Vector2(0.50f, 0.58f), new Vector2(0.96f, 0.98f));
            countLabel = countGo.GetComponent<TextMeshProUGUI>();
            countLabel.alignment     = TextAlignmentOptions.TopRight;
            countLabel.fontStyle     = FontStyles.Bold;
            countLabel.raycastTarget = false;
            return btn;
        }

        // A hidden text strip on a translucent dark backing (the HUD's white text
        // needs it over the light board), faded by its CanvasGroup.
        private static CanvasGroup MakePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                             out TextMeshProUGUI label, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = anchorMin; r.anchorMax = anchorMax;
            r.offsetMin = r.offsetMax = Vector2.zero;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.12f, 0.22f, 0.55f);
            bg.raycastTarget = false;
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable   = false;

            label = MakeText(go.transform, "Text", "", fontSize, Vector2.zero, Vector2.one).GetComponent<TextMeshProUGUI>();
            label.fontStyle     = FontStyles.Bold;
            label.raycastTarget = false;
            return group;
        }
    }
}
