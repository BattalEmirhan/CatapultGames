namespace CatapultGames
{
    public enum Sfx
    {
        Launch,         // ball leaves the tray
        Land,           // ball hits the board
        CellTick,       // one cube of the paint wave rises (pitched by PlayTick)
        IceCrack,       // an ice cell took its first hit
        ColorFanfare,   // a whole colour is finished
        Praise,         // GOOD / GREAT! / AMAZING! stinger (pitched by PlayPraise)
        Win,
        Lose,
        Booster,
        Warning,        // dead-end warning
        Undo,
        Pop             // a purged ball's firework
    }
}
