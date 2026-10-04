using System.Collections.Generic;

namespace CatapultGames.Editor
{
    public struct AutoSolverResult
    {
        public List<AutoSolverMove> moves;
        public bool       solved;
        public int        totalColored;   // cells that still needed paint at the start
        public int        remaining;      // still empty when the plan ends
        public int        ballsUsed;      // balls that actually painted something
        public int        totalBalls;
        public AutoSolverBallCount[] leftover;      // balls never thrown (set only when solved)
        public int        leftoverTotal;  // sum of leftover counts
    }
}
