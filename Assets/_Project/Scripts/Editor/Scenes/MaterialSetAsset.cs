using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatapultGames.Editor
{
    // Creates (once) and refreshes Assets/Settings/MaterialSet.asset and its three
    // base materials. Shader.Find is fine here, at edit time: the result is a set of
    // .mat assets the scenes reference, which is what keeps the shaders in a build.
    internal static class MaterialSetAsset
    {
        private const string Folder  = "Assets/Settings/Materials";
        private const string SetPath = "Assets/Settings/MaterialSet.asset";

        internal static MaterialSet Ensure()
        {
            Directory.CreateDirectory(Folder);
            var set = AssetDatabase.LoadAssetAtPath<MaterialSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<MaterialSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            SceneKit.SetRef(set, "lit",    EnsureMaterial("Lit",    "Universal Render Pipeline/Lit"));
            SceneKit.SetRef(set, "unlit",  EnsureMaterial("Unlit",  "Universal Render Pipeline/Unlit"));
            SceneKit.SetRef(set, "sprite", EnsureMaterial("Sprite", "Sprites/Default"));
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        private static Material EnsureMaterial(string name, string shaderName)
        {
            string path = $"{Folder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;
            mat = new Material(Shader.Find(shaderName)) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
