using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // Everything needed to put the world back the way it was before the last
    // shot: the queue as it stood before the ball was consumed, and the cells
    // this shot put paint into. Painting only ever ADDS hits (an Ice cell may
    // have merely cracked), so removing exactly these hits is an exact inverse.
    //
    // Set at landing, so it always describes the most recently LANDED shot.
    // GameManager only offers undo while nothing is in flight, which keeps
    // "the last shot" unambiguous when the player fires overlapping shots.
    public sealed class ShotRecord
    {
        public BallQueueSnapshot  queue;
        public List<Vector2Int>    painted;   // cells this shot hit (one hit each)
        public BallData            ball;
    }
}
