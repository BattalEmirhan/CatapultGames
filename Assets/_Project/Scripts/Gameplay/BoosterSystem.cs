using System;
using UnityEngine;

namespace CatapultGames
{
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
    // Wire up in Inspector: queue, grid, gameManager (optional, blocks use
    // once the level ended).
    public sealed class BoosterSystem : MonoBehaviour
    {
        public event Action OnChanged;

        [SerializeField] private BallQueue    queue;
        [SerializeField] private GridRenderer grid;
        [SerializeField] private GameManager  gameManager;   // optional

        [Tooltip("Uses of each booster per level.")]
        [SerializeField] private int perLevel = 1;

        private readonly int[] _counts = new int[3];

        public int Count(BoosterType type) => _counts[(int)type];

        public bool CanUse(BoosterType type)
        {
            if (_counts[(int)type] <= 0)
                return false;
            if (gameManager != null && gameManager.IsOver)
                return false;
            var ball = queue != null ? queue.Current : null;
            if (ball == null)
                return false;
            // Nothing to do on a ball that already is what the booster would make it.
            if (type == BoosterType.Rainbow && ball.color == CellColor.Any)
                return false;
            if (type == BoosterType.Bomb && ball.color == CellColor.Any && ball.shape == BallShape.Square && ball.powerLevel == 2)
                return false;
            if (type == BoosterType.Recolor && MostNeededColor() == CellColor.None)
                return false;
            return true;
        }

        public void ResetForLevel()
        {
            for (int i = 0; i < _counts.Length; i++)
                _counts[i] = Mathf.Max(0, perLevel);
            OnChanged?.Invoke();
        }

        public bool Use(BoosterType type)
        {
            if (!CanUse(type))
                return false;
            var ball = queue.Current;

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
            queue.NotifyCurrentChanged();
            GameFX.Instance.Flash(new Color(1f, 1f, 1f), 0.18f, 0.25f);
            Haptics.Medium();
            GameAudio.Play(Sfx.Booster);
            OnChanged?.Invoke();
            return true;
        }

        // The colour with the most unfilled target cells — the one a recolour
        // helps most. None when the board is done.
        private CellColor MostNeededColor()
        {
            if (grid == null)
                return CellColor.None;
            var best = CellColor.None;
            int bestLeft = 0;
            foreach (ColorProgress cp in grid.CountByColor())
            {
                int left = cp.total - cp.filled;
                if (left > bestLeft)
                {
                    bestLeft = left;
                    best = cp.color;
                }
            }
            return best;
        }
    }
}
