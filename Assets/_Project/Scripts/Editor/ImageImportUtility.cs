using System.IO;
using UnityEngine;

namespace CatapultGames.Editor
{
    public static class ImageImportUtility
    {
        // Loads a PNG/JPG from any absolute path into a readable Texture2D.
        public static Texture2D LoadTexture(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                Object.DestroyImmediate(tex);
                return null;
            }
            return tex;
        }

        // Slices tex into level.grid cells and assigns the nearest CellColor to each.
        // threshold (0–1): max normalised RGB Euclidean distance allowed for a match.
        // Cells whose region average is farther than threshold from every palette color
        // are left as CellColor.None.
        public static void ImportImageToGrid(LevelData level, Texture2D tex, float threshold)
        {
            int gw = level.grid.width;
            int gh = level.grid.height;
            int tw = tex.width;
            int th = tex.height;

            var newCells = new CellData[gw * gh];

            for (int gy = 0; gy < gh; gy++)
            {
                for (int gx = 0; gx < gw; gx++)
                {
                    // Texture origin is bottom-left; grid origin is top-left → flip Y.
                    int px = gx * tw / gw;
                    int py = (gh - 1 - gy) * th / gh;
                    int pw = Mathf.Max(1, tw / gw);
                    int ph = Mathf.Max(1, th / gh);

                    // Clamp so GetPixels never reads out of bounds.
                    px = Mathf.Clamp(px, 0, tw - 1);
                    py = Mathf.Clamp(py, 0, th - 1);
                    pw = Mathf.Min(pw, tw - px);
                    ph = Mathf.Min(ph, th - py);

                    Color[] pixels  = tex.GetPixels(px, py, pw, ph);
                    Color   avg     = AverageColor(pixels);
                    CellColor color = FindNearestColor(avg, threshold);

                    newCells[gy * gw + gx] = new CellData(gx, gy, color);
                }
            }

            level.cells = newCells;
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        private static Color AverageColor(Color[] pixels)
        {
            if (pixels == null || pixels.Length == 0) return Color.clear;

            float r = 0, g = 0, b = 0;
            // Skip fully-transparent pixels so alpha cutouts don't skew the average.
            int count = 0;
            foreach (var p in pixels)
            {
                if (p.a < 0.1f) continue;
                r += p.r; g += p.g; b += p.b;
                count++;
            }

            if (count == 0) return Color.clear;
            return new Color(r / count, g / count, b / count);
        }

        // Returns the best-matching CellColor, or None if every palette entry is
        // farther than threshold.  Distance is Euclidean in normalised RGB [0,1].
        // Max possible distance = sqrt(3) ≈ 1.732.
        public static CellColor FindNearestColor(Color avg, float threshold)
        {
            CellColor best     = CellColor.None;
            float     bestDist = float.MaxValue;

            for (int i = 1; i <= 7; i++)   // 1–7 skips None (0)
            {
                Color32 c32 = GameConstants.GetColor((CellColor)i);
                float dr = avg.r - c32.r / 255f;
                float dg = avg.g - c32.g / 255f;
                float db = avg.b - c32.b / 255f;
                float dist = Mathf.Sqrt(dr * dr + dg * dg + db * db);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best     = (CellColor)i;
                }
            }

            return bestDist <= threshold ? best : CellColor.None;
        }
    }
}
