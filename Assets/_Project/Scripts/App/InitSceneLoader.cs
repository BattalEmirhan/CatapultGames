using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatapultGames
{
    // Entry point (InitScene, build index 0). Loads the rest of the scene set
    // additively, strictly in order — Boot, Game, UI — makes GameScene the active
    // scene (new objects and lighting belong there), then unloads itself.
    // UIScene must come after GameScene: its camera stacker looks up the gameplay
    // camera, and its menus act on a board that is already there.
    public sealed class InitSceneLoader : MonoBehaviour
    {
        [SerializeField] private string[] sceneOrder = { "BootScene", "GameScene", "UIScene" };
        [SerializeField] private string   activeScene = "GameScene";

        private IEnumerator Start()
        {
            for (int i = 0; i < sceneOrder.Length; i++)
                yield return SceneManager.LoadSceneAsync(sceneOrder[i], LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(activeScene));
            yield return SceneManager.UnloadSceneAsync(gameObject.scene);
        }
    }
}
