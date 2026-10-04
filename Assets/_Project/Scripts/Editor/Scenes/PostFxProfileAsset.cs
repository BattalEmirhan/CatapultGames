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
            // Profiles from earlier builds stored their overrides outside the asset;
            // those entries reload as null, and TryGet throws on them.
            profile.components.RemoveAll(c => c == null);
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
                bloom = AddOverride<Bloom>(profile);
            bloom.intensity.Override(0.20f);
            bloom.threshold.Override(1.0f);
            bloom.scatter.Override(0.65f);
        }

        private static void WriteVignette(VolumeProfile profile)
        {
            if (!profile.TryGet(out Vignette vignette))
                vignette = AddOverride<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.45f);
        }

        // An override is its own ScriptableObject. Unless it is stored inside the
        // profile asset, the reference is lost on save and the profile reloads with
        // a missing component.
        private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
