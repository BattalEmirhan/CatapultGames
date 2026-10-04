using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames.Editor
{
    // Headless board: the game's rule engine without the scene. Every rule is
    // the runtime's own —
    //   · stamp / match / reach / ice cost  → CoverageAnalyzer (GameConstants)
    //   · pulling a ball forward            → BallQueue.SelectSlot window (3)
    //   · colour purge                      → GameManager.PurgeCompletedColors
    //   · dead-end                          → GameManager.CheckDeadEnd
    // so a bot cannot play by rules the player does not have.
    //
    // A bot CANNOT undo. A bad early throw is the loss itself — that is the
    // signal. Bots that look ahead do it on a Clone() and still commit one move.
    //
    // NOT simulated (known gaps, not oversights): the "Keep Going" extra-ball
    // rescue, the undo button, scoring, and the physical arc (landing is exact).
    public sealed class PlayoutBoard
    {
        public TargetBoard Board => _board;   // read it, never write it — use Apply
        public IReadOnlyList<BallData> Queue => _queue;
        public int Width  => _board.width;
        public int Height => _board.height;
        public int Shots  => _shots;
        public int Wasted => _wasted;
        public int RemainingHits  => _board.TotalHits();
        public int RemainingCells => _board.TargetCellCount();
        public int SelectableCount => Mathf.Min(SelectableSlots, _queue.Count);

        // Mirrors BallQueueView._selectableSlots' default. If that changes, the
        // simulator's freedom must change with it.
        public const int SelectableSlots = 3;
        private readonly TargetBoard _board;
        private readonly List<BallData> _queue;
        private int _shots;
        private int _wasted;

        private PlayoutBoard(TargetBoard board, List<BallData> queue, int shots, int wasted)
        {
            _board  = board;
            _queue  = queue;
            _shots  = shots;
            _wasted = wasted;
        }

        public static PlayoutBoard From(LevelData level)
        {
            TargetBoard board = CoverageAnalyzer.BuildTargets(level);
            var queue = new List<BallData>();
            if (level?.balls != null)
                foreach (var b in level.balls)
                    if (b != null && b.color != CellColor.None)
                        queue.Add(new BallData(b.color, Mathf.Clamp(b.powerLevel, 1, 3), b.shape));   // same clamp LevelLoader applies
            return new PlayoutBoard(board, queue, 0, 0);
        }

        // Deep copy for private exploration by look-ahead bots.
        public PlayoutBoard Clone()
        {
            TargetBoard b = new TargetBoard(_board.width, _board.height);
            System.Array.Copy(_board.colors, b.colors, b.colors.Length);
            System.Array.Copy(_board.hits,   b.hits,   b.hits.Length);
            System.Array.Copy(_board.wild,   b.wild,   b.wild.Length);
            System.Array.Copy(_board.stone,  b.stone,  b.stone.Length);
            var q = new List<BallData>(_queue.Count);
            foreach (var ball in _queue)
                q.Add(new BallData(ball.color, ball.powerLevel, ball.shape));
            return new PlayoutBoard(b, q, _shots, _wasted);
        }

        public BallData Peek(int slot) => slot >= 0 && slot < _queue.Count ? _queue[slot] : null;
        public bool IsStone(int x, int y) => _board.InBounds(x, y) && _board.stone[_board.Index(x, y)];

        // Hits a throw would land, by the game's rules. No side effects.
        public int Measure(int slot, int landX, int landY)
        {
            var ball = Peek(slot);
            if (ball == null || !_board.InBounds(landX, landY))
                return 0;
            return CoverageAnalyzer.CountPlacement(_board, ball, landX, landY);
        }

        public int Best(int slot, out int landX, out int landY)
        {
            landX = landY = -1;
            var ball = Peek(slot);
            return ball == null ? 0 : CoverageAnalyzer.BestPlacement(_board, ball, out landX, out landY);
        }

        // Commits a move: consume the chosen ball (SelectSlot + Consume), paint,
        // then purge colours the way GameManager does after every landing.
        public int Apply(SolverMove m)
        {
            int slot = Mathf.Clamp(m.slot, 0, SelectableCount - 1);
            var ball = Peek(slot);
            if (ball == null)
                return 0;
            _queue.RemoveAt(slot);
            _shots++;

            int hits = _board.InBounds(m.landX, m.landY)
                ? CoverageAnalyzer.ApplyPlacement(_board, ball, m.landX, m.landY)
                : 0;
            if (hits == 0)
                _wasted++;

            PurgeCompletedColors();
            return hits;
        }

        // Result if the run is over, null while it continues. Checked after
        // every move exactly like GameManager.OnBallLanded.
        public PlayoutOutcome? Evaluate()
        {
            if (RemainingHits == 0)
                return _shots == 0 ? PlayoutOutcome.Unplayable : PlayoutOutcome.Won;
            if (_queue.Count == 0)
                return _shots == 0 ? PlayoutOutcome.Unplayable : PlayoutOutcome.OutOfBalls;
            if (CoverageAnalyzer.AnyImpossible(CoverageAnalyzer.Analyze(_board, _queue)))
                return PlayoutOutcome.DeadEnd;
            return null;
        }

        // Same rule as GameManager.PurgeCompletedColors: a finished colour's
        // balls leave the queue — unless an unfilled joker is still on the board,
        // because then any colour can still be the one that finishes the level.
        private void PurgeCompletedColors()
        {
            if (HasUnfilledWild())
                return;
            _queue.RemoveAll(b => !ColorStillNeeded(b.color));
        }

        private bool HasUnfilledWild()
        {
            for (int i = 0; i < _board.hits.Length; i++)
                if (_board.hits[i] > 0 && _board.wild[i])
                    return true;
            return false;
        }

        private bool ColorStillNeeded(CellColor c)
        {
            for (int i = 0; i < _board.hits.Length; i++)
                if (_board.hits[i] > 0 && (_board.wild[i] || _board.colors[i] == c))
                    return true;
            return false;
        }
    }
}
