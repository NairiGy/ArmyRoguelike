using System;
using System.Collections.Generic;

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

        /// <summary>Damage gained in this battle (Bloodthirst). Added to the army member when the battle ends.</summary>
        public int GainedDamage { get; internal set; }

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
        public int MaxHp => Math.Max(1, Stats.MaxHp + (Member?.BonusMaxHp ?? 0) + PackHp);

        /// <summary>Max HP from a Pack Leader's Pack Bond: +1 while one lives, -1 once the last one dies.</summary>
        public int PackHp { get; internal set; }
        public int BaseDamage => Math.Max(1, Stats.Damage + (Member?.BonusDamage ?? 0) + GainedDamage);
        public int Range => Math.Max(1, Stats.Range);
        public int MaxAp => Math.Max(1, Stats.MaxAp);

        /// <summary>Tiles per turn: the type's moves (1, or 2 with Swift), plus 1 for Spring Boots.</summary>
        public int MaxMoves => Stats.Moves + (Stats.Moves > 0 && Has(Ability.SpringBoots) ? 1 : 0);

        public int Size => Stats.Size;

        public bool IsRanged => Stats.IsRanged;

        public bool IsAlive => Hp > 0;

        public bool IsSummon => Stats.IsSummon;

        public bool IsObjective => Stats.IsObjective;

        public bool IsWolf => Stats.Kind == UnitKind.Wolf;

        public bool IsPackLeader => Stats.Kind == UnitKind.PackLeader;

        public bool IsSnared => SnareTurns > 0 || RootedThisTurn;

        public bool IsSlowed => SlowNext > 0 || SlowedThisTurn;

        /// <summary>A dead unit that left a bone pile behind.</summary>
        public bool IsRemains => IsPlaced && !IsAlive && Stats.LeavesRemains;

        /// <summary>Has the ability, from its type or from its item.</summary>
        public bool Has(Ability ability) =>
            ability != Ability.None && (Stats.Has(ability) || Equipment?.Ability == ability);

        public override string ToString() => $"{Stats.Name}#{Id} {Pos} hp {Hp} ap {Ap} mv {Moves}";
    }
}
