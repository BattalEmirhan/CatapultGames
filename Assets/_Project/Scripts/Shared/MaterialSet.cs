using UnityEngine;

namespace CatapultGames
{
    // Base materials every runtime-built renderer copies. Held by an asset the
    // scenes reference, so the shaders are never stripped from a player build —
    // Shader.Find returns null there for anything no built asset points at.
    [CreateAssetMenu(menuName = "CatapultGames/Material Set")]
    public sealed class MaterialSet : ScriptableObject
    {
        public Material Lit => lit;
        public Material Unlit => unlit;
        public Material Sprite => sprite;

        [SerializeField] private Material lit;
        [SerializeField] private Material unlit;
        [SerializeField] private Material sprite;
    }
}
