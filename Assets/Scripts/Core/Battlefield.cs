using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>
    /// A rule for one fight, shown on its map node before you choose it. It works the same for both sides.
    /// A fight has at most one.
    /// </summary>
    public enum Battlefield
    {
        /// <summary>A push off the board kills the unit: it falls from the tower.</summary>
        EdgeOfTheWorld,

        /// <summary>Each round lightning marks a tile under one unit of each side and strikes it at the end of the enemy turn.</summary>
        Storm,

        /// <summary>Every action's cooldown is 1 shorter (1 at least).</summary>
        ArcaneOverflow,

        /// <summary>Each round one unit of each side (never a boss) is favored: it deals double damage until the next round.</summary>
        FortunesFavored,

        /// <summary>Enemy intents are hidden.</summary>
        HiddenMinds,
    }

    /// <summary>Names, rule text and numbers for battlefields, shared by the screens and the gamepedia.</summary>
    public static class Battlefields
    {
        public static readonly IReadOnlyList<Battlefield> All = new[]
        {
            Battlefield.EdgeOfTheWorld, Battlefield.Storm, Battlefield.ArcaneOverflow, Battlefield.FortunesFavored,
            Battlefield.HiddenMinds,
        };

        // Placeholders.
        public const int StormDamage = 3;
        public const int CooldownCut = 1;
        public const int FavoredMultiplier = 2;

        public static string Name(Battlefield field)
        {
            switch (field)
            {
                case Battlefield.EdgeOfTheWorld: return "Edge of the World";
                case Battlefield.Storm: return "Storm";
                case Battlefield.ArcaneOverflow: return "Arcane Overflow";
                case Battlefield.FortunesFavored: return "Fortune's Favored";
                case Battlefield.HiddenMinds: return "What's On His Mind?";
                default: return field.ToString();
            }
        }

        public static string Describe(Battlefield field)
        {
            switch (field)
            {
                case Battlefield.EdgeOfTheWorld:
                    return "A unit pushed off the board falls from the tower and dies, on either side. Nothing saves it, " +
                           "and it leaves no bone pile. Huge units can't be pushed.";
                case Battlefield.Storm:
                    return "At the start of each round, lightning marks the tile under one of your units and one enemy. " +
                           $"At the end of the enemy turn it strikes those tiles for {StormDamage} damage, hitting whoever stands there.";
                case Battlefield.ArcaneOverflow:
                    return $"Every action's cooldown is {CooldownCut} shorter (1 at least), for both sides.";
                case Battlefield.FortunesFavored:
                    return "At the start of each round, one of your units and one enemy are favored: " +
                           $"their attacks and blasts deal {FavoredMultiplier}x damage until the next round. A boss is never favored.";
                case Battlefield.HiddenMinds:
                    return "You can't see what the enemies plan to do.";
                default:
                    return "";
            }
        }
    }
}
