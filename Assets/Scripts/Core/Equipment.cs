using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>An item bought in the shop. Each unit has one slot; numbers are placeholders.</summary>
    public sealed class Equipment
    {
        public readonly string Name;
        public readonly int Price;
        public readonly int Hp;
        public readonly int Damage;
        public readonly int Ap;
        public readonly int Range;
        public readonly bool RangedOnly;

        public Equipment(string name, int price, int hp = 0, int damage = 0, int ap = 0, int range = 0, bool rangedOnly = false)
        {
            Name = name;
            Price = price;
            Hp = hp;
            Damage = damage;
            Ap = ap;
            Range = range;
            RangedOnly = rangedOnly;
        }

        public bool CanEquip(UnitStats stats) => !RangedOnly || stats.IsRanged;

        /// <summary>Short effect text, e.g. "+6 HP, -1 AP".</summary>
        public string Effect
        {
            get
            {
                var parts = new List<string>();
                Add(parts, Hp, "HP");
                Add(parts, Damage, "damage");
                Add(parts, Ap, "AP");
                Add(parts, Range, "range");
                var text = string.Join(", ", parts);
                return RangedOnly ? text + " (ranged units only)" : text;
            }
        }

        private static void Add(List<string> parts, int value, string label)
        {
            if (value != 0)
                parts.Add($"{(value > 0 ? "+" : "")}{value} {label}");
        }

        public override string ToString() => Name;

        public static readonly Equipment IronHelm = new Equipment("Iron Helm", 6, hp: 3);
        public static readonly Equipment HeavyArmor = new Equipment("Heavy Armor", 8, hp: 6, ap: -1);
        public static readonly Equipment WhetstoneBlade = new Equipment("Whetstone Blade", 10, damage: 1);
        public static readonly Equipment Longbow = new Equipment("Longbow", 8, range: 1, rangedOnly: true);
        public static readonly Equipment SwiftBoots = new Equipment("Swift Boots", 12, ap: 1);
        public static readonly Equipment BerserkerAxe = new Equipment("Berserker Axe", 9, hp: -3, damage: 2);

        public static readonly IReadOnlyList<Equipment> Catalog = new[]
        {
            IronHelm, HeavyArmor, WhetstoneBlade, Longbow, SwiftBoots, BerserkerAxe,
        };
    }
}
