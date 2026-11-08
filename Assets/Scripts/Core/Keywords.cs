using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>
    /// A rule word the texts use without explaining it: rule texts name it, and the game shows its meaning when
    /// the word is hovered (and lists it in the gamepedia).
    /// </summary>
    public enum Keyword
    {
        Pinned,
        Slowed,
        Marked,
        Exposed,
        Hidden,
        Burning,
        Rubble,
        Poison,
        Push,
        ZoneOfControl,
        LineOfSight,
        Fury,
        Swift,
        TemporaryHp,
        Free,
    }

    /// <summary>Names, meanings and the word forms of every <see cref="Keyword"/>, and finding them in a text.</summary>
    public static class Keywords
    {
        public static readonly Keyword[] All =
        {
            Keyword.Pinned, Keyword.Slowed, Keyword.Marked, Keyword.Exposed, Keyword.Hidden, Keyword.Burning, Keyword.Rubble,
            Keyword.Poison, Keyword.Push, Keyword.ZoneOfControl, Keyword.LineOfSight, Keyword.Fury, Keyword.Swift,
            Keyword.TemporaryHp, Keyword.Free,
        };

        public static string Name(Keyword k)
        {
            switch (k)
            {
                case Keyword.ZoneOfControl: return "Zone of control";
                case Keyword.LineOfSight: return "Line of sight";
                case Keyword.TemporaryHp: return "Temporary HP";
                default: return k.ToString();
            }
        }

        public static string Describe(Keyword k)
        {
            switch (k)
            {
                case Keyword.Pinned: return "Can't move on its next turn.";
                case Keyword.Slowed: return "Can't move on its next turn.";
                case Keyword.Marked: return $"The next hit on it, by anyone and from anything but poison or fire, deals +{Battle.MarkBonus}. Then the mark is gone.";
                case Keyword.Exposed: return "No ally of its own stands next to it.";
                case Keyword.Hidden: return "Until your next turn, enemies can't target or attack it (area blasts still hit). Attacking doesn't reveal it.";
                case Keyword.Burning: return $"Burning ground: {Battle.BurnDamage} damage to whoever starts a turn on it. Enemies walk through it but never stop on it.";
                case Keyword.Rubble: return $"A low heap: blocks walking but not shots, and gives no cover. A unit pushed into it is crushed for {Battle.CollisionDamage + Battle.CrushDamage} and the rubble breaks. One hit clears it.";
                case Keyword.Poison: return $"{Battle.PoisonDamage} damage at the start of the target's turn, {Battle.PoisonTurns} times.";
                case Keyword.Push: return $"A medium unit moves 1 tile, a small one 2, a huge one not at all. If it slams into a rock, a unit or the edge, it stops and takes {Battle.CollisionDamage}. An enemy pushed onto a mine sets it off.";
                case Keyword.ZoneOfControl: return "Stepping out of a tile next to an enemy uses up all remaining moves, for both sides.";
                case Keyword.LineOfSight: return "Rocks block shots. Bombs, boulders and fireballs are lobbed over them.";
                case Keyword.Fury: return $"+1 damage per Fury for the rest of the fight, {Battle.MaxFury} at most.";
                case Keyword.Swift: return $"{Battle.BaseMoves + 1} moves a turn instead of {Battle.BaseMoves}.";
                case Keyword.TemporaryHp: return "Extra HP that goes first and is gone at your next turn.";
                case Keyword.Free: return "Costs no AP. Use it before the unit acts: it can still move and attack afterwards.";
                default: return "";
            }
        }

        /// <summary>The words that name a keyword in rule texts (matched as whole words, ignoring case unless noted).</summary>
        private static string[] Forms(Keyword k)
        {
            switch (k)
            {
                case Keyword.Pinned: return new[] { "pinned" };
                case Keyword.Slowed: return new[] { "slowed" };
                case Keyword.Marked: return new[] { "marked" };
                case Keyword.Exposed: return new[] { "exposed" };
                case Keyword.Hidden: return new[] { "hidden" };
                case Keyword.Burning: return new[] { "burning", "burns" };
                case Keyword.Rubble: return new[] { "rubble" };
                case Keyword.Poison: return new[] { "poisoned", "poisons", "poison" };
                case Keyword.Push: return new[] { "pushed", "pushes", "push" };
                case Keyword.ZoneOfControl: return new[] { "zone of control" };
                case Keyword.LineOfSight: return new[] { "line of sight" };
                case Keyword.Fury: return new[] { "fury" };
                case Keyword.Swift: return new[] { "swift" };
                case Keyword.TemporaryHp: return new[] { "temporary hp" };
                default: return new string[0];
            }
        }

        /// <summary>
        /// Every keyword in <paramref name="text"/>, in order, as (start, length, keyword). "Free" counts only
        /// capitalised at the start of a sentence ("Free, before it acts"), so a "free tile" is not one.
        /// </summary>
        public static List<(int start, int length, Keyword keyword)> Find(string text)
        {
            var found = new List<(int, int, Keyword)>();
            if (string.IsNullOrEmpty(text))
                return found;
            var lower = text.ToLowerInvariant();
            var i = 0;
            while (i < text.Length)
            {
                if (!IsWordStart(text, i))
                {
                    i++;
                    continue;
                }

                var match = Match(text, lower, i);
                if (match.HasValue)
                {
                    found.Add((i, match.Value.length, match.Value.keyword));
                    i += match.Value.length;
                }
                else
                    i++;
            }

            return found;
        }

        /// <summary>The distinct keywords in a text, in the order they first appear.</summary>
        public static List<Keyword> In(string text)
        {
            var list = new List<Keyword>();
            foreach (var (_, _, k) in Find(text))
                if (!list.Contains(k))
                    list.Add(k);
            return list;
        }

        private static (int length, Keyword keyword)? Match(string text, string lower, int at)
        {
            (int length, Keyword keyword)? best = null;
            foreach (var k in All)
            {
                if (k == Keyword.Free)
                {
                    if (string.CompareOrdinal(text, at, "Free", 0, 4) == 0 && IsWordEnd(text, at + 4))
                        best = Longer(best, (4, k));
                    continue;
                }

                foreach (var form in Forms(k))
                    if (at + form.Length <= lower.Length && string.CompareOrdinal(lower, at, form, 0, form.Length) == 0 &&
                        IsWordEnd(lower, at + form.Length))
                        best = Longer(best, (form.Length, k));
            }

            return best;
        }

        private static (int, Keyword)? Longer((int length, Keyword keyword)? best, (int length, Keyword keyword) candidate) =>
            best.HasValue && best.Value.length >= candidate.length ? best : candidate;

        private static bool IsWordStart(string text, int i) => char.IsLetter(text[i]) && (i == 0 || !char.IsLetterOrDigit(text[i - 1]));

        private static bool IsWordEnd(string text, int i) => i >= text.Length || !char.IsLetterOrDigit(text[i]);
    }
}
