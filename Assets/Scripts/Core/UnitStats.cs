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
        Totem,
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

        /// <summary>Double damage the first time it hits each enemy in a battle.</summary>
        FirstShotDouble,

        /// <summary>Pinned enemies take +2 damage from everyone.</summary>
        PreyOnSnared,

        /// <summary>Shots hit the target and the 4 tiles next to it for 1 less damage, friend or foe.</summary>
        Volley,

        // ---------- Rogue line ----------

        /// <summary>+1 damage per enemy killed, kept for the rest of the run.</summary>
        Bloodthirst,

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

        // ---------- Sapper line ----------

        /// <summary>Can place mines (see the Place Mine action).</summary>
        Mines,

        /// <summary>Its mines deal 5 damage and it can have 4 on the board.</summary>
        BigMines,

        /// <summary>Its own blasts don't hurt its side.</summary>
        BlastProof,

        // ---------- Items ----------

        /// <summary>Heals 2 HP on every kill.</summary>
        VampireFang,

        /// <summary>An enemy that hits this unit in melee takes 1 damage.</summary>
        ThornedMail,

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

        /// <summary>Ignores zone of control: leaving an enemy's reach doesn't use up its moves.</summary>
        Slippery,

        /// <summary>Its action can be a fling: it throws an adjacent goblin, bomb or mine at one of your units 2-3 tiles away.</summary>
        Fling,

        /// <summary>When it dies it explodes: damage to everyone around it, its own side included.</summary>
        PowderKeg,
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

        /// <summary>Tiles per turn before items: 1, or 2 with Swift. 0 for units that can't move.</summary>
        public int Moves => Has(Ability.Immobile) ? 0 : Battle.BaseMoves + (Has(Ability.Swift) ? 1 : 0);

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

        public static readonly UnitStats Warrior = Player(UnitKind.Warrior, "Warrior", "W", 1, 12, 3, 1, 1,
            "Sturdy front line that protects its neighbours.", A(Ability.ShieldWall), Do(ActionKind.ShieldBash));

        public static readonly UnitStats Swordsman = Player(UnitKind.Warrior, "Swordsman", "Sw", 2, 14, 4, 1, 1,
            "Wide swings that catch groups.", A(Ability.ShieldWall, Ability.Sweep), Do(ActionKind.ShieldBash), from: Warrior);

        public static readonly UnitStats Longswordsman = Player(UnitKind.Warrior, "Longswordsman", "Ls", 3, 16, 5, 2, 1,
            "Hits two tiles deep from a safe distance.", A(Ability.ShieldWall, Ability.LongReach), Do(ActionKind.ShieldBash), from: Swordsman);

        public static readonly UnitStats DualBlades = Player(UnitKind.Warrior, "Dual Blades", "Db", 3, 15, 4, 1, 1,
            "A blender: sweeps and strikes twice.", A(Ability.ShieldWall, Ability.Sweep, Ability.DoubleStrike), Do(ActionKind.ShieldBash), from: Swordsman);

        public static readonly UnitStats Shielder = Player(UnitKind.Warrior, "Shielder", "Sh", 2, 16, 3, 1, 1,
            "Takes the hits meant for its friends.", A(Ability.ShieldWall, Ability.Guard), Do(ActionKind.ShieldBash), from: Warrior);

        public static readonly UnitStats Juggernaut = Player(UnitKind.Warrior, "Juggernaut", "Jg", 3, 20, 4, 1, 1,
            "Charges in a straight line and knocks enemies back.", A(Ability.ShieldWall, Ability.Guard),
            Do(ActionKind.ShieldBash, ActionKind.Charge), Shielder);

        public static readonly UnitStats Bastion = Player(UnitKind.Warrior, "Bastion", "Ba", 3, 20, 3, 1, 1,
            "Forces nearby enemies to attack it, and shrugs it off.", A(Ability.ShieldWall, Ability.Guard, Ability.Taunt, Ability.Tough),
            Do(ActionKind.ShieldBash), from: Shielder);

        // ---------- Archer tree ----------

        public static readonly UnitStats Archer = Player(UnitKind.Archer, "Archer", "A", 1, 6, 2, 3, 1,
            "Fragile ranged damage that likes to stand still.", A(Ability.SteadyAim), Do(ActionKind.PinningShot));

        public static readonly UnitStats Marksman = Player(UnitKind.Archer, "Marksman", "Mk", 2, 7, 3, 4, 1,
            "Longer range, bigger reward for holding position.", A(Ability.SteadyAimPlus), Do(ActionKind.PinningShot), from: Archer);

        public static readonly UnitStats Sniper = Player(UnitKind.Archer, "Sniper", "Sn", 3, 8, 3, 4, 1,
            "Lines enemies up and shoots through all of them.", A(Ability.SteadyAimPlus, Ability.Pierce), Do(ActionKind.PinningShot), from: Marksman);

        public static readonly UnitStats Deadeye = Player(UnitKind.Archer, "Deadeye", "De", 3, 8, 4, 4, 1,
            "A devastating opening shot on every new target.", A(Ability.SteadyAimPlus, Ability.FirstShotDouble), Do(ActionKind.PinningShot), from: Marksman);

        public static readonly UnitStats Trapper = Player(UnitKind.Archer, "Trapper", "Tr", 2, 7, 3, 3, 1,
            "Pins enemies in place; a new action comes later.", A(Ability.SteadyAim), Do(ActionKind.PinningShot), Archer);

        public static readonly UnitStats Hunter = Player(UnitKind.Archer, "Hunter", "Hu", 3, 8, 3, 3, 1,
            "Pinned prey takes extra damage from the whole army.", A(Ability.SteadyAim, Ability.PreyOnSnared),
            Do(ActionKind.PinningShot), Trapper);

        public static readonly UnitStats VolleyMaster = Player(UnitKind.Archer, "Volley Master", "Vm", 3, 8, 3, 3, 1,
            "Arrows rain on a plus-shaped area. Mind your friends.", A(Ability.SteadyAim, Ability.Volley), Do(ActionKind.PinningShot), Trapper);

        // ---------- Rogue tree ----------

        public static readonly UnitStats Rogue = Player(UnitKind.Rogue, "Rogue", "R", 1, 5, 4, 1, 1,
            "Fast, deadly, fragile. Slips out of sight.", A(Ability.Swift), Do(ActionKind.Vanish));

        public static readonly UnitStats Assassin = Player(UnitKind.Rogue, "Assassin", "As", 2, 6, 5, 1, 1,
            "Big damage when an ally pins the target from the other side.", A(Ability.Swift, Ability.Bloodthirst, Ability.Pincer), Do(ActionKind.Vanish), from: Rogue);

        public static readonly UnitStats Shadow = Player(UnitKind.Rogue, "Shadow", "Sd", 3, 7, 5, 1, 1,
            "A kill gives the action back: chain through a whole group.", A(Ability.Swift, Ability.Bloodthirst, Ability.Pincer, Ability.KillRefund), Do(ActionKind.Vanish), from: Assassin);

        public static readonly UnitStats Poisoner = Player(UnitKind.Rogue, "Poisoner", "Po", 3, 7, 5, 1, 1,
            "Every hit keeps hurting over the next turns.", A(Ability.Swift, Ability.Bloodthirst, Ability.Pincer, Ability.Poison), Do(ActionKind.Vanish), from: Assassin);

        public static readonly UnitStats Duelist = Player(UnitKind.Rogue, "Duelist", "Du", 2, 7, 4, 1, 1,
            "Strikes back when attacked.", A(Ability.Swift, Ability.Bloodthirst, Ability.Counter), Do(ActionKind.Vanish), from: Rogue);

        public static readonly UnitStats Blademaster = Player(UnitKind.Rogue, "Blademaster", "Bm", 3, 9, 5, 1, 1,
            "Counters every single melee hit.", A(Ability.Swift, Ability.Bloodthirst, Ability.CounterAll), Do(ActionKind.Vanish), from: Duelist);

        public static readonly UnitStats Swashbuckler = Player(UnitKind.Rogue, "Swashbuckler", "Sb", 3, 8, 5, 1, 1,
            "Dodges the first hit each turn, then counters.", A(Ability.Swift, Ability.Bloodthirst, Ability.Counter, Ability.Dodge), Do(ActionKind.Vanish), from: Duelist);

        // ---------- Mage tree ----------

        public static readonly UnitStats Apprentice = Player(UnitKind.Mage, "Mage Apprentice", "Ma", 1, 5, 2, 3, 1,
            "Weak bolt that flies over obstacles.", A(Ability.ArcaneBolt), Do(ActionKind.Blink));

        public static readonly UnitStats FireMage = Player(UnitKind.Mage, "Fire Mage", "Fi", 2, 6, 3, 3, 1,
            "Fireballs hit a plus-shaped area, friend or foe.", A(Ability.ArcaneBolt), Do(ActionKind.Fireball, ActionKind.Blink), Apprentice);

        public static readonly UnitStats Pyromancer = Player(UnitKind.Mage, "Pyromancer", "Py", 3, 7, 3, 3, 1,
            "Leaves the ground burning.", A(Ability.ArcaneBolt, Ability.Burning), Do(ActionKind.Fireball, ActionKind.Blink), FireMage);

        public static readonly UnitStats BlastMage = Player(UnitKind.Mage, "Blast Mage", "Bl", 3, 7, 3, 3, 1,
            "Fireballs shove everything outward and set off mines and bombs.", A(Ability.ArcaneBolt, Ability.Concussive),
            Do(ActionKind.Fireball, ActionKind.Blink), FireMage);

        public static readonly UnitStats FrostMage = Player(UnitKind.Mage, "Frost Mage", "Fr", 2, 6, 3, 3, 1,
            "Bolts slow enemies down.", A(Ability.ArcaneBolt, Ability.Chill), Do(ActionKind.Blink), from: Apprentice);

        public static readonly UnitStats Cryomancer = Player(UnitKind.Mage, "Cryomancer", "Cr", 3, 7, 3, 3, 1,
            "Raises ice walls to reshape the board.", A(Ability.ArcaneBolt, Ability.Chill), Do(ActionKind.IceWall, ActionKind.Blink), FrostMage);

        public static readonly UnitStats Glacier = Player(UnitKind.Mage, "Glacier", "Gl", 3, 7, 4, 3, 1,
            "Shatters slowed enemies for double damage.", A(Ability.ArcaneBolt, Ability.Chill, Ability.Shatter), Do(ActionKind.Blink), from: FrostMage);

        public static readonly UnitStats WardMage = Player(UnitKind.Mage, "Ward Mage", "Wa", 2, 6, 2, 3, 1,
            "Shields an ally against the next hits.", A(Ability.ArcaneBolt), Do(ActionKind.Ward, ActionKind.Blink), Apprentice);

        public static readonly UnitStats Archwarden = Player(UnitKind.Mage, "Archwarden", "Aw", 3, 7, 3, 3, 1,
            "Wards whole groups at once.", A(Ability.ArcaneBolt, Ability.WideWard), Do(ActionKind.Ward, ActionKind.Blink), WardMage);

        public static readonly UnitStats Mender = Player(UnitKind.Mage, "Mender", "Me", 3, 7, 3, 3, 1,
            "Heals as well as wards.", A(Ability.ArcaneBolt), Do(ActionKind.Ward, ActionKind.Mend, ActionKind.Blink), WardMage);

        // ---------- Sapper tree ----------

        public static readonly UnitStats Sapper = Player(UnitKind.Sapper, "Sapper", "Sa", 1, 6, 2, 1, 1,
            "Lays mines that enemies walk into.", A(Ability.Mines), Do(ActionKind.PlaceMine));

        public static readonly UnitStats Demolitionist = Player(UnitKind.Sapper, "Demolitionist", "Dm", 2, 7, 3, 1, 1,
            "Bigger mines, and more of them.", A(Ability.Mines, Ability.BigMines), Do(ActionKind.PlaceMine), Sapper);

        public static readonly UnitStats Bombardier = Player(UnitKind.Sapper, "Bombardier", "Bo", 3, 8, 3, 1, 1,
            "Lobs delayed bombs as well.", A(Ability.Mines, Ability.BigMines), Do(ActionKind.PlaceMine, ActionKind.ThrowBomb), Demolitionist);

        public static readonly UnitStats Safecracker = Player(UnitKind.Sapper, "Safecracker", "Sc", 3, 8, 3, 1, 1,
            "Its blasts never hurt your side.", A(Ability.Mines, Ability.BigMines, Ability.BlastProof), Do(ActionKind.PlaceMine), Demolitionist);

        public static readonly UnitStats Engineer = Player(UnitKind.Sapper, "Engineer", "En", 2, 8, 3, 1, 1,
            "Builds barricades to block paths and shots.", A(Ability.Mines), Do(ActionKind.PlaceMine, ActionKind.Barricade), Sapper);

        public static readonly UnitStats TurretBuilder = Player(UnitKind.Sapper, "Turret Builder", "Tb", 3, 9, 3, 1, 1,
            "Sets up a turret that fights on its own turn.", A(Ability.Mines),
            Do(ActionKind.PlaceMine, ActionKind.Barricade, ActionKind.Turret), Engineer);

        public static readonly UnitStats SapperCaptain = Player(UnitKind.Sapper, "Sapper Captain", "Cp", 3, 9, 3, 1, 1,
            "Barricades are bombs: blow them all at once.", A(Ability.Mines),
            Do(ActionKind.PlaceMine, ActionKind.Barricade, ActionKind.Detonate), Engineer);

        /// <summary>Placed by a Turret Builder. Can't move; shoots like a small archer.</summary>
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

        public static readonly UnitStats Wolf = new UnitStats(UnitKind.Wolf, "Wolf", 6, 2, 1, 1)
        {
            Abilities = A(Ability.Swift, Ability.BloodScent, Ability.WolfBite),
            Role = "Fast biter that hunts the weakest. Bites hit harder the more wolves bit before it this turn.",
        };
        public static readonly UnitStats PackLeader = new UnitStats(UnitKind.PackLeader, "Pack Leader", 10, 3, 1, 1)
        {
            Abilities = A(Ability.Swift, Ability.PackBond, Ability.Howl, Ability.WolfBite),
            Role = "Keeps the pack strong while it lives, and howls to send it further.",
        };

        public static readonly UnitStats Skeleton =
            new UnitStats(UnitKind.Skeleton, "Skeleton", 4, 2, 1, 1)
            {
                LeavesRemains = true, Role = "Leaves a bone pile when killed; a Necromancer can raise it. Stand on the pile to stop that.",
            };

        /// <summary>
        /// Act 1 boss. Its army is raised from the factions beaten earlier in the act. Its basic attack
        /// is a bolt (range 3, needs line of sight).
        /// </summary>
        public static readonly UnitStats Necromancer =
            new UnitStats(UnitKind.Necromancer, "Necromancer", 14, 4, 3, 1)
            {
                RaiseCost = 1, RaiseRange = 4,
                Role = "Boss. Brings raised versions of the factions you beat this act. One action a turn: raises every bone pile within 4 tiles at once, or fires a bolt. " +
                       "Keeps 2 tiles away, walks up to bolt range and shoots (needs line of sight).",
            };

        /// <summary>Act 1 boss (the ogre). Its basic attack is the club smash, which hits everyone adjacent.</summary>
        public static readonly UnitStats Ogre = new UnitStats(UnitKind.Ogre, "Giant", 36, 5, 1, 1)
        {
            Size = 2, AreaMelee = true, ThrowCost = 1, ThrowDamage = 4, ThrowRange = 4, Slams = true,
            Role = "Boss, 2x2. One action a turn. Slam: 3 damage to each of your units within 2 tiles, then shoves them 1 tile away. Smash hits every adjacent unit. " +
                   "Or throws a boulder 2-4 tiles away that stays as a rock.",
        };

        // ---------- Bandits ----------

        public static readonly UnitStats Cutpurse = new UnitStats(UnitKind.Cutpurse, "Cutpurse", 5, 2, 1, 1)
        {
            StealsGold = 5, Abilities = A(Ability.Swift),
            Role = "On a hit it steals 5 gold and runs for the far edge. If it gets away, the gold is gone. Kill it to get the gold back.",
        };

        public static readonly UnitStats Crossbowman = new UnitStats(UnitKind.Crossbowman, "Crossbowman", 6, 3, 3, 1)
        {
            Role = "Ranged (needs line of sight). Hides behind cover.",
        };

        public static readonly UnitStats Brute = new UnitStats(UnitKind.Brute, "Bandit Brute", 12, 3, 1, 1)
        {
            Abilities = A(Ability.Tough), Role = "Slow, heavy hitter that takes 1 less damage.",
        };

        // ---------- Elementals ----------

        public static readonly UnitStats Shard = new UnitStats(UnitKind.Shard, "Shard", 5, 2, 1, 1)
        {
            Role = "Half of an Elemental.",
        };

        public static readonly UnitStats Elemental = new UnitStats(UnitKind.Elemental, "Elemental", 10, 3, 1, 1)
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

        public static readonly UnitStats Goblin = new UnitStats(UnitKind.Goblin, "Goblin", 5, 2, 1, 1)
        {
            Abilities = A(Ability.Swift, Ability.Slippery, Ability.Fling),
            Role = "Slips past your line and flings its friends, and your own explosives, at you.",
        };

        /// <summary>Lobs bombs that explode at the start of the next enemy turn. Stabs for 1 up close.</summary>
        public static readonly UnitStats GoblinBomber = new UnitStats(UnitKind.GoblinBomber, "Goblin Bomber", 4, 1, 1, 1)
        {
            BombCost = 1, BombDamage = 3, BombRange = 3, Abilities = A(Ability.PowderKeg),
            Role = "Lobs a bomb onto a unit 2-3 tiles away; it explodes on that tile at the start of the next enemy turn. Keeps its distance, and blows up when it dies.",
        };

        public static readonly IReadOnlyList<UnitStats> Enemies = new[]
        {
            Wolf, PackLeader, Goblin, GoblinBomber, Cutpurse, Crossbowman, Brute, Elemental, Shard, Skeleton, Necromancer, Ogre, Totem,
        };
    }
}
