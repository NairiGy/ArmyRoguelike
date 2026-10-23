using System.Collections.Generic;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Placeholder sprites drawn in code, so the prototype needs no art assets. Statues are drawn in
    /// greyscale and tinted per team by the SpriteRenderer.
    /// </summary>
    public static class PlaceholderArt
    {
        public const float PixelsPerUnit = 64f;

        public static readonly Color PlayerStone = new Color(0.9f, 0.88f, 0.82f);
        public static readonly Color EnemyStone = new Color(0.45f, 0.45f, 0.52f);

        private static Sprite _tile;
        private static Sprite _rock;
        private static Sprite _pixel;
        private static readonly Dictionary<UnitKind, Sprite> Statues = new Dictionary<UnitKind, Sprite>();

        /// <summary>Flat diamond the size of one tile, white so it can be tinted.</summary>
        public static Sprite Tile => _tile != null ? _tile : _tile = MakeTile();

        public static Sprite Rock => _rock != null ? _rock : _rock = MakeRock();

        /// <summary>A single white pixel, for bars and lines.</summary>
        public static Sprite Pixel
        {
            get
            {
                if (_pixel != null)
                    return _pixel;
                var tex = NewTexture(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                return _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
        }

        public static Sprite Statue(UnitKind kind)
        {
            if (Statues.TryGetValue(kind, out var sprite))
                return sprite;
            sprite = kind == UnitKind.Wolf || kind == UnitKind.PackLeader ? MakeWolf(kind) : MakeFigure(kind);
            Statues[kind] = sprite;
            return sprite;
        }

        // ---------- Drawing ----------

        private static Texture2D NewTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var clear = new Color32[w * h];
            tex.SetPixels32(clear);
            return tex;
        }

        private static bool InDiamond(float x, float y, float cx, float cy, float halfW, float halfH) =>
            Mathf.Abs(x - cx) / halfW + Mathf.Abs(y - cy) / halfH <= 1f;

        private static Sprite MakeTile()
        {
            const int w = 64, h = 32;
            var tex = NewTexture(w, h);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var px = x + 0.5f;
                var py = y + 0.5f;
                if (!InDiamond(px, py, 32f, 16f, 32f, 16f))
                    continue;
                var edge = !InDiamond(px, py, 32f, 16f, 30f, 15f);
                tex.SetPixel(x, y, edge ? new Color(0.75f, 0.75f, 0.75f) : Color.white);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        }

        private static Sprite MakeRock()
        {
            const int w = 64, h = 56, raise = 22;
            var tex = NewTexture(w, h);
            var top = new Color(0.62f, 0.58f, 0.52f);
            var left = new Color(0.46f, 0.42f, 0.38f);
            var right = new Color(0.36f, 0.33f, 0.3f);
            // Slightly smaller than a tile so the board shows around it.
            const float halfW = 26f, halfH = 13f;
            for (var lift = 0; lift <= raise; lift++)
            {
                var cy = 16f + lift;
                var isTop = lift == raise;
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    if (!InDiamond(x + 0.5f, y + 0.5f, 32f, cy, halfW, halfH))
                        continue;
                    tex.SetPixel(x, y, isTop ? top : x < 32 ? left : right);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 16f / h), PixelsPerUnit);
        }

        /// <summary>Upright figure on a plinth: warrior, archer, rogue.</summary>
        private static Sprite MakeFigure(UnitKind kind)
        {
            const int w = 32, h = 52;
            var tex = NewTexture(w, h);
            var bodyHalf = kind == UnitKind.Warrior ? 7 : 5;

            Plinth(tex, w, 11);
            // Body.
            Fill(tex, 16 - bodyHalf, 7, 16 + bodyHalf, 30);
            // Head.
            Disc(tex, 16, 36, 5);

            switch (kind)
            {
                case UnitKind.Warrior:
                    // Shield on the left and a raised sword on the right.
                    Fill(tex, 4, 12, 9, 26);
                    Fill(tex, 24, 18, 25, 40);
                    Fill(tex, 22, 20, 27, 21);
                    break;
                case UnitKind.Archer:
                    // Bow: an arc on the right side.
                    for (var y = 12; y <= 36; y++)
                    {
                        var t = (y - 24) / 12f;
                        var x = Mathf.RoundToInt(27 - 5 * t * t);
                        Fill(tex, x, y, x + 1, y);
                    }

                    Fill(tex, 26, 12, 26, 36, 0.8f);
                    break;
                case UnitKind.Rogue:
                    // Pointed hood and two daggers held low.
                    for (var i = 0; i < 6; i++)
                        Fill(tex, 16 - (5 - i), 40 + i, 16 + (5 - i), 40 + i);
                    Fill(tex, 7, 14, 8, 22);
                    Fill(tex, 23, 14, 24, 22);
                    break;
            }

            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        /// <summary>Four-legged wolf on a plinth. The pack leader gets a mane crest.</summary>
        private static Sprite MakeWolf(UnitKind kind)
        {
            const int w = 40, h = 40;
            var tex = NewTexture(w, h);
            Plinth(tex, w, 14);
            // Legs.
            Fill(tex, 10, 7, 12, 14);
            Fill(tex, 15, 7, 17, 14);
            Fill(tex, 24, 7, 26, 14);
            Fill(tex, 29, 7, 31, 14);
            // Body.
            Fill(tex, 9, 14, 31, 22);
            // Head, ears and snout on the right.
            Fill(tex, 27, 20, 35, 28);
            Fill(tex, 34, 21, 38, 24);
            Fill(tex, 28, 28, 29, 31);
            Fill(tex, 32, 28, 33, 31);
            // Tail on the left.
            Fill(tex, 4, 18, 9, 20);
            Fill(tex, 3, 20, 5, 23);

            if (kind == UnitKind.PackLeader)
            {
                // Raised mane along the back.
                for (var x = 12; x <= 27; x += 3)
                    Fill(tex, x, 23, x + 1, 26);
            }

            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        private static void Plinth(Texture2D tex, int w, int halfWidth)
        {
            var cx = w / 2;
            Fill(tex, cx - halfWidth, 0, cx + halfWidth - 1, 3, 0.75f);
            Fill(tex, cx - halfWidth + 2, 4, cx + halfWidth - 3, 6, 0.85f);
        }

        private static void Fill(Texture2D tex, int x0, int y0, int x1, int y1, float shade = 1f)
        {
            var c = new Color(shade, shade, shade, 1f);
            for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
                if (x >= 0 && y >= 0 && x < tex.width && y < tex.height)
                    tex.SetPixel(x, y, c);
        }

        private static void Disc(Texture2D tex, int cx, int cy, int r)
        {
            for (var y = -r; y <= r; y++)
            for (var x = -r; x <= r; x++)
                if (x * x + y * y <= r * r + r)
                    tex.SetPixel(cx + x, cy + y, Color.white);
        }

        /// <summary>Light from the upper left: darkens the right side and adds a dark outline.</summary>
        private static void Shade(Texture2D tex, int w, int h)
        {
            var src = tex.GetPixels();
            var dst = (Color[])src.Clone();
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                if (src[i].a > 0f)
                {
                    var c = src[i];
                    var rightEdge = x + 1 >= w || src[i + 1].a <= 0f;
                    var shade = x > w / 2 + 1 ? 0.82f : 1f;
                    if (rightEdge)
                        shade *= 0.7f;
                    dst[i] = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                    continue;
                }

                // Outline any transparent pixel that touches the figure.
                var touches = (x > 0 && src[i - 1].a > 0f) || (x + 1 < w && src[i + 1].a > 0f) ||
                              (y > 0 && src[i - w].a > 0f) || (y + 1 < h && src[i + w].a > 0f);
                if (touches)
                    dst[i] = new Color(0.18f, 0.18f, 0.2f, 1f);
            }

            tex.SetPixels(dst);
        }
    }
}
