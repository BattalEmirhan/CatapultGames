using System.Collections.Generic;
using UnityEngine;

namespace CatapultGames
{
    // The board's art, in the block-match structure: saturated glossy tiles on a
    // neutral slate board with a checkered socket grid. The board is deliberately
    // colourless — any hue in it (the purple tried first) eats into the contrast
    // of the tiles that share it. Every sprite is DRAWN IN CODE
    // (the project ships no art), once, and kept for the app's lifetime.
    //
    // Swapping in real art needs no code: a Sprite at Resources/Art/<name> wins
    // over the drawn one —
    //   Art/Tiles/<display name>   e.g. Art/Tiles/Red, Art/Tiles/Yellow, Art/Tiles/Grey
    //   Art/Board/Panel  Art/Board/Socket  Art/Board/Marker  Art/Ice
    // A replacement tile should be ~1 world unit (pixels-per-unit = its width),
    // and the panel 9-sliced.
    public static class TileArt
    {
        // Board tones, shared by the grid, the tray plate and the menus so the
        // whole game sits on one background family.
        public static readonly Color Board   = new Color32( 34,  36,  48, 255);
        public static readonly Color SocketA = new Color32( 52,  55,  72, 255);
        public static readonly Color SocketB = new Color32( 44,  47,  62, 255);
        public static readonly Color Hole    = new Color32( 14,  15,  22, 255);   // Stone: a gap in the board

        private const int Size = 128;   // px per tile sprite; 1 world unit

        private static readonly Dictionary<CellColor, Sprite> _tiles = new();
        private static Sprite _socket, _marker, _panel, _ice, _grey;

        // ── Public ────────────────────────────────────────────────────────
        // A filled cell / a ball block of this colour. Any (rainbow) has no tile
        // of its own — BallVisual cycles the real colours for it.
        public static Sprite Tile(CellColor c)
        {
            if (c == CellColor.None || c == CellColor.Any) return Grey();
            if (_tiles.TryGetValue(c, out var s) && s != null) return s;
            s = Load("Art/Tiles/" + GameConstants.GetColorDisplayName(c))
                ?? Draw("Tile_" + c, (u, v) => TilePixel(u, v, GameConstants.GetColorF(c)));
            _tiles[c] = s;
            return s;
        }

        // Neutral tile — Joker cells and anything without a colour.
        public static Sprite Grey() => _grey != null ? _grey : _grey =
            Load("Art/Tiles/Grey") ?? Draw("Tile_Grey", (u, v) => TilePixel(u, v, new Color32(150, 150, 165, 255)));

        // White rounded square; SpriteRenderer.color picks SocketA / SocketB.
        public static Sprite Socket() => _socket != null ? _socket : _socket =
            Load("Art/Board/Socket") ?? Draw("Socket", (u, v) => Flat(u, v, 0.47f, 0.12f));

        // The "paint me" marker on an empty target: a small flat square in the
        // TRUE target colour (tinted by the renderer). Flat and small on purpose —
        // nothing like a filled tile, and never washed out by alpha. White with a
        // grey rim, so once tinted the rim is a darker shade of the same hue that
        // separates the marker from the socket under it.
        public static Sprite Marker() => _marker != null ? _marker : _marker =
            Load("Art/Board/Marker") ?? Draw("Marker", (u, v) =>
            {
                float d = RoundedRect(u, v, 0.47f, 0.24f);
                float rim = Mathf.Clamp01(1f + d / 0.10f);   // 0 inside, 1 at the edge
                return ToColor32(Color.white * (1f - 0.62f * rim), Coverage(d, Size));
            });

