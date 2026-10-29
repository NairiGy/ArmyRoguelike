using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>
    /// An item for a unit's single slot. Only rule-bending items exist: each grants an ability.
    /// Numbers are placeholders.
    /// </summary>
    public sealed class Equipment
    {
        public readonly string Name;
        public readonly int Price;
        public readonly Ability Ability;

        public Equipment(string name, int price, Ability ability)
        {
            Name = name;
            Price = price;
            Ability = ability;
        }

        public bool CanEquip(UnitStats stats) => true;

        public string Effect => Abilities.Describe(Ability);

        public override string ToString() => Name;

        public static readonly Equipment VampireFang = new Equipment("Vampire Fang", 9, Ability.VampireFang);
        public static readonly Equipment ThornedMail = new Equipment("Thorned Mail", 8, Ability.ThornedMail);
        public static readonly Equipment SpringBoots = new Equipment("Spring Boots", 7, Ability.SpringBoots);
        public static readonly Equipment LuckyCharm = new Equipment("Lucky Charm", 10, Ability.LuckyCharm);

        public static readonly IReadOnlyList<Equipment> Catalog = new[] { VampireFang, ThornedMail, SpringBoots, LuckyCharm };
    }

    /// <summary>Names and rule text for abilities and actions, shared by the HUD and the gamepedia.</summary>
    public static class Abilities
    {
        public static string Name(Ability ability)
        {
            switch (ability)
            {
                case Ability.ShieldWall: return "Shield Wall";
                case Ability.Sweep: return "Sweep";
                case Ability.LongReach: return "Long Reach";
                case Ability.DoubleStrike: return "Double Strike";
                case Ability.Guard: return "Guard";
                case Ability.Taunt: return "Taunt";
                case Ability.Tough: return "Tough";
                case Ability.SteadyAim: return "Steady Aim";
                case Ability.SteadyAimPlus: return "Steady Aim+";
                case Ability.Pierce: return "Pierce";
                case Ability.FirstShotDouble: return "Opening Shot";
                case Ability.PreyOnSnared: return "Prey";
                case Ability.Volley: return "Volley";
                case Ability.Bloodthirst: return "Bloodthirst";
                case Ability.Pincer: return "Pincer";
                case Ability.KillRefund: return "Momentum";
                case Ability.Poison: return "Poison";
                case Ability.Counter: return "Riposte";
                case Ability.CounterAll: return "Riposte+";
                case Ability.Dodge: return "Dodge";
                case Ability.ArcaneBolt: return "Arcane Bolt";
                case Ability.Burning: return "Burning Ground";
                case Ability.Concussive: return "Concussive";
                case Ability.Chill: return "Chill";
                case Ability.Shatter: return "Shatter";
                case Ability.WideWard: return "Wide Ward";
                case Ability.Mines: return "Mines";
                case Ability.BigMines: return "Big Mines";
                case Ability.BlastProof: return "Blast Proof";
                case Ability.VampireFang: return "Vampire Fang";
                case Ability.ThornedMail: return "Thorns";
                case Ability.SpringBoots: return "Spring Step";
                case Ability.LuckyCharm: return "Last Stand";
                case Ability.Immobile: return "Immobile";
                case Ability.Swift: return "Swift";
                default: return "";
            }
        }

        public static string Describe(Ability ability)
        {
            switch (ability)
            {
                case Ability.ShieldWall: return "allies next to it take 1 less damage";
                case Ability.Sweep: return "attacks also hit the enemies on both sides of the target";
                case Ability.LongReach: return "melee reach 2 in a straight line, hitting both tiles";
                case Ability.DoubleStrike: return "every attack strikes twice";
                case Ability.Guard: return "takes the hit when an adjacent ally is attacked";
                case Ability.Taunt: return $"enemies within {Battle.TauntRange} tiles must attack it";
                case Ability.Tough: return "takes 1 less damage from everything";
                case Ability.SteadyAim: return "+1 damage if it hasn't moved this turn";
                case Ability.SteadyAimPlus: return "+2 damage if it hasn't moved this turn";
                case Ability.Pierce: return "shots along a straight row or column hit every enemy in the line";
                case Ability.FirstShotDouble: return "double damage the first time it hits each enemy in a battle";
                case Ability.PreyOnSnared: return $"snared enemies take +{Battle.PreyBonus} damage from everyone";
                case Ability.Volley: return "shots hit the target and the 4 tiles next to it for 1 less damage, friend or foe";
                case Ability.Bloodthirst: return $"+1 damage per kill, kept for the run (up to +{Battle.BloodthirstCap})";
                case Ability.Pincer: return $"+{Battle.PincerBonus} damage if an ally stands on the far side of the target";
                case Ability.KillRefund: return "a kill gives its action back: it can attack again";
                case Ability.Poison: return $"hits poison: {Battle.PoisonDamage} damage at the start of the target's next {Battle.PoisonTurns} turns";
                case Ability.Counter: return "counter-attacks the first melee hit each enemy turn";
                case Ability.CounterAll: return "counter-attacks every melee hit";
                case Ability.Dodge: return "the first hit each enemy turn deals 0 damage";
                case Ability.ArcaneBolt: return "its bolt ignores line of sight";
                case Ability.Burning: return $"fireballs leave the area burning for {Battle.BurnRounds} rounds ({Battle.BurnDamage} damage at the start of each turn)";
                case Ability.Concussive: return "fireballs push units outward and set off mines and bombs";
                case Ability.Chill: return "its hits slow: the target can't move on its next turn";
                case Ability.Shatter: return "double damage to slowed enemies";
                case Ability.WideWard: return "Ward also shields every ally next to the target";
                case Ability.Mines: return "an enemy stepping on one of its mines sets off a blast that hits everyone around";
                case Ability.BigMines: return $"its mines deal {Battle.BigMineDamage} damage and it can have {Battle.BigMaxMines} out";
                case Ability.BlastProof: return "its blasts don't hurt your units";
                case Ability.VampireFang: return $"heals {Battle.VampireHeal} HP on a kill";
                case Ability.ThornedMail: return $"melee attackers take {Battle.ThornsDamage} damage";
                case Ability.SpringBoots: return "+1 move each turn";
                case Ability.LuckyCharm: return "once per battle, survives a killing blow at 1 HP";
                case Ability.Immobile: return "can't move";
                case Ability.Swift: return $"{Battle.BaseMoves + 1} moves per turn instead of {Battle.BaseMoves}";
                default: return "";
            }
        }

        public static string Name(ActionKind action)
        {
            switch (action)
            {
                case ActionKind.PlaceMine: return "Place Mine";
                case ActionKind.ThrowBomb: return "Throw Bomb";
                case ActionKind.Snare: return "Snare";
                case ActionKind.Fireball: return "Fireball";
                case ActionKind.IceWall: return "Ice Wall";
                case ActionKind.Ward: return "Ward";
                case ActionKind.Mend: return "Mend";
                case ActionKind.Barricade: return "Barricade";
                case ActionKind.Turret: return "Build Turret";
                case ActionKind.Detonate: return "Detonate";
                case ActionKind.Charge: return "Charge";
                default: return action.ToString();
            }
        }

        public static string Describe(ActionKind action)
        {
            switch (action)
            {
                case ActionKind.PlaceMine:
                    return $"put a mine on a free tile next to it (max {Battle.MaxMinesPerUnit}); an enemy stepping on it sets off a {Battle.MineDamage}-damage 3x3 blast, friend or foe";
                case ActionKind.ThrowBomb:
                    return $"lob a bomb 2-{Battle.PlayerBombRange} tiles away; it explodes for {Battle.PlayerBombDamage} on that tile at the start of the next enemy turn";
                case ActionKind.Snare: return $"an enemy within {Battle.ActionRange} tiles can't move on its next turn";
                case ActionKind.Fireball: return $"{Battle.FireballDamage} damage to everyone on the tile and the 4 next to it, up to {Battle.FireballRange} tiles away";
                case ActionKind.IceWall: return $"turn a free tile within {Battle.ActionRange} into ice (blocks like a rock) for {Battle.IceRounds} rounds";
                case ActionKind.Ward: return $"an ally within {Battle.ActionRange} tiles gets a shield that absorbs {Battle.WardShield} damage";
                case ActionKind.Mend: return $"heal an ally within {Battle.ActionRange} tiles for {Battle.MendHeal}";
                case ActionKind.Barricade: return $"build a barricade (a rock) on a free tile next to it (max {Battle.MaxBarricades})";
                case ActionKind.Turret: return "build a turret next to it (one at a time); it acts on your turn but can't move";
                case ActionKind.Detonate: return $"blow up all its barricades: {Battle.BarricadeBlastDamage} damage in a 3x3 around each";
                case ActionKind.Charge: return $"rush 2-{Battle.ChargeRange} tiles in a straight line into an enemy, hit it and push it back 1";
                default: return "";
            }
        }
    }
}
