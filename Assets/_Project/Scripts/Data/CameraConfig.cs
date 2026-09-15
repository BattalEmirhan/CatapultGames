using System;
using UnityEngine;

namespace CatapultGames
{
    // Per-level camera framing. Stored inside LevelData so every level can pick
    // its own grid angle / zoom / position. Applied by GridCameraController at load.
    //
    // Backward-compatible: levels saved before this field existed simply load with
    // these defaults (which match the values GameplaySceneBuilder used).
    [Serializable]
    public class CameraConfig
    {
        // Defaults are the "flat board" framing of a casual block puzzle: steep tilt
        // so the grid reads like a 2D board, sitting in the upper two thirds with
        // the ball tray in the band below it.
        [Range(30f, 90f)] public float fieldOfView  = 60f;   // vertical FOV
        [Range(30f, 85f)] public float tiltAngle    = 66f;   // 90 = top-down, 45 = diagonal
        [Range(1f, 1.5f)] public float padding      = 1.12f; // extra breathing room

        // Where the GRID CENTRE sits on screen vertically:
        // 0 = bottom, 0.5 = centred, 1 = top.
        [Range(0f, 1f)]   public float gridScreenPos = 0.60f;

        // Manual nudge applied AFTER auto-fit (world units). Lets a designer slide
        // the framing around without touching the fit math.
        public Vector3 offset = Vector3.zero;
    }
}
