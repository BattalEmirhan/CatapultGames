using System;
using UnityEngine;

namespace CatapultGames
{
    public enum BoosterType
    {
        Rainbow = 0,   // the selected ball paints ANY colour
        Recolor = 1,   // the selected ball takes the colour the board needs most
        Bomb    = 2    // the selected ball becomes a 3x3 rainbow stamp
    }

    // The three boosters of a casual block puzzle, applied to the SELECTED tray
    // ball in place. Each one is a plain edit of that BallData (colour / shape /
    // power) followed by BallQueue.NotifyCurrentChanged, so the tray, the aim
    // preview and the launcher all see the new ball through the paths they
    // already have — no second "which ball" concept.
    //
    // Counts reset per level (LevelLoader.Apply). Undo does not refund a booster:
    // the shot's queue snapshot is taken after the booster was applied, so
    // undoing the throw gives the boosted ball back, which is the honest result.
    //
    // Wire up in Inspector: _queue, _grid, _gameManager (optional, blocks use
    // once the level ended).
    public class BoosterSystem : MonoBehaviour
    {
        [SerializeField] private BallQueue    _queue;
        [SerializeField] private GridRenderer _grid;
        [SerializeField] private GameManager  _gameManager;   // optional

        [Tooltip("Uses of each booster per level.")]
        [SerializeField] private int _perLevel = 1;

        private readonly int[] _counts = new int[3];

        public event Action OnChanged;

        public int Count(BoosterType type) => _counts[(int)type];

        public bool CanUse(BoosterType type)
        {
            if (_counts[(int)type] <= 0) return false;
            if (_gameManager != null && _gameManager.IsOver) return false;
            var ball = _queue != null ? _queue.Current : null;
            if (ball == null) return false;
            // Nothing to do on a ball that already is what the booster would make it.
            if (type == BoosterType.Rainbow && ball.color == CellColor.Any) return false;
            if (type == BoosterType.Bomb && ball.color == CellColor.Any && ball.shape == BallShape.Square && ball.powerLevel == 2) return false;
            if (type == BoosterType.Recolor && MostNeededColor() == CellColor.None) return false;
            return true;
        }

        public void ResetForLevel()
        {
            for (int i = 0; i < _counts.Length; i++) _counts[i] = Mathf.Max(0, _perLevel);
            OnChanged?.Invoke();
        }

        public bool Use(BoosterType type)
        {
            if (!CanUse(type)) return false;
            var ball = _queue.Current;

            switch (type)
            {
                case BoosterType.Rainbow:
                    ball.color = CellColor.Any;
                    break;
                case BoosterType.Recolor:
                    ball.color = MostNeededColor();
                    break;
                case BoosterType.Bomb:
                    ball.color      = CellColor.Any;
                    ball.shape      = BallShape.Square;
                    ball.powerLevel = 2;   // 3x3
                    break;
            }

            _counts[(int)type]--;
            _queue.NotifyCurrentChanged();
            GameFX.Instance.Flash(new Color(1f, 1f, 1f), 0.18f, 0.25f);
            Haptics.Medium();
            OnChanged?.Invoke();
            return true;
        }

        // The colour with the most unfilled target cells — the one a recolour
        // helps most. None when the board is done.
        private CellColor MostNeededColor()
        {
            if (_grid == null) return CellColor.None;
            var best = CellColor.None;
            int bestLeft = 0;
            foreach (var cp in _grid.CountByColor())
            {
                int left = cp.total - cp.filled;
                if (left > bestLeft) { bestLeft = left; best = cp.color; }
            }
            return best;
        }
    }
}
