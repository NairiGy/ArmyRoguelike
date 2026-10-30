using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    /// <summary>One statue on the board during a battle.</summary>
    public sealed class Unit
    {
        public readonly int Id;
        public readonly Team Team;
        public readonly UnitStats Stats;

        /// <summary>The army member this unit was made from (player units only), for run-long bonuses.</summary>
        public readonly ArmyMember Member;

        /// <summary>For summons: the unit that built it.</summary>
        public readonly Unit Summoner;

        public int Hp { get; internal set; }
        public int Ap { get; internal set; }

        /// <summary>Tiles the unit can still walk this turn. Moving doesn't cost AP.</summary>
        public int Moves { get; internal set; }

        /// <summary>The tile the unit stands on. For a 2x2 unit, the corner with the lowest X and Y.</summary>
        public GridPos Pos { get; internal set; }

        public bool IsPlaced { get; internal set; }

        /// <summary>True once the unit has finished its turn in the current phase.</summary>
        public bool IsDone { get; internal set; }

        // ---------- Per-turn flags ----------

        /// <summary>Moved at least one tile this turn (for Steady Aim).</summary>
        public bool MovedThisTurn { get; internal set; }

        /// <summary>Spent AP this turn (attack or action), so it can't move again until its next turn.</summary>
        public bool ActedThisTurn { get; internal set; }

        /// <summary>Has attacked this turn.</summary>
        public bool AttackedThisTurn { get; internal set; }

        // Own turns left before each action is ready again (missing = ready).
        private readonly Dictionary<ActionKind, int> _cooldowns = new Dictionary<ActionKind, int>();

        /// <summary>Own turns until the action is ready again; 0 = ready now.</summary>
        public int CooldownLeft(ActionKind action) => _cooldowns.TryGetValue(action, out var left) ? left : 0;

        internal void StartCooldown(ActionKind action, int turns)
        {
            if (turns > 0)
                _cooldowns[action] = turns;
        }

        // How often each special action was used this battle (for the playtest log).
        private readonly Dictionary<ActionKind, int> _uses = new Dictionary<ActionKind, int>();

        /// <summary>Times the unit used this special action in this battle.</summary>
        public int TimesUsed(ActionKind action) => _uses.TryGetValue(action, out var n) ? n : 0;

        internal void CountUse(ActionKind action) => _uses[action] = TimesUsed(action) + 1;

        /// <summary>Every action ready again (tests).</summary>
        internal void ClearCooldowns() => _cooldowns.Clear();

        /// <summary>Start of its own turn: every cooldown counts down by one.</summary>
        internal void TickCooldowns()
        {
            foreach (var action in new List<ActionKind>(_cooldowns.Keys))
                if (--_cooldowns[action] <= 0)
                    _cooldowns.Remove(action);
        }

        // Ids of the units this one has attacked this battle (for the Deadeye's opening shot).
        private readonly HashSet<int> _struck = new HashSet<int>();

        /// <summary>Has attacked this target before in this battle.</summary>
        public bool HasStruck(Unit target) => _struck.Contains(target.Id);

        internal void MarkStruck(Unit target) => _struck.Add(target.Id);

        /// <summary>Riposte already used this enemy turn.</summary>
        public bool CounterUsed { get; internal set; }

        /// <summary>Dodge already used this enemy turn.</summary>
        public bool DodgeUsed { get; internal set; }

        // ---------- Statuses ----------

        /// <summary>Vanished (rogue): enemy turns left before it shows again. Its next attack reveals it.</summary>
        public int HiddenTurns { get; internal set; }

        /// <summary>Invisible: enemies can't target or attack it, though area blasts still hit it.</summary>
        public bool IsHidden => HiddenTurns > 0;

        /// <summary>Pinned: can't move on its next turn.</summary>
        public int SnareTurns { get; internal set; }

        /// <summary>Can't move this turn (a snare took effect).</summary>
        public bool RootedThisTurn { get; internal set; }

        /// <summary>Chilled: can't move on its next turn.</summary>
        public int SlowNext { get; internal set; }

        /// <summary>Can't move this turn because of Chill.</summary>
        public bool SlowedThisTurn { get; internal set; }

        /// <summary>Poison ticks left; each deals poison damage at the start of the unit's turn.</summary>
        public int PoisonTurns { get; internal set; }

        /// <summary>Ward shield: absorbs this much damage before HP.</summary>
        public int Shield { get; internal set; }

        /// <summary>Moon omen shield: absorbs damage before HP, gone at the start of the next player turn.</summary>
        public int MoonShield { get; internal set; }

        /// <summary>The Lucky Charm has already saved this unit in this battle.</summary>
        public bool LastStandUsed { get; internal set; }

        /// <summary>Gold a cutpurse is carrying after a hit. It flees with it; killing it gets it back.</summary>
        public int CarryingLoot { get; internal set; }

        /// <summary>Left the board (a cutpurse that got away). No longer in the battle.</summary>
        public bool Escaped { get; internal set; }

        public Unit(int id, Team team, UnitStats stats, ArmyMember member = null, Unit summoner = null)
        {
            Id = id;
            Team = team;
            Stats = stats;
            Member = member;
            Summoner = summoner;
            Hp = MaxHp;
        }

        /// <summary>The item in this unit's single slot, or null.</summary>
        public Equipment Equipment => Member?.Equipment;

        // Stats after run-long bonuses.
        public int MaxHp => Math.Max(1, Stats.MaxHp + (Member?.BonusMaxHp ?? 0) + (Member?.LevelHp ?? 0) + PackHp);

        /// <summary>Max HP from a Pack Leader's Pack Bond: +1 while one lives, -1 once the last one dies.</summary>
        public int PackHp { get; internal set; }
        public int BaseDamage => Math.Max(1, Stats.Damage + (Member?.BonusDamage ?? 0) + (Member?.LevelDamage ?? 0));

        /// <summary>Attack range in steps. Long Reach makes it 2 (still melee).</summary>
        public int Range => Math.Max(Has(Ability.LongReach) ? 2 : 1, Stats.Range);
        public int MaxAp => Math.Max(1, Stats.MaxAp) + (InWrath ? 1 : 0);

        /// <summary>Wrath (the Giant): at half HP or less it gets one more AP each turn.</summary>
        public bool InWrath => Has(Ability.Wrath) && IsAlive && Hp * 2 <= MaxHp;

        /// <summary>Tiles per turn: the type's moves (1, or 2 with Swift), plus 1 for Spring Boots.</summary>
        public int MaxMoves => Stats.Moves + (Stats.Moves > 0 && Has(Ability.SpringBoots) ? 1 : 0);

        public int Size => Stats.Size;

        /// <summary>A ranged attack needs line of sight and can't be countered. Long Reach is still melee.</summary>
        public bool IsRanged => Range > 1 && !Has(Ability.LongReach);

        public bool IsAlive => Hp > 0;

        public bool IsSummon => Stats.IsSummon;

        public bool IsObjective => Stats.IsObjective;

        public bool IsWolf => Stats.Kind == UnitKind.Wolf;

        public bool IsPackLeader => Stats.Kind == UnitKind.PackLeader;

        public bool IsSnared => SnareTurns > 0 || RootedThisTurn;

        public bool IsSlowed => SlowNext > 0 || SlowedThisTurn;

        /// <summary>A dead unit that left a bone pile behind.</summary>
        public bool IsRemains => IsPlaced && !IsAlive && (Stats.LeavesRemains || LeftPile) && !PileGone;

        /// <summary>One of your units that died while a Necromancer with Soul Harvest was on the board: it left a bone pile.</summary>
        public bool LeftPile { get; internal set; }

        /// <summary>Its bone pile is used up (raised as a new unit by Soul Harvest, or blown up by Corpse Blast).</summary>
        public bool PileGone { get; internal set; }

        /// <summary>Has the ability, from its type, its item or a technique.</summary>
        public bool Has(Ability ability) =>
            ability != Ability.None && (Stats.Has(ability) || Equipment?.Ability == ability || (Member?.HasTechniqueAbility(ability) ?? false));

        /// <summary>Its special actions: its type's, then those from techniques.</summary>
        public IEnumerable<ActionKind> Actions =>
            Member == null ? Stats.Actions : Stats.Actions.Concat(Member.TechniqueActions).Distinct();

        public bool HasAction(ActionKind action) => Actions.Contains(action);

        public override string ToString() => $"{Stats.Name}#{Id} {Pos} hp {Hp} ap {Ap} mv {Moves}";
    }
}
