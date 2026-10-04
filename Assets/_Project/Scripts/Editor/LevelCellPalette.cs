using UnityEngine;

namespace CatapultGames.Editor
{
    // The ONE place a cell's on-screen colour is decided in the editor. Both the
    // Editor grid (LevelGridElement) and the Gallery thumbnails
    // (LevelThumbnailElement) read it, so clicking a card drops you onto a board
    // that looks exactly like the card. If you change a colour here, both agree;
    // if you write a colour anywhere else, they will drift.
    //
    // Hues come from GameConstants.CellColorPalette (the runtime palette), so the
    // editor never invents a colour the game does not have.
    public static class LevelCellPalette
    {
        public static readonly Color Board      = new Color(0.16f, 0.16f, 0.17f);
        public static readonly Color EmptyCell  = new Color(0.22f, 0.22f, 0.23f);
        public static readonly Color Stone      = new Color(0.45f, 0.45f, 0.50f);
        public static readonly Color StoneEdge  = new Color(0.30f, 0.30f, 0.34f);
        public static readonly Color IceTint    = new Color(0.55f, 0.85f, 1.00f);
        public static readonly Color JokerGlyph = new Color(1.00f, 0.97f, 0.72f);
        public static readonly Color IceGlyph   = new Color(0.85f, 0.97f, 1.00f);

        // Fill colour of a cell body. Stone paints over its colour entirely: the
        // colour is ignored in play, so showing it would lie. Ice frosts the hue.
        // Joker keeps its colour — a joker still fills to that colour.
        public static Color Resolve(CellColor color, CellType type)
        {
            if (type == CellType.Stone)
                return Stone;
            if (color == CellColor.None)
                return EmptyCell;

            Color c = GameConstants.GetColorF(color);
            if (type == CellType.Ice)
                return Color.Lerp(c, IceTint, 0.35f);
            return c;
        }

        // Swatch colour for a colour button; None gets the board grey.
        public static Color Swatch(CellColor color) =>
            color == CellColor.None ? EmptyCell : GameConstants.GetColorF(color);

        // Editor-only tints for the special-type buttons, kept close to the
        // runtime look (CellView) so the board reads the same in both places.
        public static Color TypeTint(CellType type) => type switch
        {
            CellType.Ice   => IceTint,
            CellType.Stone => Stone,
            CellType.Joker => new Color(1f, 0.95f, 0.60f),
            _              => new Color(0.45f, 0.75f, 1f)
        };

        public static string TypeGlyph(CellType type) => type switch
        {
            CellType.Ice   => "❄",
            CellType.Joker => "◆",
            _              => ""
        };
    }
}
