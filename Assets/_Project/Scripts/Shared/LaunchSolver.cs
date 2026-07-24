using UnityEngine;

namespace CatapultGames
{
    // Aim assist for the launch. The ball's landing is SNAPPED to the nearest
    // grid cell centre, so the aim locks onto a cell and jumps cell-by-cell as
    // the player re-aims — and it always stays inside the grid. The drag still
    // chooses which cell; this just discretises and constrains it.
    public static class LaunchSolver
    {
        private const float Gravity = 9.81f;   // matches TrajectorySimulator

        // Returns a velocity whose arc lands exactly on the nearest cell centre.
        public static Vector3 SnapToCell(GridRenderer grid, Vector3 origin, Vector3 rawVelocity)
        {
            float cs = grid != null ? grid.CellSize : 0f;
            if (grid == null || grid.Width < 1 || grid.Height < 1 || cs <= 0f ||
                rawVelocity == Vector3.zero)
                return rawVelocity;

            TrajectorySimulator.Simulate(origin, rawVelocity,
                GameConstants.TrajectorySteps, GameConstants.TrajectoryTimeStep, out Vector3 landPos);

            // Round the raw landing to the nearest cell, clamped to grid bounds.
            Vector3 local = grid.transform.InverseTransformPoint(landPos);
            int gx = Mathf.Clamp(Mathf.RoundToInt(local.x / cs), 0, grid.Width  - 1);
            int gy = Mathf.Clamp(Mathf.RoundToInt(local.z / cs), 0, grid.Height - 1);

            Vector3 target = grid.transform.TransformPoint(new Vector3(gx * cs, 0f, gy * cs));
            return SolveVelocity(origin, target, rawVelocity);
        }

        // Velocity that lands exactly on a given grid cell, at a fixed launch angle.
        // Used by the tap-to-target mode (Gameplay2): the cell is chosen directly,
        // and we solve the speed/direction needed to reach it.
        public static Vector3 SolveToCell(GridRenderer grid, Vector3 origin,
                                          int gx, int gy, float launchAngleDeg)
        {
            if (grid == null) return Vector3.zero;
            return SolveToPoint(origin, grid.GridToWorld(gx, gy), launchAngleDeg);
        }

        // Velocity to land on a world point (on the y=0 plane) at a fixed launch angle.
        // Returns zero if the point is unreachable at that angle.
        public static Vector3 SolveToPoint(Vector3 origin, Vector3 target, float launchAngleDeg)
        {
            Vector2 horiz = new Vector2(target.x - origin.x, target.z - origin.z);
            float R = horiz.magnitude;
            if (R < 0.001f) return Vector3.zero;

            Vector2 dir   = horiz / R;
            float   theta = launchAngleDeg * Mathf.Deg2Rad;
            float   dy    = target.y - origin.y;        // drop to the landing plane
            float   cosT  = Mathf.Cos(theta);
            float   sinT  = Mathf.Sin(theta);
            float   tanT  = Mathf.Tan(theta);

            float denom = 2f * cosT * cosT * (R * tanT - dy);
            if (denom <= 0.0001f) return Vector3.zero;  // unreachable at this angle

            float v = Mathf.Sqrt(Gravity * R * R / denom);
            Vector3 launchDir = new Vector3(dir.x * cosT, sinT, dir.y * cosT);
            return launchDir * v;
        }

        // Speed + direction needed to land at `target` from `origin`, keeping the
        // launch angle encoded in `rawVelocity`. Standard projectile range solve,
        // landing on the world y=0 plane (where TrajectorySimulator stops).
        private static Vector3 SolveVelocity(Vector3 origin, Vector3 target, Vector3 rawVelocity)
        {
            Vector2 rawHoriz = new Vector2(rawVelocity.x, rawVelocity.z);
            float theta = Mathf.Atan2(rawVelocity.y, rawHoriz.magnitude);

            Vector2 horiz = new Vector2(target.x - origin.x, target.z - origin.z);
            float R = horiz.magnitude;
            if (R < 0.001f) return rawVelocity;

            Vector2 dir  = horiz / R;
            float    dy   = -origin.y;          // drop to the y=0 landing plane
            float    cosT = Mathf.Cos(theta);
            float    sinT = Mathf.Sin(theta);
            float    tanT = Mathf.Tan(theta);

            float denom = 2f * cosT * cosT * (R * tanT - dy);
            if (denom <= 0.0001f) return rawVelocity;   // unreachable at this angle

            float v = Mathf.Sqrt(Gravity * R * R / denom);
            Vector3 launchDir = new Vector3(dir.x * cosT, sinT, dir.y * cosT);
            return launchDir * v;
        }
    }
}