        // 9-sliced board panel; the border is the rounded corner plus its rim.
        public static Sprite Panel()
        {
            if (_panel != null) return _panel;
            _panel = Load("Art/Board/Panel");
            if (_panel != null) return _panel;

            const int px = 64, border = 20;
            var tex = NewTexture("Panel", px);
            var pixels = new Color32[px * px];
            for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
            {
                float u = (x + 0.5f) / px - 0.5f, v = (y + 0.5f) / px - 0.5f;
                float d = RoundedRect(u, v, 0.5f, 0.25f);
                float a = Coverage(d, px);
                // A darker rim inside the edge gives the panel a lip.
                float rim = Mathf.Clamp01(1f + d * px / 4f);
                Color c = Color.Lerp(Board, Board * 0.72f, rim);
                pixels[y * px + x] = ToColor32(c, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            _panel = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px,
                                   0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            _panel.name = "Panel";
            return _panel;
        }

        // Frosted overlay for an Ice cell: pale and translucent, with two streaks.
        public static Sprite Ice() => _ice != null ? _ice : _ice =
            Load("Art/Ice") ?? Draw("Ice", (u, v) =>
            {
                float a = Coverage(RoundedRect(u, v, 0.47f, 0.12f), Size);
                float streak = Mathf.Clamp01(1f - Mathf.Abs(u + v - 0.12f) * 22f) +
                               Mathf.Clamp01(1f - Mathf.Abs(u + v + 0.16f) * 30f) * 0.6f;
                Color c = Color.Lerp(new Color(0.70f, 0.90f, 1f), Color.white, Mathf.Clamp01(streak));
                return ToColor32(c, a * Mathf.Lerp(0.55f, 0.9f, Mathf.Clamp01(streak)));
            });

        // ── Drawing ───────────────────────────────────────────────────────
        // Glossy block: a beveled rim lit from the top, a flat inner face, a soft
        // gloss band across the face's upper half, and a dark hairline outline.
        private static Color32 TilePixel(float u, float v, Color b)
        {
            float dOuter = RoundedRect(u, v, 0.48f, 0.13f);
            float a = Coverage(dOuter, Size);
            if (a <= 0f) return new Color32(0, 0, 0, 0);

            // Saturation first: the face is the pure colour, and the light only
            // touches the rim and a thin gloss band — whitening the whole tile is
            // what made the earlier set look washed out.
            Color dark  = b * 0.58f;
            Color light = Color.Lerp(b, Color.white, 0.18f);
            Color col   = Color.Lerp(dark, light, Mathf.Clamp01(0.5f + v * 1.1f));

            float face = Coverage(RoundedRect(u, v - 0.012f, 0.36f, 0.09f), Size);
            col = Color.Lerp(col, b, face);

            float gloss = Mathf.Clamp01((v - 0.10f) / 0.24f) * face * Mathf.Clamp01(1f - Mathf.Pow(Mathf.Abs(u) / 0.34f, 6f));
            col = Color.Lerp(col, Color.white, 0.16f * gloss);

            float edge = Mathf.Clamp01(1f + dOuter * Size / 2.5f);
            col = Color.Lerp(col, b * 0.40f, 0.6f * edge);
            return ToColor32(col, a);
        }

        private static Color32 Flat(float u, float v, float half, float radius) =>
            ToColor32(Color.white, Coverage(RoundedRect(u, v, half, radius), Size));

        // Signed distance to a rounded square centred on 0 (unit = sprite width).
        private static float RoundedRect(float u, float v, float half, float radius)
        {
            float qx = Mathf.Abs(u) - half + radius, qy = Mathf.Abs(v) - half + radius;
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude +
                   Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        // ~1.5 px of anti-aliasing at the given resolution.
        private static float Coverage(float d, int px) => Mathf.Clamp01(0.5f - d * px / 1.5f);

        private static Color32 ToColor32(Color c, float a) =>
            new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f),
                        (byte)(Mathf.Clamp01(c.b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));

        private static Sprite Draw(string name, System.Func<float, float, Color32> pixel)
        {
            var tex = NewTexture(name, Size);
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                pixels[y * Size + x] = pixel((x + 0.5f) / Size - 0.5f, (y + 0.5f) / Size - 0.5f);
            tex.SetPixels32(pixels);
            tex.Apply(true, true);   // mipmaps: the board is seen in perspective at many sizes
            var s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size,
                                  0, SpriteMeshType.FullRect);
            s.name = name;
            return s;
        }

        private static Texture2D NewTexture(string name, int px) =>
            new Texture2D(px, px, TextureFormat.RGBA32, true)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4
            };

        private static Sprite Load(string path) => Resources.Load<Sprite>(path);
    }
}
