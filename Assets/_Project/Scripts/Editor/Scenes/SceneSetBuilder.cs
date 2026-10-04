using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatapultGames.Editor
{
    // Builds the whole scene set in one click and makes it the build:
    //   InitScene (index 0) → loads Boot, Game, UI additively, then unloads itself
    //   BootScene           → app-wide settings (Bootstrap)
    //   GameScene           → board, gameplay systems, in-game HUD
    //   UIScene             → meta UI over the board, overlay camera, EventSystem
    // Nothing is placed by hand, so a rebuild can never lose a reference.
    internal static class SceneSetBuilder
    {
        private const string Folder = "Assets/Scenes";

        private static readonly string[] Order = { "InitScene", "BootScene", "GameScene", "UIScene" };

        [MenuItem("CatapultGames/Build Scenes", priority = 20)]
        private static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Directory.CreateDirectory(Folder);
            var materials = MaterialSetAsset.Ensure();
            BuildSingle(PathOf("InitScene"), "InitSceneLoader", typeof(InitSceneLoader));
            BuildSingle(PathOf("BootScene"), "Bootstrap",       typeof(Bootstrap));
            GameSceneBuilder.Build(materials, PathOf("GameScene"));
            UiSceneBuilder.Build(PathOf("UIScene"));
            AssetDatabase.Refresh();
            SetBuildScenes();
            EditorSceneManager.OpenScene(PathOf("InitScene"));
            EditorUtility.DisplayDialog("Scenes built",
                $"Saved {string.Join(", ", Order)} to {Folder}.\n\n" +
                "Press Play from InitScene — it loads the others in order.", "OK");
        }

        private static string PathOf(string scene) => $"{Folder}/{scene}.unity";

        // Init and Boot each hold a single component on a single object.
        private static void BuildSingle(string path, string objectName, System.Type component)
        {
            var scene = SceneKit.NewScene();
            new GameObject(objectName, component);
            EditorSceneManager.SaveScene(scene, path);
        }

        // Exactly the set, in load order: a stale extra scene at index 0 would boot
        // the app somewhere that never loads the rest.
        private static void SetBuildScenes()
        {
            var scenes = new EditorBuildSettingsScene[Order.Length];
            for (int i = 0; i < Order.Length; i++)
                scenes[i] = new EditorBuildSettingsScene(PathOf(Order[i]), true);
            EditorBuildSettings.scenes = scenes;
        }
    }
}
