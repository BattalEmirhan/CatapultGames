using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames.Editor
{
    // Creates a ready-to-play Gameplay scene with one menu click.
    // Menu: CatapultGames → Build Gameplay Scene
    public static class GameplaySceneBuilder
    {
        private const string ScenePath  = "Assets/_Project/Scenes/Gameplay.unity";
        private const string Scene2Path = "Assets/Scenes/Gameplay2.unity";

        [MenuItem("CatapultGames/Build Gameplay Scene", priority = 1)]
        public static void Build() => BuildScene(tapMode: false, scenePath: ScenePath);

        // Gameplay2 — same game, but you tap a grid cell to auto-launch there
        // (no slingshot drag). Overwrites the existing Gameplay2.unity.
        [MenuItem("CatapultGames/Build Gameplay2 Scene (Tap to Target)", priority = 2)]
        public static void BuildGameplay2() => BuildScene(tapMode: true, scenePath: Scene2Path);

        private static void BuildScene(bool tapMode, string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string sceneFolder = Path.GetDirectoryName(scenePath);
            if (!string.IsNullOrEmpty(sceneFolder) && !Directory.Exists(sceneFolder))
                Directory.CreateDirectory(sceneFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Register the CG_Grid layer so CellView and GridBoard can find it by name.
            EnsureLayer("CG_Grid");

            // ── Lighting ──────────────────────────────────────────────────
            var lightGo = new GameObject("DirectionalLight");
            var light   = lightGo.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.color     = new Color(1f, 0.97f, 0.90f);
            light.intensity = 1.15f;
            light.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.28f, 0.40f);

            // ── Single Perspective Camera — full screen, all layers ───────
            // Renders grid cubes, slingshot, trajectory, and queue together.
            // GridCameraController.FitToGrid() auto-positions at runtime so that
            // both the grid and the slingshot area are always in frame.
            // Inspector sliders: fieldOfView (60°) and tiltAngle (55°) to taste.
            var camGo  = new GameObject("GridCamera");
            camGo.tag  = "MainCamera";   // so Camera.main resolves — GameFX shake/zoom/firework use it
            var cam    = camGo.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView  = 60f;
            cam.clearFlags   = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.15f);
            cam.cullingMask  = -1;  // render everything
            cam.depth        = 0;
            // Placeholder position — FitToGrid() overwrites this at runtime
            cam.transform.SetPositionAndRotation(
                new Vector3(5.5f, 14f, -5f),
                Quaternion.Euler(55f, 0f, 0f));
            var camCtrl = camGo.AddComponent<GridCameraController>();
            camCtrl.fieldOfView   = 60f;
            camCtrl.tiltAngle     = 50f;    // 50° = good balance: grid readable + slingshot visible
            camCtrl.padding       = 1.08f;
            camCtrl.gridScreenPos = 0.5f;   // grid centred on screen (per-level override via LevelData.camera)

            // ── Grid + board ──────────────────────────────────────────────
            var gridGo = new GameObject("GridRoot");
            var grid   = gridGo.AddComponent<GridRenderer>();
            var board  = gridGo.AddComponent<GridBoard>();  // background board, same GO

            // ── Ball queue ────────────────────────────────────────────────
            var queueGo = new GameObject("BallQueue");
            var queue   = queueGo.AddComponent<BallQueue>();

            // ── Launch area root ──────────────────────────────────────────
            // The catapult AND the queue waypoints live under one root so
            // LaunchAreaAnchor can pin the whole group to a fixed band at the bottom
            // of the screen — it never slides when a level's grid camera changes.
            const float catX = 5.5f;
            const float catZ = -8f;
            var launchAreaGo = new GameObject("LaunchArea");
            launchAreaGo.transform.position = new Vector3(catX, 0f, catZ);

            // Waypoints: spread left on X from the catapult, same plane (Z=catZ).
            // WP_0: catapult rest pos (slide target; the ball is parented to ballPivot).
            // WP_1..5: queue balls to the left of the catapult, Y=0.4 (sit on ground).
            var wpParent = new GameObject("Waypoints").transform;
            wpParent.SetParent(launchAreaGo.transform);
            var waypoints = new Transform[6];   // 1 catapult slot + 5 queue slots
            for (int i = 0; i < waypoints.Length; i++)
            {
                var wp = new GameObject($"WP_{i}").transform;
                wp.SetParent(wpParent);
                wp.position = i == 0
                    ? new Vector3(catX, 1.5f, catZ)            // catapult
                    : new Vector3(catX - i * 1.15f, 0.4f, catZ); // queue spreads left
                waypoints[i] = wp;
            }

            var queueView = queueGo.AddComponent<BallQueueView>();
            SetRef(queueView, "_queue", queue);
            SetFloat(queueView, "_currentBallScale", 1.10f);  // big — catapult ball must be obvious
            SetFloat(queueView, "_queueBallScale",   0.65f);  // clearly visible queue balls
            // _catapultPivot wired after ballPivot is created (below)
            SetRefArray(queueView, "_waypoints", waypoints);

            // ── Launch origin — placed well in front of grid so camera shows it ──
            // Grid occupies Z=0..height. Slingshot at Z=-8 sits low in the bottom
            // area below the centred grid (camera frames both via gridScreenPos).
            // LaunchSolver re-solves the launch speed per shot, so the further origin
            // doesn't affect which cells are reachable.
            var originGo = new GameObject("LaunchOrigin").transform;
            originGo.position = new Vector3(5.5f, 1.5f, -8f);

            // ── Catapult / launch base ────────────────────────────────────
            // In both modes the ball sits on the catapult and flies from here.
            // tapMode: no SlingshotController (no drag) — TapLaunchController fires it.
            var slingshotGo = new GameObject("Slingshot");
            slingshotGo.transform.position = new Vector3(catX, 0f, catZ);
            slingshotGo.transform.SetParent(launchAreaGo.transform, worldPositionStays: true);

            SlingshotController slingshot = null;
            if (!tapMode)
            {
                slingshot = slingshotGo.AddComponent<SlingshotController>();
                SetFloat(slingshot, "_maxDragPixels",   300f);  // longer pull = finer power control
                SetFloat(slingshot, "_minDragPixels",   22f);   // bigger dead-zone = less accidental jitter
                SetFloat(slingshot, "_maxLaunchSpeed",  15f);   // full drag lands ~far edge (less overshoot band)
                SetFloat(slingshot, "_launchAngle",     50f);
                SetFloat(slingshot, "_inputZoneHeight",     0.5f);  // larger comfy bottom drag area
                SetFloat(slingshot, "_aimSmoothing",        14f);   // low-pass the aim so it isn't twitchy
                SetFloat(slingshot, "_releaseJitterPixels", 35f);   // ignore finger-lift smear on release
            }

            // Catapult body — simple visible base so the user sees the catapult
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "CatapultBase";
            Object.DestroyImmediate(baseObj.GetComponent<BoxCollider>());
            baseObj.transform.SetParent(slingshotGo.transform);
            baseObj.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            baseObj.transform.localScale    = new Vector3(1.2f, 0.4f, 0.6f);
            var baseMr = baseObj.GetComponent<MeshRenderer>();
            var baseShader = Shader.Find("Universal Render Pipeline/Lit");
            if (!baseShader) baseShader = Shader.Find("Standard");
            baseMr.sharedMaterial = new Material(baseShader) { color = new Color(0.25f, 0.18f, 0.12f) };

            // Arm
            var armObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            armObj.name = "CatapultArm";
            Object.DestroyImmediate(armObj.GetComponent<BoxCollider>());
            armObj.transform.SetParent(slingshotGo.transform);
            armObj.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            armObj.transform.localScale    = new Vector3(0.15f, 0.9f, 0.15f);
            armObj.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(baseShader) { color = new Color(0.35f, 0.25f, 0.15f) };

            // Fork tips (Y-shape slingshot)
            var forkL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            forkL.name = "ForkL";
            Object.DestroyImmediate(forkL.GetComponent<SphereCollider>());
            forkL.transform.SetParent(slingshotGo.transform);
            forkL.transform.localPosition = new Vector3(-0.3f, 1.1f, 0f);
            forkL.transform.localScale    = Vector3.one * 0.18f;
            forkL.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(baseShader) { color = new Color(0.35f, 0.25f, 0.15f) };

            var forkR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            forkR.name = "ForkR";
            Object.DestroyImmediate(forkR.GetComponent<SphereCollider>());
            forkR.transform.SetParent(slingshotGo.transform);
            forkR.transform.localPosition = new Vector3(0.3f, 1.1f, 0f);
            forkR.transform.localScale    = Vector3.one * 0.18f;
            forkR.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(baseShader) { color = new Color(0.35f, 0.25f, 0.15f) };

            // BallPivot: height 1.5 = same as originGo so ball sits at arc start point
            var anchorL = new GameObject("AnchorLeft").transform;
            anchorL.SetParent(slingshotGo.transform);
            anchorL.localPosition = new Vector3(-0.3f, 1.4f, 0f);
            var anchorR = new GameObject("AnchorRight").transform;
            anchorR.SetParent(slingshotGo.transform);
            anchorR.localPosition = new Vector3(0.3f, 1.4f, 0f);
            var ballPivot = new GameObject("BallPivot").transform;
            ballPivot.SetParent(slingshotGo.transform);
            ballPivot.localPosition = new Vector3(0f, 1.5f, 0f);  // matches originGo Y

            originGo.SetParent(slingshotGo.transform);

            // Rubber band + stretch visual — slingshot mode only (no drag in tap mode).
            if (!tapMode)
            {
                var lr = slingshotGo.AddComponent<LineRenderer>();
                lr.startWidth    = 0.10f;
                lr.endWidth      = 0.10f;
                lr.positionCount = 3;
                lr.useWorldSpace = true;
                var lrShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!lrShader) lrShader = Shader.Find("Unlit/Color");
                lr.sharedMaterial = new Material(lrShader) { color = new Color(1f, 0.85f, 0.1f) };

                var slingshotVis = slingshotGo.AddComponent<SlingshotVisual>();
                SetRef(slingshotVis, "_controller",  slingshot);
                SetRef(slingshotVis, "_anchorLeft",  anchorL);
                SetRef(slingshotVis, "_anchorRight", anchorR);
                SetRef(slingshotVis, "_ballPivot",   ballPivot);
            }

            // Now that ballPivot exists, wire it as the catapult position for the queue view
            SetRef(queueView, "_catapultPivot", ballPivot);

            // ── Aim preview — bright dotted arc, thick enough to read on phone ─
            var aimGo  = new GameObject("AimPreview");
            var aimLR  = aimGo.AddComponent<LineRenderer>();
            aimLR.startWidth   = 0.22f;   // thick start
            aimLR.endWidth     = 0.08f;   // tapers toward landing
            var aimShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!aimShader) aimShader = Shader.Find("Unlit/Color");
            aimLR.sharedMaterial = new Material(aimShader) { color = new Color(1f, 1f, 0.3f) };  // bright yellow
            aimLR.textureMode    = LineTextureMode.Tile;

            var aimPreview = aimGo.AddComponent<AimPreview>();
            if (!tapMode) SetRef(aimPreview, "_slingshot", slingshot);  // tap mode drives it directly
            SetRef(aimPreview, "_queue",        queue);
            SetRef(aimPreview, "_grid",         grid);
            SetRef(aimPreview, "_launchOrigin", originGo);
            // Arc resolution is shared via GameConstants so preview == real landing.

            // ── Ball launcher ─────────────────────────────────────────────
            var launcherGo = new GameObject("BallLauncher");
            var launcher   = launcherGo.AddComponent<BallLauncher>();
            if (!tapMode) SetRef(launcher, "_slingshot", slingshot);  // tap mode calls Launch() directly
            SetRef(launcher, "_queue",        queue);
            SetRef(launcher, "_grid",         grid);
            SetRef(launcher, "_launchOrigin", originGo);
            SetFloat(launcher, "_flightDuration",   0.55f);  // snappy travel from the Z=-5 slingshot (was 1.10)
            SetFloat(launcher, "_ballVisualScale",  0.45f);

            // ── EventSystem (required for Canvas button clicks on mobile) ──
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Reflection avoids a hard assembly reference — works with old and new Input System
            var inputModuleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
                esGo.AddComponent(inputModuleType);
            else
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // ── Result screen UI ──────────────────────────────────────────
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

            // ── GameManager ───────────────────────────────────────────────
            var gmGo = new GameObject("GameManager");
            var gm   = gmGo.AddComponent<GameManager>();
            SetRef(gm, "_grid",         grid);
            SetRef(gm, "_queue",        queue);
            SetRef(gm, "_launcher",     launcher);
            SetRef(gm, "_aimPreview",   aimPreview);

            // ResultScreenUI
            var resultUI = canvasGo.AddComponent<ResultScreenUI>();
            SetRef(resultUI, "_panel",        panel);
            SetRef(resultUI, "_titleText",    titleGo.GetComponent<TextMeshProUGUI>());
            SetRef(resultUI, "_subtitleText", subGo.GetComponent<TextMeshProUGUI>());
            SetRef(resultUI, "_retryButton",  retryBtn);
            SetRef(resultUI, "_menuButton",   menuBtn);
            SetRef(resultUI, "_gameManager",  gm);
            SetRef(gm, "_resultScreen",  resultUI);
            // Button listeners are wired in ResultScreenUI.Awake — no need to add them here.

            // ── Progress HUD — top-left, inside safe area ─────────────────
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
            SetRef(progressHUD, "_label", pTMP);
            SetRef(progressHUD, "_grid",  grid);

            // ── Level picker HUD (dev tool — top-right dropdown) ──────────
            var pickerGo = new GameObject("LevelPickerHUD");
            var picker   = pickerGo.AddComponent<LevelPickerHUD>();

            // ── Launch area anchor — pins catapult + queue to the screen bottom ──
            // Keeps the balls in a fixed bottom band regardless of the level's grid
            // camera (tilt / zoom / offset), so they never slide around the screen.
            var launchAnchor = launchAreaGo.AddComponent<LaunchAreaAnchor>();
            SetRef(launchAnchor,   "_camera",  cam);
            SetFloat(launchAnchor, "_screenY", 0.08f);   // sits in the bottom fifth

            // ── Level loader ──────────────────────────────────────────────
            var loaderGo = new GameObject("LevelLoader");
            var loader   = loaderGo.AddComponent<LevelLoader>();
            SetRef(loader,  "_grid",             grid);
            SetRef(loader,  "_queue",            queue);
            SetRef(loader,  "_cam",              camCtrl);
            SetRef(loader,  "_board",            board);
            SetRef(loader,  "_progressHUD",      progressHUD);
            SetRef(loader,  "_levelPicker",      picker);
            SetRef(loader,  "_launchAnchor",     launchAnchor);
            SetStr(loader,  "_defaultLevelName", "level1");
            SetRef(picker,  "_loader",           loader);

            // ── Tap-to-target input (Gameplay2 only) ──────────────────────
            // Tap a grid cell → the catapult ball auto-launches there. Reuses the
            // BallLauncher (flight/paint/queue) and AimPreview (arc) built above.
            if (tapMode)
            {
                var tapGo = new GameObject("TapLaunchController");
                var tap   = tapGo.AddComponent<TapLaunchController>();
                SetRef(tap, "_camera",       cam);
                SetRef(tap, "_grid",         grid);
                SetRef(tap, "_launcher",     launcher);
                SetRef(tap, "_aimPreview",   aimPreview);
                SetRef(tap, "_launchOrigin", originGo);
                SetRef(tap, "_gameManager",  gm);
                SetFloat(tap, "_launchAngle",     50f);
                SetFloat(tap, "_aimLiftCells",    2f);       // max lift when held (grid rows)
                SetFloat(tap, "_tapMaxTime",      0.12f);    // quick tap = direct (no lift)
                SetFloat(tap, "_aimLiftRampTime", 0.18f);    // lift ramps in while holding
            }

            // ── Save scene ────────────────────────────────────────────────
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            // Add to build settings
            AddSceneToBuildSettings(scenePath);

            string controls = tapMode
                ? "Controls: TAP a grid cell — the ball auto-launches there.\n(Hold to preview the arc, release on the cell to fire.)"
                : "Controls: drag bottom-half of Game view to aim, release to fire.";

            EditorUtility.DisplayDialog("Scene built",
                $"{(tapMode ? "Gameplay2 (tap to target)" : "Gameplay")} scene saved to:\n{scenePath}\n\n" +
                "NEXT STEPS:\n" +
                "1. Level Editor → Export to StreamingAssets (exports level1.json)\n" +
                $"2. Open {Path.GetFileName(scenePath)} scene\n" +
                "3. Press Play\n\n" +
                controls,
                "OK");
        }

        // ── Helpers ───────────────────────────────────────────────────────

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
                if (el.stringValue == layerName) return i;
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

        private static void SetRef(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null) { prop.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning($"[SceneBuilder] Field not found: {fieldName} on {target.GetType().Name}");
        }

        private static void SetRefArray(Object target, string fieldName, Object[] values)
        {
            var so   = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogWarning($"[SceneBuilder] Array field not found: {fieldName}"); return; }
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
            if (prop != null) { prop.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static void SetStr(Object target, string fieldName, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop != null) { prop.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        private static GameObject MakeText(Transform parent, string name, string text,
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

        private static Button MakeButton(Transform parent, string label,
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

        private static void StretchToParent(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax  = Vector2.zero;
        }

        private static void AddSceneToBuildSettings(string scenePath)
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
    }
}
