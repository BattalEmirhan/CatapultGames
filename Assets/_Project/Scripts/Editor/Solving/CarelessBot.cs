using UnityEngine;

namespace CatapultGames.Editor
{
    // Greedy-window intent with a mis-aim: with probability p the throw
    // lands one cell off in x and/or y. The skill spectrum is this one
    // policy at different error rates.
    internal sealed class CarelessBot : ISolverBot
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description => $"Greedy window, but {_p * 100:0} % of throws land one cell off. The band bot models a careful human.";
        public bool IsExpensive => false;

        internal readonly float _p;

        public CarelessBot(string id, string name, float errorRate) { Id = id; DisplayName = name; _p = errorRate; }

        public void BeginEpisode(PlayoutBoard board, System.Random rng) { }

        public SolverMove ChooseMove(PlayoutBoard board, System.Random rng)
        {
            var m = SolverRoster.BestOverWindow(board, out _);
            if (rng.NextDouble() < _p)
            {
                m.landX = Mathf.Clamp(m.landX + rng.Next(-1, 2), 0, board.Width - 1);
                m.landY = Mathf.Clamp(m.landY + rng.Next(-1, 2), 0, board.Height - 1);
            }
            return m;
        }
    }
}
