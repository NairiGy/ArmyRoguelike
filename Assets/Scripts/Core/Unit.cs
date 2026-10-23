namespace ArmyRoguelike.Core
{
    /// <summary>One statue on the board during a battle.</summary>
    public sealed class Unit
    {
        public readonly int Id;
        public readonly Team Team;
        public readonly UnitStats Stats;

        public int Hp { get; internal set; }
        public int Ap { get; internal set; }
        public GridPos Pos { get; internal set; }
        public bool IsPlaced { get; internal set; }

        /// <summary>True once the unit has finished its turn in the current phase.</summary>
        public bool IsDone { get; internal set; }

        public Unit(int id, Team team, UnitStats stats)
        {
            Id = id;
            Team = team;
            Stats = stats;
            Hp = stats.MaxHp;
        }

        public bool IsAlive => Hp > 0;

        public bool IsWolf => Stats.Kind == UnitKind.Wolf;

        public bool IsPackLeader => Stats.Kind == UnitKind.PackLeader;

        public override string ToString() => $"{Stats.Name}#{Id} {Pos} hp {Hp} ap {Ap}";
    }
}
