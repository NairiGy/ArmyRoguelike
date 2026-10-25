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

        private static Sprite _gridLine;
        private static Sprite _softShadow;

        /// <summary>A thin white outline of one tile, for drawing the grid over painted floors.</summary>
        public static Sprite GridLine => _gridLine != null ? _gridLine : _gridLine = MakeGridLine();

        /// <summary>A soft dark oval the width of a tile, for grounding things that stand on the board.</summary>
        public static Sprite SoftShadow => _softShadow != null ? _softShadow : _softShadow = MakeSoftShadow();

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

        /// <summary>Art experiment: finished sprites that replace a statue's placeholder. Shown untinted.</summary>
        public static readonly Dictionary<UnitKind, Sprite> StatueOverrides = new Dictionary<UnitKind, Sprite>();

        private static readonly Dictionary<UnitKind, Sprite> Painted = new Dictionary<UnitKind, Sprite>();

        /// <summary>
        /// The painted statue for a unit type, from Resources/Statues (made by Tools/Art/Import Statues),
        /// or null if it has none yet. Painted statues carry their own colours and are shown untinted.
        /// </summary>
        public static Sprite PaintedStatue(UnitKind kind)
        {
            if (StatueOverrides.TryGetValue(kind, out var custom))
                return custom;
            if (!Painted.TryGetValue(kind, out var sprite))
                Painted[kind] = sprite = Resources.Load<Sprite>("Statues/" + (kind == UnitKind.PackLeader ? UnitKind.Wolf : kind));
            return sprite;
        }

        public static bool IsPainted(UnitKind kind) => PaintedStatue(kind) != null;

        public static Sprite Statue(UnitKind kind)
        {
            var painted = PaintedStatue(kind);
            if (painted != null)
                return painted;
            if (Statues.TryGetValue(kind, out var sprite))
                return sprite;
            switch (kind)
            {
                case UnitKind.Wolf:
                case UnitKind.PackLeader:
                    sprite = MakeWolf(kind);
                    break;
                case UnitKind.Skeleton:
                    sprite = MakeSkeleton();
                    break;
                case UnitKind.Necromancer:
                    sprite = MakeNecromancer();
                    break;
                case UnitKind.Ogre:
                    sprite = MakeOgre();
                    break;
                case UnitKind.Sapper:
                    sprite = MakeSapper();
                    break;
                case UnitKind.Mage:
                    sprite = MakeMage();
                    break;
                case UnitKind.Cutpurse:
                    sprite = MakeBandit(BanditKind.Cutpurse);
                    break;
                case UnitKind.Crossbowman:
                    sprite = MakeBandit(BanditKind.Crossbow);
                    break;
                case UnitKind.Brute:
                    sprite = MakeBandit(BanditKind.Brute);
                    break;
                case UnitKind.Elemental:
                    sprite = MakeElemental(false);
                    break;
                case UnitKind.Shard:
                    sprite = MakeElemental(true);
                    break;
                case UnitKind.Turret:
                    sprite = MakeTurret();
                    break;
                case UnitKind.Goblin:
                case UnitKind.GoblinBomber:
                    sprite = MakeGoblin(kind == UnitKind.GoblinBomber);
                    break;
                default:
                    sprite = MakeFigure(kind);
                    break;
            }

            Statues[kind] = sprite;
            return sprite;
        }

        private static Sprite _bones;
        private static Sprite _mine;
        private static Sprite _bomb;

        /// <summary>A small heap of stone bones: what a skeleton leaves behind.</summary>
        public static Sprite Bones => _bones != null ? _bones : _bones = MakeBones();

        /// <summary>A flat spiked plate lying on a tile. Drawn in colour, not tinted.</summary>
        public static Sprite Mine => _mine != null ? _mine : _mine = MakeMine();

        /// <summary>A round bomb with a lit fuse.</summary>
        public static Sprite Bomb => _bomb != null ? _bomb : _bomb = MakeBomb();

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

        private static Sprite MakeGridLine()
        {
            const int w = 128, h = 64;
            var tex = NewTexture(w, h);
            tex.filterMode = FilterMode.Bilinear;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                // Distance to the diamond's edge in pixels (measured vertically), faded over about 1.5 px.
                var d = (1f - (Mathf.Abs(x + 0.5f - w * 0.5f) / (w * 0.5f) + Mathf.Abs(y + 0.5f - h * 0.5f) / (h * 0.5f))) * h * 0.5f;
                var a = d < 0f ? 0f : 1f - Mathf.Clamp01((d - 0.5f) / 1.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        }

        private static Sprite MakeSoftShadow()
        {
            const int w = 64, h = 32;
            var tex = NewTexture(w, h);
            tex.filterMode = FilterMode.Bilinear;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var dx = (x + 0.5f - w * 0.5f) / (w * 0.5f);
                var dy = (y + 0.5f - h * 0.5f) / (h * 0.5f);
                var r = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r) * 1.6f));
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
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

        /// <summary>Thin figure with visible ribs and a skull, holding a short sword.</summary>
        private static Sprite MakeSkeleton()
        {
            const int w = 32, h = 50;
            var tex = NewTexture(w, h);
            Plinth(tex, w, 10);
            // Legs and spine.
            Fill(tex, 13, 7, 14, 17);
            Fill(tex, 18, 7, 19, 17);
            Fill(tex, 15, 17, 17, 31);
            // Ribs.
            for (var y = 21; y <= 29; y += 3)
                Fill(tex, 11, y, 21, y + 1);
            // Arms and sword.
            Fill(tex, 9, 18, 10, 29);
            Fill(tex, 22, 18, 23, 29);
            Fill(tex, 24, 22, 25, 36);
            // Skull with eye holes.
            Disc(tex, 16, 36, 5);
            Fill(tex, 13, 36, 14, 37, 0.25f);
            Fill(tex, 18, 36, 19, 37, 0.25f);
            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        /// <summary>Hooded, robed figure with a tall staff.</summary>
        private static Sprite MakeNecromancer()
        {
            const int w = 32, h = 60;
            var tex = NewTexture(w, h);
            Plinth(tex, w, 11);
            // Robe widening to the floor.
            for (var y = 7; y <= 32; y++)
            {
                var half = 4 + (32 - y) / 4;
                Fill(tex, 15 - half, y, 16 + half, y);
            }

            // Hood.
            Disc(tex, 16, 37, 5);
            for (var i = 0; i < 5; i++)
                Fill(tex, 16 - (4 - i), 42 + i, 16 + (4 - i), 42 + i);
            Fill(tex, 14, 35, 18, 38, 0.3f);
            // Staff with a skull-ish knob.
            Fill(tex, 25, 7, 26, 50);
            Disc(tex, 26, 52, 3);
            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        /// <summary>Huge hunched brute on a wide plinth, club raised. Drawn to cover a 2x2 area.</summary>
        private static Sprite MakeOgre()
        {
            const int w = 80, h = 96;
            var tex = NewTexture(w, h);
            Plinth(tex, w, 34);
            // Legs.
            Fill(tex, 24, 7, 34, 26);
            Fill(tex, 46, 7, 56, 26);
            // Belly and chest.
            for (var y = 24; y <= 62; y++)
            {
                var t = (y - 43) / 19f;
                var half = Mathf.RoundToInt(22 - 8 * t * t);
                Fill(tex, 40 - half, y, 39 + half, y);
            }

            // Head sunk between the shoulders, with a heavy brow.
            Disc(tex, 40, 68, 9);
            Fill(tex, 32, 70, 48, 72, 0.8f);
            Fill(tex, 35, 66, 37, 67, 0.25f);
            Fill(tex, 43, 66, 45, 67, 0.25f);
            // Left arm hanging, right arm holding the club up.
            Fill(tex, 10, 30, 17, 56);
            Fill(tex, 63, 46, 70, 62);
            Fill(tex, 64, 60, 72, 88);
            Disc(tex, 68, 86, 7);
            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        /// <summary>Stocky figure with a helmet, a pickaxe over the shoulder and a satchel of charges.</summary>
        private static Sprite MakeSapper()
        {
            const int w = 32, h = 50;
            var tex = NewTexture(w, h);
            Plinth(tex, w, 11);
            // Legs and body.
            Fill(tex, 12, 7, 14, 15);
            Fill(tex, 18, 7, 20, 15);
            Fill(tex, 10, 15, 22, 30);
            // Satchel on the hip.
            Fill(tex, 6, 14, 11, 21, 0.85f);
            // Head and round helmet with a brim.
            Disc(tex, 16, 35, 5);
            Fill(tex, 9, 37, 23, 38);
            // Pickaxe over the right shoulder.
            Fill(tex, 22, 26, 23, 44);
            Fill(tex, 18, 43, 28, 44);
            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        /// <summary>Robed figure with a pointed hat and an orb-topped staff.</summary>
        private static Sprite MakeMage()
        {
            const int w = 32, h = 58;
            var tex = NewTexture(w, h);
            Plinth(tex, w, 10);
            // Robe widening to the floor.
            for (var y = 7; y <= 30; y++)
            {
                var half = 4 + (30 - y) / 5;
                Fill(tex, 15 - half, y, 16 + half, y);
            }

            Disc(tex, 16, 34, 4);
            // Wide brim and tall pointed hat.
            Fill(tex, 9, 38, 23, 39);
            for (var i = 0; i < 9; i++)
                Fill(tex, 16 - (5 - i / 2), 40 + i, 16 + (5 - i / 2) - 1, 40 + i);
            // Staff with an orb, on the left.
            Fill(tex, 5, 8, 6, 40);
            Disc(tex, 6, 44, 3);
            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        private enum BanditKind
        {
            Cutpurse,
            Crossbow,
            Brute,
        }

        /// <summary>Masked bandits: a slim cutpurse with a sack, a crossbowman, a broad brute with a cudgel.</summary>
        private static Sprite MakeBandit(BanditKind kind)
        {
            const int w = 32, h = 52;
            var tex = NewTexture(w, h);
            var brute = kind == BanditKind.Brute;
            Plinth(tex, w, brute ? 12 : 10);
            var half = brute ? 8 : kind == BanditKind.Cutpurse ? 4 : 5;
            // Legs, body, head with a dark mask band.
            Fill(tex, 16 - half + 1, 7, 16 - half + 3, 15);
            Fill(tex, 16 + half - 4, 7, 16 + half - 2, 15);
            Fill(tex, 16 - half, 15, 15 + half, 30);
            Disc(tex, 16, 35, brute ? 6 : 5);
            Fill(tex, 11, 35, 21, 36, 0.3f);
            switch (kind)
            {
                case BanditKind.Cutpurse:
                    // Hood point and a loot sack on the back.
                    for (var i = 0; i < 4; i++)
                        Fill(tex, 16 - (3 - i), 40 + i, 16 + (3 - i), 40 + i);
                    Disc(tex, 7, 24, 4);
                    break;
                case BanditKind.Crossbow:
                    // Crossbow held across the chest.
                    Fill(tex, 6, 24, 26, 25);
                    Fill(tex, 24, 19, 25, 30);
                    break;
                case BanditKind.Brute:
                    // Cudgel raised on the right.
                    Fill(tex, 25, 22, 27, 44);
                    Disc(tex, 26, 45, 3);
                    break;
            }

            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        /// <summary>A jagged crystal cluster; the shard is a smaller half of one.</summary>
        private static Sprite MakeElemental(bool shard)
        {
            const int w = 36, h = 52;
            var tex = NewTexture(w, h);
            Plinth(tex, w, shard ? 9 : 13);
            var spikes = shard ? new[] { (14, 26), (20, 20) } : new[] { (9, 22), (14, 34), (19, 40), (25, 30), (29, 18) };
            foreach (var (x, top) in spikes)
            {
                // Each spike: a wide base tapering to a point.
                for (var y = 7; y <= top; y++)
                {
                    var t = (float)(y - 7) / (top - 7);
                    var half = Mathf.RoundToInt(Mathf.Lerp(4f, 0f, t));
                    Fill(tex, x - half, y, x + half, y);
                }
            }

            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        /// <summary>A squat tower on a plinth with a crossbow on top.</summary>
        private static Sprite MakeTurret()
        {
            const int w = 32, h = 36;
            var tex = NewTexture(w, h);
            Plinth(tex, w, 10);
            Fill(tex, 10, 7, 21, 22);
            // Battlements.
            for (var x = 10; x <= 20; x += 4)
                Fill(tex, x, 23, x + 1, 25);
            // Crossbow arms and bolt.
            Fill(tex, 7, 27, 24, 28);
            Fill(tex, 15, 24, 16, 31);
            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        /// <summary>Small hunched goblin with big ears. The bomber holds a bomb overhead instead of a knife.</summary>
        private static Sprite MakeGoblin(bool bomber)
        {
            const int w = 32, h = 44;
            var tex = NewTexture(w, h);
            Plinth(tex, w, 9);
            // Short legs, round body.
            Fill(tex, 12, 7, 13, 12);
            Fill(tex, 18, 7, 19, 12);
            Disc(tex, 16, 18, 6);
            // Big head with long ears.
            Disc(tex, 16, 28, 5);
            for (var i = 0; i < 6; i++)
            {
                Fill(tex, 10 - i, 29 + i / 2, 11 - i, 29 + i / 2);
                Fill(tex, 21 + i, 29 + i / 2, 22 + i, 29 + i / 2);
            }

            Fill(tex, 13, 28, 14, 29, 0.25f);
            Fill(tex, 18, 28, 19, 29, 0.25f);
            if (bomber)
            {
                // Arm up holding a bomb.
                Fill(tex, 21, 22, 22, 33);
                Disc(tex, 22, 37, 4);
            }
            else
            {
                // Knife held out.
                Fill(tex, 22, 16, 23, 20);
                Fill(tex, 24, 18, 28, 19);
            }

            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 4f / h), PixelsPerUnit);
        }

        private static Sprite MakeMine()
        {
            const int w = 24, h = 14;
            var tex = NewTexture(w, h);
            var body = new Color(0.35f, 0.33f, 0.3f);
            var spike = new Color(0.75f, 0.7f, 0.6f);
            var light = new Color(1f, 0.25f, 0.2f);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (InDiamond(x + 0.5f, y + 0.5f, 12f, 6f, 9f, 4.5f))
                    tex.SetPixel(x, y, body);
            }

            // Spikes around the rim and a red light in the middle.
            foreach (var (x, y) in new[] { (2, 6), (21, 6), (12, 1), (12, 11), (6, 3), (18, 3), (6, 9), (18, 9) })
                tex.SetPixel(x, y, spike);
            Fill(tex, 11, 6, 12, 7);
            tex.SetPixel(11, 6, light);
            tex.SetPixel(12, 6, light);
            tex.SetPixel(11, 7, light);
            tex.SetPixel(12, 7, light);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.45f), PixelsPerUnit);
        }

        private static Sprite MakeBomb()
        {
            const int w = 20, h = 24;
            var tex = NewTexture(w, h);
            var body = new Color(0.15f, 0.15f, 0.17f);
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var dx = x - 10;
                var dy = y - 8;
                if (dx * dx + dy * dy <= 49)
                    tex.SetPixel(x, y, dx < -2 && dy > 1 ? new Color(0.35f, 0.35f, 0.4f) : body);
            }

            // Fuse and spark.
            for (var i = 0; i < 6; i++)
                tex.SetPixel(10 + i / 2, 15 + i, new Color(0.6f, 0.5f, 0.35f));
            tex.SetPixel(13, 21, new Color(1f, 0.85f, 0.3f));
            tex.SetPixel(13, 22, new Color(1f, 0.5f, 0.2f));
            tex.SetPixel(12, 22, new Color(1f, 0.85f, 0.3f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.1f), PixelsPerUnit);
        }

        private static Sprite MakeBones()
        {
            const int w = 32, h = 16;
            var tex = NewTexture(w, h);
            // Two crossed bones and a skull on the ground.
            for (var i = 0; i < 18; i++)
            {
                Fill(tex, 6 + i, 3 + i / 3, 7 + i, 4 + i / 3);
                Fill(tex, 24 - i, 3 + i / 3, 25 - i, 4 + i / 3);
            }

            Disc(tex, 16, 9, 4);
            Fill(tex, 14, 9, 14, 10, 0.25f);
            Fill(tex, 17, 9, 17, 10, 0.25f);
            Shade(tex, w, h);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 3f / h), PixelsPerUnit);
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
