using System;

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

        public int Hp { get; internal set; }
        public int Ap { get; internal set; }

        /// <summary>The tile the unit stands on. For a 2x2 unit, the corner with the lowest X and Y.</summary>
        public GridPos Pos { get; internal set; }

        public bool IsPlaced { get; internal set; }

        /// <summary>True once the unit has finished its turn in the current phase.</summary>
        public bool IsDone { get; internal set; }

        /// <summary>Moved at least one tile this turn (for Steady Aim).</summary>
        public bool MovedThisTurn { get; internal set; }

        /// <summary>The free first step of Spring Boots has been used this turn.</summary>
        public bool FreeStepUsed { get; internal set; }

        /// <summary>The Lucky Charm has already saved this unit in this battle.</summary>
        public bool LastStandUsed { get; internal set; }

        /// <summary>Damage gained in this battle (Bloodthirst). Added to the army member when the battle ends.</summary>
        public int GainedDamage { get; internal set; }

        public Unit(int id, Team team, UnitStats stats, ArmyMember member = null)
        {
            Id = id;
            Team = team;
            Stats = stats;
            Member = member;
            Hp = MaxHp;
        }

        /// <summary>The item in this unit's single slot, or null.</summary>
        public Equipment Equipment => Member?.Equipment;

        // Stats after equipment and run-long bonuses.
        public int MaxHp => Math.Max(1, Stats.MaxHp + (Equipment?.Hp ?? 0) + (Member?.BonusMaxHp ?? 0));
        public int BaseDamage => Math.Max(1, Stats.Damage + (Equipment?.Damage ?? 0) + (Member?.BonusDamage ?? 0) + GainedDamage);
        public int Range => Math.Max(1, Stats.Range + (Equipment?.Range ?? 0));
        public int MaxAp => Math.Max(1, Stats.MaxAp + (Equipment?.Ap ?? 0));

        public int Size => Stats.Size;

        public bool IsRanged => Range > 1;

        public bool IsAlive => Hp > 0;

        public bool IsWolf => Stats.Kind == UnitKind.Wolf;

        public bool IsPackLeader => Stats.Kind == UnitKind.PackLeader;

        /// <summary>A dead unit that left a bone pile behind.</summary>
        public bool IsRemains => IsPlaced && !IsAlive && Stats.LeavesRemains;

        /// <summary>Has the ability, built in or from its item.</summary>
        public bool Has(Ability ability) =>
            ability != Ability.None && (Stats.Ability == ability || Equipment?.Ability == ability);

        public override string ToString() => $"{Stats.Name}#{Id} {Pos} hp {Hp} ap {Ap}";
    }
}
