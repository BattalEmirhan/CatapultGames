using UnityEngine;

namespace CatapultGames
{
    // Tiny UI sprites drawn in code: the project ships no UI art, and an Image with
    // no sprite can only be a hard square. White, so an Image tints them.
    //
    // Made once and kept for the app's lifetime (a few KB, used by every scene
    // that shows the tutorial); the static reference also keeps a scene change
    // from unloading them.
    public static class UiSprites
    {
        private const int Size = 128;
        private static Sprite _disc, _ring;

        // Filled disc with a soft edge — a fingertip.
        public static Sprite Disc() => _disc != null ? _disc : _disc = Make("UiDisc", r => Edge(0.47f - r));

        // Thin circle outline — the "tap" pulse that grows out of the fingertip.
        public static Sprite Ring() => _ring != null ? _ring : _ring = Make("UiRing", r => Edge(0.035f - Mathf.Abs(r - 0.44f)));

        // Alpha from a signed distance (in units of the sprite size): ~1.5 px of
        // smoothing at 128 px, enough to look round without a mipmap.
        private static float Edge(float d) => Mathf.Clamp01(0.5f + d * Size / 1.5f);

        private static Sprite Make(string name, System.Func<float, float> alphaAtRadius)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = (x + 0.5f) / Size - 0.5f, dy = (y + 0.5f) / Size - 0.5f;
                float a  = alphaAtRadius(Mathf.Sqrt(dx * dx + dy * dy));
                px[y * Size + x] = new Color32(255, 255, 255, (byte)(255f * a));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);   // no CPU copy needed after upload
            var sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = name;
            return sprite;
        }
    }
}
