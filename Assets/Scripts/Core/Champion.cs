using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>
    /// A champion's extra rule. In the later part of the act, battle and elite nodes bring one champion: the
    /// toughest enemy in the fight gets <see cref="Champions.ExtraHp"/> max HP and one of these. Shown on the map
    /// node, so the route can go towards or around it. Numbers are placeholders.
    /// </summary>
    public enum Affix
    {
        None,

        /// <summary>Takes 1 less damage from everything (at least 1).</summary>
        Armored,

        /// <summary>When it dies, it deals damage to each of your units next to it.</summary>
        Vengeful,

        /// <summary>Heals at the start of its side's turn.</summary>
        Regenerating,

        /// <summary>+1 damage on everything it deals with its attack.</summary>
        Mighty,

        /// <summary>+1 move.</summary>
        Fleet,
    }

    public static class Champions
    {
        /// <summary>Map steps (0-based) from which battles and elites bring a champion.</summary>
        public const int FirstStep = 5;

        public const int ExtraHp = 3;
        public const int ArmoredReduction = 1;
        public const int VengefulDamage = 2;
        public const int RegenHeal = 2;
        public const int MightyDamage = 1;
        public const int FleetMoves = 1;

        /// <summary>Extra gold for winning a fight with a champion in it.</summary>
        public const int BonusGold = 5;

        public static readonly IReadOnlyList<Affix> All = new[] { Affix.Armored, Affix.Vengeful, Affix.Regenerating, Affix.Mighty, Affix.Fleet };

        public static string Name(Affix affix)
        {
            switch (affix)
            {
                case Affix.Armored: return "Armored";
                case Affix.Vengeful: return "Vengeful";
                case Affix.Regenerating: return "Regenerating";
                case Affix.Mighty: return "Mighty";
                case Affix.Fleet: return "Fleet";
                default: return "";
            }
        }

        public static string Describe(Affix affix)
        {
            switch (affix)
            {
                case Affix.Armored: return $"takes {ArmoredReduction} less damage from everything (at least 1)";
                case Affix.Vengeful: return $"when it dies, {VengefulDamage} damage to each of your units next to it";
                case Affix.Regenerating: return $"heals {RegenHeal} at the start of its turn";
                case Affix.Mighty: return $"+{MightyDamage} damage";
                case Affix.Fleet: return $"+{FleetMoves} move";
                default: return "";
            }
        }

        /// <summary>The full rule for the map and the hover card.</summary>
        public static string Rule(Affix affix) =>
            $"Champion ({Name(affix)}): the toughest enemy has +{ExtraHp} max HP and {Describe(affix)}. Winning pays +{BonusGold} gold.";
    }
}
