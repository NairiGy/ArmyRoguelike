using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    public enum Team
    {
        Player,
        Enemy,
    }

    /// <summary>The look of a unit: its statue family. Evolved units share their line's statue.</summary>
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
    }

    /// <summary>A passive rule a unit has, from its type or from an item.</summary>
    public enum Ability
    {
        None,

        // ---------- Warrior line ----------

        /// <summary>Allies standing next to this unit take 1 less damage (minimum 1).</summary>
        ShieldWall,

        /// <summary>Attacks also hit the enemies on both sides of the target.</summary>
        Sweep,

        /// <summary>Melee reach 2 in a straight line; the attack hits both tiles.</summary>
        LongReach,

        /// <summary>Every attack strikes twice.</summary>
        DoubleStrike,

        /// <summary>When an adjacent ally is attacked, this unit takes the hit instead.</summary>
        Guard,

        /// <summary>Enemies within 3 tiles must attack this unit.</summary>
        Taunt,

        /// <summary>Takes 1 less damage from everything (minimum 1).</summary>
        Tough,

        // ---------- Archer line ----------

        /// <summary>+1 damage if the unit hasn't moved this turn.</summary>
        SteadyAim,

        /// <summary>+2 damage if the unit hasn't moved this turn.</summary>
        SteadyAimPlus,

        /// <summary>Shots along a straight row or column hit every enemy in the line.</summary>
        Pierce,

        /// <summary>The first attack each turn deals double damage.</summary>
        FirstShotDouble,

        /// <summary>Snared enemies take +2 damage from everyone.</summary>
        PreyOnSnared,

        /// <summary>Shots hit the 3x3 around the target for 1 less damage, friend or foe.</summary>
        Volley,

        // ---------- Rogue line ----------

        /// <summary>+1 damage per enemy killed, kept for the rest of the run.</summary>
        Bloodthirst,

        /// <summary>+3 damage if an ally stands on the far side of the target.</summary>
        Pincer,

        /// <summary>A kill refunds 2 AP.</summary>
        KillRefund,

        /// <summary>Hits poison the target: 2 damage at the start of its next 2 turns.</summary>
        Poison,

        /// <summary>Counter-attacks the first melee hit each enemy turn.</summary>
        Counter,

        /// <summary>Counter-attacks every melee hit.</summary>
        CounterAll,

        /// <summary>The first hit each enemy turn deals 0 damage.</summary>
        Dodge,

        // ---------- Mage line ----------

        /// <summary>Its bolt ignores line of sight.</summary>
        ArcaneBolt,

        /// <summary>Fireballs leave the area burning for 2 rounds (1 damage at the start of each turn).</summary>
        Burning,

        /// <summary>Fireballs push units outward and set off mines and bombs.</summary>
        Concussive,

        /// <summary>Its bolt slows: the target gets 2 less AP on its next turn.</summary>
        Chill,

        /// <summary>Double damage to slowed enemies.</summary>
        Shatter,

        /// <summary>Ward also shields every ally next to the target.</summary>
        WideWard,

        // ---------- Sapper line ----------

        /// <summary>Can place mines (see the Place Mine action).</summary>
        Mines,

        /// <summary>Its mines deal 5 damage and it can have 5 on the board.</summary>
        BigMines,

        /// <summary>Its own blasts don't hurt its side.</summary>
        BlastProof,

        // ---------- Items ----------

        /// <summary>Heals 2 HP on every kill.</summary>
        VampireFang,

        /// <summary>An enemy that hits this unit in melee takes 1 damage.</summary>
        ThornedMail,

        /// <summary>The first tile moved each turn costs no AP.</summary>
        SpringBoots,

        /// <summary>Once per battle, a hit that would kill leaves the unit at 1 HP instead.</summary>
        LuckyCharm,

        // ---------- Summons ----------

        /// <summary>Can't move.</summary>
        Immobile,
    }

    /// <summary>A special action a player unit can spend AP on, besides moving and attacking.</summary>
    public enum ActionKind
    {
        PlaceMine,
        ThrowBomb,
        Snare,
        Fireball,
        IceWall,
        Ward,
        Mend,
        Barricade,
        Turret,
        Detonate,
        Charge,
    }

    /// <summary>
    /// Base numbers and rules for a unit type. All values are placeholders from the design note.
    /// Player types form evolution trees: each type lists what it can evolve into at a Trainer.
    /// </summary>
    public sealed class UnitStats
    {
        public readonly UnitKind Kind;
        public readonly string Name;
        public readonly int MaxHp;
        public readonly int Damage;
        public readonly int Range;
        public readonly int MaxAp;

        /// <summary>1 for base units, 2 and 3 for evolutions. 0 for enemies and summons.</summary>
        public int Tier { get; private set; }

        /// <summary>Short text drawn on the statue.</summary>
        public string Label { get; private set; }

        /// <summary>One-line flavour of how the unit plays.</summary>
        public string Role { get; private set; } = "";

        public IReadOnlyList<Ability> Abilities { get; private set; } = new Ability[0];
        public IReadOnlyList<ActionKind> Actions { get; private set; } = new ActionKind[0];

        private readonly List<UnitStats> _evolutions = new List<UnitStats>();

        /// <summary>The types this one can evolve into (empty at the top of a tree).</summary>
        public IReadOnlyList<UnitStats> Evolutions => _evolutions;

        public UnitStats EvolvesFrom { get; private set; }

        /// <summary>Side length in tiles. 1 for normal units, 2 for a 2x2 unit like the ogre.</summary>
        public int Size { get; private set; } = 1;

        /// <summary>Leaves a bone pile when killed, which a necromancer can raise.</summary>
        public bool LeavesRemains { get; private set; }

        /// <summary>The basic attack hits every enemy next to the unit, not just one.</summary>
        public bool AreaMelee { get; private set; }

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

        public bool HasAction(ActionKind action) => Actions.Contains(action);

        /// <summary>A ranged attack needs line of sight and can't be countered. Long reach is still melee.</summary>
        public bool IsRanged => Range > 1 && !Has(Ability.LongReach);

        public bool CanRaise => RaiseCost > 0;

        public bool CanThrow => ThrowCost > 0;

        public bool CanBomb => BombCost > 0;

        public bool IsPlayerType => Tier > 0;

        /// <summary>The tier-1 unit at the root of this one's tree.</summary>
        public UnitStats Root
        {
            get
            {
                var s = this;
                while (s.EvolvesFrom != null)
                    s = s.EvolvesFrom;
                return s;
            }
        }

        private static UnitStats Player(UnitKind kind, string name, string label, int tier, int hp, int damage, int range, int ap,
            string role, Ability[] abilities, ActionKind[] actions = null, UnitStats from = null)
        {
            var s = new UnitStats(kind, name, hp, damage, range, ap)
            {
                Tier = tier, Label = label, Role = role, Abilities = abilities, Actions = actions ?? new ActionKind[0],
                EvolvesFrom = from,
            };
            from?._evolutions.Add(s);
            return s;
        }

        private static Ability[] A(params Ability[] a) => a;

        private static ActionKind[] Do(params ActionKind[] a) => a;

        // ---------- Warrior tree ----------

        public static readonly UnitStats Warrior = Player(UnitKind.Warrior, "Warrior", "W", 1, 12, 3, 1, 3,
            "Sturdy front line that protects its neighbours.", A(Ability.ShieldWall));

        public static readonly UnitStats Swordsman = Player(UnitKind.Warrior, "Swordsman", "Sw", 2, 14, 4, 1, 3,
            "Wide swings that catch groups.", A(Ability.ShieldWall, Ability.Sweep), from: Warrior);

        public static readonly UnitStats Longswordsman = Player(UnitKind.Warrior, "Longswordsman", "Ls", 3, 16, 5, 2, 3,
            "Hits two tiles deep from a safe distance.", A(Ability.ShieldWall, Ability.LongReach), from: Swordsman);

        public static readonly UnitStats DualBlades = Player(UnitKind.Warrior, "Dual Blades", "Db", 3, 15, 4, 1, 4,
            "A blender: sweeps and strikes twice.", A(Ability.ShieldWall, Ability.Sweep, Ability.DoubleStrike), from: Swordsman);

        public static readonly UnitStats Shielder = Player(UnitKind.Warrior, "Shielder", "Sh", 2, 16, 3, 1, 3,
            "Takes the hits meant for its friends.", A(Ability.ShieldWall, Ability.Guard), from: Warrior);

        public static readonly UnitStats Juggernaut = Player(UnitKind.Warrior, "Juggernaut", "Jg", 3, 20, 4, 1, 3,
            "Charges in a straight line and knocks enemies back.", A(Ability.ShieldWall, Ability.Guard),
            Do(ActionKind.Charge), Shielder);

        public static readonly UnitStats Bastion = Player(UnitKind.Warrior, "Bastion", "Ba", 3, 20, 3, 1, 3,
            "Forces nearby enemies to attack it, and shrugs it off.", A(Ability.ShieldWall, Ability.Guard, Ability.Taunt, Ability.Tough),
            from: Shielder);

        // ---------- Archer tree ----------

        public static readonly UnitStats Archer = Player(UnitKind.Archer, "Archer", "A", 1, 6, 2, 4, 4,
            "Fragile ranged damage that likes to stand still.", A(Ability.SteadyAim));

        public static readonly UnitStats Marksman = Player(UnitKind.Archer, "Marksman", "Mk", 2, 7, 3, 5, 4,
            "Longer range, bigger reward for holding position.", A(Ability.SteadyAimPlus), from: Archer);

        public static readonly UnitStats Sniper = Player(UnitKind.Archer, "Sniper", "Sn", 3, 8, 3, 5, 4,
            "Lines enemies up and shoots through all of them.", A(Ability.SteadyAimPlus, Ability.Pierce), from: Marksman);

        public static readonly UnitStats Deadeye = Player(UnitKind.Archer, "Deadeye", "De", 3, 8, 4, 5, 4,
            "One devastating opening shot per turn.", A(Ability.SteadyAimPlus, Ability.FirstShotDouble), from: Marksman);

        public static readonly UnitStats Trapper = Player(UnitKind.Archer, "Trapper", "Tr", 2, 7, 3, 4, 4,
            "Roots enemies in place with snares.", A(Ability.SteadyAim), Do(ActionKind.Snare), Archer);

        public static readonly UnitStats Hunter = Player(UnitKind.Archer, "Hunter", "Hu", 3, 8, 3, 4, 4,
            "Snared prey takes extra damage from the whole army.", A(Ability.SteadyAim, Ability.PreyOnSnared),
            Do(ActionKind.Snare), Trapper);

        public static readonly UnitStats VolleyMaster = Player(UnitKind.Archer, "Volley Master", "Vm", 3, 8, 3, 4, 4,
            "Arrows rain on a 3x3 area. Mind your friends.", A(Ability.SteadyAim, Ability.Volley), Do(ActionKind.Snare), Trapper);

        // ---------- Rogue tree ----------

        public static readonly UnitStats Rogue = Player(UnitKind.Rogue, "Rogue", "R", 1, 5, 4, 1, 5,
            "Fast, deadly, fragile. Grows with every kill.", A(Ability.Bloodthirst));

        public static readonly UnitStats Assassin = Player(UnitKind.Rogue, "Assassin", "As", 2, 6, 5, 1, 5,
            "Big damage when an ally pins the target from the other side.", A(Ability.Bloodthirst, Ability.Pincer), from: Rogue);

        public static readonly UnitStats Shadow = Player(UnitKind.Rogue, "Shadow", "Sd", 3, 7, 5, 1, 6,
            "Kills refund AP: chain through a whole group.", A(Ability.Bloodthirst, Ability.Pincer, Ability.KillRefund), from: Assassin);

        public static readonly UnitStats Poisoner = Player(UnitKind.Rogue, "Poisoner", "Po", 3, 7, 5, 1, 5,
            "Every hit keeps hurting over the next turns.", A(Ability.Bloodthirst, Ability.Pincer, Ability.Poison), from: Assassin);

        public static readonly UnitStats Duelist = Player(UnitKind.Rogue, "Duelist", "Du", 2, 7, 4, 1, 5,
            "Strikes back when attacked.", A(Ability.Bloodthirst, Ability.Counter), from: Rogue);

        public static readonly UnitStats Blademaster = Player(UnitKind.Rogue, "Blademaster", "Bm", 3, 9, 5, 1, 5,
            "Counters every single melee hit.", A(Ability.Bloodthirst, Ability.CounterAll), from: Duelist);

        public static readonly UnitStats Swashbuckler = Player(UnitKind.Rogue, "Swashbuckler", "Sb", 3, 8, 5, 1, 6,
            "Dodges the first hit each turn, then counters.", A(Ability.Bloodthirst, Ability.Counter, Ability.Dodge), from: Duelist);

        // ---------- Mage tree ----------

        public static readonly UnitStats Apprentice = Player(UnitKind.Mage, "Mage Apprentice", "Ma", 1, 5, 2, 3, 4,
            "Weak bolt that flies over obstacles.", A(Ability.ArcaneBolt));

        public static readonly UnitStats FireMage = Player(UnitKind.Mage, "Fire Mage", "Fi", 2, 6, 3, 3, 4,
            "Fireballs hit a 3x3 area, friend or foe.", A(Ability.ArcaneBolt), Do(ActionKind.Fireball), Apprentice);

        public static readonly UnitStats Pyromancer = Player(UnitKind.Mage, "Pyromancer", "Py", 3, 7, 3, 3, 4,
            "Leaves the ground burning.", A(Ability.ArcaneBolt, Ability.Burning), Do(ActionKind.Fireball), FireMage);

        public static readonly UnitStats BlastMage = Player(UnitKind.Mage, "Blast Mage", "Bl", 3, 7, 3, 3, 4,
            "Fireballs shove everything outward and set off mines and bombs.", A(Ability.ArcaneBolt, Ability.Concussive),
            Do(ActionKind.Fireball), FireMage);

        public static readonly UnitStats FrostMage = Player(UnitKind.Mage, "Frost Mage", "Fr", 2, 6, 3, 3, 4,
            "Bolts slow enemies down.", A(Ability.ArcaneBolt, Ability.Chill), from: Apprentice);

        public static readonly UnitStats Cryomancer = Player(UnitKind.Mage, "Cryomancer", "Cr", 3, 7, 3, 3, 4,
            "Raises ice walls to reshape the board.", A(Ability.ArcaneBolt, Ability.Chill), Do(ActionKind.IceWall), FrostMage);

        public static readonly UnitStats Glacier = Player(UnitKind.Mage, "Glacier", "Gl", 3, 7, 4, 3, 4,
            "Shatters slowed enemies for double damage.", A(Ability.ArcaneBolt, Ability.Chill, Ability.Shatter), from: FrostMage);

        public static readonly UnitStats WardMage = Player(UnitKind.Mage, "Ward Mage", "Wa", 2, 6, 2, 3, 4,
            "Shields an ally against the next hits.", A(Ability.ArcaneBolt), Do(ActionKind.Ward), Apprentice);

        public static readonly UnitStats Archwarden = Player(UnitKind.Mage, "Archwarden", "Aw", 3, 7, 3, 3, 4,
            "Wards whole groups at once.", A(Ability.ArcaneBolt, Ability.WideWard), Do(ActionKind.Ward), WardMage);

        public static readonly UnitStats Mender = Player(UnitKind.Mage, "Mender", "Me", 3, 7, 3, 3, 4,
            "Heals as well as wards.", A(Ability.ArcaneBolt), Do(ActionKind.Ward, ActionKind.Mend), WardMage);

        // ---------- Sapper tree ----------

        public static readonly UnitStats Sapper = Player(UnitKind.Sapper, "Sapper", "Sa", 1, 6, 2, 1, 4,
            "Lays mines that enemies walk into.", A(Ability.Mines), Do(ActionKind.PlaceMine));

        public static readonly UnitStats Demolitionist = Player(UnitKind.Sapper, "Demolitionist", "Dm", 2, 7, 3, 1, 4,
            "Bigger mines, and more of them.", A(Ability.Mines, Ability.BigMines), Do(ActionKind.PlaceMine), Sapper);

        public static readonly UnitStats Bombardier = Player(UnitKind.Sapper, "Bombardier", "Bo", 3, 8, 3, 1, 4,
            "Lobs delayed bombs as well.", A(Ability.Mines, Ability.BigMines), Do(ActionKind.PlaceMine, ActionKind.ThrowBomb), Demolitionist);

        public static readonly UnitStats Safecracker = Player(UnitKind.Sapper, "Safecracker", "Sc", 3, 8, 3, 1, 4,
            "Its blasts never hurt your side.", A(Ability.Mines, Ability.BigMines, Ability.BlastProof), Do(ActionKind.PlaceMine), Demolitionist);

        public static readonly UnitStats Engineer = Player(UnitKind.Sapper, "Engineer", "En", 2, 8, 3, 1, 4,
            "Builds barricades to block paths and shots.", A(Ability.Mines), Do(ActionKind.PlaceMine, ActionKind.Barricade), Sapper);

        public static readonly UnitStats TurretBuilder = Player(UnitKind.Sapper, "Turret Builder", "Tb", 3, 9, 3, 1, 4,
            "Sets up a turret that fights on its own turn.", A(Ability.Mines),
            Do(ActionKind.PlaceMine, ActionKind.Barricade, ActionKind.Turret), Engineer);

        public static readonly UnitStats SapperCaptain = Player(UnitKind.Sapper, "Sapper Captain", "Cp", 3, 9, 3, 1, 4,
            "Barricades are bombs: blow them all at once.", A(Ability.Mines),
            Do(ActionKind.PlaceMine, ActionKind.Barricade, ActionKind.Detonate), Engineer);

        /// <summary>Placed by a Turret Builder. Can't move; shoots like a small archer.</summary>
        public static readonly UnitStats Turret = new UnitStats(UnitKind.Turret, "Turret", 4, 2, 3, 2)
        {
            Label = "T", Role = "Stationary shooter.", Abilities = A(Ability.Immobile), IsSummon = true,
        };

        /// <summary>The Moon Shrine the player defends in a ritual fight. Sides with the player but never acts.</summary>
        public static readonly UnitStats MoonShrine = new UnitStats(UnitKind.Shrine, "Moon Shrine", 15, 0, 1, 0)
        {
            Label = "S", Role = "Hold it until the ritual completes. If it breaks, the fight is lost.",
            Abilities = A(Ability.Immobile), IsSummon = true, IsObjective = true,
        };

        /// <summary>The five tree roots the player drafts their starting army from.</summary>
        public static readonly IReadOnlyList<UnitStats> BaseUnits = new[] { Warrior, Archer, Rogue, Apprentice, Sapper };

        /// <summary>Every player unit type, tree by tree.</summary>
        public static IEnumerable<UnitStats> AllPlayerTypes => BaseUnits.SelectMany(Tree);

        public static IEnumerable<UnitStats> Tree(UnitStats root)
        {
            yield return root;
            foreach (var child in root.Evolutions)
            foreach (var s in Tree(child))
                yield return s;
        }

        // ---------- Enemies ----------

        public static readonly UnitStats Wolf = new UnitStats(UnitKind.Wolf, "Wolf", 6, 2, 1, 4)
        {
            Role = "Fast biter. +1 damage within 2 tiles of a living Pack Leader, -1 once the leader is dead.",
        };
        public static readonly UnitStats PackLeader = new UnitStats(UnitKind.PackLeader, "Pack Leader", 10, 3, 1, 4)
        {
            Role = "Wolves within 2 tiles get +1 damage. When it dies, every wolf gets -1.",
        };

        public static readonly UnitStats Skeleton =
            new UnitStats(UnitKind.Skeleton, "Skeleton", 4, 2, 1, 3)
            {
                LeavesRemains = true, Role = "Leaves a bone pile when killed; a Necromancer can raise it. Stand on the pile to stop that.",
            };

        /// <summary>
        /// Act 1 boss. Its army is raised from the factions beaten earlier in the act. Its basic attack
        /// is a bolt (range 3, needs line of sight).
        /// </summary>
        public static readonly UnitStats Necromancer =
            new UnitStats(UnitKind.Necromancer, "Necromancer", 14, 3, 3, 3)
            {
                RaiseCost = 3, RaiseRange = 5,
                Role = "Boss. Brings raised versions of the factions you beat this act. Raises a bone pile within 5 tiles (3 AP). " +
                       "Keeps 3 tiles away, walks up to bolt range and shoots (needs line of sight).",
            };

        /// <summary>Act 1 boss (the ogre). Its basic attack is the club smash, which hits everyone adjacent.</summary>
        public static readonly UnitStats Ogre = new UnitStats(UnitKind.Ogre, "Giant", 36, 5, 1, 4)
        {
            Size = 2, AreaMelee = true, ThrowCost = 3, ThrowDamage = 3, ThrowRange = 5, Slams = true,
            Role = "Boss, 2x2. Slam shoves everyone within 2 tiles 1 tile away (2 AP). Smash hits every adjacent unit. " +
                   "Once a turn throws a boulder 2-5 tiles away that stays as a rock.",
        };

        // ---------- Bandits ----------

        public static readonly UnitStats Cutpurse = new UnitStats(UnitKind.Cutpurse, "Cutpurse", 5, 2, 1, 5)
        {
            StealsGold = 5,
            Role = "On a hit it steals 5 gold and runs for the far edge. If it gets away, the gold is gone. Kill it to get the gold back.",
        };

        public static readonly UnitStats Crossbowman = new UnitStats(UnitKind.Crossbowman, "Crossbowman", 6, 3, 4, 3)
        {
            Role = "Ranged (needs line of sight). Hides behind cover.",
        };

        public static readonly UnitStats Brute = new UnitStats(UnitKind.Brute, "Bandit Brute", 12, 3, 1, 3)
        {
            Abilities = A(Ability.Tough), Role = "Slow, heavy hitter that takes 1 less damage.",
        };

        // ---------- Elementals ----------

        public static readonly UnitStats Shard = new UnitStats(UnitKind.Shard, "Shard", 5, 2, 1, 3)
        {
            Role = "Half of an Elemental.",
        };

        public static readonly UnitStats Elemental = new UnitStats(UnitKind.Elemental, "Elemental", 10, 3, 1, 3)
        {
            SplitsInto = Shard, Role = "When it dies it splits into 2 Shards (5 HP each).",
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
                Abilities = s.Abilities, LeavesRemains = true, BombCost = s.BombCost, BombDamage = s.BombDamage, BombRange = s.BombRange,
                Role = $"Raised {s.Name}. Leaves a bone pile; the Necromancer can raise it again.", IsUndead = true,
            };
            RaisedCache[s] = r;
            return r;
        }

        public static readonly UnitStats Goblin = new UnitStats(UnitKind.Goblin, "Goblin", 5, 2, 1, 4) { Role = "Melee runner." };

        /// <summary>Lobs bombs that explode at the start of the next enemy turn. Stabs for 1 up close.</summary>
        public static readonly UnitStats GoblinBomber = new UnitStats(UnitKind.GoblinBomber, "Goblin Bomber", 4, 1, 1, 3)
        {
            BombCost = 2, BombDamage = 3, BombRange = 4,
            Role = "Lobs a bomb onto a unit 2-4 tiles away; it explodes in a 3x3 at the start of the next enemy turn. Keeps its distance.",
        };

        public static readonly IReadOnlyList<UnitStats> Enemies = new[]
        {
            Wolf, PackLeader, Goblin, GoblinBomber, Cutpurse, Crossbowman, Brute, Elemental, Shard, Skeleton, Necromancer, Ogre,
        };
    }
}
