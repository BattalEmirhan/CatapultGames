using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CatapultGames.Editor
{
    // The gameplay post-processing profile. A Volume must reference an asset or the
    // saved scene loses it; the values are rewritten on every build so a profile
    // left from an earlier build can't keep old ones.
    internal static class PostFxProfileAsset
    {
        private const string Path = "Assets/Settings/GameplayPostFX.asset";

        internal static VolumeProfile Ensure()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, Path);
            }
            WriteBloom(profile);
            WriteVignette(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        // Faint: the cubes don't emit, and a strong bloom haloed them into each other.
        private static void WriteBloom(VolumeProfile profile)
        {
            if (!profile.TryGet(out Bloom bloom))
                bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.20f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.65f);
        }

        private static void WriteVignette(VolumeProfile profile)
        {
            if (!profile.TryGet(out Vignette vignette))
                vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.45f);
        }
    }
}
