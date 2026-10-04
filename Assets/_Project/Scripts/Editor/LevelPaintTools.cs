using System;

namespace CatapultGames.Editor
{
    // Brush → UXML button name, plus which contextual controls each brush shows.
    // Adding a brush to the enum without a case here throws at bind time, on
    // purpose: a brush silently bound to the wrong button is worse than a crash.
    public static class LevelPaintTools
    {
        public static readonly LevelPaintTool[] All =
        {
            LevelPaintTool.Paint, LevelPaintTool.Erase, LevelPaintTool.Fill,
            LevelPaintTool.Brush, LevelPaintTool.RectSelect, LevelPaintTool.MultiSelect
        };

        public static string ElementName(LevelPaintTool tool)
        {
            switch (tool)
            {
                case LevelPaintTool.Paint:       return "tool-paint";
                case LevelPaintTool.Erase:       return "tool-erase";
                case LevelPaintTool.Fill:        return "tool-fill";
                case LevelPaintTool.Brush:       return "tool-brush";
                case LevelPaintTool.RectSelect:  return "tool-rect";
                case LevelPaintTool.MultiSelect: return "tool-multi";
                default:
                    throw new ArgumentOutOfRangeException(nameof(tool), tool, "No button mapped for this brush.");
            }
        }

        // Which brushes write colour + type (and therefore show the swatch rows).
        public static bool UsesColor(LevelPaintTool tool) =>
            tool == LevelPaintTool.Paint || tool == LevelPaintTool.Fill || tool == LevelPaintTool.Brush;

        public static bool UsesRadius(LevelPaintTool tool) => tool == LevelPaintTool.Brush;

        public static bool IsSelection(LevelPaintTool tool) =>
            tool == LevelPaintTool.RectSelect || tool == LevelPaintTool.MultiSelect;
    }
}
