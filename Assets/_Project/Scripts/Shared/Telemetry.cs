using System;
using UnityEngine;

namespace CatapultGames
{
    // Very small telemetry helper for local A/B testing and metrics.
    public static class Telemetry
    {
        public static event Action<float> OnTimeBetweenLaunchesRecorded;

        private static float _lastLaunchTime = -1f;

        public static void RecordLaunch()
        {
            float now = Time.realtimeSinceStartup;
            if (_lastLaunchTime > 0f)
            {
                float dt = now - _lastLaunchTime;
                OnTimeBetweenLaunchesRecorded?.Invoke(dt);
                Debug.Log($"[Telemetry] TimeBetweenLaunches: {dt:F3}s");
            }
            _lastLaunchTime = now;
        }

        public static void Reset() => _lastLaunchTime = -1f;
    }
}
