namespace ArmyRoguelike.Core
{
    public enum Team
    {
        Player,
        Enemy,
    }

    public enum UnitKind
    {
        Warrior,
        Archer,
        Rogue,
        Wolf,
        PackLeader,
    }

    /// <summary>Base numbers for a unit type. All values are placeholders from the design note.</summary>
    public sealed class UnitStats
    {
        public readonly UnitKind Kind;
        public readonly string Name;
        public readonly int MaxHp;
        public readonly int Damage;
        public readonly int Range;
        public readonly int MaxAp;

        public UnitStats(UnitKind kind, string name, int maxHp, int damage, int range, int maxAp)
        {
            Kind = kind;
            Name = name;
            MaxHp = maxHp;
            Damage = damage;
            Range = range;
            MaxAp = maxAp;
        }

        public bool IsRanged => Range > 1;

        public static readonly UnitStats Warrior = new UnitStats(UnitKind.Warrior, "Warrior", 12, 3, 1, 3);
        public static readonly UnitStats Archer = new UnitStats(UnitKind.Archer, "Archer", 6, 2, 4, 4);
        public static readonly UnitStats Rogue = new UnitStats(UnitKind.Rogue, "Rogue", 5, 4, 1, 5);
        public static readonly UnitStats Wolf = new UnitStats(UnitKind.Wolf, "Wolf", 6, 2, 1, 4);
        public static readonly UnitStats PackLeader = new UnitStats(UnitKind.PackLeader, "Pack Leader", 10, 3, 1, 4);
    }
}
