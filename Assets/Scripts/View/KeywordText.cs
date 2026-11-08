using System.Collections.Generic;
using System.Text;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Draws a rule text word by word in IMGUI, wrapping it itself, with every <see cref="Keyword"/> in it
    /// highlighted. Hovering a keyword shows its meaning in a box drawn last by <see cref="DrawTooltip"/>. Plain
    /// text only (no rich-text tags); the style's own colour is the text colour.
    /// </summary>
    public static class KeywordText
    {
        /// <summary>The colour of keywords in a text (placeholder for the art chat).</summary>
        public static readonly Color KeywordColor = new Color(1f, 0.82f, 0.45f);

        /// <summary>
        /// The mouse position to hit-test words against, set while drawing inside a scroll view, where Unity's own
        /// position is off under a scaled GUI.matrix. Null = use the event's.
        /// </summary>
        public static Vector2? MouseOverride;

        private static Keyword? _hovered;
        private static int _hoveredFrame = -1;

        /// <summary>The height <paramref name="text"/> takes at <paramref name="width"/>.</summary>
        public static float Height(string text, GUIStyle style, float width) => Layout(text, style, width, null);

        /// <summary>Draws <paramref name="text"/> from the top-left of <paramref name="rect"/>, wrapped to its width.</summary>
        public static void Draw(Rect rect, string text, GUIStyle style) => Layout(text, style, rect.width, rect);

        /// <summary>A label in GUILayout flow: reserves the text's height, then draws it there.</summary>
        public static void LayoutLabel(string text, GUIStyle style, float width)
        {
            var rect = GUILayoutUtility.GetRect(width, Height(text, style, width), GUILayout.Width(width));
            Draw(rect, text, style);
        }

        /// <summary>
        /// "Name: meaning" for every keyword in <paramref name="text"/>, one per line, for tooltips that can't be
        /// hovered themselves. Empty if it names none.
        /// </summary>
        public static string Glossary(string text)
        {
            var lines = new StringBuilder();
            foreach (var k in Keywords.In(text))
            {
                if (lines.Length > 0)
                    lines.Append('\n');
                lines.Append(Keywords.Name(k)).Append(": ").Append(Keywords.Describe(k));
            }

            return lines.ToString();
        }

        /// <summary>The hovered keyword's meaning beside the pointer. Call last in OnGUI, over everything else.</summary>
        public static void DrawTooltip(float guiWidth, float guiHeight)
        {
            if (!_hovered.HasValue || _hoveredFrame != Time.frameCount || Event.current.type != EventType.Repaint)
                return;
            const float width = 280f, pad = 8f;
            var name = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, padding = new RectOffset() };
            name.normal.textColor = KeywordColor;
            var body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, padding = new RectOffset() };
            body.normal.textColor = new Color(0.92f, 0.9f, 0.86f);
            var title = Keywords.Name(_hovered.Value);
            var meaning = Keywords.Describe(_hovered.Value);
            var titleHeight = name.CalcSize(new GUIContent(title)).y;
            var height = pad * 2 + titleHeight + 2f + body.CalcHeight(new GUIContent(meaning), width - 2 * pad);
            var mouse = Event.current.mousePosition;
            var x = Mathf.Min(mouse.x + 16f, guiWidth - width - 4f);
            var y = mouse.y + 20f + height > guiHeight ? mouse.y - height - 8f : mouse.y + 20f;
            var rect = new Rect(x, Mathf.Max(4f, y), width, height);
            GUI.DrawTexture(rect, HudArt.Solid(HudArt.CardColor));
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), HudArt.Solid(KeywordColor));
            GUI.Label(new Rect(rect.x + pad, rect.y + pad, width - 2 * pad, titleHeight), title, name);
            GUI.Label(new Rect(rect.x + pad, rect.y + pad + titleHeight + 2f, width - 2 * pad, height - 2 * pad - titleHeight - 2f), meaning, body);
        }

        /// <summary>A piece of a word: its text, and the keyword it belongs to (if any).</summary>
        private struct Piece
        {
            public string Text;
            public Keyword? Keyword;
            public bool Number;
        }

        /// <summary>Lays the text out (and draws it when <paramref name="rect"/> is set); returns its height.</summary>
        private static float Layout(string text, GUIStyle style, float width, Rect? rect)
        {
            if (string.IsNullOrEmpty(text))
                return 0f;
            // A bare label with the text's font, so a button style's background isn't drawn behind every word.
            var plain = new GUIStyle(GUI.skin.label)
            {
                font = style.font, fontSize = style.fontSize, fontStyle = style.fontStyle, wordWrap = false, richText = false,
                padding = new RectOffset(), margin = new RectOffset(), overflow = new RectOffset(), clipping = TextClipping.Overflow,
            };
            plain.normal.textColor = style.normal.textColor;
            var number = new GUIStyle(plain) { fontStyle = FontStyle.Bold };
            number.normal.textColor = Color.white;
            var lit = new GUIStyle(plain);
            lit.normal.textColor = KeywordColor;
            var lineHeight = plain.CalcSize(new GUIContent("Ag")).y;
            var space = plain.CalcSize(new GUIContent("a a")).x - plain.CalcSize(new GUIContent("aa")).x;
            var draw = rect.HasValue && Event.current.type == EventType.Repaint;
            var mouse = MouseOverride ?? Event.current.mousePosition;

            var x = 0f;
            var y = 0f;
            foreach (var word in Words(text))
            {
                if (word == null)
                {
                    // A line break in the text.
                    x = 0f;
                    y += lineHeight;
                    continue;
                }

                var w = 0f;
                foreach (var piece in word)
                    w += (piece.Number ? number : plain).CalcSize(new GUIContent(piece.Text)).x;
                if (x > 0f && x + w > width)
                {
                    x = 0f;
                    y += lineHeight;
                }

                foreach (var piece in word)
                {
                    var pw = (piece.Number ? number : plain).CalcSize(new GUIContent(piece.Text)).x;
                    if (rect.HasValue)
                    {
                        var r = new Rect(rect.Value.x + x, rect.Value.y + y, pw, lineHeight);
                        if (draw)
                        {
                            GUI.Label(r, piece.Text, piece.Keyword.HasValue ? lit : piece.Number ? number : plain);
                            if (piece.Keyword.HasValue)
                                GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 1f), HudArt.Solid(new Color(KeywordColor.r, KeywordColor.g, KeywordColor.b, 0.5f)));
                        }

                        if (draw && piece.Keyword.HasValue && r.Contains(mouse))
                        {
                            _hovered = piece.Keyword;
                            _hoveredFrame = Time.frameCount;
                        }
                    }

                    x += pw;
                }

                x += space;
            }

            return y + lineHeight;
        }

        /// <summary>
        /// Adds a run of plain text to a word, split so numbers ("2", "+2", "2-4", "x2") are pieces of their own and
        /// stand out.
        /// </summary>
        private static void AddPlain(List<Piece> word, string text)
        {
            var at = 0;
            var i = 0;
            while (i < text.Length)
            {
                var start = i;
                if ((text[i] == '+' || text[i] == '-' || text[i] == 'x') && i + 1 < text.Length && char.IsDigit(text[i + 1]) &&
                    (i == 0 || !char.IsLetterOrDigit(text[i - 1])))
                    i++;
                if (i < text.Length && char.IsDigit(text[i]))
                {
                    while (i < text.Length && char.IsDigit(text[i]))
                        i++;
                    // A range: "2-4".
                    if (i + 1 < text.Length && text[i] == '-' && char.IsDigit(text[i + 1]))
                    {
                        i++;
                        while (i < text.Length && char.IsDigit(text[i]))
                            i++;
                    }

                    if (start > at)
                        word.Add(new Piece { Text = text.Substring(at, start - at) });
                    word.Add(new Piece { Text = text.Substring(start, i - start), Number = true });
                    at = i;
                    continue;
                }

                i = start + 1;
            }

            if (at < text.Length)
                word.Add(new Piece { Text = text.Substring(at) });
        }

        /// <summary>
        /// The text as words, each split into pieces at keyword edges ("(pinned)." is "(", "pinned", ")."). A
        /// keyword of several words is one word. Null stands for a line break.
        /// </summary>
        private static IEnumerable<List<Piece>> Words(string text)
        {
            var found = Keywords.Find(text);
            var at = 0;
            var word = new List<Piece>();
            var plain = new StringBuilder();
            var next = 0;
            while (at < text.Length)
            {
                if (next < found.Count && found[next].start == at)
                {
                    if (plain.Length > 0)
                    {
                        AddPlain(word, plain.ToString());
                        plain.Clear();
                    }

                    var (start, length, keyword) = found[next++];
                    word.Add(new Piece { Text = text.Substring(start, length), Keyword = keyword });
                    at += length;
                    continue;
                }

                var c = text[at++];
                if (c == ' ' || c == '\n')
                {
                    if (plain.Length > 0)
                    {
                        AddPlain(word, plain.ToString());
                        plain.Clear();
                    }

                    if (word.Count > 0)
                        yield return word;
                    word = new List<Piece>();
                    if (c == '\n')
                        yield return null;
                    continue;
                }

                plain.Append(c);
            }

            if (plain.Length > 0)
                AddPlain(word, plain.ToString());
            if (word.Count > 0)
                yield return word;
        }
    }
}
