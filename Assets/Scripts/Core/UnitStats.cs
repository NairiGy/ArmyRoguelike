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
        Skeleton,
        Necromancer,
        Ogre,
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

        /// <summary>Side length in tiles. 1 for normal units, 2 for a 2x2 unit like the ogre.</summary>
        public int Size { get; private set; } = 1;

        /// <summary>Leaves a bone pile when killed, which a necromancer can raise.</summary>
        public bool LeavesRemains { get; private set; }

        /// <summary>The basic attack hits every enemy next to the unit, not just one.</summary>
        public bool AreaMelee { get; private set; }

        /// <summary>Raising a bone pile: AP cost and range in steps. 0 = can't raise.</summary>
        public int RaiseCost { get; private set; }

        public int RaiseRange { get; private set; }

        /// <summary>Boulder throw: AP cost, damage and range in steps. 0 = can't throw. Ignores line of sight.</summary>
        public int ThrowCost { get; private set; }

        public int ThrowDamage { get; private set; }

        public int ThrowRange { get; private set; }

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

        public bool CanRaise => RaiseCost > 0;

        public bool CanThrow => ThrowCost > 0;

        public static readonly UnitStats Warrior = new UnitStats(UnitKind.Warrior, "Warrior", 12, 3, 1, 3);
        public static readonly UnitStats Archer = new UnitStats(UnitKind.Archer, "Archer", 6, 2, 4, 4);
        public static readonly UnitStats Rogue = new UnitStats(UnitKind.Rogue, "Rogue", 5, 4, 1, 5);
        public static readonly UnitStats Wolf = new UnitStats(UnitKind.Wolf, "Wolf", 6, 2, 1, 4);
        public static readonly UnitStats PackLeader = new UnitStats(UnitKind.PackLeader, "Pack Leader", 10, 3, 1, 4);

        public static readonly UnitStats Skeleton =
            new UnitStats(UnitKind.Skeleton, "Skeleton", 4, 2, 1, 3) { LeavesRemains = true };

        /// <summary>Its basic attack is a weak bolt (range 3, needs line of sight).</summary>
        public static readonly UnitStats Necromancer =
            new UnitStats(UnitKind.Necromancer, "Necromancer", 6, 2, 3, 3) { RaiseCost = 3, RaiseRange = 4 };

        /// <summary>Its basic attack is the club smash, which hits everyone adjacent.</summary>
        public static readonly UnitStats Ogre = new UnitStats(UnitKind.Ogre, "Ogre", 30, 5, 1, 4)
        {
            Size = 2, AreaMelee = true, ThrowCost = 3, ThrowDamage = 3, ThrowRange = 5,
        };
    }
}
