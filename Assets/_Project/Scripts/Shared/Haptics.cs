using UnityEngine;

namespace CatapultGames
{
    // Lightweight haptic feedback.
    //
    // On a real Android device it drives the Vibrator service with amplitude
    // control (API 26+), falling back to a plain buzz on older devices. It is a
    // no-op in the editor and on other platforms. The Handheld.Vibrate() call in
    // the fallback also makes Unity add android.permission.VIBRATE to the build.
    public static class Haptics
    {
        public static bool Enabled = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static int  _apiLevel;
        private static bool _init;

        private static void EnsureInit()
        {
            if (_init) return;
            _init = true;
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    _apiLevel = version.GetStatic<int>("SDK_INT");

                using (var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch { _vibrator = null; }
        }

        // ms = duration, amplitude = 1..255 (ignored < API 26).
        private static void Buzz(long ms, int amplitude)
        {
            EnsureInit();
            if (_vibrator == null) { Handheld.Vibrate(); return; }   // also triggers VIBRATE permission
            try
            {
                if (_apiLevel >= 26)
                {
                    using (var fx = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = fx.CallStatic<AndroidJavaObject>(
                               "createOneShot", ms, Mathf.Clamp(amplitude, 1, 255)))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", ms);
                }
            }
            catch { /* device without a usable vibrator — ignore */ }
        }
#endif

        // Tiny tick — e.g. a single grid cube starting to rise.
        public static void Light()
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Buzz(10, 70);
#endif
        }

        // Medium pulse.
        public static void Medium()
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Buzz(25, 140);
#endif
        }

        // Stronger pulse — e.g. the whole paint wave finishing.
        public static void Heavy()
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            Buzz(50, 220);
#endif
        }
    }
}
