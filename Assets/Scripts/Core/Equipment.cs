using System.Collections.Generic;
using System.Linq;

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
                case Ability.BloodScent: return "Blood Scent";
                case Ability.WolfBite: return "Wolf Bite";
                case Ability.PackBond: return "Pack Bond";
                case Ability.Howl: return "Howl";
                case Ability.Slippery: return "Slippery";
                case Ability.Fling: return "Fling";
                case Ability.PowderKeg: return "Powder Keg";
                case Ability.LightFingers: return "Light Fingers";
                case Ability.Caltrops: return "Caltrops";
                case Ability.BehindCover: return "Behind Cover";
                case Ability.Overwatch: return "Overwatch";
                case Ability.BullRush: return "Bull Rush";
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
                case Ability.PreyOnSnared: return $"pinned enemies take +{Battle.PreyBonus} damage from everyone";
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
                case Ability.BloodScent: return "goes for the lowest-HP unit it can reach and attack this turn, instead of the nearest";
                case Ability.WolfBite: return "+1 damage for every wolf bite earlier this turn";
                case Ability.PackBond: return $"while it lives, every wolf has +{Battle.PackBondHp} max HP; once it dies they have -{Battle.PackBondHp} and lose {2 * Battle.PackBondHp} HP on the spot";
                case Ability.Howl: return $"every wolf that hasn't acted yet gets +{Battle.HowlMoves} move this turn";
                case Ability.Slippery: return "ignores zone of control: leaving your units' reach doesn't use up its moves";
                case Ability.Fling: return $"throws what is next to it at a unit 2-{Battle.FlingRange} tiles away, over obstacles: a bomb or mine goes off there with its own blast; a goblin that hasn't acted lands next to the unit, deals {Battle.FlingDamage}, and can still act";
                case Ability.PowderKeg: return $"when it dies it explodes: {Battle.PowderKegDamage} damage to everyone around it, its own side included";
                case Ability.LightFingers: return "the only unit that can still move after attacking: it stabs, grabs the gold and runs in the same turn";
                case Ability.Caltrops: return $"drops caltrops on its tile or a free tile next to it; a unit of yours stepping on them takes {Battle.CaltropsDamage} and its movement ends";
                case Ability.BehindCover: return "while next to a rock it can't be hit by ranged attacks; melee, bombs and blasts still reach it";
                case Ability.Overwatch: return "instead of shooting, it aims: during your next turn the first of your units that walks onto a tile it can see in range is shot and stops there";
                case Ability.BullRush: return $"hit a unit next to it and push it 1 tile; if it slams into a unit, both take {Battle.CollisionDamage}";
                default: return "";
            }
        }

        public static string Name(ActionKind action)
        {
            switch (action)
            {
                case ActionKind.PlaceMine: return "Place Mine";
                case ActionKind.ThrowBomb: return "Throw Bomb";
                case ActionKind.PinningShot: return "Pinning Shot";
                case ActionKind.ShieldBash: return "Shield Bash";
                case ActionKind.Vanish: return "Vanish";
                case ActionKind.Blink: return "Blink";
                case ActionKind.Slam: return "Slam";
                case ActionKind.Boulder: return "Boulder";
                case ActionKind.Bomb: return "Bomb";
                case ActionKind.Raise: return "Raise";
                case ActionKind.Howl: return "Howl";
                case ActionKind.Fling: return "Fling";
                case ActionKind.Overwatch: return "Overwatch";
                case ActionKind.Caltrops: return "Caltrops";
                case ActionKind.BullRush: return "Bull Rush";
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
                case ActionKind.PinningShot: return $"{Battle.PinDamage} damage to an enemy in shot range (needs line of sight), and it can't move on its next turn";
                case ActionKind.ShieldBash: return $"push an enemy next to it 1 tile away; no damage, but {Battle.CollisionDamage} if it slams into a rock, unit or the edge, and the unit it slams into takes {Battle.CollisionDamage} too";
                case ActionKind.Vanish: return $"go invisible: enemies can't target or attack it (area blasts still hit) until it attacks, for {Battle.VanishTurns} enemy turns at most; the attack that reveals it deals +{Battle.VanishBonus}";
                case ActionKind.Blink: return $"teleport to a free tile within {Battle.BlinkRange}, ignoring rocks and zone of control";
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

        // ---------- A unit's kit: passives and actions ----------

        /// <summary>Abilities that describe one of the unit's actions rather than a passive (listed with the actions).</summary>
        public static bool IsAction(Ability ability) =>
            ability == Ability.WolfBite || ability == Ability.Howl || ability == Ability.Fling || ability == Ability.Caltrops ||
            ability == Ability.Overwatch || ability == Ability.BullRush;

        /// <summary>The name of a unit type's basic attack, which is one of its actions.</summary>
        public static string AttackName(UnitStats s)
        {
            if (s.Has(Ability.WolfBite))
                return "Wolf Bite";
            if (s.AreaMelee)
                return "Smash";
            if (s.Has(Ability.LongReach))
                return "Long Strike";
            if (s.IsRanged)
                return s.Has(Ability.ArcaneBolt) || s.CanRaise ? "Bolt" : "Shot";
            return s.Kind == UnitKind.GoblinBomber ? "Stab" : "Strike";
        }

        /// <summary>What the basic attack does, for a given damage (the unit's current damage on the HUD).</summary>
        public static string DescribeAttack(UnitStats s, int damage)
        {
            if (s.Has(Ability.WolfBite))
                return $"{damage} damage to a unit next to it, +1 for every wolf bite earlier this turn";
            if (s.AreaMelee)
                return $"{damage} damage to every unit next to it";
            if (s.Has(Ability.LongReach))
                return $"{damage} damage up to {s.Range} tiles in a straight line, hitting every tile on the way";
            if (s.IsRanged)
                return $"{damage} damage up to {s.Range} tiles away, " + (s.Has(Ability.ArcaneBolt) ? "over obstacles" : "needs line of sight");
            return $"{damage} damage to a unit next to it";
        }

        /// <summary>
        /// Every action a unit type can spend AP on: its attack first (no cooldown), then special actions, each with
        /// the action it is for its cooldown (null for the attack).
        /// </summary>
        public static List<(string name, int cost, ActionKind? kind, string text)> ActionsOf(UnitStats s, int damage)
        {
            var list = new List<(string, int, ActionKind?, string)>();
            if (s.Damage > 0)
                list.Add((AttackName(s), Battle.AttackCost, null, DescribeAttack(s, damage)));
            foreach (var action in s.Actions)
                list.Add((Name(action), Battle.ActionCost(action), action, Describe(action)));
            if (s.Slams)
                list.Add(("Slam", Battle.AttackCost, ActionKind.Slam,
                    $"{Battle.SlamDamage} damage to each of your units within 2 tiles, then shoves everyone within 2 tiles 1 tile away"));
            if (s.CanThrow)
                list.Add(("Boulder", s.ThrowCost, ActionKind.Boulder, $"{s.ThrowDamage} damage to a unit 2-{s.ThrowRange} tiles away, over obstacles; the rock stays"));
            if (s.CanBomb)
                list.Add(("Bomb", s.BombCost, ActionKind.Bomb, $"lands on a unit 2-{s.BombRange} tiles away and explodes on that tile for {s.BombDamage} at the start of the next enemy turn"));
            if (s.CanRaise)
                list.Add(("Raise", s.RaiseCost, ActionKind.Raise, $"brings back every bone pile within {s.RaiseRange} tiles at full HP"));
            foreach (var (ability, kind) in new[]
                     {
                         (Ability.Fling, ActionKind.Fling), (Ability.Howl, ActionKind.Howl), (Ability.Caltrops, ActionKind.Caltrops),
                         (Ability.Overwatch, ActionKind.Overwatch), (Ability.BullRush, ActionKind.BullRush),
                     })
                if (s.Has(ability))
                    list.Add((Name(ability), Battle.AttackCost, kind, Describe(ability)));
            return list;
        }

        /// <summary>A unit type's passives: its abilities that aren't actions, plus traits from its stats.</summary>
        public static List<(string name, string text)> PassivesOf(UnitStats s)
        {
            var list = s.Abilities.Where(a => !IsAction(a)).Select(a => (Name(a), Describe(a))).ToList();
            if (s.Size > 1)
                list.Add(("Huge", $"takes up {s.Size}x{s.Size} tiles"));
            if (s.SplitsInto != null)
                list.Add(("Split", $"when it dies it splits into 2 {s.SplitsInto.Name}s ({s.SplitsInto.MaxHp} HP each)"));
            if (s.StealsGold > 0)
                list.Add(("Thief", $"its first hit steals {s.StealsGold} gold, then it runs for the far edge; kill it to get the gold back"));
            if (s.LeavesRemains)
                list.Add(("Bone Pile", "leaves a bone pile when killed; a Necromancer can raise it unless someone stands on it"));
            return list;
        }
    }
}
