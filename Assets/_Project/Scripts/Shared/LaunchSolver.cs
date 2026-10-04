using UnityEngine;

namespace CatapultGames
{
    // Launch solver for tap-to-target aiming: the player picks a grid cell and
    // this works out the velocity whose arc lands exactly on that cell centre,
    // at a fixed launch angle.
    public static class LaunchSolver
    {
        // Positive g for the range solve — same source TrajectorySimulator
        // integrates, so the solved arc is the arc that gets drawn and flown.
        private const float Gravity = GameConstants.GravityMagnitude;

        // Velocity that lands exactly on a given grid cell, at a fixed launch angle.
        // The cell is chosen directly by the player (TapLaunchController), and we
        // solve the speed/direction needed to reach it.
        public static Vector3 SolveToCell(GridRenderer grid, Vector3 origin,
                                          int gx, int gy, float launchAngleDeg)
        {
            if (grid == null)
                return Vector3.zero;
            return SolveToPoint(origin, grid.GridToWorld(gx, gy), launchAngleDeg);
        }

        // Velocity to land on a world point (on the y=0 plane) at a fixed launch angle.
        // Returns zero if the point is unreachable at that angle.
        public static Vector3 SolveToPoint(Vector3 origin, Vector3 target, float launchAngleDeg)
        {
            Vector2 horiz = new Vector2(target.x - origin.x, target.z - origin.z);
            float R = horiz.magnitude;
            if (R < 0.001f)
                return Vector3.zero;

            Vector2 dir   = horiz / R;
            float   theta = launchAngleDeg * Mathf.Deg2Rad;
            float   dy    = target.y - origin.y;        // drop to the landing plane
            float   cosT  = Mathf.Cos(theta);
            float   sinT  = Mathf.Sin(theta);
            float   tanT  = Mathf.Tan(theta);

            float denom = 2f * cosT * cosT * (R * tanT - dy);
            if (denom <= 0.0001f)
                return Vector3.zero;  // unreachable at this angle

            float v = Mathf.Sqrt(Gravity * R * R / denom);
            Vector3 launchDir = new Vector3(dir.x * cosT, sinT, dir.y * cosT);
            return launchDir * v;
        }
    }
}
