using System.Collections.Generic;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Placeholder HUD art drawn in code: solid panel backgrounds and the intent icons shown over enemies.
    /// The art chat can restyle all of it (see the "Art needed" table).
    /// </summary>
    public static class HudArt
    {
        public enum Icon
        {
            Sword,
            Arrow,
            Burst,
            Up,
            Eye,
            Hold,
            Push,
            Bolt,
            Skull,
        }

        public static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.09f, 0.94f);
        public static readonly Color CardColor = new Color(0.12f, 0.12f, 0.15f, 0.96f);
        public static readonly Color CardHoverColor = new Color(0.2f, 0.19f, 0.15f, 0.97f);
        public static readonly Color CardAimColor = new Color(0.35f, 0.28f, 0.08f, 0.97f);

        public static readonly Color HarmColor = new Color(1f, 0.6f, 0.35f);
        public static readonly Color BuffColor = new Color(0.6f, 0.9f, 0.55f);
        public static readonly Color ControlColor = new Color(0.78f, 0.62f, 1f);
        public static readonly Color FleeColor = new Color(1f, 0.85f, 0.3f);

        private const int IconSize = 32;

        private static readonly Dictionary<Color, Texture2D> Solids = new Dictionary<Color, Texture2D>();
        private static readonly Dictionary<Icon, Texture2D> Icons = new Dictionary<Icon, Texture2D>();

        /// <summary>A 1x1 texture of one colour, for IMGUI backgrounds and bars.</summary>
        public static Texture2D Solid(Color color)
        {
            if (Solids.TryGetValue(color, out var tex) && tex != null)
                return tex;
            tex = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            tex.SetPixel(0, 0, color);
            tex.Apply();
            Solids[color] = tex;
            return tex;
        }

        /// <summary>A box style with a solid background and some padding.</summary>
        public static GUIStyle PanelStyle(Color color, int padding)
        {
            var style = new GUIStyle(GUI.skin.box) { padding = new RectOffset(padding, padding, padding, padding) };
            style.normal.background = Solid(color);
            return style;
        }

        /// <summary>The icon, the short word and the colour for an enemy's intent badge.</summary>
        public static (Icon icon, string word, Color color) ForIntent(Intent intent, Unit enemy)
        {
            switch (intent.Kind)
            {
                case IntentKind.Smash:
                    return (Icon.Burst, "Smash", HarmColor);
                case IntentKind.Throw:
                    return (Icon.Arrow, "Boulder", HarmColor);
                case IntentKind.Raise:
                    return (Icon.Up, "Raise", ControlColor);
                case IntentKind.Bomb:
                    return (Icon.Burst, "Bomb", HarmColor);
                case IntentKind.Flee:
                    return (Icon.Push, "Flee", FleeColor);
                case IntentKind.Slam:
                    return (Icon.Burst, "Slam", HarmColor);
                case IntentKind.Howl:
                    return (Icon.Up, Abilities.Name(Ability.Howl), BuffColor);
                case IntentKind.Fling:
                    return (intent.Thrown != null ? Icon.Arrow : Icon.Burst, Abilities.Name(Ability.Fling), HarmColor);
                case IntentKind.Overwatch:
                    return (Icon.Eye, Abilities.Name(Ability.Overwatch), HarmColor);
                case IntentKind.Upheaval:
                    return (Icon.Burst, Abilities.Name(Ability.Upheaval), HarmColor);
                case IntentKind.CorpseBlast:
                    return (Icon.Burst, Abilities.Name(Ability.CorpseBlast), HarmColor);
                case IntentKind.Grasp:
                    return (Icon.Hold, Abilities.Name(Ability.Grasp), ControlColor);
                case IntentKind.BullRush:
                    return (Icon.Push, Abilities.Name(Ability.BullRush), HarmColor);
                // Demons (placeholder icons, reusing the existing set).
                case IntentKind.Hellswap:
                    return (Icon.Push, Abilities.Name(Ability.Hellswap), ControlColor);
                case IntentKind.RendingBlow:
                    return (Icon.Sword, Abilities.Name(Ability.RendingBlow), HarmColor);
                case IntentKind.SoulContract:
                    return (Icon.Eye, Abilities.Name(Ability.SoulContract), HarmColor);
                case IntentKind.FireBreath:
                    return (Icon.Burst, Abilities.Name(Ability.FireBreath), HarmColor);
                default:
                    return (enemy.Range > 1 ? Icon.Arrow : Icon.Sword, Abilities.AttackName(enemy.Stats), HarmColor);
            }
        }

        /// <summary>A white icon on transparent, to be tinted with GUI.color.</summary>
        public static Texture2D IconTexture(Icon icon)
        {
            if (Icons.TryGetValue(icon, out var tex) && tex != null)
                return tex;
            tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(new Color32[IconSize * IconSize]);
            // Coordinates run from the bottom-left, in a 32x32 square.
            switch (icon)
            {
                case Icon.Sword:
                    Line(tex, 9, 9, 27, 27, 3.2f);
                    Line(tex, 5, 13, 13, 5, 2.6f);
                    Line(tex, 4, 4, 9, 9, 2.6f);
                    break;
                case Icon.Arrow:
                    Line(tex, 4, 16, 27, 16, 2.6f);
                    Line(tex, 27, 16, 20, 23, 2.8f);
                    Line(tex, 27, 16, 20, 9, 2.8f);
                    Line(tex, 8, 16, 3, 21, 2f);
                    Line(tex, 8, 16, 3, 11, 2f);
                    break;
                case Icon.Burst:
                    for (var i = 0; i < 8; i++)
                    {
                        var a = i * Mathf.PI / 4f + Mathf.PI / 8f;
                        Line(tex, 16 + Mathf.Cos(a) * 6f, 16 + Mathf.Sin(a) * 6f, 16 + Mathf.Cos(a) * 14f, 16 + Mathf.Sin(a) * 14f, 2.8f);
                    }

                    Ring(tex, 16, 16, 0f, 5f);
                    break;
                case Icon.Up:
                    Line(tex, 6, 14, 16, 24, 3.2f);
                    Line(tex, 16, 24, 26, 14, 3.2f);
                    Line(tex, 6, 5, 16, 15, 3.2f);
                    Line(tex, 16, 15, 26, 5, 3.2f);
                    break;
                case Icon.Eye:
                    for (var y = 0; y < IconSize; y++)
                    for (var x = 0; x < IconSize; x++)
                    {
                        var e = (x - 15.5f) * (x - 15.5f) / 196f + (y - 15.5f) * (y - 15.5f) / 64f;
                        if (Mathf.Abs(e - 1f) < 0.2f)
                            tex.SetPixel(x, y, Color.white);
                    }

                    Ring(tex, 16, 16, 0f, 4.5f);
                    break;
                case Icon.Hold:
                    Ring(tex, 16, 16, 10f, 13.5f);
                    Line(tex, 8, 24, 24, 8, 3.2f);
                    break;
                case Icon.Push:
                    Line(tex, 6, 6, 15, 16, 3.2f);
                    Line(tex, 15, 16, 6, 26, 3.2f);
                    Line(tex, 15, 6, 24, 16, 3.2f);
                    Line(tex, 24, 16, 15, 26, 3.2f);
                    break;
                case Icon.Bolt:
                    Line(tex, 20, 29, 10, 16, 4f);
                    Line(tex, 10, 16, 21, 16, 4f);
                    Line(tex, 21, 16, 11, 3, 4f);
                    break;
                case Icon.Skull:
                    Ring(tex, 16, 19, 0f, 11f);
                    Line(tex, 11, 7, 21, 7, 7f);
                    Ring(tex, 12, 19, 0f, 3.2f, Color.clear);
                    Ring(tex, 20, 19, 0f, 3.2f, Color.clear);
                    Line(tex, 13, 3, 13, 9, 1.2f, Color.clear);
                    Line(tex, 19, 3, 19, 9, 1.2f, Color.clear);
                    break;
            }

            tex.Apply();
            Icons[icon] = tex;
            return tex;
        }

        private static void Line(Texture2D tex, float x0, float y0, float x1, float y1, float thickness, Color? color = null)
        {
            var a = new Vector2(x0, y0);
            var b = new Vector2(x1, y1);
            for (var y = 0; y < tex.height; y++)
            for (var x = 0; x < tex.width; x++)
            {
                var p = new Vector2(x, y);
                var t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                if (Vector2.Distance(p, a + t * (b - a)) <= thickness * 0.5f)
                    tex.SetPixel(x, y, color ?? Color.white);
            }
        }

        private static void Ring(Texture2D tex, float cx, float cy, float inner, float outer, Color? color = null)
        {
            for (var y = 0; y < tex.height; y++)
            for (var x = 0; x < tex.width; x++)
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                if (d >= inner && d <= outer)
                    tex.SetPixel(x, y, color ?? Color.white);
            }
        }
    }
}
