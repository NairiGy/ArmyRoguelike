using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>
    /// An army-wide rule for the rest of the run. You pick one at the start, and can buy more in shops or take one
    /// instead of an elite's technique. There's no limit on how many you hold; each is held once.
    /// </summary>
    public enum Banner
    {
        /// <summary>Every attack that deals damage heals the attacker 1. The Curse of the Sun is on every fight.</summary>
        Vampiric,

        /// <summary>Your units get +1 AP and +1 move. The army holds 2 units at most.</summary>
        ThirdWheel,

        /// <summary>A unit of yours that dies in a fight gets +1 max HP and +1 damage for the rest of the run.</summary>
        Loser,

        /// <summary>Before each fight, hire 1 of 2 random base units for that fight only.</summary>
        Mercenaries,

        /// <summary>Your units' pushes go 1 tile further and deal 2 damage.</summary>
        BatteringRam,
    }

    /// <summary>Names, rule text and numbers for banners, shared by the screens and the gamepedia.</summary>
    public static class Banners
    {
        public static readonly IReadOnlyList<Banner> All =
            new[] { Banner.Vampiric, Banner.ThirdWheel, Banner.Loser, Banner.Mercenaries, Banner.BatteringRam };

        // Placeholders.
        public const int VampiricHeal = 1;
        public const int ThirdWheelArmySize = 2;
        public const int ThirdWheelAp = 1;
        public const int ThirdWheelMoves = 1;
        public const int LoserHp = 1;
        public const int LoserDamage = 1;
        public const int MercOffers = 2;
        public const int MercPrice = 8;
        public const int RamDistance = 1;
        public const int RamDamage = 2;

        /// <summary>Banners offered at the start of a run.</summary>
        public const int StartChoices = 3;

        /// <summary>A banner's price in a shop.</summary>
        public const int Price = 20;

        public static string Name(Banner banner)
        {
            switch (banner)
            {
                case Banner.ThirdWheel: return "Third Wheel";
                case Banner.BatteringRam: return "Battering Ram";
                default: return banner.ToString();
            }
        }

        public static string Describe(Banner banner)
        {
            switch (banner)
            {
                case Banner.Vampiric:
                    return $"Every attack of yours that deals damage heals the attacker {VampiricHeal}. " +
                           $"But the sun hunts you: every fight has the Curse of the Sun (a sun beam targets one of your units each turn, {Battle.SunDamage} damage).";
                case Banner.ThirdWheel:
                    return $"Your units get +{ThirdWheelAp} AP and +{ThirdWheelMoves} move. But the army holds {ThirdWheelArmySize} units at most.";
                case Banner.Loser:
                    return $"A unit of yours that dies in a fight gets +{LoserHp} max HP and +{LoserDamage} damage for the rest of the run (stacks).";
                case Banner.Mercenaries:
                    return $"Before each fight, hire 1 of {MercOffers} random base units for {MercPrice} gold. It fights for that battle only " +
                           "and doesn't count as a survivor for gold.";
                case Banner.BatteringRam:
                    return $"Your units' pushes go {RamDistance} tile further and deal {RamDamage} damage.";
                default:
                    return "";
            }
        }
    }
}
