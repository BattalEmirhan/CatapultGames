namespace CatapultGames
{
    // Why the level ended. The panel owns all the wording, so GameManager
    // passes the reason rather than a string.
    public enum ResultReason
    {
        Won,
        OutOfBalls    // queue ran dry with cells still empty. A proven dead end
                      // with balls left is only a warning (GameManager), since
                      // undo and boosters can still turn it around.
    }
}
