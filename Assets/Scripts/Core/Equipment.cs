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

        /// <summary>An ability the item grants on top of the unit's own, or None.</summary>
        public readonly Ability Ability;

        public Equipment(string name, int price, int hp = 0, int damage = 0, int ap = 0, int range = 0,
            bool rangedOnly = false, Ability ability = Ability.None)
        {
            Name = name;
            Price = price;
            Hp = hp;
            Damage = damage;
            Ap = ap;
            Range = range;
            RangedOnly = rangedOnly;
            Ability = ability;
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
                if (Ability != Ability.None)
                    parts.Add(Abilities.Describe(Ability));
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
        public static readonly Equipment VampireFang = new Equipment("Vampire Fang", 9, ability: Ability.VampireFang);
        public static readonly Equipment ThornedMail = new Equipment("Thorned Mail", 8, ability: Ability.ThornedMail);
        public static readonly Equipment SpringBoots = new Equipment("Spring Boots", 7, ability: Ability.SpringBoots);
        public static readonly Equipment LuckyCharm = new Equipment("Lucky Charm", 10, ability: Ability.LuckyCharm);

        public static readonly IReadOnlyList<Equipment> Catalog = new[]
        {
            IronHelm, HeavyArmor, WhetstoneBlade, Longbow, SwiftBoots, BerserkerAxe,
            VampireFang, ThornedMail, SpringBoots, LuckyCharm,
        };
    }

    public static class Abilities
    {
        public static string Name(Ability ability)
        {
            switch (ability)
            {
                case Ability.ShieldWall: return "Shield Wall";
                case Ability.SteadyAim: return "Steady Aim";
                case Ability.Bloodthirst: return "Bloodthirst";
                case Ability.Mines: return "Mines";
                case Ability.VampireFang: return "Vampire Fang";
                case Ability.ThornedMail: return "Thorns";
                case Ability.SpringBoots: return "Spring Step";
                case Ability.LuckyCharm: return "Last Stand";
                default: return "";
            }
        }

        public static string Describe(Ability ability)
        {
            switch (ability)
            {
                case Ability.ShieldWall: return "allies next to it take 1 less damage";
                case Ability.SteadyAim: return "+1 damage if it hasn't moved this turn";
                case Ability.Bloodthirst: return "+1 damage per kill, kept for the run";
                case Ability.Mines:
                    return $"place a mine next to it ({Battle.MineCost} AP, max {Battle.MaxMinesPerUnit}); " +
                           $"an enemy stepping on it sets off a {Battle.MineDamage}-damage blast that hits everyone around";
                case Ability.VampireFang: return "heals 2 HP on a kill";
                case Ability.ThornedMail: return "melee attackers take 1 damage";
                case Ability.SpringBoots: return "the first tile moved each turn is free";
                case Ability.LuckyCharm: return "once per battle, survives a killing blow at 1 HP";
                default: return "";
            }
        }
    }
}
