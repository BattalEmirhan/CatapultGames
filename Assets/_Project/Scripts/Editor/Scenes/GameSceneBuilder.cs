using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CatapultGames.Editor
{
    // GameScene: the board, the tray and every gameplay system, plus Canvas_Game —
    // the in-game HUD, on the gameplay camera and layer 0 (only UIScene's meta UI
    // uses the UI layer). Built and wired entirely in code; see SceneSetBuilder.
    internal static class GameSceneBuilder
    {
        private static readonly Color Wood     = new Color(0.60f, 0.46f, 0.34f);
        private static readonly Color DarkWood = new Color(0.52f, 0.40f, 0.30f);
        private static readonly Color Neutral  = new Color(0.2f, 0.2f, 0.2f);

        internal static void Build(MaterialSet materials, string path)
        {
            var scene = SceneKit.NewScene();
            SceneKit.EnsureLayer("CG_Grid");
            var p = new GameSceneParts { Materials = materials };
            BuildWorld(p);
            BuildHud(p);
            AddLoader(p);
            AddInput(p);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void BuildWorld(GameSceneParts p)
        {
            AddLight();
            AddCamera(p);
            AddPostFx();
            AddBoard(p);
            AddTray(p);
            AddCatapult(p);
            AddAim(p);
            AddLauncher(p);
            AddFx(p);
            AddGameManager(p);
        }

        private static void BuildHud(GameSceneParts p)
        {
            p.Canvas = SceneKit.MakeCanvas("Canvas_Game", p.Camera, 0, 10).transform;
            AddTrayCounter(p);
            GameHudBuilder.AddResultPanel(p);
            GameHudBuilder.AddUndo(p);
            GameHudBuilder.AddBoosters(p);
            GameHudBuilder.AddWarning(p);
            GameHudBuilder.AddProgressAndScore(p);
            GameHudBuilder.AddTutorial(p);
        }

        // Warm key light with soft shadows over a dim, cool ambient — the dark theme.
        // A bright ambient lifts every face toward grey and washes the colours out.
        private static void AddLight()
        {
            var light = new GameObject("DirectionalLight").AddComponent<Light>();
            light.type           = LightType.Directional;
            light.color          = new Color(1f, 0.96f, 0.90f);
            light.intensity      = 1.15f;
            light.shadows        = LightShadows.Soft;
            light.shadowStrength = 0.55f;
            light.transform.rotation    = Quaternion.Euler(58f, -28f, 0f);
            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.28f, 0.40f);
        }

        // The Base camera. It excludes the UI layer: UIScene's overlay camera draws
        // that, and seeing it here too would draw the meta UI twice.
        private static void AddCamera(GameSceneParts p)
        {
            var go  = new GameObject("GridCamera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView     = 60f;
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.15f);
            cam.cullingMask     = ~(1 << UiLayout.UiLayer);
            cam.transform.SetPositionAndRotation(new Vector3(5.5f, 14f, -5f), Quaternion.Euler(55f, 0f, 0f));
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            SceneKit.SetRef(go.AddComponent<BackgroundGradient>(), "materials", p.Materials);
            go.AddComponent<AudioListener>();
            p.Camera           = cam;
            p.CameraController = AddCameraController(go);
        }

        // Per-level overrides come from LevelData.camera; these are the defaults.
        private static GridCameraController AddCameraController(GameObject cameraGo)
        {
            var ctrl = cameraGo.AddComponent<GridCameraController>();
            ctrl.fieldOfView   = 60f;
            ctrl.tiltAngle     = 66f;
            ctrl.padding       = 1.12f;
            ctrl.gridScreenPos = 0.60f;
            return ctrl;
        }

        private static void AddPostFx()
        {
            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal      = true;
            volume.priority      = 0f;
            volume.sharedProfile = PostFxProfileAsset.Ensure();
        }

        private static void AddBoard(GameSceneParts p)
        {
            var gridGo = new GameObject("GridRoot");
            p.Grid  = gridGo.AddComponent<GridRenderer>();
            p.Board = gridGo.AddComponent<GridBoard>();
            SceneKit.SetRef(p.Grid,  "materials", p.Materials);
            SceneKit.SetRef(p.Board, "materials", p.Materials);
            p.Queue = new GameObject("BallQueue").AddComponent<BallQueue>();
        }

        // The tray and the decorative catapult share one root, which LaunchAreaAnchor
        // pins to a fixed band at the bottom of the screen whatever the level's camera.
        private static void AddTray(GameSceneParts p)
        {
            p.LaunchArea = new GameObject("LaunchArea").transform;
            p.LaunchArea.position = new Vector3(5.5f, 0f, -8f);
            SceneKit.AddDeco(p.LaunchArea, "TrayPlate", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f),
                             new Vector3(5.4f, 0.24f, 1.9f), p.Materials.Lit, new Color(0.13f, 0.14f, 0.21f));
            p.QueueView = p.Queue.gameObject.AddComponent<BallQueueView>();
            SceneKit.SetRef(p.QueueView, "queue", p.Queue);
            SceneKit.SetRef(p.QueueView, "materials", p.Materials);
            SceneKit.SetRefArray(p.QueueView, "slots", AddSlots(p.LaunchArea));
            p.LaunchOrigin = new GameObject("LaunchOrigin").transform;
            p.LaunchOrigin.SetParent(p.LaunchArea, false);
            p.LaunchOrigin.localPosition = new Vector3(0f, 0.92f, 0f);
        }

        private static Transform[] AddSlots(Transform launchArea)
        {
            var parent = new GameObject("TraySlots").transform;
            parent.SetParent(launchArea, false);
            var slots = new Transform[3];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new GameObject($"Slot_{i}").transform;
                slots[i].SetParent(parent, false);
                slots[i].localPosition = new Vector3((i - 1) * 1.65f, 0.62f, 0f);
            }
            return slots;
        }

        private static void AddCatapult(GameSceneParts p)
        {
            var root = new GameObject("Catapult").transform;
            root.SetParent(p.LaunchArea, false);
            root.localPosition = new Vector3(0f, 0f, -1.35f);
            root.localScale    = Vector3.one * 0.7f;
            var lit = p.Materials.Lit;
            SceneKit.AddDeco(root, "CatapultBase", PrimitiveType.Cube,   new Vector3(0f, 0.2f, 0f),    new Vector3(1.2f, 0.4f, 0.6f),   lit, DarkWood);
            SceneKit.AddDeco(root, "CatapultArm",  PrimitiveType.Cube,   new Vector3(0f, 0.6f, 0f),    new Vector3(0.15f, 0.9f, 0.15f), lit, Wood);
            SceneKit.AddDeco(root, "ForkL",        PrimitiveType.Sphere, new Vector3(-0.3f, 1.1f, 0f), Vector3.one * 0.18f,             lit, Wood);
            SceneKit.AddDeco(root, "ForkR",        PrimitiveType.Sphere, new Vector3(0.3f, 1.1f, 0f),  Vector3.one * 0.18f,             lit, Wood);
        }

        private static void AddAim(GameSceneParts p)
        {
            var go   = new GameObject("AimPreview");
            var line = go.AddComponent<LineRenderer>();
            line.startWidth     = 0.22f;
            line.endWidth       = 0.08f;
            line.textureMode    = LineTextureMode.Tile;
            line.sharedMaterial = new Material(p.Materials.Unlit) { color = new Color(1f, 1f, 0.3f) };
            p.Aim = go.AddComponent<AimPreview>();
            SceneKit.SetRef(p.Aim, "queue",        p.Queue);
            SceneKit.SetRef(p.Aim, "grid",         p.Grid);
            SceneKit.SetRef(p.Aim, "launchOrigin", p.LaunchOrigin);
            SceneKit.SetRef(p.Aim, "materials",    p.Materials);
        }

        private static void AddLauncher(GameSceneParts p)
        {
            p.Launcher = new GameObject("BallLauncher").AddComponent<BallLauncher>();
            SceneKit.SetRef(p.Launcher, "queue",          p.Queue);
            SceneKit.SetRef(p.Launcher, "grid",           p.Grid);
            SceneKit.SetRef(p.Launcher, "launchOrigin",   p.LaunchOrigin);
            SceneKit.SetRef(p.Launcher, "materials",      p.Materials);
            SceneKit.SetFloat(p.Launcher, "flightDuration",  0.55f);
            SceneKit.SetFloat(p.Launcher, "ballVisualScale", 0.45f);
        }

        private static void AddFx(GameSceneParts p) =>
            SceneKit.SetRef(new GameObject("GameFX").AddComponent<GameFX>(), "materials", p.Materials);

        private static void AddGameManager(GameSceneParts p)
        {
            p.GameManager = new GameObject("GameManager").AddComponent<GameManager>();
            SceneKit.SetRef(p.GameManager, "grid",       p.Grid);
            SceneKit.SetRef(p.GameManager, "queue",      p.Queue);
            SceneKit.SetRef(p.GameManager, "launcher",   p.Launcher);
            SceneKit.SetRef(p.GameManager, "aimPreview", p.Aim);
        }

        private static void AddTrayCounter(GameSceneParts p)
        {
            var label = SceneKit.MakeText(p.Canvas, "TrayRemaining", "", 62f, new Vector2(0.78f, 0.03f), new Vector2(0.98f, 0.10f));
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.fontStyle = FontStyles.Bold;
            SceneKit.SetRef(p.QueueView, "remainingLabel", label);
        }

        private static void AddLoader(GameSceneParts p)
        {
            var anchor = p.LaunchArea.gameObject.AddComponent<LaunchAreaAnchor>();
            SceneKit.SetRef(anchor, "gameCamera", p.Camera);
            SceneKit.SetFloat(anchor, "screenY", 0.10f);
            var loader = new GameObject("LevelLoader").AddComponent<LevelLoader>();
            SceneKit.SetRef(loader, "grid",         p.Grid);
            SceneKit.SetRef(loader, "queue",        p.Queue);
            SceneKit.SetRef(loader, "cam",          p.CameraController);
            SceneKit.SetRef(loader, "board",        p.Board);
            SceneKit.SetRef(loader, "progressHUD",  p.Progress);
            SceneKit.SetRef(loader, "launchAnchor", anchor);
            SceneKit.SetRef(loader, "gameManager",  p.GameManager);
            SceneKit.SetRef(loader, "boosters",     p.Boosters);
            SceneKit.SetRef(loader, "tutorial",     p.Tutorial);
            SceneKit.SetStr(loader, "defaultLevelName", "level1");
        }

        private static void AddInput(GameSceneParts p)
        {
            var tap = new GameObject("TapLaunchController").AddComponent<TapLaunchController>();
            SceneKit.SetRef(tap, "gameCamera",   p.Camera);
            SceneKit.SetRef(tap, "grid",         p.Grid);
            SceneKit.SetRef(tap, "launcher",     p.Launcher);
            SceneKit.SetRef(tap, "aimPreview",   p.Aim);
            SceneKit.SetRef(tap, "launchOrigin", p.LaunchOrigin);
            SceneKit.SetRef(tap, "gameManager",  p.GameManager);
            SceneKit.SetRef(tap, "queue",        p.Queue);
            SceneKit.SetRef(tap, "queueView",    p.QueueView);
            SceneKit.SetFloat(tap, "launchAngle", 50f);
        }
    }
}
