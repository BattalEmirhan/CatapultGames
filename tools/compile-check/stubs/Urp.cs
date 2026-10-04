// Compile-check stubs for URP / SRP core. Shape only.
namespace UnityEngine.Rendering
{
    public class VolumeComponent : ScriptableObject { }
    public class VolumeParameter<T> { public T value; public void Override(T v) { } }
    public class ClampedFloatParameter : VolumeParameter<float> { }
    public class MinFloatParameter : VolumeParameter<float> { }
    public class VolumeProfile : ScriptableObject { public T Add<T>(bool overrides = false) where T : VolumeComponent => null; }
    public class Volume : MonoBehaviour { public bool isGlobal; public float priority; public VolumeProfile sharedProfile; }
}
namespace UnityEngine.Rendering.Universal
{
    public class Bloom : VolumeComponent { public ClampedFloatParameter intensity, scatter; public MinFloatParameter threshold; }
    public class Vignette : VolumeComponent { public ClampedFloatParameter intensity, smoothness; }
    public class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing; }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera c) => null; }
}
