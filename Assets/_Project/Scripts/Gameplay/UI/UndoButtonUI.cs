using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames
{
    // The in-game "take that shot back" button.
    //
    // Undo is only meaningful with the board at rest and a shot on record, so the
    // button hides itself rather than sitting there greyed out — a dead control in
    // the corner reads as broken, an absent one reads as "not now".
    //
    // Polls GameManager.CanUndo instead of subscribing: the state depends on the
    // launcher's flight count, which changes inside a coroutine with no event of
    // its own. One bool read per frame is cheaper than the event plumbing.
    //
    // This component must NOT sit on the button's own GameObject: it hides the
    // button by deactivating it, and a deactivated object stops running Update,
    // so it could never bring itself back. GameplaySceneBuilder puts it on the
    // canvas, like ResultScreenUI.
    //
    // Wire up in Inspector:
    //   button      — the Button itself
    //   gameManager — GameManager (owns the undo)
    public sealed class UndoButtonUI : MonoBehaviour
    {
        [SerializeField] private Button      button;
        [SerializeField] private GameManager gameManager;

        private bool _visible = true;

        private void Awake()
        {
            if (button != null && button.gameObject == gameObject)
            {
                Debug.LogError("[UndoButtonUI] Must live on a different GameObject than the " +
                               "button it hides, or it will deactivate itself and stop updating.",
                               this);
                enabled = false;
                return;
            }

            if (button)
                button.onClick.AddListener(OnClick);
            Apply(false);   // nothing to undo before the first shot lands
        }

        private void Update()
        {
            Apply(gameManager != null && gameManager.CanUndo);
        }

        private void OnDestroy()
        {
            if (button)
                button.onClick.RemoveListener(OnClick);
        }

        private void OnClick() => gameManager?.UndoLastShot();

        private void Apply(bool visible)
        {
            if (visible == _visible)
                return;
            _visible = visible;
            if (button)
                button.gameObject.SetActive(visible);
        }
    }
}
