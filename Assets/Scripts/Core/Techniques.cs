using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    public enum TechniqueRarity
    {
        Common,
        Rare,
    }

    /// <summary>
    /// A passive or action a unit learns during the run (from a reward pick, the shop or a cart). Each one belongs to
    /// a base line (a null line would mean any line, but none is shared for now: effects for any unit are items), and
    /// some need another technique first. Learning one also levels the unit up (see <see cref="Growth"/>). Numbers are
    /// placeholders.
    /// </summary>
    public sealed class Technique
    {
        public const int CommonPrice = 10;
        public const int RarePrice = 16;

        /// <summary>The passive it grants, or None for an action.</summary>
        public readonly Ability Ability;

        /// <summary>The action it grants, or null for a passive.</summary>
        public readonly ActionKind? Action;

        /// <summary>The base unit whose line can learn it; null = any line.</summary>
        public readonly UnitStats Line;

        /// <summary>A technique the unit must already know, or null.</summary>
        public readonly Technique Requires;

        public readonly TechniqueRarity Rarity;

        private Technique(Ability ability, ActionKind? action, UnitStats line, TechniqueRarity rarity, Technique requires)
        {
            Ability = ability;
            Action = action;
            Line = line;
            Rarity = rarity;
            Requires = requires;
        }

        public string Name => Action.HasValue ? Abilities.Name(Action.Value) : Abilities.Name(Ability);

        public string Description => Action.HasValue ? Abilities.Describe(Action.Value) : Abilities.Describe(Ability);

        public int Price => Rarity == TechniqueRarity.Rare ? RarePrice : CommonPrice;

        public bool IsShared => Line == null;

        /// <summary>Whether this member could learn it now: its line, not known yet, and its requirement known.</summary>
        public bool CanLearn(ArmyMember member) =>
            (Line == null || member.Stats.Root == Line) && !member.Knows(this) && (Requires == null || member.Knows(Requires));

        public override string ToString() => Name;

        private static Technique P(UnitStats line, Ability ability, TechniqueRarity rarity = TechniqueRarity.Common, Technique requires = null) =>
            new Technique(ability, null, line, rarity, requires);

        private static Technique Act(UnitStats line, ActionKind action, TechniqueRarity rarity = TechniqueRarity.Common, Technique requires = null) =>
            new Technique(Ability.None, action, line, rarity, requires);

        private const TechniqueRarity Rare = TechniqueRarity.Rare;

        // ---------- Warrior ----------

        public static readonly Technique Sweep = P(UnitStats.Warrior, Ability.Sweep);
        public static readonly Technique Guard = P(UnitStats.Warrior, Ability.Guard);
        public static readonly Technique LongReach = P(UnitStats.Warrior, Ability.LongReach, Rare);
        public static readonly Technique DoubleStrike = P(UnitStats.Warrior, Ability.DoubleStrike, Rare);
        public static readonly Technique Charge = Act(UnitStats.Warrior, ActionKind.Charge, Rare);
        public static readonly Technique Taunt = P(UnitStats.Warrior, Ability.Taunt, Rare);
        public static readonly Technique Tough = P(UnitStats.Warrior, Ability.Tough, Rare);

        // ---------- Archer ----------

        public static readonly Technique SteadyAimPlus = P(UnitStats.Archer, Ability.SteadyAimPlus);
        public static readonly Technique Pierce = P(UnitStats.Archer, Ability.Pierce, Rare);
        public static readonly Technique OpeningShot = P(UnitStats.Archer, Ability.FirstShotDouble, Rare);
        public static readonly Technique Prey = P(UnitStats.Archer, Ability.PreyOnSnared, Rare);
        public static readonly Technique Volley = P(UnitStats.Archer, Ability.Volley, Rare);

        // ---------- Rogue ----------

        public static readonly Technique Pincer = P(UnitStats.Rogue, Ability.Pincer);
        public static readonly Technique Riposte = P(UnitStats.Rogue, Ability.Counter);
        public static readonly Technique Momentum = P(UnitStats.Rogue, Ability.KillRefund, Rare);
        public static readonly Technique Poison = P(UnitStats.Rogue, Ability.Poison, Rare);
        public static readonly Technique RipostePlus = P(UnitStats.Rogue, Ability.CounterAll, Rare, Riposte);
        public static readonly Technique Dodge = P(UnitStats.Rogue, Ability.Dodge, Rare);

        // ---------- Mage ----------

        public static readonly Technique Fireball = Act(UnitStats.Apprentice, ActionKind.Fireball);
        public static readonly Technique Chill = P(UnitStats.Apprentice, Ability.Chill);
        public static readonly Technique Ward = Act(UnitStats.Apprentice, ActionKind.Ward);
        public static readonly Technique Burning = P(UnitStats.Apprentice, Ability.Burning, Rare, Fireball);
        public static readonly Technique Concussive = P(UnitStats.Apprentice, Ability.Concussive, Rare, Fireball);
        public static readonly Technique IceWall = Act(UnitStats.Apprentice, ActionKind.IceWall, Rare);
        public static readonly Technique Shatter = P(UnitStats.Apprentice, Ability.Shatter, Rare, Chill);
        public static readonly Technique WideWard = P(UnitStats.Apprentice, Ability.WideWard, Rare, Ward);
        public static readonly Technique Mend = Act(UnitStats.Apprentice, ActionKind.Mend, Rare, Ward);

        // ---------- Sapper ----------

        public static readonly Technique BigMines = P(UnitStats.Sapper, Ability.BigMines);
        public static readonly Technique Barricade = Act(UnitStats.Sapper, ActionKind.Barricade);
        public static readonly Technique ThrowBomb = Act(UnitStats.Sapper, ActionKind.ThrowBomb, Rare);
        public static readonly Technique BlastProof = P(UnitStats.Sapper, Ability.BlastProof, Rare);
        public static readonly Technique Turret = Act(UnitStats.Sapper, ActionKind.Turret, Rare, Barricade);
        public static readonly Technique Detonate = Act(UnitStats.Sapper, ActionKind.Detonate, Rare, Barricade);

        public static readonly IReadOnlyList<Technique> All = new[]
        {
            Sweep, Guard, LongReach, DoubleStrike, Charge, Taunt, Tough,
            SteadyAimPlus, Pierce, OpeningShot, Prey, Volley,
            Pincer, Riposte, Momentum, Poison, RipostePlus, Dodge,
            Fireball, Chill, Ward, Burning, Concussive, IceWall, Shatter, WideWard, Mend,
            BigMines, Barricade, ThrowBomb, BlastProof, Turret, Detonate,
        };

        /// <summary>The techniques a member could learn now.</summary>
        public static IEnumerable<Technique> LearnableBy(ArmyMember member) => All.Where(t => t.CanLearn(member));
    }

    /// <summary>
    /// What each level gives, by line. A unit's level is the number of techniques it has learned (forgetting one
    /// keeps the level). Damage comes rarely on purpose: HP is 5-12 on a 6x6 board. Numbers are placeholders.
    /// </summary>
    public static class Growth
    {
        public static int HpPerLevel(UnitStats line) => line == UnitStats.Warrior ? 2 : 1;

        /// <summary>The levels at which the line gets +1 damage.</summary>
        public static IReadOnlyList<int> DamageLevels(UnitStats line)
        {
            if (line == UnitStats.Rogue)
                return new[] { 2, 4, 6 };
            if (line == UnitStats.Archer || line == UnitStats.Apprentice)
                return new[] { 3, 6 };
            return new int[0];
        }

        /// <summary>The level at which the line gets one more mine on the board (Sapper only); 0 = never.</summary>
        public static int MineLevel(UnitStats line) => line == UnitStats.Sapper ? 3 : 0;

        public static int Hp(UnitStats line, int level) => HpPerLevel(line) * level;

        public static int Damage(UnitStats line, int level) => DamageLevels(line).Count(l => level >= l);

        public static int Mines(UnitStats line, int level) => MineLevel(line) > 0 && level >= MineLevel(line) ? 1 : 0;
    }
}
