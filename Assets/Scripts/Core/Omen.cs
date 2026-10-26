using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>
    /// A run-long effect from the Moon Shrine: a blessing if the shrine held, a curse if it broke.
    /// Each omen works the same way either way; a blessing helps the player and a curse helps the enemy.
    /// </summary>
    public enum Omen
    {
        /// <summary>Each turn a tile under one of the other side's units is marked, and a sun beam strikes it.</summary>
        Sun,

        /// <summary>Each turn one unit and the 3x3 around it are shrouded: the other side can't hit those tiles from range.</summary>
        Night,

        /// <summary>Each turn one unit gets a shield that lasts a round.</summary>
        Moon,
    }

    /// <summary>Names and rule text for omens, shared by the screens and the gamepedia.</summary>
    public static class Omens
    {
        public static readonly IReadOnlyList<Omen> All = new[] { Omen.Sun, Omen.Night, Omen.Moon };

        public static string Name(Omen omen, bool blessing) =>
            $"{(blessing ? "Blessing" : "Curse")} of the {omen}";

        public static string Describe(Omen omen, bool blessing)
        {
            var mine = blessing ? "your" : "an enemy";
            var theirs = blessing ? "an enemy" : "one of your units";
            switch (omen)
            {
                case Omen.Sun:
                    return $"At the start of your turn, the tile under {theirs} is marked. At the start of the enemy turn a sun beam " +
                           $"strikes it for {Battle.SunDamage} damage, hitting whoever stands there.";
                case Omen.Night:
                    return $"At the start of your turn, {mine} unit and the 3x3 around it are shrouded until your next turn: " +
                           $"{(blessing ? "enemies" : "your units")} can't hit units there from range.";
                case Omen.Moon:
                    return $"At the start of your turn, {mine} unit gets a shield that absorbs {Battle.MoonShield} damage until your next turn.";
                default:
                    return "";
            }
        }
    }
}
