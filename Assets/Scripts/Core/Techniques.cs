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
    /// some need another technique first. Learning a new one also levels the unit up (see <see cref="Growth"/>). Numbers
    /// are placeholders.
    /// </summary>
    public sealed class Technique
    {
        public const int CommonPrice = 15;
        public const int RarePrice = 24;

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
            (Line == null || member.Stats == Line) && !member.Knows(this) && (Requires == null || member.Knows(Requires));

        public override string ToString() => Name;

        private static Technique P(UnitStats line, Ability ability, TechniqueRarity rarity = TechniqueRarity.Common, Technique requires = null) =>
            new Technique(ability, null, line, rarity, requires);

        private static Technique Act(UnitStats line, ActionKind action, TechniqueRarity rarity = TechniqueRarity.Common, Technique requires = null) =>
            new Technique(Ability.None, action, line, rarity, requires);

        private const TechniqueRarity Rare = TechniqueRarity.Rare;

        // ---------- Warrior ----------

        public static readonly Technique Sweep = Act(UnitStats.Warrior, ActionKind.Sweep);
        public static readonly Technique ShieldUp = Act(UnitStats.Warrior, ActionKind.ShieldUp);
        public static readonly Technique ThrowShield = Act(UnitStats.Warrior, ActionKind.ThrowShield);
        public static readonly Technique Vanguard = P(UnitStats.Warrior, Ability.Vanguard);
        public static readonly Technique Charge = Act(UnitStats.Warrior, ActionKind.Charge, Rare);
        public static readonly Technique Battlecry = Act(UnitStats.Warrior, ActionKind.Battlecry, Rare);
        public static readonly Technique Taunt = Act(UnitStats.Warrior, ActionKind.Taunt, Rare);
        public static readonly Technique ZocStrike = P(UnitStats.Warrior, Ability.ZocStrike, Rare);

        // Parked Warrior skills: their rules still work, but they aren't in All, so no run offers them.
        public static readonly Technique ShieldWall = P(UnitStats.Warrior, Ability.ShieldWall);
        public static readonly Technique Guard = P(UnitStats.Warrior, Ability.Guard);
        public static readonly Technique LongReach = P(UnitStats.Warrior, Ability.LongReach, Rare);
        public static readonly Technique DoubleStrike = P(UnitStats.Warrior, Ability.DoubleStrike, Rare);
        public static readonly Technique Tough = P(UnitStats.Warrior, Ability.Tough, Rare);

        // ---------- Archer ----------

        public static readonly Technique SteadyAimPlus = P(UnitStats.Archer, Ability.SteadyAimPlus);
        public static readonly Technique LongShot = P(UnitStats.Archer, Ability.LongShot);
        public static readonly Technique Vantage = P(UnitStats.Archer, Ability.Vantage);
        public static readonly Technique Kick = Act(UnitStats.Archer, ActionKind.Kick);
        public static readonly Technique SnareTrap = Act(UnitStats.Archer, ActionKind.SnareTrap);
        public static readonly Technique SpikedSnares = P(UnitStats.Archer, Ability.SpikedSnares, requires: SnareTrap);
        public static readonly Technique FireArrow = Act(UnitStats.Archer, ActionKind.FireArrow);
        public static readonly Technique Pierce = P(UnitStats.Archer, Ability.Pierce, Rare);
        public static readonly Technique OpeningShot = P(UnitStats.Archer, Ability.FirstShotDouble, Rare);
        public static readonly Technique Prey = P(UnitStats.Archer, Ability.PreyOnSnared, Rare);
        public static readonly Technique CoveringFire = Act(UnitStats.Archer, ActionKind.CoveringFire, Rare);
        public static readonly Technique SuppressingFire = Act(UnitStats.Archer, ActionKind.SuppressingFire, Rare, Pierce);
        public static readonly Technique MarkTarget = Act(UnitStats.Archer, ActionKind.MarkTarget);
        public static readonly Technique DeathMark = P(UnitStats.Archer, Ability.DeathMark, Rare, MarkTarget);
        public static readonly Technique Planted = Act(UnitStats.Archer, ActionKind.Planted);

        // Parked Archer skills: their rules still work, but they aren't in All, so no run offers them.
        public static readonly Technique Volley = P(UnitStats.Archer, Ability.Volley, Rare);

        // ---------- Rogue ----------

        public static readonly Technique Pincer = P(UnitStats.Rogue, Ability.Pincer);
        public static readonly Technique Riposte = P(UnitStats.Rogue, Ability.Counter);
        public static readonly Technique Poison = P(UnitStats.Rogue, Ability.Poison);
        public static readonly Technique CheapShot = P(UnitStats.Rogue, Ability.CheapShot);
        public static readonly Technique Elusive = P(UnitStats.Rogue, Ability.Elusive);
        public static readonly Technique ThrowingKnife = Act(UnitStats.Rogue, ActionKind.ThrowingKnife);
        public static readonly Technique Shadowstep = Act(UnitStats.Rogue, ActionKind.Shadowstep);
        public static readonly Technique Momentum = P(UnitStats.Rogue, Ability.KillRefund, Rare);
        public static readonly Technique Dodge = P(UnitStats.Rogue, Ability.Dodge, Rare);
        public static readonly Technique SmokeBomb = Act(UnitStats.Rogue, ActionKind.SmokeBomb, Rare);
        public static readonly Technique Rupture = Act(UnitStats.Rogue, ActionKind.Rupture, Rare, Poison);
        public static readonly Technique IntoShadows = P(UnitStats.Rogue, Ability.IntoShadows, Rare);
        public static readonly Technique Isolate = P(UnitStats.Rogue, Ability.Isolate);
        public static readonly Technique LoneWolf = P(UnitStats.Rogue, Ability.LoneWolf);

        // Parked Rogue skill: its rule still works, but it isn't in All, so no run offers it.
        public static readonly Technique RipostePlus = P(UnitStats.Rogue, Ability.CounterAll, Rare, Riposte);

        // ---------- Mage ----------

        // ---------- Mage (night pool 2026-11-08: fire, ice and arcane; see docs/night/mage-sapper.md) ----------

        // Fire
        public static readonly Technique Fireball = Act(UnitStats.Apprentice, ActionKind.Fireball);
        public static readonly Technique Burning = P(UnitStats.Apprentice, Ability.Burning, requires: Fireball);
        public static readonly Technique Firewall = Act(UnitStats.Apprentice, ActionKind.Firewall);
        public static readonly Technique SearingBolt = P(UnitStats.Apprentice, Ability.SearingBolt);
        public static readonly Technique Concussive = P(UnitStats.Apprentice, Ability.Concussive, Rare, Fireball);

        // Ice
        public static readonly Technique Chill = P(UnitStats.Apprentice, Ability.Chill);
        public static readonly Technique IceWall = Act(UnitStats.Apprentice, ActionKind.IceWall);
        public static readonly Technique Shatter = P(UnitStats.Apprentice, Ability.Shatter, Rare, Chill);
        public static readonly Technique Blizzard = Act(UnitStats.Apprentice, ActionKind.Blizzard, Rare);

        // Arcane
        public static readonly Technique Ward = Act(UnitStats.Apprentice, ActionKind.Ward);
        public static readonly Technique Mend = Act(UnitStats.Apprentice, ActionKind.Mend, requires: Ward);
        public static readonly Technique Transpose = Act(UnitStats.Apprentice, ActionKind.Transpose);
        public static readonly Technique Twincast = P(UnitStats.Apprentice, Ability.Twincast, Rare);
        public static readonly Technique Overchannel = Act(UnitStats.Apprentice, ActionKind.Overchannel);

        // Parked Mage skills: their rules still work, but they aren't in All, so no run offers them.
        public static readonly Technique WideWard = P(UnitStats.Apprentice, Ability.WideWard, Rare, Ward);
        public static readonly Technique Blink = Act(UnitStats.Apprentice, ActionKind.Blink);

        // ---------- Sapper (night pool 2026-11-08: mines, rubble, walls and blasts) ----------

        // Mines
        public static readonly Technique BigMines = P(UnitStats.Sapper, Ability.BigMines);
        public static readonly Technique Hook = Act(UnitStats.Sapper, ActionKind.Hook);
        public static readonly Technique QuickFuse = P(UnitStats.Sapper, Ability.QuickFuse, Rare);

        // Rubble and walls
        public static readonly Technique Barricade = Act(UnitStats.Sapper, ActionKind.Barricade);
        public static readonly Technique CaveIn = Act(UnitStats.Sapper, ActionKind.CaveIn);
        public static readonly Technique RiggedBarricades = P(UnitStats.Sapper, Ability.RiggedBarricades, requires: Barricade);
        public static readonly Technique Demolish = Act(UnitStats.Sapper, ActionKind.Demolish, Rare);
        public static readonly Technique Turret = Act(UnitStats.Sapper, ActionKind.Turret, Rare);

        // Blasts
        public static readonly Technique ThrowBomb = Act(UnitStats.Sapper, ActionKind.ThrowBomb);
        public static readonly Technique BlastProof = P(UnitStats.Sapper, Ability.BlastProof);
        public static readonly Technique Incendiary = P(UnitStats.Sapper, Ability.Incendiary);
        public static readonly Technique Detonate = Act(UnitStats.Sapper, ActionKind.Detonate, Rare, Barricade);

        public static readonly IReadOnlyList<Technique> All = new[]
        {
            Sweep, ShieldUp, ThrowShield, Vanguard, Charge, Battlecry, Taunt, ZocStrike,
            SteadyAimPlus, Planted, LongShot, Vantage, Kick, SnareTrap, FireArrow, MarkTarget, Pierce, OpeningShot, Prey, CoveringFire, SuppressingFire, DeathMark,
            Pincer, Riposte, Poison, CheapShot, Elusive, ThrowingKnife, Shadowstep, Isolate, Momentum, Dodge, SmokeBomb, Rupture, IntoShadows, LoneWolf,
            Fireball, Burning, Firewall, SearingBolt, Concussive, Chill, IceWall, Shatter, Blizzard, Ward, Mend, Transpose, Twincast, Overchannel,
            BigMines, Hook, QuickFuse, Barricade, CaveIn, RiggedBarricades, Demolish, Turret, ThrowBomb, BlastProof, Incendiary, Detonate,
        };

        /// <summary>The techniques a member could learn now.</summary>
        public static IEnumerable<Technique> LearnableBy(ArmyMember member) => All.Where(t => t.CanLearn(member));
    }

    /// <summary>
    /// What each level gives, by line. A unit's level is the number of different techniques it has learned (forgetting
    /// one keeps the level, relearning it adds none). Damage comes rarely on purpose: HP is 5-12 on a 6x6 board. Numbers
    /// are placeholders.
    /// </summary>
    public static class Growth
    {
        public static int HpPerLevel(UnitStats line) => line == UnitStats.Warrior ? 2 : 1;

        /// <summary>The levels at which the line gets +1 damage.</summary>
        public static IReadOnlyList<int> DamageLevels(UnitStats line)
        {
            if (line == UnitStats.Rogue)
                return new[] { 2, 4, 6 };
            // The Archer's pool grew to 12 (2026-11-08), so its second point moved from 5 to 6 like the Mage's.
            if (line == UnitStats.Archer)
                return new[] { 3, 6 };
            if (line == UnitStats.Apprentice)
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
