using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    public enum Team
    {
        Player,
        Enemy,
    }

    /// <summary>The look of a unit: its statue family.</summary>
    public enum UnitKind
    {
        Warrior,
        Archer,
        Rogue,
        Mage,
        Sapper,
        Turret,
        Wolf,
        PackLeader,
        Skeleton,
        Necromancer,
        Ogre,
        Goblin,
        GoblinBomber,
        Cutpurse,
        Crossbowman,
        Brute,
        Elemental,
        Shard,
        Shrine,
        Totem,

        // Demons
        Imp,
        Demon,
        Devil,
        Fiend,
    }

    /// <summary>How far pushes move a unit, relative to the push's distance.</summary>
    public enum Weight
    {
        /// <summary>Pushed 1 tile further.</summary>
        Small,
        Medium,

        /// <summary>Pushed 1 tile less (a normal push doesn't move it).</summary>
        Huge,
    }

    /// <summary>A passive rule a unit has, from its type or from an item.</summary>
    public enum Ability
    {
        None,

        // ---------- Warrior line ----------

        /// <summary>Allies standing next to this unit take 1 less damage (minimum 1).</summary>
        ShieldWall,

        /// <summary>Melee reach 2 in a straight line; the attack hits both tiles.</summary>
        LongReach,

        /// <summary>Every attack strikes twice.</summary>
        DoubleStrike,

        /// <summary>When an adjacent ally is attacked, this unit takes the hit instead.</summary>
        Guard,

        /// <summary>Can be deployed one row further forward.</summary>
        Vanguard,

        /// <summary>Attacks an enemy that steps out of its reach, at most once a round.</summary>
        ZocStrike,

        /// <summary>Takes 1 less damage from everything (minimum 1).</summary>
        Tough,

        // ---------- Archer line ----------

        /// <summary>+1 damage if the unit hasn't moved this turn.</summary>
        SteadyAim,

        /// <summary>+2 damage if the unit hasn't moved this turn.</summary>
        SteadyAimPlus,

        /// <summary>Shots along a straight row or column hit every enemy in the line.</summary>
        Pierce,

        /// <summary>Double damage the first time it hits each enemy in a battle.</summary>
        FirstShotDouble,

        /// <summary>Pinned enemies take +2 damage from everyone.</summary>
        PreyOnSnared,

        /// <summary>Shots hit the target and the 4 tiles next to it for 1 less damage, friend or foe.</summary>
        Volley,

        /// <summary>+1 damage against targets 3 or more tiles away.</summary>
        LongShot,

        /// <summary>While next to a rock, ice or barricade: +1 range, and its shots ignore Behind Cover.</summary>
        Vantage,

        /// <summary>Its snares deal 3 damage instead of 1.</summary>
        SpikedSnares,

        /// <summary>A marked enemy that dies passes its mark to every enemy next to it.</summary>
        DeathMark,

        // ---------- Rogue line ----------

        /// <summary>+3 damage if an ally stands on the far side of the target.</summary>
        Pincer,

        /// <summary>A kill gives its action back (refunds the AP).</summary>
        KillRefund,

        /// <summary>Hits poison the target: 2 damage at the start of its next 2 turns.</summary>
        Poison,

        /// <summary>Counter-attacks the first melee hit each enemy turn.</summary>
        Counter,

        /// <summary>Counter-attacks every melee hit.</summary>
        CounterAll,

        /// <summary>The first hit each enemy turn deals 0 damage.</summary>
        Dodge,

        /// <summary>+2 damage against enemies that can't move (pinned or slowed).</summary>
        CheapShot,

        /// <summary>Ignores zone of control: leaving an enemy's reach doesn't use up its moves (Rogue technique, and goblins).</summary>
        Elusive,

        /// <summary>A kill hides it until your next turn (as Vanish, without the ambush bonus).</summary>
        IntoShadows,

        /// <summary>+2 damage against exposed enemies (no ally of theirs next to them).</summary>
        Isolate,

        // ---------- Mage line ----------

        /// <summary>Its bolt ignores line of sight.</summary>
        ArcaneBolt,

        /// <summary>Fireballs leave the area burning for 2 rounds (1 damage at the start of each turn).</summary>
        Burning,

        /// <summary>Fireballs push units outward and set off mines and bombs.</summary>
        Concussive,

        /// <summary>Its bolt slows: the target can't move on its next turn.</summary>
        Chill,

        /// <summary>Double damage to slowed enemies.</summary>
        Shatter,

        /// <summary>Ward also shields every ally next to the target.</summary>
        WideWard,

        /// <summary>Its bolt sets the target's tile burning.</summary>
        SearingBolt,

        /// <summary>The first special action it uses each turn costs no AP (it still can't move afterwards).</summary>
        Twincast,

        // ---------- Sapper line ----------

        /// <summary>Can place mines (see the Place Mine action).</summary>
        Mines,

        /// <summary>Its mines deal 5 damage and it can have 4 on the board.</summary>
        BigMines,

        /// <summary>Its own blasts don't hurt its side.</summary>
        BlastProof,

        /// <summary>Place Mine is free (before acting) and ready every turn.</summary>
        QuickFuse,

        /// <summary>Its mines, bombs and blasts leave their area burning.</summary>
        Incendiary,

        /// <summary>Enemies starting their turn next to one of its barricades take damage.</summary>
        SpikedBarricades,

        // ---------- Items ----------

        /// <summary>Heals 2 HP on every kill.</summary>
        VampireFang,

        /// <summary>Thorns: a unit that hits this one in melee takes 1 damage (the Thorned Mail item, and the Fiend).</summary>
        Thorns,

        /// <summary>+1 move each turn.</summary>
        SpringBoots,

        /// <summary>Once per battle, a hit that would kill leaves the unit at 1 HP instead.</summary>
        LuckyCharm,

        // ---------- Summons ----------

        /// <summary>Can't move.</summary>
        Immobile,

        // ---------- Movement ----------

        /// <summary>2 moves per turn instead of 1.</summary>
        Swift,

        // ---------- Wolves ----------

        /// <summary>Goes for the lowest-HP unit it can reach and attack this turn, instead of the nearest.</summary>
        BloodScent,

        /// <summary>Its attack is a Wolf Bite: +1 damage for every wolf bite earlier in the same turn.</summary>
        WolfBite,

        /// <summary>While it lives, every wolf on its side has +1 max HP; once the last one dies, they have -1 and lose 2 HP.</summary>
        PackBond,

        /// <summary>Its action can be a howl: every wolf that hasn't acted yet gets +1 move this turn.</summary>
        Howl,

        // ---------- Goblins ----------

        /// <summary>Its action can be a fling: it throws an adjacent goblin, bomb or mine at one of your units 2-3 tiles away.</summary>
        Fling,

        /// <summary>When it dies it explodes: damage to everyone around it, its own side included.</summary>
        PowderKeg,

        // ---------- Bandits ----------

        /// <summary>The only unit that may still move after attacking.</summary>
        LightFingers,

        /// <summary>Its action can be Caltrops: a unit of yours stepping on them takes 1 and its movement ends.</summary>
        Caltrops,

        /// <summary>While next to a rock it can't be hit by ranged attacks (melee, bombs and blasts still reach it).</summary>
        BehindCover,

        /// <summary>Its action can be Overwatch: the first of your units that walks into its sight next turn gets shot.</summary>
        Overwatch,

        /// <summary>Its action can be Bull Rush: a hit that also pushes the target 1 tile.</summary>
        BullRush,

        // ---------- Elementals and skeletons ----------

        /// <summary>Its action can be Upheaval: the ground bursts up under a unit within 2 tiles, hurting it and pushing it off.</summary>
        Upheaval,

        /// <summary>Its action can be Grasp: an adjacent unit is pinned.</summary>
        Grasp,

        // ---------- Bosses ----------

        /// <summary>Can't be pushed, pinned, slowed or grasped.</summary>
        Colossus,

        /// <summary>At half HP or less it gets one more AP each turn (two actions).</summary>
        Wrath,

        /// <summary>While it is on the board, your units that die leave a bone pile it can raise as one of its own.</summary>
        SoulHarvest,

        /// <summary>Its action can be Corpse Blast: a bone pile in range explodes in a 3x3.</summary>
        CorpseBlast,

        /// <summary>Its HP falls in phases: it can lose at most one phase a round, and each new phase changes how it fights.</summary>
        StoneHeart,
        // ---------- Demons ----------

        /// <summary>Burning ground doesn't hurt it.</summary>
        Hellborn,

        /// <summary>When it dies, the nearest other demon within a few tiles gains Fury (+1 damage each, for the fight).</summary>
        BloodOffering,

        /// <summary>Its action can be Hellswap: it trades places with one of your units within 3 tiles.</summary>
        Hellswap,

        /// <summary>Gains 1 Fury whenever a unit next to it dies, friend or foe.</summary>
        Bloodlust,

        /// <summary>Its action can be Rending Blow: it pays some of its own HP for a much harder hit.</summary>
        RendingBlow,

        /// <summary>Its action can be Soul Contract: one of your units loses HP next enemy turn unless the Devil is hit first.</summary>
        SoulContract,

        /// <summary>Its action can be Fire Breath: 2 tiles in a straight line take damage and burn.</summary>
        FireBreath,
    }

    /// <summary>A special action a unit can spend AP on besides its basic attack. Every one has a cooldown.</summary>
    public enum ActionKind
    {
        PlaceMine,
        ThrowBomb,
        PinningShot,
        Fireball,
        IceWall,
        Ward,
        Mend,
        Barricade,
        Turret,
        Detonate,
        Charge,
        ShieldBash,
        Vanish,
        Blink,
        Sweep,
        ShieldUp,
        Battlecry,
        Taunt,
        ThrowShield,

        // Archer/Rogue night pool
        Kick,
        SnareTrap,
        FireArrow,
        /// <summary>The Archer's Overwatch, the same rule as the Crossbowman's (kept as its own action in code; shown as "Overwatch").</summary>
        CoveringFire,
        SuppressingFire,
        ThrowingKnife,
        Shadowstep,
        SmokeBomb,
        Rupture,
        MarkTarget,
        // Mage/Sapper night pool
        FrostNova,
        Firewall,
        Blizzard,
        Transpose,
        CaveIn,
        Hook,
        Demolish,

        // Enemy actions: never in a unit's Actions list (the AI uses them), but they share the cooldown table.
        Slam,
        Boulder,
        Bomb,
        Raise,
        Howl,
        Fling,
        Overwatch,
        Caltrops,
        BullRush,
        Upheaval,
        Grasp,
        CorpseBlast,

        // Demons
        Hellswap,
        RendingBlow,
        SoulContract,
        FireBreath,
    }

    /// <summary>
    /// Base numbers and rules for a unit type. All values are placeholders from the design note.
    /// The player's five types grow during a run by learning techniques (see <see cref="Technique"/>).
    /// </summary>
    public sealed class UnitStats
    {
        public readonly UnitKind Kind;
        public readonly string Name;
        public readonly int MaxHp;
        public readonly int Damage;
        public readonly int Range;
        public readonly int MaxAp;

        /// <summary>1 for the player's units, 0 for enemies and summons (evolution tiers are gone).</summary>
        public int Tier { get; private set; }

        /// <summary>Short text drawn on the statue.</summary>
        public string Label { get; private set; }

        /// <summary>One-line flavour of how the unit plays.</summary>
        public string Role { get; private set; } = "";

        public IReadOnlyList<Ability> Abilities { get; private set; } = new Ability[0];
        public IReadOnlyList<ActionKind> Actions { get; private set; } = new ActionKind[0];

        /// <summary>Side length in tiles. 1 for normal units, 2 for a 2x2 unit like the ogre.</summary>
        public int Size { get; private set; } = 1;

        /// <summary>How far pushes move it: small units 1 tile further, huge ones 1 tile less.</summary>
        public Weight Weight { get; private set; } = Weight.Medium;

        /// <summary>Leaves a bone pile when killed, which a necromancer can raise.</summary>
        public bool LeavesRemains { get; private set; }

        /// <summary>The basic attack hits every enemy next to the unit, not just one.</summary>
        public bool AreaMelee { get; private set; }

        /// <summary>The act's boss itself, not its minions: Fortune's Favored never picks it.</summary>
        public bool IsBoss { get; private set; }

        /// <summary>A battle-only unit (turret): doesn't count for survival or defeat.</summary>
        public bool IsSummon { get; private set; }

        /// <summary>An objective the player defends (the Moon Shrine): can't act, and the fight is lost if it breaks.</summary>
        public bool IsObjective { get; private set; }

        /// <summary>When it dies it splits into two of these. Null = no split.</summary>
        public UnitStats SplitsInto { get; private set; }

        /// <summary>Gold stolen on a hit (then it flees). 0 = doesn't steal.</summary>
        public int StealsGold { get; private set; }

        /// <summary>Slam: shoves every unit within 2 tiles 1 tile away.</summary>
        public bool Slams { get; private set; }

        /// <summary>A raised version of another enemy (necromancer boss army).</summary>
        public bool IsUndead { get; private set; }

        /// <summary>Raising a bone pile: AP cost and range in steps. 0 = can't raise.</summary>
        public int RaiseCost { get; private set; }

        public int RaiseRange { get; private set; }

        /// <summary>Boulder throw: AP cost, damage and range in steps. 0 = can't throw. Ignores line of sight.</summary>
        public int ThrowCost { get; private set; }

        public int ThrowDamage { get; private set; }

        public int ThrowRange { get; private set; }

        /// <summary>Delayed bomb (enemy AI): AP cost, blast damage and range in steps (minimum 2). 0 = can't.</summary>
        public int BombCost { get; private set; }

        public int BombDamage { get; private set; }

        public int BombRange { get; private set; }

        public UnitStats(UnitKind kind, string name, int maxHp, int damage, int range, int maxAp)
        {
            Kind = kind;
            Name = name;
            MaxHp = maxHp;
            Damage = damage;
            Range = range;
            MaxAp = maxAp;
            Label = name.Substring(0, 1);
        }

        public bool Has(Ability ability) => Abilities.Contains(ability);

        /// <summary>Tiles per turn before items: 1, or 2 with Swift. 0 for units that can't move.</summary>
        public int Moves => Has(Ability.Immobile) ? 0 : Battle.BaseMoves + (Has(Ability.Swift) ? 1 : 0);

        public bool HasAction(ActionKind action) => Actions.Contains(action);

        /// <summary>A ranged attack needs line of sight and can't be countered. Long reach is still melee.</summary>
        public bool IsRanged => Range > 1 && !Has(Ability.LongReach);

        public bool CanRaise => RaiseCost > 0;

        public bool CanThrow => ThrowCost > 0;

        public bool CanBomb => BombCost > 0;

        public bool IsPlayerType => Tier > 0;

        private static UnitStats Player(UnitKind kind, string name, string label, int hp, int damage, int range, int ap,
            string role, Ability[] abilities, ActionKind[] actions, Weight weight = Weight.Medium) =>
            new UnitStats(kind, name, hp, damage, range, ap)
            {
                Tier = 1, Label = label, Role = role, Abilities = abilities, Actions = actions, Weight = weight,
            };

        private static Ability[] A(params Ability[] a) => a;

        private static ActionKind[] Do(params ActionKind[] a) => a;

        // ---------- The five base units (they grow by learning techniques, see Techniques.cs) ----------

        public static readonly UnitStats Warrior = Player(UnitKind.Warrior, "Warrior", "W", 12, 3, 1, 1,
            "Sturdy front line.", A(), Do(ActionKind.ShieldBash));

        public static readonly UnitStats Archer = Player(UnitKind.Archer, "Archer", "A", 6, 2, 3, 1,
            "Fragile ranged damage that likes to stand still.", A(Ability.SteadyAim), Do(ActionKind.PinningShot));

        public static readonly UnitStats Rogue = Player(UnitKind.Rogue, "Rogue", "R", 6, 4, 1, 1,
            "Fast, deadly, fragile. Slips out of sight.", A(Ability.Swift), Do(ActionKind.Vanish), Weight.Small);

        public static readonly UnitStats Apprentice = Player(UnitKind.Mage, "Mage Apprentice", "Ma", 5, 2, 3, 1,
            "Fragile spellcaster that controls space with fire and ice.", A(Ability.ArcaneBolt), Do(ActionKind.FrostNova), Weight.Small);

        public static readonly UnitStats Sapper = Player(UnitKind.Sapper, "Sapper", "Sa", 7, 2, 1, 1,
            "Engineer who reshapes the board: mines, rubble and walls.", A(Ability.Mines), Do(ActionKind.PlaceMine));

        /// <summary>Placed with the Sapper's Turret technique. Can't move; shoots like a small archer.</summary>
        public static readonly UnitStats Turret = new UnitStats(UnitKind.Turret, "Turret", 4, 2, 2, 1)
        {
            Label = "T", Role = "Stationary shooter.", Abilities = A(Ability.Immobile), IsSummon = true,
        };

        /// <summary>The Moon Shrine the player defends in a ritual fight. Sides with the player but never acts.</summary>
        public static readonly UnitStats MoonShrine = new UnitStats(UnitKind.Shrine, "Moon Shrine", 15, 0, 1, 0)
        {
            Label = "S", Role = "Hold it until the ritual completes. If it breaks, the fight is lost.",
            Abilities = A(Ability.Immobile), IsSummon = true, IsObjective = true,
        };

        /// <summary>
        /// A summoning totem in Break the Ritual. On the enemy side but never acts; smash all of them before the
        /// summoning completes. Its own side's blasts don't hurt it.
        /// </summary>
        public static readonly UnitStats Totem = new UnitStats(UnitKind.Totem, "Totem", 6, 0, 1, 0)
        {
            Label = "T", Role = "Smash all the totems before the summoning completes.",
            Abilities = A(Ability.Immobile), IsObjective = true,
        };

        /// <summary>The five unit types the player drafts and recruits.</summary>
        public static readonly IReadOnlyList<UnitStats> BaseUnits = new[] { Warrior, Archer, Rogue, Apprentice, Sapper };

        // ---------- Enemies ----------

        public static readonly UnitStats Wolf = new UnitStats(UnitKind.Wolf, "Wolf", 6, 2, 1, 1)
        {
            Weight = Weight.Small, Abilities = A(Ability.Swift, Ability.BloodScent, Ability.WolfBite),
            Role = "Fast biter that hunts the weakest. Bites hit harder the more wolves bit before it this turn.",
        };
        public static readonly UnitStats PackLeader = new UnitStats(UnitKind.PackLeader, "Pack Leader", 10, 3, 1, 1)
        {
            Weight = Weight.Huge, Abilities = A(Ability.Swift, Ability.PackBond, Ability.Howl, Ability.WolfBite),
            Role = "Keeps the pack strong while it lives, and howls to send it further.",
        };

        public static readonly UnitStats Skeleton =
            new UnitStats(UnitKind.Skeleton, "Skeleton", 4, 2, 1, 1)
            {
                Weight = Weight.Small, LeavesRemains = true, Abilities = A(Ability.Grasp),
                Role = "Grabs your units and pins them. Leaves a bone pile when killed; a Necromancer can raise it. Stand on the pile to stop that.",
            };

        /// <summary>
        /// Act 1 boss. Its army is raised from the factions beaten earlier in the act. Its basic attack
        /// is a bolt (range 3, needs line of sight).
        /// </summary>
        public static readonly UnitStats Necromancer =
            new UnitStats(UnitKind.Necromancer, "Necromancer", 14, 4, 3, 1)
            {
                IsBoss = true, RaiseCost = 1, RaiseRange = 4, Abilities = A(Ability.SoulHarvest, Ability.CorpseBlast, Ability.StoneHeart),
                Role = "Boss. Brings raised versions of the factions you beat this act, and raises your fallen against you. One action a turn: blows up a bone pile " +
                       "your units stand on or next to, raises every bone pile within 4 tiles at once, or fires a bolt. Keeps 2 tiles away, walks up to bolt range and shoots (needs line of sight).",
            };

        /// <summary>Act 1 boss (the ogre). Its basic attack is the club smash, which hits everyone adjacent.</summary>
        public static readonly UnitStats Ogre = new UnitStats(UnitKind.Ogre, "Giant", 42, 5, 1, 1)
        {
            IsBoss = true, Size = 2, Weight = Weight.Huge, AreaMelee = true, ThrowCost = 1, ThrowDamage = 4, ThrowRange = 4, Slams = true, Abilities = A(Ability.Colossus, Ability.Wrath, Ability.StoneHeart),
            Role = "Boss, 2x2. One action a turn, two from its second phase; rocks fall in its last. Slam: 3 damage to each of your units within 2 tiles, then shoves them 1 tile away. Smash hits every adjacent unit. " +
                   "Or throws a boulder 2-4 tiles away that stays as a rock. Nothing pins, slows or pushes it.",
        };

        // ---------- Bandits ----------

        public static readonly UnitStats Cutpurse = new UnitStats(UnitKind.Cutpurse, "Cutpurse", 5, 2, 1, 1)
        {
            Weight = Weight.Small, StealsGold = 5, Abilities = A(Ability.Swift, Ability.LightFingers, Ability.Caltrops),
            Role = "Stabs, grabs 5 gold and runs in the same turn, dropping caltrops behind it. Kill it before it gets away.",
        };

        public static readonly UnitStats Crossbowman = new UnitStats(UnitKind.Crossbowman, "Crossbowman", 6, 2, 3, 1)
        {
            Abilities = A(Ability.BehindCover, Ability.Overwatch), Role = "Shoots from behind a rock, and covers the approach when nobody is in range.",
        };

        public static readonly UnitStats Brute = new UnitStats(UnitKind.Brute, "Bandit Brute", 12, 3, 1, 1)
        {
            Weight = Weight.Huge, Abilities = A(Ability.Guard, Ability.BullRush), Role = "Takes the hits meant for the bandits next to it, and rushes your line apart.",
        };

        // ---------- Elementals ----------

        public static readonly UnitStats Shard = new UnitStats(UnitKind.Shard, "Shard", 4, 2, 1, 1)
        {
            Weight = Weight.Small, Role = "Half of an Elemental.",
        };

        public static readonly UnitStats Elemental = new UnitStats(UnitKind.Elemental, "Elemental", 8, 3, 1, 1)
        {
            Weight = Weight.Huge, SplitsInto = Shard, Abilities = A(Ability.Upheaval),
            Role = "Throws your units out of line with the ground beneath them. When it dies it splits into 2 Shards (5 HP each).",
        };

        // ---------- Raised versions (necromancer boss) ----------

        private static readonly Dictionary<UnitStats, UnitStats> RaisedCache = new Dictionary<UnitStats, UnitStats>();

        /// <summary>
        /// The undead version of an enemy type: same numbers and look, but it leaves a bone pile and can be
        /// raised again. Splitting and stealing are dropped so the dead stay simple.
        /// </summary>
        public static UnitStats Raised(UnitStats s)
        {
            if (RaisedCache.TryGetValue(s, out var r))
                return r;
            r = new UnitStats(s.Kind, "Bone " + s.Name, s.MaxHp, s.Damage, s.Range, s.MaxAp)
            {
                Weight = s.Weight, Abilities = s.Abilities, LeavesRemains = true, BombCost = s.BombCost, BombDamage = s.BombDamage, BombRange = s.BombRange,
                Role = $"Raised {s.Name}. Leaves a bone pile; the Necromancer can raise it again.", IsUndead = true,
            };
            RaisedCache[s] = r;
            return r;
        }

        private static readonly Dictionary<UnitStats, UnitStats> HarvestedCache = new Dictionary<UnitStats, UnitStats>();

        /// <summary>
        /// One of your fallen units raised by Soul Harvest: its HP, damage, range and moves, but none of its passives,
        /// actions or item; it only uses its basic attack. Leaves a bone pile like any undead.
        /// </summary>
        public static UnitStats Harvested(UnitStats s)
        {
            if (HarvestedCache.TryGetValue(s, out var r))
                return r;
            r = new UnitStats(s.Kind, "Bone " + s.Name, s.MaxHp, s.Damage, s.Has(Ability.LongReach) ? 1 : s.Range, 1)
            {
                Weight = s.Weight, Abilities = s.Has(Ability.Swift) ? A(Ability.Swift) : A(), Label = s.Label, LeavesRemains = true, IsUndead = true,
                Role = $"Your fallen {s.Name}, raised by the Necromancer. Only its basic attack; leaves a bone pile it can raise again.",
            };
            HarvestedCache[s] = r;
            return r;
        }

        public static readonly UnitStats Goblin = new UnitStats(UnitKind.Goblin, "Goblin", 6, 2, 1, 1)
        {
            Weight = Weight.Small, Abilities = A(Ability.Swift, Ability.Elusive, Ability.Fling),
            Role = "Slips past your line and flings its friends, and your own explosives, at you.",
        };

        /// <summary>Lobs bombs that explode at the start of the next enemy turn. Stabs for 1 up close.</summary>
        public static readonly UnitStats GoblinBomber = new UnitStats(UnitKind.GoblinBomber, "Goblin Bomber", 4, 1, 1, 1)
        {
            Weight = Weight.Small, BombCost = 1, BombDamage = 3, BombRange = 3, Abilities = A(Ability.PowderKeg),
            Role = "Lobs a bomb onto a unit 2-3 tiles away; it explodes on that tile at the start of the next enemy turn. Keeps its distance, and blows up when it dies.",
        };

        // ---------- Demons (see docs/night/demons.md) ----------

        /// <summary>Swaps one of your units into the demons' reach. Feeds Fury to a demon when it dies.</summary>
        public static readonly UnitStats Imp = new UnitStats(UnitKind.Imp, "Imp", 5, 2, 1, 1)
        {
            Weight = Weight.Small, Abilities = A(Ability.Swift, Ability.Hellborn, Ability.BloodOffering, Ability.Hellswap),
            Role = "Trades places with one of your units, dropping it next to the demons. When it dies, the nearest demon grows stronger.",
        };

        public static readonly UnitStats Demon = new UnitStats(UnitKind.Demon, "Demon", 14, 3, 1, 1)
        {
            Weight = Weight.Huge, Abilities = A(Ability.Hellborn, Ability.Bloodlust, Ability.RendingBlow),
            Role = "Pays its own blood for crushing blows, and grows stronger from every death next to it.",
        };

        /// <summary>Its trident is a Long Reach strike (2 tiles in a straight line).</summary>
        public static readonly UnitStats Devil = new UnitStats(UnitKind.Devil, "Devil", 10, 2, 2, 1)
        {
            Abilities = A(Ability.LongReach, Ability.Hellborn, Ability.SoulContract),
            Role = "Signs a contract on one of your units: hit the Devil before its next turn, or the contract takes its HP.",
        };

        public static readonly UnitStats Fiend = new UnitStats(UnitKind.Fiend, "Fiend", 7, 2, 1, 1)
        {
            Weight = Weight.Small, Abilities = A(Ability.Swift, Ability.Hellborn, Ability.Thorns, Ability.FireBreath),
            Role = "Fast and spiny: breathes fire down a line of your units and leaves the ground burning.",
        };

        public static readonly IReadOnlyList<UnitStats> Enemies = new[]
        {
            Wolf, PackLeader, Goblin, GoblinBomber, Cutpurse, Crossbowman, Brute, Elemental, Shard, Imp, Demon, Devil, Fiend, Skeleton, Necromancer, Ogre, Totem,
        };
    }
}
