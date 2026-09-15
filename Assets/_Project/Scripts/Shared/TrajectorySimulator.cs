using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Shared parabolic arc simulation used by both AimPreview and BallLauncher.
    // Simulates projectile motion in world space (Y = up), under the game's one
    // gravity value (GameConstants.Gravity).
    // Stops when the trajectory crosses Y = 0 (the grid plane).
    public static class TrajectorySimulator
    {

        // Returns list of world-space sample points along the arc.
        // landingPos is the interpolated Y=0 crossing point.
        public static List<Vector3> Simulate(
            Vector3 startPos,
            Vector3 velocity,
            int     maxSteps,
            float   timeStep,
            out Vector3 landingPos)
        {
            var  points    = new List<Vector3>(maxSteps + 1) { startPos };
            var  pos       = startPos;
            var  vel       = velocity;
            bool descending = false;
            landingPos = startPos;

            for (int i = 0; i < maxSteps; i++)
            {
                float prevY = pos.y;
                vel.y += GameConstants.Gravity * timeStep;
                pos   += vel      * timeStep;

                // Only check for ground hit once the ball is coming back down
                if (vel.y < 0f) descending = true;

                if (descending && pos.y <= 0f && prevY > 0f)
                {
                    float t = prevY / (prevY - pos.y);     // fraction at Y=0
                    landingPos   = Vector3.Lerp(points[points.Count - 1], pos, t);
                    landingPos.y = 0f;
                    points.Add(landingPos);
                    return points;
                }

                points.Add(pos);
            }

            // Arc never reached Y=0 — use last simulated point
            landingPos = points[points.Count - 1];
            return points;
        }

        // Sample a world position at normalized time t ∈ [0,1] along a path.
        public static Vector3 SamplePath(List<Vector3> path, float t)
        {
            if (path.Count == 0) return Vector3.zero;
            if (path.Count == 1 || t <= 0f) return path[0];
            if (t >= 1f) return path[path.Count - 1];

            float scaled = t * (path.Count - 1);
            int   idx    = Mathf.FloorToInt(scaled);
            float frac   = scaled - idx;

            return idx < path.Count - 1
                ? Vector3.Lerp(path[idx], path[idx + 1], frac)
                : path[path.Count - 1];
        }
    }
}
