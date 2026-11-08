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

        public static readonly Equipment VampireFang = new Equipment("Vampire Fang", 14, Ability.VampireFang);
        public static readonly Equipment ThornedMail = new Equipment("Thorned Mail", 12, Ability.ThornedMail);
        public static readonly Equipment SpringBoots = new Equipment("Spring Boots", 11, Ability.SpringBoots);
        public static readonly Equipment LuckyCharm = new Equipment("Lucky Charm", 15, Ability.LuckyCharm);

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
                case Ability.LongReach: return "Long Reach";
                case Ability.DoubleStrike: return "Double Strike";
                case Ability.Guard: return "Guard";
                case Ability.Vanguard: return "Vanguard";
                case Ability.ZocStrike: return "Placeholder";
                case Ability.Tough: return "Tough";
                case Ability.SteadyAim: return "Steady Aim";
                case Ability.SteadyAimPlus: return "Steady Aim+";
                case Ability.Pierce: return "Pierce";
                case Ability.FirstShotDouble: return "Opening Shot";
                case Ability.PreyOnSnared: return "Prey";
                case Ability.Volley: return "Volley";
                case Ability.LongShot: return "Long Shot";
                case Ability.Vantage: return "Vantage";
                case Ability.SpikedSnares: return "Spiked Snares";
                case Ability.CheapShot: return "Cheap Shot";
                case Ability.Elusive: return "Elusive";
                case Ability.IntoShadows: return "Into the Shadows";
                case Ability.DeathMark: return "Death Mark";
                case Ability.Isolate: return "Isolate";
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
                case Ability.SearingBolt: return "Searing Bolt";
                case Ability.Twincast: return "Twincast";
                case Ability.QuickFuse: return "Quick Fuse";
                case Ability.Incendiary: return "Incendiary";
                case Ability.SpikedBarricades: return "Spiked Barricades";
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
                case Ability.Upheaval: return "Upheaval";
                case Ability.Grasp: return "Grasp";
                case Ability.Colossus: return "Colossus";
                case Ability.Wrath: return "Wrath";
                case Ability.SoulHarvest: return "Soul Harvest";
                case Ability.CorpseBlast: return "Corpse Blast";
                case Ability.StoneHeart: return "Stone Heart";
                case Ability.Hellborn: return "Hellborn";
                case Ability.BloodOffering: return "Blood Offering";
                case Ability.Hellswap: return "Hellswap";
                case Ability.Bloodlust: return "Bloodlust";
                case Ability.RendingBlow: return "Rending Blow";
                case Ability.SoulContract: return "Soul Contract";
                case Ability.Spines: return "Spines";
                case Ability.FireBreath: return "Fire Breath";
                default: return "";
            }
        }

        public static string Describe(Ability ability)
        {
            switch (ability)
            {
                case Ability.ShieldWall: return "allies next to it take 1 less damage";
                case Ability.LongReach: return "melee reach 2 in a straight line, hitting both tiles";
                case Ability.DoubleStrike: return "every attack strikes twice";
                case Ability.Guard: return "takes the hit when an adjacent ally is attacked";
                case Ability.Vanguard: return "can be deployed 1 row further forward";
                case Ability.ZocStrike: return "attacks any unit that steps out of its reach, at most once a round";
                case Ability.Tough: return "takes 1 less damage from everything";
                case Ability.SteadyAim: return "+1 damage if it hasn't moved this turn";
                case Ability.SteadyAimPlus: return "+2 damage if it hasn't moved this turn";
                case Ability.Pierce: return "shots along a straight row or column hit every enemy in the line";
                case Ability.FirstShotDouble: return "double damage the first time it hits each enemy in a battle";
                case Ability.PreyOnSnared: return $"pinned enemies take +{Battle.PreyBonus} damage from everyone";
                case Ability.Volley: return "shots hit the target and the 4 tiles next to it for 1 less damage, friend or foe";
                case Ability.LongShot: return $"+{Battle.LongShotBonus} damage to targets {Battle.LongShotRange} or more tiles away";
                case Ability.Vantage: return $"while next to a rock, ice or barricade: +{Battle.VantageRange} range, and its shots ignore Behind Cover";
                case Ability.SpikedSnares: return $"its snares deal {Battle.SpikedSnareDamage} damage instead of {Battle.SnareDamage}";
                case Ability.CheapShot: return $"+{Battle.CheapShotBonus} damage to enemies that can't move (pinned or slowed)";
                case Ability.Elusive: return "ignores zone of control: leaving an enemy's reach doesn't use up its moves";
                case Ability.DeathMark: return "its marks spread: when a marked enemy dies, every enemy next to it is marked";
                case Ability.Isolate: return $"+{Battle.IsolateBonus} damage to exposed enemies (no ally of theirs next to them)";
                case Ability.IntoShadows: return $"a kill hides it until your next turn: enemies can't target or attack it (area blasts still hit). At most once every {Battle.IntoShadowsCooldown} turns";
                case Ability.Pincer: return $"+{Battle.PincerBonus} damage if an ally stands on the far side of the target";
                case Ability.KillRefund: return "a kill gives its action back and 1 move: it can attack again or walk on";
                case Ability.Poison: return $"hits poison: {Battle.PoisonDamage} damage at the start of the target's next {Battle.PoisonTurns} turns";
                case Ability.Counter: return "counter-attacks the first melee hit each enemy turn";
                case Ability.CounterAll: return "counter-attacks every melee hit";
                case Ability.Dodge: return "the first hit each enemy turn deals 0 damage";
                case Ability.ArcaneBolt: return "its bolt ignores line of sight";
                case Ability.Burning: return $"fireballs leave the area burning for {Battle.BurnRounds} rounds ({Battle.BurnDamage} damage at the start of each turn there; enemies won't stop on burning ground)";
                case Ability.Concussive: return "fireballs push units outward and set off mines and bombs";
                case Ability.Chill: return "its hits slow: the target can't move on its next turn";
                case Ability.Shatter: return $"slowed enemies take +{Battle.ShatterBonus} damage from every attack of your units";
                case Ability.WideWard: return "Ward also shields every ally next to the target";
                case Ability.Mines: return "an enemy stepping on one of its mines sets it off: it hurts whoever stands on that tile";
                case Ability.BigMines: return $"its mines deal {Battle.BigMineDamage} damage and it can have {Battle.BigMaxMines} out";
                case Ability.BlastProof: return "its blasts don't hurt your units";
                case Ability.SearingBolt: return $"its bolt sets the target's tile burning for {Battle.BurnRounds} rounds ({Battle.BurnDamage} damage at the start of each turn there; enemies won't stop on burning ground)";
                case Ability.Twincast: return "the first action it uses each turn costs no AP, so it can still attack or cast another (it can't move afterwards)";
                case Ability.QuickFuse: return "Place Mine is free and ready every turn: use it before acting, then move and attack as usual";
                case Ability.Incendiary: return $"its mines, bombs and blasts leave the ground they hit burning for {Battle.BurnRounds} rounds";
                case Ability.SpikedBarricades: return $"an enemy starting its turn next to one of its barricades takes {Battle.SpikeDamage}";
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
                case Ability.Upheaval: return $"the ground bursts up under one of your units within {Battle.UpheavalRange} tiles (no line of sight needed): {Battle.UpheavalDamage} damage, and it is thrown 1 tile away from the caster ({Battle.CollisionDamage} more if blocked)";
                case Ability.Grasp: return "grabs a unit next to it: no damage, but it can't move on its next turn";
                case Ability.Colossus: return "can't be pushed, pinned, slowed or grasped";
                case Ability.Wrath: return "from its second phase it gets 2 actions a turn instead of 1 (each still has its cooldown)";
                case Ability.SoulHarvest: return "while it is on the board your units that die leave a bone pile; raising one brings it back on its side as a Bone version with only its basic attack";
                case Ability.CorpseBlast: return $"a bone pile within its raise range explodes for {Battle.CorpseBlastDamage} damage in the 3x3 around it, friend or foe; the pile is used up";
                case Ability.StoneHeart: return $"its HP falls in {Battle.BossPhases} phases, and it loses at most one a round: damage past the next phase is lost until your next turn. Each new phase changes how it fights (Giant: 2 actions a turn, then falling rocks on your units' tiles each round; Necromancer: {Battle.PhaseSkeletons} skeletons climb onto the roof). Its max HP grows by {Battle.BossHpPerLevel} for every level in your army";
                case Ability.Hellborn: return "burning ground doesn't hurt it";
                case Ability.BloodOffering: return $"when it dies, the nearest other demon within {Battle.OfferingRange} tiles gains {Battle.OfferingFury} Fury (+1 damage per Fury for the rest of the fight)";
                case Ability.Hellswap: return $"trades places with one of your units 2-{Battle.HellswapRange} tiles away (no line of sight needed); nothing on either tile goes off";
                case Ability.Bloodlust: return $"gains {Battle.BloodlustFury} Fury (+1 damage for the rest of the fight) whenever a unit next to it dies, friend or foe";
                case Ability.RendingBlow: return $"loses {Battle.RendingBlowCost} of its own HP, then hits a unit next to it for its damage +{Battle.RendingBlowBonus}";
                case Ability.SoulContract: return $"marks one of your units within {Battle.ContractRange} tiles (no line of sight needed): at the start of the next enemy turn it loses {Battle.ContractDamage} HP, unless the Devil takes damage before then";
                case Ability.Spines: return $"melee attackers take {Battle.ThornsDamage} damage";
                case Ability.FireBreath: return $"breathes down a straight line of {Battle.FireBreathLength} tiles at one of your units on it (a rock stops it): {Battle.FireBreathDamage} damage to each of your units there, and the tiles burn for {Battle.BurnRounds} rounds";
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
                case ActionKind.Upheaval: return "Upheaval";
                case ActionKind.Grasp: return "Grasp";
                case ActionKind.CorpseBlast: return "Corpse Blast";
                case ActionKind.Hellswap: return "Hellswap";
                case ActionKind.RendingBlow: return "Rending Blow";
                case ActionKind.SoulContract: return "Soul Contract";
                case ActionKind.FireBreath: return "Fire Breath";
                case ActionKind.Fireball: return "Fireball";
                case ActionKind.IceWall: return "Ice Wall";
                case ActionKind.Ward: return "Ward";
                case ActionKind.Mend: return "Mend";
                case ActionKind.Barricade: return "Barricade";
                case ActionKind.Turret: return "Build Turret";
                case ActionKind.Detonate: return "Detonate";
                case ActionKind.Charge: return "Charge";
                case ActionKind.Sweep: return "Sweep";
                case ActionKind.ShieldUp: return "Shield Up";
                case ActionKind.Battlecry: return "Battlecry";
                case ActionKind.Taunt: return "Taunt";
                case ActionKind.ThrowShield: return "Throw Shield";
                case ActionKind.Kick: return "Kick";
                case ActionKind.SnareTrap: return "Snare Trap";
                case ActionKind.FireArrow: return "Fire Arrow";
                case ActionKind.CoveringFire: return "Covering Fire";
                case ActionKind.SuppressingFire: return "Suppressing Fire";
                case ActionKind.ThrowingKnife: return "Throwing Knife";
                case ActionKind.Shadowstep: return "Shadowstep";
                case ActionKind.SmokeBomb: return "Smoke Bomb";
                case ActionKind.Rupture: return "Rupture";
                case ActionKind.MarkTarget: return "Mark Target";
                case ActionKind.FrostNova: return "Frost Nova";
                case ActionKind.Firewall: return "Firewall";
                case ActionKind.Blizzard: return "Blizzard";
                case ActionKind.Transpose: return "Transpose";
                case ActionKind.CaveIn: return "Cave-In";
                case ActionKind.Hook: return "Grappling Hook";
                case ActionKind.Demolish: return "Demolish";
                default: return action.ToString();
            }
        }

        public static string Describe(ActionKind action)
        {
            switch (action)
            {
                case ActionKind.PlaceMine:
                    return $"put a mine on a free tile within {Battle.MineRange} (max {Battle.MaxMinesPerUnit}); an enemy stepping on it sets it off for {Battle.MineDamage} damage to whoever stands on that tile";
                case ActionKind.ThrowBomb:
                    return $"lob a bomb 2-{Battle.PlayerBombRange} tiles away; it explodes for {Battle.PlayerBombDamage} on that tile at the start of the next enemy turn";
                case ActionKind.PinningShot: return $"{Battle.PinDamage} damage to an enemy in shot range (needs line of sight), and it can't move on its next turn";
                case ActionKind.ShieldBash: return $"{Battle.BashDamage} damage to an enemy next to it and push it 1 tile; {Battle.CollisionDamage} more if it slams into a rock, unit or the edge, and the unit it slams into takes {Battle.CollisionDamage} too";
                case ActionKind.Vanish: return $"go invisible until your next turn: enemies can't target or attack it (area blasts still hit). Attacking doesn't reveal it, and its first attack while hidden deals +{Battle.VanishBonus}. Free, before it acts: it can still move and attack";
                case ActionKind.Kick: return $"{Battle.KickDamage} damage to an enemy next to it and push it 1 tile ({Battle.CollisionDamage} more if it slams into something)";
                case ActionKind.SnareTrap: return $"hide a snare on a free tile within {Battle.SnareRange} (max {Battle.MaxSnares}); the first enemy to walk onto it stops, takes {Battle.SnareDamage} and is pinned: it can't move on its next turn. Enemies don't see snares";
                case ActionKind.FireArrow: return $"shoot an enemy in shot range for its attack damage; the ground under it burns for {Battle.BurnRounds} rounds ({Battle.BurnDamage} damage at the start of each turn)";
                case ActionKind.CoveringFire: return "aim instead of shooting: during the enemy turn, the first enemy that walks onto a tile it can shoot at is shot for its attack damage and stops there";
                case ActionKind.SuppressingFire: return $"shoot along a row or column up to its range, stopped by rocks: every enemy on the line takes {Battle.PinDamage} and is pinned (can't move on its next turn)";
                case ActionKind.ThrowingKnife: return $"{Battle.KnifeDamage} damage (plus its damage bonuses) to an enemy 2-{Battle.KnifeRange} tiles away, needs line of sight. Carries Poison, Cheap Shot and the Vanish bonus";
                case ActionKind.Shadowstep: return $"teleport to a free tile next to an enemy within {Battle.ShadowstepRange}, ignoring rocks and zone of control. Free, before it acts, but it can't walk afterwards: it can still attack";
                case ActionKind.SmokeBomb: return $"smoke itself and one ally within {Battle.SmokeRange} tiles: both are hidden until your next turn, as with Vanish but without its +{Battle.VanishBonus}";
                case ActionKind.MarkTarget: return $"mark an enemy in shot range: the next hit on it, by anyone and from anything but poison or fire, deals +{Battle.MarkBonus}, then the mark is gone. Free, before it acts: it can still move and attack";
                case ActionKind.Rupture: return $"strike a poisoned enemy next to it: first its poison bursts, {Battle.PoisonDamage} damage for every turn of poison left, then the strike (which poisons again)";
                case ActionKind.Blink: return $"teleport to a free tile within {Battle.BlinkRange}, ignoring rocks and zone of control. Free, before it acts: it can still move and attack";
                case ActionKind.Fireball: return $"{Battle.FireballDamage} damage to everyone on the tile and the 4 next to it, up to {Battle.FireballRange} tiles away";
                case ActionKind.IceWall: return $"turn a free tile within {Battle.ActionRange} into ice (blocks like a rock) for {Battle.IceRounds} rounds";
                case ActionKind.Ward: return $"an ally within {Battle.ActionRange} tiles gets a shield that absorbs {Battle.WardShield} damage";
                case ActionKind.Mend: return $"heal an ally within {Battle.ActionRange} tiles for {Battle.MendHeal}";
                case ActionKind.Barricade: return $"build a barricade on a free tile next to it (max {Battle.MaxBarricades}): blocks paths and shots, {Battle.BarricadeHp} HP, either side can break it, and a broken one collapses into rubble";
                case ActionKind.Turret: return "build a turret next to it (one at a time); it acts on your turn but can't move";
                case ActionKind.Detonate: return $"blow up all its barricades: {Battle.BarricadeBlastDamage} damage in a 3x3 around each, and each leaves rubble";
                case ActionKind.Charge: return $"rush up to {Battle.ChargeRange} tiles in a straight line, past allies: {Battle.ChargeDamage} damage to every enemy on the way, each pushed to a random side. One that doesn't move stops the charge";
                case ActionKind.Sweep: return "hit an enemy next to it and the enemies on both sides of it";
                case ActionKind.ShieldUp: return $"takes {Battle.ShieldUpReduction} less damage from every source (at least 1) for {Battle.ShieldUpTurns} enemy turns";
                case ActionKind.Battlecry: return $"every ally within {Battle.BattlecryRange} tiles, itself included, gets +{Battle.BattlecryMoves} move and {Battle.BattlecryHp} temporary HP until your next turn";
                case ActionKind.Taunt: return $"until your next turn, enemies within {Battle.TauntRange} tiles can only attack or use offensive abilities on this unit";
                case ActionKind.ThrowShield: return $"for the rest of the battle: +{Battle.ThrowShieldMoves} move and +{Battle.ThrowShieldDamage} damage, for {Battle.ThrowShieldHpCost} HP. Once per battle. Free, before it acts: it can still move and attack";
                case ActionKind.FrostNova: return "every enemy next to it is slowed (can't move on its next turn) and pushed 1 tile away";
                case ActionKind.Firewall: return $"set 3 tiles in a line across its aim, up to {Battle.FirewallRange} tiles away, burning for {Battle.BurnRounds} rounds; whoever stands there takes {Battle.FirewallDamage} now. Enemies won't stop on burning ground";
                case ActionKind.Blizzard: return $"{Battle.BlizzardDamage} damage to every enemy on a tile up to {Battle.BlizzardRange} tiles away and the 4 next to it, and they are slowed (can't move on their next turn). Spares your units";
                case ActionKind.Transpose: return $"swap places with any unit within {Battle.TransposeRange} tiles, friend or foe (not the Giant or objectives). An enemy put down on a mine sets it off";
                case ActionKind.CaveIn: return $"drop rubble on a free tile within {Battle.CaveInRange}: it blocks walking but not shots, and a unit pushed into it takes {Battle.CrushDamage} more and breaks it";
                case ActionKind.Hook: return $"pull an enemy 2-{Battle.HookRange} tiles away in a straight line (in sight) to the tile next to it; mines on the way go off, and it slams into whatever blocks it (not huge units)";
                case ActionKind.Demolish: return $"blow up a rock, rubble, barricade or ice wall within {Battle.DemolishRange}: {Battle.DemolishDamage} damage to everyone in the 3x3 around it. A rock or barricade collapses into rubble";
                default: return "";
            }
        }

        // ---------- A unit's kit: passives and actions ----------

        /// <summary>Abilities that describe one of the unit's actions rather than a passive (listed with the actions).</summary>
        public static bool IsAction(Ability ability) =>
            ability == Ability.WolfBite || ability == Ability.Howl || ability == Ability.Fling || ability == Ability.Caltrops ||
            ability == Ability.Overwatch || ability == Ability.BullRush || ability == Ability.Upheaval || ability == Ability.Grasp ||
            ability == Ability.CorpseBlast || ability == Ability.Hellswap || ability == Ability.RendingBlow || ability == Ability.SoulContract ||
            ability == Ability.FireBreath;

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
                         (Ability.Upheaval, ActionKind.Upheaval), (Ability.Grasp, ActionKind.Grasp), (Ability.CorpseBlast, ActionKind.CorpseBlast),
                         (Ability.Hellswap, ActionKind.Hellswap), (Ability.RendingBlow, ActionKind.RendingBlow),
                         (Ability.SoulContract, ActionKind.SoulContract), (Ability.FireBreath, ActionKind.FireBreath),
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
