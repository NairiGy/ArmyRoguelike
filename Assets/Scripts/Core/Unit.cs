using System;

namespace ArmyRoguelike.Core
{
    /// <summary>One statue on the board during a battle.</summary>
    public sealed class Unit
    {
        public readonly int Id;
        public readonly Team Team;
        public readonly UnitStats Stats;

        /// <summary>The item in this unit's single slot, or null.</summary>
        public readonly Equipment Equipment;

        public int Hp { get; internal set; }
        public int Ap { get; internal set; }

        /// <summary>The tile the unit stands on. For a 2x2 unit, the corner with the lowest X and Y.</summary>
        public GridPos Pos { get; internal set; }

        public bool IsPlaced { get; internal set; }

        /// <summary>True once the unit has finished its turn in the current phase.</summary>
        public bool IsDone { get; internal set; }

        public Unit(int id, Team team, UnitStats stats, Equipment equipment = null)
        {
            Id = id;
            Team = team;
            Stats = stats;
            Equipment = equipment;
            Hp = MaxHp;
        }

        // Stats after equipment.
        public int MaxHp => Math.Max(1, Stats.MaxHp + (Equipment?.Hp ?? 0));
        public int BaseDamage => Math.Max(1, Stats.Damage + (Equipment?.Damage ?? 0));
        public int Range => Math.Max(1, Stats.Range + (Equipment?.Range ?? 0));
        public int MaxAp => Math.Max(1, Stats.MaxAp + (Equipment?.Ap ?? 0));

        public int Size => Stats.Size;

        public bool IsRanged => Range > 1;

        public bool IsAlive => Hp > 0;

        public bool IsWolf => Stats.Kind == UnitKind.Wolf;

        public bool IsPackLeader => Stats.Kind == UnitKind.PackLeader;

        /// <summary>A dead unit that left a bone pile behind.</summary>
        public bool IsRemains => IsPlaced && !IsAlive && Stats.LeavesRemains;

        public override string ToString() => $"{Stats.Name}#{Id} {Pos} hp {Hp} ap {Ap}";
    }
}
