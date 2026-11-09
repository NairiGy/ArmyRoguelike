using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    public enum Phase
    {
        Placement,
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat,
    }

    /// <summary>How the pack leader currently affects a wolf.</summary>
    public enum PackState
    {
        None,
        /// <summary>A Pack Leader of its side lives: +1 max HP.</summary>
        Bonded,

        /// <summary>Every Pack Leader of its side is dead: -1 max HP.</summary>
        Leaderless,
    }

    /// <summary>What kind of damage this is; decides which abilities react to it.</summary>
    public enum DamageKind
    {
        /// <summary>A direct attack: triggers Guard, Dodge, Poison, Chill, Thorns and counters.</summary>
        Attack,

        /// <summary>Mines, bombs, fireballs, barricade blasts.</summary>
        Blast,

        /// <summary>Poison and burning ground at the start of a turn.</summary>
        Tick,

        /// <summary>Being pushed into something.</summary>
        Collision,
    }

    /// <summary>A sapper's mine on a tile. Enemies stepping on it set it off.</summary>
    public sealed class Mine
    {
        public readonly GridPos Tile;
        public readonly Unit Owner;
        public readonly int Damage;

        public Mine(GridPos tile, Unit owner, int damage)
        {
            Tile = tile;
            Owner = owner;
            Damage = damage;
        }
    }

    /// <summary>A bomb waiting on a tile. It explodes at the start of the next enemy turn.</summary>
    public sealed class Bomb
    {
        public readonly GridPos Tile;
        public readonly Unit Owner;
        public readonly int Damage;

        public Bomb(GridPos tile, Unit owner, int damage)
        {
            Tile = tile;
            Owner = owner;
            Damage = damage;
        }
    }

    /// <summary>
    /// The rules of one battle. Pure C# with no Unity dependency, so it can be unit-tested and
    /// queried by the AI and the view without side effects. Actions return the events they caused,
    /// in order, including follow-ups such as ability effects and chain explosions.
    /// </summary>
    public sealed class Battle
    {
        /// <summary>Move points per tile walked. Moves are separate from AP.</summary>
        public const int MoveCost = 1;

        /// <summary>Tiles a unit can walk each turn without Swift or Spring Boots.</summary>
        public const int BaseMoves = 1;

        /// <summary>Every unit has 1 AP: one attack or action per turn. Unused AP doesn't carry over.</summary>
        public const int AttackCost = 1;

        public const int MaxCarriedAp = 0;
        public const int PlayerZoneRows = 2;
        /// <summary>Pack Bond: max HP a wolf gains while a leader lives, and loses once the last one is dead.</summary>
        public const int PackBondHp = 1;

        /// <summary>Howl: extra moves for every wolf that hasn't acted yet this turn.</summary>
        public const int HowlMoves = 1;

        // Mines and bombs.
        public const int MineCost = 1;
        public const int MineDamage = 4;
        public const int BigMineDamage = 6;
        public const int MaxMinesPerUnit = 3;
        public const int BigMaxMines = 2;
        public const int PlayerBombRange = 3;
        public const int PlayerBombDamage = 4;

        // Ability numbers (placeholders).
        public const int VampireHeal = 2;
        public const int ThornsDamage = 1;
        public const int PincerBonus = 3;
        public const int PreyBonus = 2;
        public const int KillRefundAp = 1;
        public const int KillRefundMoves = 1;
        public const int PoisonDamage = 2;
        public const int PoisonTurns = 2;
        public const int CollisionDamage = 2;

        /// <summary>The Giant's slam: damage to each of your units within 2 tiles, before they are shoved.</summary>
        public const int SlamDamage = 3;
        public const int TauntRange = 2;

        // Actions (placeholders).
        public const int ActionRange = 3;
        public const int FireballRange = 3;
        public const int FireballDamage = 3;
        public const int BurnRounds = 2;
        public const int BurnDamage = 1;
        public const int IceRounds = 2;
        public const int WardShield = 3;
        public const int MendHeal = 3;
        public const int MaxBarricades = 3;
        public const int BarricadeBlastDamage = 3;

        /// <summary>HP of a barricade: either side can attack it, and it breaks at 0.</summary>
        public const int BarricadeHp = 4;
        public const int ChargeRange = 3;
        public const int ChargeDamage = 2;

        /// <summary>Shield Bash: damage before the push (run-long damage bonuses add to it).</summary>
        public const int BashDamage = 2;

        /// <summary>Shield Up: damage taken off every hit, and how many enemy turns it lasts.</summary>
        public const int ShieldUpReduction = 2;
        public const int ShieldUpTurns = 2;

        /// <summary>Taunt: how many enemy turns it lasts.</summary>
        public const int TauntTurns = 1;

        /// <summary>Battlecry: range, extra moves, shield, and own turns it lasts (the current one counts).</summary>
        public const int BattlecryRange = 2;
        public const int BattlecryMoves = 1;
        public const int BattlecryShield = 2;
        public const int BattlecryTurns = 1;

        /// <summary>Throw Shield, for the rest of the battle.</summary>
        public const int ThrowShieldMoves = 1;
        public const int ThrowShieldDamage = 2;
        public const int ThrowShieldHpCost = 4;

        /// <summary>Pinning Shot (archers): damage, and the target can't move on its next turn.</summary>
        public const int PinDamage = 1;

        /// <summary>The flat damage of Pinning Shot and Suppressing Fire: Planted adds to it.</summary>
        public static int PinShotDamage(Unit unit) => PinDamage + (unit.IsPlanted ? PlantedBonus : 0);

        /// <summary>Vanish (rogues): enemy turns it stays hidden (until your next turn), and the bonus on its first attack while hidden.</summary>
        public const int VanishTurns = 1;

        public const int VanishBonus = 2;

        // Archer/Rogue night pool (placeholders).

        /// <summary>Long Shot: bonus damage against targets at least this many steps away.</summary>
        public const int LongShotBonus = 1;
        public const int LongShotRange = 3;

        /// <summary>Vantage: extra range while next to a rock, ice or barricade.</summary>
        public const int VantageRange = 1;

        /// <summary>Kick: damage before the push (run-long damage bonuses add to it).</summary>
        public const int KickDamage = 1;

        /// <summary>Snare Trap: range, snares per archer, and damage to the enemy that walks onto one (Spiked Snares: more).</summary>
        public const int SnareRange = 3;
        public const int MaxSnares = 2;
        public const int SnareDamage = 1;
        public const int SpikedSnareDamage = 3;

        /// <summary>Cheap Shot: bonus damage against an enemy that can't move.</summary>
        public const int CheapShotBonus = 2;

        /// <summary>Throwing Knife: base damage (in place of the attack's) and range (at least 2).</summary>
        public const int KnifeDamage = 2;
        public const int KnifeRange = 3;

        /// <summary>Shadowstep: teleport range in steps.</summary>
        public const int ShadowstepRange = 3;

        /// <summary>Smoke Bomb: range of the centre tile.</summary>
        public const int SmokeRange = 3;

        /// <summary>Mark Target: extra damage on the next hit of a marked enemy.</summary>
        public const int MarkBonus = 2;

        /// <summary>Isolate: bonus damage against an exposed enemy (no ally of its own next to it).</summary>
        public const int IsolateBonus = 2;

        /// <summary>Into the Shadows: turns between two hides (a kill this turn, then again 3 turns later at the earliest).</summary>
        public const int IntoShadowsCooldown = 3;

        /// <summary>Most extra damage one hit can get from the conditional flat bonuses (Pincer, Prey, Cheap Shot, Long Shot, Isolate, Shatter, ambush).</summary>
        public const int MaxConditionalBonus = 4;

        /// <summary>Blink (mages): teleport range in steps.</summary>
        public const int BlinkRange = 3;

        /// <summary>Fling (goblins): range in steps (at least 2), and the damage a landing goblin deals.</summary>
        public const int FlingRange = 4;

        public const int FlingDamage = 2;

        /// <summary>Powder Keg (goblin bombers): damage to everyone in the 3x3 when it dies.</summary>
        public const int PowderKegDamage = 2;

        /// <summary>Caltrops (cutpurses): damage to a unit of yours stepping on them; its movement ends there.</summary>
        public const int CaltropsDamage = 1;

        /// <summary>Upheaval (elementals): range in steps (ignores line of sight), and damage to the unit it throws.</summary>
        public const int UpheavalRange = 2;

        public const int UpheavalDamage = 2;

        /// <summary>Corpse Blast (Necromancer): damage to everyone in the 3x3 around the bone pile, friend or foe.</summary>
        public const int CorpseBlastDamage = 2;

        // Demons (placeholders, see docs/night/demons.md).

        /// <summary>Blood Offering (imps): Fury given to the nearest other demon within this many steps.</summary>
        public const int OfferingFury = 1;

        public const int OfferingRange = 3;

        /// <summary>Bloodlust (demons): Fury gained per death next to it.</summary>
        public const int BloodlustFury = 1;

        /// <summary>Fury never goes above this (so Rending Blow plus Fortune's Favored stays readable).</summary>
        public const int MaxFury = 3;

        /// <summary>Hellswap (imps): range in steps, no line of sight needed.</summary>
        public const int HellswapRange = 3;

        /// <summary>Rending Blow (demons): HP it pays, and the extra damage of the hit.</summary>
        public const int RendingBlowCost = 2;

        public const int RendingBlowBonus = 2;

        /// <summary>Soul Contract (devils): range in steps (no line of sight needed), and the HP lost when it comes due.</summary>
        public const int ContractRange = 3;

        public const int ContractDamage = 3;

        /// <summary>Fire Breath (fiends): damage to each of your units on the 2 tiles of the line, which then burn.</summary>
        public const int FireBreathDamage = 2;

        public const int FireBreathLength = 2;

        // Omens (placeholders).
        public const int SunDamage = 3;
        public const int MoonShield = 4;

        // ---------- Mage and Sapper night pool (placeholders) ----------

        /// <summary>Firewall: range, and damage to whoever stands in the line when it lights.</summary>
        public const int FirewallRange = 3;
        public const int FirewallDamage = 1;

        /// <summary>Blizzard: range, and damage to each enemy in the plus (they are also slowed).</summary>
        public const int BlizzardRange = 3;
        public const int BlizzardDamage = 1;

        /// <summary>Shatter: extra damage slowed enemies take from every attack of your side.</summary>
        public const int ShatterBonus = 2;

        /// <summary>Transpose: range in steps (ignores line of sight).</summary>
        public const int TransposeRange = 3;

        /// <summary>Place Mine: how far from the sapper a mine can go.</summary>
        public const int MineRange = 2;

        /// <summary>Cave-In: range for dropping rubble.</summary>
        public const int CaveInRange = 3;

        /// <summary>Rubble: extra collision damage for a unit pushed into it (on top of the normal collision); the rubble breaks.</summary>
        public const int CrushDamage = 2;

        /// <summary>Grappling Hook: the target must be this many steps away in a straight line (at least 2).</summary>
        public const int HookRange = 3;

        /// <summary>Demolish: range, and blast damage in the 3x3 around the demolished tile.</summary>
        public const int DemolishRange = 3;
        public const int DemolishDamage = 3;

        /// <summary>Rigged Barricades: blast damage in the 3x3 when one of its barricades breaks.</summary>
        public const int RiggedBlastDamage = 3;

        // Tension pieces (2026-11-09, placeholders).

        /// <summary>Steady Aim+: bonus if the Archer hasn't moved this turn, and the loss if it has.</summary>
        public const int SteadyAimPlusBonus = 3;
        public const int SteadyAimPlusPenalty = 1;

        /// <summary>Planted: extra damage on its shots while planted, and own turns it lasts (the one it was used in counts).</summary>
        public const int PlantedBonus = 2;
        public const int PlantedTurns = 2;

        /// <summary>Lone Wolf: bonus damage while the Rogue itself is exposed.</summary>
        public const int LoneWolfBonus = 2;

        /// <summary>Special actions cost the unit's one AP, except the free ones (Blink, Vanish and others): free, but only before it has acted.</summary>
        public static int ActionCost(ActionKind action) =>
            action == ActionKind.Blink || action == ActionKind.Vanish || action == ActionKind.ThrowShield || action == ActionKind.Shadowstep || action == ActionKind.MarkTarget ||
            action == ActionKind.Planted || action == ActionKind.Overchannel ? 0 : action == ActionKind.PlaceMine ? MineCost : AttackCost;

        /// <summary>
        /// What the action costs this unit: <see cref="ActionCost(ActionKind)"/>, but Quick Fuse makes Place Mine free
        /// (before acting, like Blink). Twincast is separate: see <see cref="TwincastFree"/>.
        /// </summary>
        public static int CostOf(Unit unit, ActionKind action) =>
            action == ActionKind.PlaceMine && unit.Has(Ability.QuickFuse) ? 0 : ActionCost(action);

        /// <summary>Twincast: the unit's first AP-costing action this turn takes no AP (it still stops it moving).</summary>
        public static bool TwincastFree(Unit unit, ActionKind action) =>
            unit.Has(Ability.Twincast) && !unit.TwincastUsed && CostOf(unit, action) > 0;

        /// <summary>The action's cooldown for this unit in this battle: Quick Fuse makes Place Mine ready every turn.</summary>
        public int CooldownFor(Unit unit, ActionKind action)
        {
            var cooldown = action == ActionKind.PlaceMine && unit.Has(Ability.QuickFuse) ? 1 : CooldownIn(action);
            // Hone (a shop service): one turn shorter for this unit, 1 at least; once-per-battle actions stay as they are.
            return cooldown > 1 && unit.Member != null && unit.Member.IsHoned(action) ? cooldown - 1 : cooldown;
        }

        /// <summary>
        /// Cooldown of every action in the unit's own turns *(placeholders)*: used now, ready again this many turns
        /// later. Basic attacks have none.
        /// </summary>
        public static int Cooldown(ActionKind action)
        {
            switch (action)
            {
                case ActionKind.Fireball:
                case ActionKind.IceWall:
                case ActionKind.Mend:
                case ActionKind.Turret:
                case ActionKind.Detonate:
                case ActionKind.Vanish:
                case ActionKind.Raise:
                case ActionKind.Caltrops:
                case ActionKind.ShieldUp:
                case ActionKind.Battlecry:
                case ActionKind.Taunt:
                case ActionKind.FireArrow:
                case ActionKind.SuppressingFire:
                case ActionKind.Shadowstep:
                case ActionKind.Firewall:
                case ActionKind.Blizzard:
                case ActionKind.Demolish:
                case ActionKind.Hellswap:
                case ActionKind.SoulContract:
                case ActionKind.Planted:
                case ActionKind.Overchannel:
                    return 3;
                case ActionKind.SmokeBomb:
                    return 4;
                // Once per battle instead.
                case ActionKind.ThrowShield:
                    return 0;
                default:
                    return 2;
            }
        }

        /// <summary>
        /// Whether the action is off cooldown for the unit's coming or current turn. During your turn an enemy's
        /// cooldowns still tick once before it acts, so one turn left counts as ready (for the AI and its intent).
        /// </summary>
        public bool Ready(Unit unit, ActionKind action) =>
            unit.CooldownLeft(action) <= (unit.Team == Team.Enemy && Phase != Phase.EnemyTurn ? 1 : 0);

        /// <summary>The action's cooldown in this battle: Arcane Overflow cuts it, down to 1.</summary>
        public int CooldownIn(ActionKind action) =>
            Field == Battlefield.ArcaneOverflow && Cooldown(action) > 0 ? Math.Max(1, Cooldown(action) - Battlefields.CooldownCut) : Cooldown(action);

        /// <summary>Pays for an action and puts it on cooldown.</summary>
        private void UseUp(Unit unit, ActionKind action)
        {
            if (TwincastFree(unit, action))
            {
                // Twincast: no AP, but the unit has committed, so it can't move again this turn.
                unit.TwincastUsed = true;
                unit.ActedThisTurn = true;
            }
            else if (CostOf(unit, action) > 0)
                SpendAp(unit, CostOf(unit, action));

            unit.StartCooldown(action, CooldownFor(unit, action));
            unit.CountUse(action);
        }

        public readonly Board Board;

        private readonly List<Unit> _units = new List<Unit>();
        private readonly List<Mine> _mines = new List<Mine>();
        private readonly List<Bomb> _bombs = new List<Bomb>();
        private readonly List<(GridPos tile, Unit owner)> _barricades = new List<(GridPos, Unit)>();
        private readonly Dictionary<GridPos, int> _barricadeHp = new Dictionary<GridPos, int>();
        private readonly Dictionary<GridPos, int> _ice = new Dictionary<GridPos, int>();
        private readonly Dictionary<GridPos, int> _burning = new Dictionary<GridPos, int>();

        // Who set each tile burning and who dropped each caltrop: only for crediting the damage in the playtest log.
        private readonly Dictionary<GridPos, Unit> _burnOwners = new Dictionary<GridPos, Unit>();
        private readonly Dictionary<GridPos, Unit> _caltrops = new Dictionary<GridPos, Unit>();
        private readonly Dictionary<Unit, HashSet<GridPos>> _overwatch = new Dictionary<Unit, HashSet<GridPos>>();

        // The Archer's hidden snares, with the archer that set each one.
        private readonly Dictionary<GridPos, Unit> _snares = new Dictionary<GridPos, Unit>();
        private int _nextId;

        // Ritual fights: where the player deploys (null = the back rows) and enemies still to arrive.
        private HashSet<GridPos> _deployZone;
        private readonly List<(int afterEnemyTurn, UnitStats stats)> _waves = new List<(int, UnitStats)>();

        // Omens: which side each one helps, the tiles a sun beam will strike, and shrouded tiles with the side they protect.
        private readonly List<(Omen omen, Team side)> _omens = new List<(Omen, Team)>();
        private readonly List<GridPos> _sunMarks = new List<GridPos>();
        private readonly Dictionary<GridPos, Team> _shroud = new Dictionary<GridPos, Team>();

        /// <summary>Random picks during the battle (omen targets, where reinforcements arrive).</summary>
        internal SeededRng Rng { get; set; } = new SeededRng(0);

        public IReadOnlyList<Unit> Units => _units;
        public IReadOnlyList<Mine> Mines => _mines;
        public IReadOnlyList<Bomb> Bombs => _bombs;
        public IEnumerable<GridPos> Barricades => _barricades.Select(b => b.tile);
        public IReadOnlyDictionary<GridPos, int> Ice => _ice;
        public IReadOnlyDictionary<GridPos, int> BurningTiles => _burning;

        /// <summary>Bandit caltrops on the board: a unit of yours stepping on them takes 1 and stops.</summary>
        public IReadOnlyCollection<GridPos> Caltrops => _caltrops.Keys;

        /// <summary>The Archer's snares (Snare Trap): the first enemy to walk onto one stops, takes damage and is pinned. Enemies can't see them.</summary>
        public IReadOnlyCollection<GridPos> Snares => _snares.Keys;

        public int SnaresOf(Unit unit) => _snares.Values.Count(o => o == unit);

        /// <summary>Tiles watched by a crossbowman on overwatch (your next unit walking onto one gets shot).</summary>
        public IEnumerable<GridPos> OverwatchTiles => _overwatch.Where(o => o.Key.IsAlive && o.Key.Team == Team.Enemy).SelectMany(o => o.Value).Distinct();

        /// <summary>Tiles your archers cover with Overwatch (CoveringFire in code; the next enemy walking onto one gets shot).</summary>
        public IEnumerable<GridPos> CoverTiles => _overwatch.Where(o => o.Key.IsAlive && o.Key.Team == Team.Player).SelectMany(o => o.Value).Distinct();

        public Phase Phase { get; private set; } = Phase.Placement;

        /// <summary>The player unit that has started acting this turn. Others can't act until it is done.</summary>
        public Unit ActiveUnit { get; private set; }

        public int TurnNumber { get; private set; }

        public bool IsOver => Phase == Phase.Victory || Phase == Phase.Defeat;

        /// <summary>True if the battle was ended with <see cref="DebugWin"/> instead of being played.</summary>
        public bool WasSkipped { get; private set; }

        // ---------- Ritual (defend the objective) ----------

        /// <summary>The unit the player defends (the Moon Shrine), or null in a normal fight.</summary>
        public Unit Objective { get; private set; }

        /// <summary>Enemy turns to survive for the ritual to complete. 0 in a normal fight.</summary>
        public int RitualTurns { get; private set; }

        /// <summary>The fight was won by surviving until the ritual completed.</summary>
        public bool RitualComplete { get; private set; }

        /// <summary>The fight was lost because the objective broke.</summary>
        public bool ObjectiveLost => Phase == Phase.Defeat && Objective != null && !Objective.IsAlive && LivingArmy.Any();

        /// <summary>Enemy reinforcements still to come: after which enemy turn, and what.</summary>
        public IReadOnlyList<(int afterEnemyTurn, UnitStats stats)> PendingWaves => _waves;

        /// <summary>Events from the first player turn's start (omens), which happen inside <see cref="StartBattle"/>.</summary>
        public List<BattleEvent> OpeningEvents { get; } = new List<BattleEvent>();

        /// <summary>
        /// Turns this into a ritual fight: the objective stands on its tile, the player deploys inside the zone,
        /// the fight is won after surviving <paramref name="turns"/> enemy turns, and waves of enemies walk in from
        /// the edges at the end of the given enemy turns.
        /// </summary>
        internal void SetupRitual(UnitStats objective, GridPos tile, IEnumerable<GridPos> deployZone, int turns,
            IEnumerable<(int afterEnemyTurn, UnitStats stats)> waves)
        {
            Objective = new Unit(_nextId++, Team.Player, objective) { Pos = tile, IsPlaced = true, IsDone = true };
            _units.Add(Objective);
            _deployZone = new HashSet<GridPos>(deployZone);
            RitualTurns = turns;
            _waves.AddRange(waves);
        }

        /// <summary>Ritual turns left to survive, counting the coming enemy turn. 0 in a normal fight.</summary>
        public int RitualTurnsLeft => RitualTurns == 0 ? 0 : Math.Max(0, RitualTurns - Math.Max(0, TurnNumber - 1));

        // ---------- Break the Ritual (smash the totems) ----------

        private readonly List<Unit> _totems = new List<Unit>();

        /// <summary>The enemy totems to smash in Break the Ritual (empty in other fights).</summary>
        public IReadOnlyList<Unit> Totems => _totems;

        /// <summary>The summoning completes at the start of this enemy turn unless every totem is smashed. 0 = none.</summary>
        public int SummonTurn { get; private set; }

        /// <summary>The fight was lost because the summoning completed with totems still standing.</summary>
        public bool SummoningComplete { get; private set; }

        /// <summary>The fight was won by smashing every totem in time.</summary>
        public bool TotemsBroken => _totems.Count > 0 && Phase == Phase.Victory && _totems.All(t => !t.IsAlive);

        /// <summary>Your turns left before the summoning completes, counting this one. 0 when there is no summoning.</summary>
        public int SummonTurnsLeft => SummonTurn == 0 ? 0 : Math.Max(0, SummonTurn - TurnNumber + 1);

        /// <summary>
        /// Turns this into Break the Ritual: totems stand on the given tiles, and the fight is lost if any is still
        /// standing at the start of enemy turn <paramref name="summonTurn"/>.
        /// </summary>
        internal void SetupSummoning(UnitStats totem, IEnumerable<GridPos> tiles, int summonTurn)
        {
            foreach (var tile in tiles)
            {
                var unit = new Unit(_nextId++, Team.Enemy, totem) { Pos = tile, IsPlaced = true, IsDone = true };
                _units.Add(unit);
                _totems.Add(unit);
            }

            SummonTurn = summonTurn;
        }

        /// <summary>A scenario fight (Moon Shrine or Break the Ritual) was lost with the army still standing: it costs a curse.</summary>
        public bool ScenarioLost => ObjectiveLost || (SummoningComplete && LivingArmy.Any());

        // ---------- Omens ----------

        /// <summary>Omens in play: each one and the side it helps (the player for a blessing, the enemy for a curse).</summary>
        public IReadOnlyList<(Omen omen, Team side)> Omens => _omens;

        /// <summary>Tiles a sun beam will strike at the start of the enemy turn.</summary>
        public IReadOnlyList<GridPos> SunMarks => _sunMarks;

        /// <summary>Shrouded tiles and the side each one protects from the other side's ranged attacks.</summary>
        public IReadOnlyDictionary<GridPos, Team> Shroud => _shroud;

        /// <summary>Puts an omen in play for this battle, helping <paramref name="side"/>. Call before the battle starts.</summary>
        public void AddOmen(Omen omen, Team side) => _omens.Add((omen, side));

        // ---------- Battlefield ----------

        private readonly List<GridPos> _stormMarks = new List<GridPos>();

        /// <summary>This fight's battlefield rule, or null for none.</summary>
        public Battlefield? Field { get; private set; }

        /// <summary>Sets the battlefield rule. Call before the battle starts.</summary>
        public void SetField(Battlefield? field) => Field = field;

        /// <summary>Tiles lightning will strike at the end of the enemy turn (Storm).</summary>
        public IReadOnlyList<GridPos> StormMarks => _stormMarks;

        /// <summary>What's On His Mind?: enemy intents aren't shown.</summary>
        public bool IntentsHidden => Field == Battlefield.HiddenMinds;

        /// <summary>A push that runs off the board kills (Edge of the World).</summary>
        public bool EdgeKills => Field == Battlefield.EdgeOfTheWorld;

        /// <summary>
        /// Where a push from the attacker's side would send the unit: whether it would fall off the board
        /// (Edge of the World) if pushed along <paramref name="dir"/> now. For the AI and the hover.
        /// </summary>
        public bool WouldFall(Unit unit, GridPos dir, Unit pusher)
        {
            if (!(EdgeKills || RoofCrumbles) || !unit.IsAlive || unit.IsObjective || unit.Stats.IsBoss || unit.Has(Ability.Colossus) || unit.Size > 1)
                return false;
            var p = unit.Pos;
            for (var step = 0; step < PushDistance(unit, pusher); step++)
            {
                var to = p + dir;
                if (!Board.InBounds(to))
                    return EdgeKills;
                if (Board.IsHole(to))
                    return true;
                if (!CanStand(unit, to))
                    return false;
                p = to;
            }

            return false;
        }

        /// <summary>
        /// Edge of the World: the unit drops off the tower. It dies outright (no shield, Guard or Lucky Charm saves
        /// it), leaves no bone pile and its Powder Keg doesn't go off; a Pack Leader's bond still breaks.
        /// </summary>
        private void Fall(Unit unit, Unit pusher, GridPos edge, GridPos dir, List<BattleEvent> events)
        {
            var hp = unit.Hp;
            unit.Hp = 0;
            unit.Fell = true;
            unit.PileGone = true;
            unit.Pos = edge;
            var after = new List<BattleEvent>();
            BreakPackBond(unit, after);
            DemonDeath(unit, after);
            events.Add(new FallEvent(unit, pusher, edge, edge + dir, new Hit(unit, hp, 0)));
            events.AddRange(after);
            CheckEnd();
        }

        // ---------- Crumbling Roof ----------

        private readonly Dictionary<GridPos, int> _floorCracks = new Dictionary<GridPos, int>();

        /// <summary>Crumbling Roof: cracked tiles and how many cracks each has (a hole opens at <see cref="Battlefields.CracksToHole"/>).</summary>
        public IReadOnlyDictionary<GridPos, int> FloorCracks => _floorCracks;

        public bool RoofCrumbles => Field == Battlefield.CrumblingRoof;

        /// <summary>Crumbling Roof: one more crack in the tile; the last one opens a hole, and whoever stands there falls.</summary>
        private void CrackFloor(GridPos p, List<BattleEvent> events)
        {
            if (!RoofCrumbles || !Board.InBounds(p) || Board.IsObstacle(p))
                return;
            _floorCracks.TryGetValue(p, out var cracks);
            cracks++;
            if (cracks < Battlefields.CracksToHole)
            {
                _floorCracks[p] = cracks;
                events.Add(new TileEvent("crack", p, true, null));
                return;
            }

            _floorCracks.Remove(p);
            Board.AddHole(p);
            _mines.RemoveAll(m => m.Tile == p);
            _bombs.RemoveAll(b => b.Tile == p);
            _caltrops.Remove(p);
            foreach (var pile in _units.Where(u => u.IsRemains && u.Pos == p))
                pile.PileGone = true;
            events.Add(new TileEvent("hole", p, true, null));
            var victim = UnitAt(p);
            if (victim != null && victim.IsAlive && victim.Size == 1 && !victim.IsObjective && !victim.Stats.IsBoss && !victim.Has(Ability.Colossus))
                DropIntoHole(victim, null, p, p, events);
        }

        /// <summary>A unit falls through a hole in the roof: like falling off the edge, it dies outright with no bone pile.</summary>
        private void DropIntoHole(Unit unit, Unit pusher, GridPos from, GridPos hole, List<BattleEvent> events)
        {
            var hp = unit.Hp;
            unit.Hp = 0;
            unit.Fell = true;
            unit.PileGone = true;
            unit.Pos = hole;
            var after = new List<BattleEvent>();
            BreakPackBond(unit, after);
            DemonDeath(unit, after);
            events.Add(new FallEvent(unit, pusher, from, hole, new Hit(unit, hp, 0)));
            events.AddRange(after);
            CheckEnd();
        }

        /// <summary>
        /// Start of the player's turn: Storm marks the tile under a random unit of each side, and Fortune's Favored
        /// picks a new favored unit on each side.
        /// </summary>
        private void ApplyField(List<BattleEvent> events)
        {
            if (IsOver)
                return;
            if (Field == Battlefield.Storm)
            {
                foreach (var team in new[] { Team.Player, Team.Enemy })
                {
                    var pool = Living(team).Where(u => !u.IsObjective).ToList();
                    if (pool.Count == 0)
                        continue;
                    var unit = pool[Rng.Next(pool.Count)];
                    var cells = Cells(unit).ToList();
                    var tile = cells[Rng.Next(cells.Count)];
                    if (_stormMarks.Contains(tile))
                        continue;
                    _stormMarks.Add(tile);
                    events.Add(new FieldEvent(Battlefield.Storm, unit, new[] { tile }));
                }
            }

            if (Field == Battlefield.FortunesFavored)
            {
                foreach (var u in _units)
                    u.Favored = false;
                foreach (var team in new[] { Team.Player, Team.Enemy })
                {
                    // A boss is never favored, so a boss fighting alone leaves its side without one.
                    var pool = Living(team).Where(u => !u.IsObjective && !u.Stats.IsBoss && u.Stats.Damage > 0).ToList();
                    if (pool.Count == 0)
                        continue;
                    var unit = pool[Rng.Next(pool.Count)];
                    unit.Favored = true;
                    events.Add(new FieldEvent(Battlefield.FortunesFavored, unit, new GridPos[0]));
                }
            }
        }

        /// <summary>End of the enemy turn (Storm): lightning strikes each marked tile, hurting whoever stands there now.</summary>
        private void StrikeStormMarks(List<BattleEvent> events)
        {
            foreach (var tile in _stormMarks.ToList())
            {
                if (IsOver)
                    break;
                var hits = new List<Hit>();
                var after = new List<BattleEvent>();
                var victim = UnitAt(tile);
                var rocks = _rockfall.Contains(tile);
                if (victim != null)
                    hits.Add(Damage(victim, rocks ? RockfallDamage : Battlefields.StormDamage, null, DamageKind.Blast, after));
                events.Add(new ExplosionEvent(rocks ? "rockfall" : "lightning", null, tile, new[] { tile }, hits, 0));
                events.AddRange(after);
                CheckEnd();
            }

            _stormMarks.Clear();
            _rockfall.Clear();
        }

        // ---------- Champions and boss phases ----------

        /// <summary>Stone Heart: a boss's HP is split into this many phases.</summary>
        public const int BossPhases = 3;

        /// <summary>A boss's extra max HP for each level of the army (all levels added up).</summary>
        public const int BossHpPerLevel = 1;

        /// <summary>Giant, last phase: tiles under this many of your units are marked each round; falling rocks hit them at the end of the enemy turn.</summary>
        public const int RockfallMarks = 2;
        public const int RockfallDamage = 3;

        /// <summary>Necromancer, each new phase: skeletons that climb onto the roof's edge.</summary>
        public const int PhaseSkeletons = 2;

        private readonly HashSet<GridPos> _rockfall = new HashSet<GridPos>();

        /// <summary>The champion of this fight, or null.</summary>
        public Unit Champion => _units.FirstOrDefault(u => u.IsChampion);

        /// <summary>
        /// Makes the toughest enemy (most max HP; not a boss or an objective) this fight's champion, with the affix and
        /// <see cref="Champions.ExtraHp"/> max HP. Call before the battle starts.
        /// </summary>
        public Unit MakeChampion(Affix affix)
        {
            if (affix == Affix.None)
                return null;
            var unit = _units.Where(u => u.Team == Team.Enemy && !u.IsObjective && !u.Stats.IsBoss && u.IsAlive)
                .OrderByDescending(u => u.MaxHp).ThenBy(u => u.Id).FirstOrDefault();
            if (unit == null)
                return null;
            unit.Affix = affix;
            unit.ExtraHp += Champions.ExtraHp;
            unit.Hp = unit.MaxHp;
            return unit;
        }

        /// <summary>Gives the bosses extra max HP (scaled to the army's levels by the run). Call before the battle starts.</summary>
        public void StrengthenBosses(int extraHp)
        {
            foreach (var boss in _units.Where(u => u.Stats.IsBoss && u.IsAlive))
            {
                boss.ExtraHp += extraHp;
                boss.Hp = boss.MaxHp;
            }
        }

        /// <summary>Stone Heart: the HP at which the boss's next phase starts (0 in its last phase).</summary>
        public static int NextPhaseHp(Unit boss) =>
            boss.BossPhase >= BossPhases ? 0 : (int)Math.Ceiling(boss.MaxHp * (BossPhases - boss.BossPhase) / (double)BossPhases);

        /// <summary>Start of your turn: each Stone Heart boss can't drop below its next phase until your next turn.</summary>
        private void SetPhaseFloors()
        {
            foreach (var boss in Living(Team.Enemy).Where(u => u.Has(Ability.StoneHeart)))
                boss.PhaseFloor = NextPhaseHp(boss);
        }

        /// <summary>A boss reached its phase floor: the next phase starts, and the rest of this round's damage stops there.</summary>
        private void EnterPhase(Unit boss, List<BattleEvent> after)
        {
            boss.BossPhase++;
            boss.Enraged = true;
            after.Add(new StatusEvent(boss, boss, StatusKind.Phase, boss.BossPhase));
            if (boss.Has(Ability.SoulHarvest))
                SpawnEdgeUnits(UnitStats.Raised(UnitStats.Skeleton), PhaseSkeletons, after);
        }

        /// <summary>Puts new units of the enemy side on free edge tiles as far as possible from your units. They act from the next enemy turn.</summary>
        private void SpawnEdgeUnits(UnitStats stats, int count, List<BattleEvent> events)
        {
            var arrived = new List<Unit>();
            for (var i = 0; i < count; i++)
            {
                var tile = Board.AllTiles()
                    .Where(p => (p.X == 0 || p.Y == 0 || p.X == Board.Width - 1 || p.Y == Board.Height - 1) && IsFree(p) && !HasHazard(p) && RemainsAt(p) == null)
                    .OrderByDescending(p => Living(Team.Player).Select(u => p.StepsTo(u.Pos)).DefaultIfEmpty(99).Min())
                    .ThenBy(p => p.Y).ThenBy(p => p.X).Cast<GridPos?>().FirstOrDefault();
                if (!tile.HasValue)
                    break;
                var unit = new Unit(_nextId++, Team.Enemy, stats) { Pos = tile.Value, IsPlaced = true, IsDone = true };
                _units.Add(unit);
                arrived.Add(unit);
            }

            if (arrived.Count > 0)
                events.Add(new ReinforceEvent(arrived));
        }

        /// <summary>Giant in its last phase: marks the tiles under some of your units for falling rocks (start of your turn).</summary>
        private void MarkRockfall(List<BattleEvent> events)
        {
            if (IsOver || !Living(Team.Enemy).Any(u => u.Has(Ability.StoneHeart) && u.Has(Ability.Wrath) && u.BossPhase >= BossPhases))
                return;
            var pool = Living(Team.Player).Where(u => !u.IsObjective).OrderBy(_ => Rng.Next(1000)).Take(RockfallMarks).ToList();
            foreach (var unit in pool)
            {
                var tile = unit.Pos;
                if (_stormMarks.Contains(tile))
                    continue;
                _stormMarks.Add(tile);
                _rockfall.Add(tile);
                events.Add(new StatusEvent(unit, unit, StatusKind.Rockfall, 0));
            }
        }

        /// <summary>Whether a marked tile is for falling rocks (Giant) rather than lightning (Storm).</summary>
        public bool IsRockfallMark(GridPos tile) => _rockfall.Contains(tile);

        /// <summary>Fortune's Favored doubles what a favored unit deals to the other side (attacks and blasts, not collisions).</summary>
        private static int FavoredAmount(Unit source, Unit target, int amount, DamageKind kind) =>
            source != null && source.Favored && source.Team != target.Team && (kind == DamageKind.Attack || kind == DamageKind.Blast)
                ? amount * Battlefields.FavoredMultiplier
                : amount;

        // ---------- Banners ----------

        private readonly HashSet<Banner> _banners = new HashSet<Banner>();

        /// <summary>The player's banners in play this battle.</summary>
        public IReadOnlyCollection<Banner> Banners => _banners;

        public bool HasBanner(Banner banner) => _banners.Contains(banner);

        /// <summary>
        /// Puts one of the player's banners in play. Call before the battle starts, after the army is in.
        /// Third Wheel gives the army's units their AP and move; Vampiric brings the Curse of the Sun.
        /// </summary>
        public void AddBanner(Banner banner)
        {
            if (!_banners.Add(banner))
                return;
            if (banner == Banner.ThirdWheel)
            {
                foreach (var u in _units.Where(u => u.Team == Team.Player && u.Member != null))
                {
                    u.BannerAp = Core.Banners.ThirdWheelAp;
                    u.BannerMoves = Core.Banners.ThirdWheelMoves;
                }
            }

            if (banner == Banner.Vampiric && !_omens.Contains((Omen.Sun, Team.Enemy)))
                AddOmen(Omen.Sun, Team.Enemy);
        }

        /// <summary>Marks the unit of this army member as a hired mercenary (Mercenaries banner). Call before the battle starts.</summary>
        public void MarkMercenary(ArmyMember member)
        {
            foreach (var u in _units.Where(u => u.Member == member))
                u.IsMercenary = true;
        }

        /// <summary>A unit on this tile can't be hit from range by the given team: it's shrouded against them.</summary>
        public bool IsShroudedFrom(GridPos tile, Team attackerTeam) => _shroud.TryGetValue(tile, out var side) && side != attackerTeam;

        private bool IsShroudedFrom(Unit target, Team attackerTeam) => Cells(target).Any(c => IsShroudedFrom(c, attackerTeam));

        public Battle(Board board, IEnumerable<ArmyMember> playerArmy, IEnumerable<(UnitStats stats, GridPos pos)> enemies)
        {
            Board = board;
            foreach (var member in playerArmy)
                _units.Add(new Unit(_nextId++, Team.Player, member.Stats, member));
            foreach (var (stats, pos) in enemies)
                _units.Add(new Unit(_nextId++, Team.Enemy, stats) { Pos = pos, IsPlaced = true });
            ApplyPackBond();
        }

        public Battle(Board board, IEnumerable<UnitStats> playerArmy, IEnumerable<(UnitStats stats, GridPos pos)> enemies)
            : this(board, playerArmy.Select(s => new ArmyMember(s)), enemies)
        {
        }

        private Battle(Board board)
        {
            Board = board;
        }

        /// <summary>
        /// A separate copy of this battle in its exact current state, random generator included: the same moves on both
        /// give the same results, and nothing done to one touches the other (for a bot looking ahead). Army members and
        /// unit types are shared, since nothing changes them during a battle; <see cref="OpeningEvents"/> starts empty,
        /// since it describes the original's units. Any state added to Battle or Unit must be copied here too; a test
        /// compares every field.
        /// </summary>
        public Battle Clone()
        {
            var map = new Dictionary<Unit, Unit>(_units.Count);
            var copy = new Battle(Board.Clone());
            foreach (var u in _units)
            {
                var c = u.CopyFor(map);
                map[u] = c;
                copy._units.Add(c);
            }

            foreach (var u in _units)
                map[u].CopyLinks(u, map);

            Unit Mapped(Unit u) => u == null ? null : map[u];

            foreach (var m in _mines)
                copy._mines.Add(new Mine(m.Tile, Mapped(m.Owner), m.Damage));
            foreach (var b in _bombs)
                copy._bombs.Add(new Bomb(b.Tile, Mapped(b.Owner), b.Damage));
            foreach (var b in _barricades)
                copy._barricades.Add((b.tile, Mapped(b.owner)));
            foreach (var hp in _barricadeHp)
                copy._barricadeHp.Add(hp.Key, hp.Value);
            foreach (var ice in _ice)
                copy._ice.Add(ice.Key, ice.Value);
            foreach (var burn in _burning)
                copy._burning.Add(burn.Key, burn.Value);
            foreach (var owner in _burnOwners)
                copy._burnOwners.Add(owner.Key, Mapped(owner.Value));
            foreach (var caltrop in _caltrops)
                copy._caltrops.Add(caltrop.Key, Mapped(caltrop.Value));
            foreach (var watch in _overwatch)
                copy._overwatch.Add(map[watch.Key], new HashSet<GridPos>(watch.Value));
            foreach (var snare in _snares)
                copy._snares.Add(snare.Key, Mapped(snare.Value));
            foreach (var shroud in _shroud)
                copy._shroud.Add(shroud.Key, shroud.Value);
            foreach (var totem in _totems)
                copy._totems.Add(map[totem]);
            copy._waves.AddRange(_waves);
            copy._omens.AddRange(_omens);
            copy._sunMarks.AddRange(_sunMarks);
            copy._stormMarks.AddRange(_stormMarks);
            copy._rockfall.UnionWith(_rockfall);
            foreach (var crack in _floorCracks)
                copy._floorCracks.Add(crack.Key, crack.Value);
            copy._banners.UnionWith(_banners);
            copy._deployZone = _deployZone == null ? null : new HashSet<GridPos>(_deployZone);
            copy._nextId = _nextId;
            copy.Rng = Rng.Clone();
            copy.Phase = Phase;
            copy.ActiveUnit = Mapped(ActiveUnit);
            copy.TurnNumber = TurnNumber;
            copy.WasSkipped = WasSkipped;
            copy.Objective = Mapped(Objective);
            copy.RitualTurns = RitualTurns;
            copy.RitualComplete = RitualComplete;
            copy.SummonTurn = SummonTurn;
            copy.SummoningComplete = SummoningComplete;
            copy.Field = Field;
            copy.BiteChain = BiteChain;
            copy.StolenGold = StolenGold;
            return copy;
        }

        /// <summary>A standalone wolf fight with warrior, archer and rogue.</summary>
        public static Battle CreatePrototype() => Encounter.Wolves.CreateBattle(ArmyMember.StartingArmy());

        // ---------- Footprints and distance ----------

        /// <summary>The tiles covered by a unit of the given size standing at anchor.</summary>
        public static IEnumerable<GridPos> Footprint(GridPos anchor, int size)
        {
            for (var dy = 0; dy < size; dy++)
            for (var dx = 0; dx < size; dx++)
                yield return new GridPos(anchor.X + dx, anchor.Y + dy);
        }

        public static IEnumerable<GridPos> Cells(Unit unit) => Footprint(unit.Pos, unit.Size);

        /// <summary>Fewest orthogonal steps between any tile of one footprint and any tile of a unit.</summary>
        public static int Distance(GridPos anchor, int size, Unit target) =>
            Footprint(anchor, size).Min(a => Cells(target).Min(b => a.StepsTo(b)));

        public static int Distance(Unit a, Unit b) => Distance(a.Pos, a.Size, b);

        /// <summary>The 3x3 area around a tile, clipped to the board.</summary>
        public List<GridPos> BlastArea(GridPos center)
        {
            var area = new List<GridPos>();
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var p = new GridPos(center.X + dx, center.Y + dy);
                if (Board.InBounds(p))
                    area.Add(p);
            }

            return area;
        }

        /// <summary>A tile and its 4 orthogonal neighbours, clipped to the board: fireballs and volleys.</summary>
        public List<GridPos> PlusArea(GridPos center)
        {
            var area = new List<GridPos> { center };
            area.AddRange(center.Neighbours().Where(Board.InBounds));
            return area;
        }

        /// <summary>A bomb hits only the tile it landed on.</summary>
        public List<GridPos> BombArea(GridPos center) => new List<GridPos> { center };

        /// <summary>The tiles an explosion of this cause hits: bombs and mines their own tile, everything else the 3x3.</summary>
        private List<GridPos> ExplosionArea(string cause, GridPos center) => cause == "bomb" || cause == "mine" ? BombArea(center) : BlastArea(center);

        /// <summary>The orthogonal direction from one tile to another in the same row or column, else null.</summary>
        internal static GridPos? StraightDirection(GridPos from, GridPos to)
        {
            if (from == to)
                return null;
            if (from.X == to.X)
                return new GridPos(0, Math.Sign(to.Y - from.Y));
            if (from.Y == to.Y)
                return new GridPos(Math.Sign(to.X - from.X), 0);
            return null;
        }

        // ---------- Queries ----------

        public IEnumerable<Unit> Living(Team team) => _units.Where(u => u.Team == team && u.IsAlive && u.IsPlaced);

        /// <summary>Living army units, not counting summons such as turrets or hired mercenaries.</summary>
        public IEnumerable<Unit> LivingArmy => Living(Team.Player).Where(u => !u.IsSummon && !u.IsMercenary);

        public Unit UnitAt(GridPos p) =>
            _units.FirstOrDefault(u => u.IsPlaced && u.IsAlive && Cells(u).Contains(p));

        /// <summary>The bone pile on a tile, if any.</summary>
        public Unit RemainsAt(GridPos p) => _units.FirstOrDefault(u => u.IsRemains && u.Pos == p);

        public IEnumerable<Unit> Remains(Team team) => _units.Where(u => u.Team == team && u.IsRemains);

        public Mine MineAt(GridPos p) => _mines.FirstOrDefault(m => m.Tile == p);

        public Bomb BombAt(GridPos p) => _bombs.FirstOrDefault(b => b.Tile == p);

        public bool IsBarricade(GridPos p) => _barricades.Any(b => b.tile == p);

        /// <summary>
        /// Rubble (the Sapper's keyword): a low heap that blocks walking and standing like a rock but not shots. A unit
        /// pushed into it is crushed for <see cref="CrushDamage"/> more and the rubble breaks; any unit can clear it with a
        /// basic attack. It doesn't give Behind Cover.
        /// </summary>
        public bool IsRubble(GridPos p) => Board.IsLowObstacle(p);

        public IEnumerable<GridPos> Rubble => Board.AllTiles().Where(IsRubble);

        /// <summary>Leaves rubble on an empty tile (no unit, rock or wall); whatever lay there (mine, bomb, caltrops, fire) is buried.</summary>
        private void AddRubble(GridPos p, Unit source, List<BattleEvent> events)
        {
            if (!Board.InBounds(p) || Board.IsObstacle(p) || UnitAt(p) != null)
                return;
            _mines.RemoveAll(m => m.Tile == p);
            _bombs.RemoveAll(b => b.Tile == p);
            _caltrops.Remove(p);
            _burning.Remove(p);
            _burnOwners.Remove(p);
            Board.AddLowObstacle(p);
            events.Add(new TileEvent("rubble", p, true, source));
        }

        private void ClearRubble(GridPos p, Unit source, List<BattleEvent> events)
        {
            if (!IsRubble(p))
                return;
            Board.RemoveObstacle(p);
            events.Add(new TileEvent("rubble", p, false, source));
        }

        /// <summary>Sets a tile burning for <see cref="BurnRounds"/> rounds (not under a rock or wall).</summary>
        private void SetBurning(GridPos p, Unit owner, List<BattleEvent> events)
        {
            if (!Board.InBounds(p) || Board.IsObstacle(p))
                return;
            _burning[p] = BurnRounds;
            _burnOwners[p] = owner;
            events.Add(new TileEvent("burn", p, true, owner));
        }

        /// <summary>HP left on the barricade at this tile (0 if there is none).</summary>
        public int BarricadeHpAt(GridPos p) => _barricadeHp.TryGetValue(p, out var hp) ? hp : 0;

        /// <summary>A tile with no rock and no living unit. Bone piles, mines and bombs don't block.</summary>
        public bool IsFree(GridPos p) => Board.InBounds(p) && !Board.IsObstacle(p) && UnitAt(p) == null;

        private bool HasHazard(GridPos p) => MineAt(p) != null || BombAt(p) != null || _caltrops.ContainsKey(p) || _snares.ContainsKey(p);

        /// <summary>Whether the unit could stand with its anchor on this tile.</summary>
        public bool CanStand(Unit unit, GridPos anchor)
        {
            foreach (var c in Footprint(anchor, unit.Size))
            {
                if (!Board.InBounds(c) || Board.IsObstacle(c))
                    return false;
                var occupant = UnitAt(c);
                if (occupant != null && occupant != unit)
                    return false;
            }

            return true;
        }

        /// <summary>Whether the unit can walk over this anchor on its way: as standing, but allied units don't block it (objectives do).</summary>
        private bool CanPass(Unit unit, GridPos anchor) =>
            Footprint(anchor, unit.Size).All(c => Board.InBounds(c) && !Board.IsObstacle(c) &&
                                                  (UnitAt(c) is not Unit occupant || occupant == unit || (occupant.Team == unit.Team && !occupant.IsObjective)));

        /// <summary>Where the player may deploy: the back two rows, or the ritual's zone around the objective.</summary>
        public bool IsPlayerZone(GridPos p) => Board.InBounds(p) && (_deployZone != null ? _deployZone.Contains(p) : p.Y < PlayerZoneRows);

        /// <summary>Pack Bond on a wolf: bonded while a leader of its side lives, leaderless once the last one died.</summary>
        public PackState GetPackState(Unit unit)
        {
            if (!unit.IsWolf)
                return PackState.None;
            var leaders = _units.Where(u => u.Team == unit.Team && u.Has(Ability.PackBond)).ToList();
            if (leaders.Count == 0)
                return PackState.None;
            return leaders.Any(l => l.IsAlive) ? PackState.Bonded : PackState.Leaderless;
        }

        /// <summary>Wolf bites landed so far this turn; each Wolf Bite gets +1 damage per earlier one.</summary>
        public int BiteChain { get; private set; }

        /// <summary>Pack Bond at the start: every wolf with a living leader on its side gets +1 max HP.</summary>
        private void ApplyPackBond()
        {
            foreach (var wolf in _units.Where(u => GetPackState(u) == PackState.Bonded))
            {
                wolf.PackHp = PackBondHp;
                wolf.Hp = wolf.MaxHp;
            }
        }

        /// <summary>
        /// The last Pack Leader of a side died: its wolves drop to -1 max HP and lose the difference now
        /// (which can kill them).
        /// </summary>
        private void BreakPackBond(Unit leader, List<BattleEvent> after)
        {
            if (!leader.Has(Ability.PackBond) || _units.Any(u => u.Team == leader.Team && u.Has(Ability.PackBond) && u.IsAlive))
                return;
            foreach (var wolf in Living(leader.Team).Where(u => u.IsWolf).ToList())
            {
                var loss = wolf.PackHp + PackBondHp;
                wolf.PackHp = -PackBondHp;
                wolf.Hp = Math.Max(0, Math.Min(wolf.Hp - loss, wolf.MaxHp));
                after.Add(new TickEvent("leader lost", new Hit(wolf, loss, wolf.Hp)));
            }
        }

        /// <summary>The unit's attack damage before target-specific bonuses.</summary>
        public int EffectiveDamage(Unit unit)
        {
            var damage = unit.BaseDamage + unit.Fury;
            // Steady Aim+ is a bet: more for standing still, less for moving.
            if (unit.Has(Ability.SteadyAimPlus))
                damage += unit.MovedThisTurn ? -SteadyAimPlusPenalty : SteadyAimPlusBonus;
            else if (unit.Has(Ability.SteadyAim) && !unit.MovedThisTurn)
                damage += 1;

            return Math.Max(1, damage);
        }

        /// <summary>Damage of an attack on this particular target: Pincer, Prey, Shatter (any of your units on a slowed enemy) and the opening shot.</summary>
        public int DamageAgainst(Unit attacker, Unit target) => DamageFrom(attacker, target, EffectiveDamage(attacker));

        /// <summary>
        /// The target bonuses on top of a base damage: the conditional flat bonuses (at most
        /// <see cref="MaxConditionalBonus"/> in total), Wolf Bite's chain, then the opening shot's double.
        /// </summary>
        private int DamageFrom(Unit attacker, Unit target, int damage, bool shot = true)
        {
            damage += ConditionalBonus(attacker, target, shot);
            if (attacker.Has(Ability.WolfBite))
                damage += BiteChain;
            if (attacker.Has(Ability.FirstShotDouble) && !attacker.HasStruck(target))
                damage *= 2;
            // Overchannel: the Mage's next damaging spell (its bolt included) deals double.
            if (attacker.Overchanneled)
                damage *= 2;
            return damage;
        }

        /// <summary>
        /// The HP the target would lose if the attacker attacked it now, for the hover preview: its damage against
        /// this target, then the same reductions as <see cref="Damage"/> (Shield Wall, Tough, Dodge, wards, Lucky
        /// Charm), over both strikes of a Double Strike. Changes nothing. A Guard next to the target would take the
        /// hit instead: then the target loses 0 and <paramref name="guard"/> is set.
        /// </summary>
        public int PredictHpLoss(Unit attacker, Unit target, out Unit guard)
        {
            guard = _units.FirstOrDefault(g => g != target && g.Team == target.Team && g.IsPlaced && g.IsAlive &&
                                               g.Has(Ability.Guard) && Distance(g, target) == 1);
            if (guard != null)
                return 0;

            var hp = target.Hp;
            var shield = target.Shield + target.MoonShield + target.CryShield;
            var dodge = target.Has(Ability.Dodge) && !target.DodgeUsed;
            var lucky = target.Has(Ability.LuckyCharm) && !target.LastStandUsed;
            var strikes = attacker.Has(Ability.DoubleStrike) ? 2 : 1;
            for (var i = 0; i < strikes && hp > 0; i++)
            {
                var amount = DamageAgainst(attacker, target);
                // The opening shot only doubles the first strike.
                if (i > 0 && attacker.Has(Ability.FirstShotDouble) && !attacker.HasStruck(target))
                    amount /= 2;
                amount = FavoredAmount(attacker, target, amount, DamageKind.Attack);
                if (i == 0 && target.Marked && amount > 0)
                    amount += MarkBonus;
                if (IsShielded(target))
                    amount = Math.Max(1, amount - 1);
                if (target.Has(Ability.Tough))
                    amount = Math.Max(1, amount - 1);
                if (target.Affix == Affix.Armored)
                    amount = Math.Max(1, amount - Champions.ArmoredReduction);
                if (target.ShieldUpTurns > 0)
                    amount = Math.Max(1, amount - ShieldUpReduction);
                if (dodge)
                {
                    dodge = false;
                    amount = 0;
                }

                var absorbed = Math.Min(shield, amount);
                shield -= absorbed;
                hp -= amount - absorbed;
                if (hp <= 0 && lucky)
                {
                    hp = 1;
                    lucky = false;
                }
            }

            return target.Hp - Math.Max(0, hp);
        }

        /// <summary>The enemies the unit's basic attack would kill, here or after walking to a spot it can reach.</summary>
        public List<Unit> AttackKills(Unit unit) =>
            Living(Team.Enemy).Where(e => FindAttackSpot(unit, e).HasValue && PredictHpLoss(unit, e, out _) is var loss && loss > 0 && loss >= e.Hp).ToList();

        /// <summary>The units this action would kill if used on the tile now: Pinning Shot and Fireball, the actions that deal damage at once.</summary>
        public List<Unit> ActionKills(Unit unit, ActionKind action, GridPos tile)
        {
            var kills = new List<Unit>();
            if (!CanUseAction(unit, action, tile))
                return kills;
            if (action == ActionKind.PinningShot && FixedHitKills(UnitAt(tile), PinShotDamage(unit), unit, DamageKind.Attack))
                kills.Add(UnitAt(tile));
            var target = UnitAt(tile);
            if ((action == ActionKind.Kick && FixedHitKills(target, ActionDamage(unit, target, KickDamage, shot: false), unit, DamageKind.Attack)) ||
                (action == ActionKind.ThrowingKnife && FixedHitKills(target, ActionDamage(unit, target, KnifeDamage), unit, DamageKind.Attack)) ||
                (action == ActionKind.FireArrow && FixedHitKills(target, DamageAgainst(unit, target), unit, DamageKind.Attack)) ||
                (action == ActionKind.Rupture && (FixedHitKills(target, target.PoisonTurns * PoisonDamage, unit, DamageKind.Tick) ||
                                                   FixedHitKills(target, target.PoisonTurns * PoisonDamage + DamageAgainst(unit, target), unit, DamageKind.Attack))))
                kills.Add(target);
            if (action == ActionKind.SuppressingFire)
                kills.AddRange(SuppressLine(unit, tile).Select(UnitAt).Where(u => u != null && u.Team != unit.Team && !u.IsObjective).Distinct()
                    .Where(u => FixedHitKills(u, PinShotDamage(unit), unit, DamageKind.Attack)));
            if (action == ActionKind.Fireball)
            {
                var area = PlusArea(tile);
                kills.AddRange(_units.Where(u => u.IsPlaced && u.IsAlive && Cells(u).Any(area.Contains) && FixedHitKills(u, SpellDamage(unit, FireballDamage), unit, DamageKind.Blast)));
            }

            return kills;
        }

        /// <summary>Whether a hit of a set amount would kill the target, by the same steps as <see cref="Damage"/>.</summary>
        private bool FixedHitKills(Unit target, int amount, Unit source, DamageKind kind)
        {
            if (kind == DamageKind.Attack && source.Team != target.Team)
            {
                if (target.Has(Ability.Dodge) && !target.DodgeUsed)
                    return false;
                if (_units.Any(g => g != target && g.Team == target.Team && g.IsPlaced && g.IsAlive && g.Has(Ability.Guard) && Distance(g, target) == 1))
                    return false;
            }

            if ((target.Has(Ability.LuckyCharm) && !target.LastStandUsed) || (target.IsObjective && target.Team == Team.Enemy && source.Team == target.Team))
                return false;
            amount = FavoredAmount(source, target, amount, kind);
            if (kind != DamageKind.Tick && target.Marked && source.Team != target.Team)
                amount += MarkBonus;
            if (IsShielded(target))
                amount = Math.Max(1, amount - 1);
            if (target.Has(Ability.Tough))
                amount = Math.Max(1, amount - 1);
            if (target.Affix == Affix.Armored)
                amount = Math.Max(1, amount - Champions.ArmoredReduction);
            if (target.ShieldUpTurns > 0)
                amount = Math.Max(1, amount - ShieldUpReduction);
            return amount - target.Shield - target.MoonShield - target.CryShield >= target.Hp;
        }

        /// <summary>Exposed (the Rogue's keyword): no living unit of its own side stands next to it.</summary>
        public bool IsExposed(Unit unit) =>
            !_units.Any(u => u != unit && u.IsPlaced && u.IsAlive && u.Team == unit.Team && Distance(u, unit) == 1);

        /// <summary>An ally of the attacker stands on the far side of a 1x1 target.</summary>
        public bool IsPinched(Unit attacker, Unit target)
        {
            if (target.Size != 1 || attacker.Size != 1)
                return false;
            var dir = StraightDirection(attacker.Pos, target.Pos);
            if (dir == null || attacker.Pos.StepsTo(target.Pos) != 1)
                return false;
            var ally = UnitAt(target.Pos + dir.Value);
            return ally != null && ally.Team == attacker.Team;
        }

        /// <summary>True if a living ally with Shield Wall stands next to this unit.</summary>
        public bool IsShielded(Unit unit) =>
            _units.Any(w => w != unit && w.Team == unit.Team && w.IsPlaced && w.IsAlive && w.Has(Ability.ShieldWall) && Distance(w, unit) == 1);

        /// <summary>
        /// True if the unit, standing with its anchor here, is next to a living enemy (inside its reach).
        /// Objectives (shrine, totems) can't attack, so they have no reach.
        /// </summary>
        public bool InEnemyReach(Unit unit, GridPos anchor) => EngagedBy(unit, anchor).Any();

        /// <summary>The enemies whose reach holds the unit standing here: stepping out of their reach uses up its remaining moves.</summary>
        public IEnumerable<Unit> EngagedBy(Unit unit, GridPos anchor) => unit.Has(Ability.Elusive) ? Enumerable.Empty<Unit>() :
            _units.Where(u => u.IsPlaced && u.IsAlive && u.Team != unit.Team && !u.IsObjective && Distance(anchor, unit.Size, u) == 1);

        /// <summary>
        /// Tiles inside the reach of the unit's enemies (next to them), where stepping out again ends its movement.
        /// Rocks and occupied tiles are left out.
        /// </summary>
        public HashSet<GridPos> EnemyReachTiles(Unit unit)
        {
            var tiles = new HashSet<GridPos>();
            foreach (var e in _units.Where(u => u.IsPlaced && u.IsAlive && u.Team != unit.Team && !u.IsObjective))
            foreach (var n in Cells(e).SelectMany(c => c.Neighbours()))
                if (IsFree(n) || UnitAt(n) == unit)
                    tiles.Add(n);
            return tiles;
        }

        /// <summary>
        /// Reachable tiles whose path steps out of an enemy's reach while moves would be left over, so getting
        /// there ends the unit's movement early (zone of control).
        /// </summary>
        public HashSet<GridPos> CostlyMoveTiles(Unit unit) =>
            new HashSet<GridPos>(GetReachable(unit).Keys.Where(t => FindPath(unit, t) is List<GridPos> path && PathCost(unit, path) > path.Count));

        /// <summary>
        /// Moves for one step, given the moves already spent this turn. Zone of control: stepping out of a
        /// tile next to an enemy uses up all the unit's remaining moves.
        /// </summary>
        public int StepCost(Unit unit, GridPos from, int spent) =>
            InEnemyReach(unit, from) ? Math.Max(MoveCost, unit.Moves - spent) : MoveCost;

        /// <summary>Total moves for walking a path from the unit's tile (path excludes the start).</summary>
        public int PathCost(Unit unit, IReadOnlyList<GridPos> path)
        {
            var cost = 0;
            var from = unit.Pos;
            foreach (var p in path)
            {
                cost += StepCost(unit, from, cost);
                from = p;
            }

            return cost;
        }

        /// <summary>How many steps of a path leave an enemy's reach.</summary>
        public int ZocSteps(Unit unit, IReadOnlyList<GridPos> path)
        {
            var count = 0;
            var from = unit.Pos;
            foreach (var p in path)
            {
                if (InEnemyReach(unit, from))
                    count++;
                from = p;
            }

            return count;
        }

        /// <summary>
        /// Pinned or slowed on its own turn: Blink and Shadowstep are blocked (a pinned unit can't move; for a slowed
        /// one they would be a move and an action at once).
        /// </summary>
        public static bool IsHeld(Unit unit) => unit.RootedThisTurn || unit.SlowedThisTurn;

        /// <summary>
        /// Immobile and pinned units can't move, and nobody moves again after spending AP that turn (Light Fingers
        /// excepted, but never while slowed: a slowed unit moves or acts, not both).
        /// </summary>
        public bool CanMove(Unit unit) => !unit.Has(Ability.Immobile) && !unit.RootedThisTurn &&
            (!unit.ActedThisTurn || (unit.Has(Ability.LightFingers) && !unit.SlowedThisTurn));

        /// <summary>Pays for an attack or action. Once a unit has spent AP it can't move for the rest of the turn.</summary>
        private static void SpendAp(Unit unit, int cost)
        {
            unit.Ap -= cost;
            unit.ActedThisTurn = true;
            if (unit.SlowedThisTurn)
                unit.Moves = 0;
        }

        /// <summary>
        /// Cheapest move cost to every reachable anchor tile (Dijkstra, since steps cost different amounts),
        /// with the parent of each tile for rebuilding the path. Stops at <paramref name="budget"/>.
        /// </summary>
        private (Dictionary<GridPos, int> cost, Dictionary<GridPos, GridPos> parent) CheapestRoutes(Unit unit, int budget)
        {
            var cost = new Dictionary<GridPos, int> { [unit.Pos] = 0 };
            var parent = new Dictionary<GridPos, GridPos>();
            var open = new List<GridPos> { unit.Pos };
            var closed = new HashSet<GridPos>();
            while (open.Count > 0)
            {
                // The board is tiny, so a linear scan for the cheapest open tile is plenty.
                var p = open[0];
                foreach (var o in open)
                    if (cost[o] < cost[p])
                        p = o;
                open.Remove(p);
                if (!closed.Add(p))
                    continue;

                foreach (var n in p.Neighbours())
                {
                    if (closed.Contains(n) || !CanPass(unit, n))
                        continue;
                    var c = cost[p] + StepCost(unit, p, cost[p]);
                    if (c > budget || (cost.TryGetValue(n, out var old) && old <= c))
                        continue;
                    cost[n] = c;
                    parent[n] = p;
                    open.Add(n);
                }
            }

            return (cost, parent);
        }

        /// <summary>
        /// Every anchor tile the unit can walk to with its remaining moves, with the move cost. Excludes its own tile.
        /// Enemies never stop on a storm mark or on burning ground.
        /// </summary>
        public Dictionary<GridPos, int> GetReachable(Unit unit)
        {
            if (!CanMove(unit))
                return new Dictionary<GridPos, int>();
            var routes = CheapestRoutes(unit, unit.Moves).cost;
            foreach (var anchor in routes.Keys.Where(a => !CanStand(unit, a)).ToList())
                routes.Remove(anchor);
            routes.Remove(unit.Pos);
            if (unit.Team == Team.Enemy && _stormMarks.Count > 0)
                foreach (var anchor in routes.Keys.Where(a => Footprint(a, unit.Size).Any(_stormMarks.Contains)).ToList())
                    routes.Remove(anchor);
            // Burning ground (the Mage's keyword): enemies walk through it but never stop on it.
            if (unit.Team == Team.Enemy && _burning.Count > 0 && !unit.Has(Ability.Hellborn))
                foreach (var anchor in routes.Keys.Where(a => Footprint(a, unit.Size).Any(_burning.ContainsKey)).ToList())
                    routes.Remove(anchor);
            return routes;
        }

        /// <summary>The unit stands on a tile lightning will strike (Storm).</summary>
        public bool OnStormMark(Unit unit) => _stormMarks.Count > 0 && Cells(unit).Any(_stormMarks.Contains);

        /// <summary>Cheapest path to an anchor tile within the unit's remaining moves, without the start tile. Null if unreachable.</summary>
        public List<GridPos> FindPath(Unit unit, GridPos target)
        {
            var (cost, parent) = CheapestRoutes(unit, unit.Moves);
            if (!cost.ContainsKey(target) || target == unit.Pos || !CanStand(unit, target))
                return null;
            var path = new List<GridPos>();
            for (var c = target; c != unit.Pos; c = parent[c])
                path.Add(c);
            path.Reverse();
            return path;
        }

        /// <summary>Vantage: a ranged 1x1 unit with it standing next to a rock, ice or barricade.</summary>
        public bool HasVantage(Unit unit, GridPos from) =>
            unit.Has(Ability.Vantage) && unit.IsRanged && unit.Size == 1 && from.Neighbours().Any(n => Board.InBounds(n) && Board.IsRock(n));

        /// <summary>The unit's attack range standing here: Vantage adds 1 next to a rock, ice or barricade.</summary>
        public int RangeFrom(Unit unit, GridPos from) => unit.Range + (HasVantage(unit, from) ? VantageRange : 0);

        /// <summary>Whether the attacker's basic attack could hit the target from a given anchor, ignoring AP.</summary>
        public bool CanHitFrom(Unit attacker, GridPos from, Unit target)
        {
            var steps = Distance(from, attacker.Size, target);
            if (steps < 1 || steps > RangeFrom(attacker, from))
                return false;

            if (attacker.Has(Ability.LongReach))
            {
                // Straight line only, and a rock in between blocks the second tile.
                foreach (var cell in Cells(target))
                {
                    var dir = StraightDirection(from, cell);
                    if (dir == null || from.StepsTo(cell) > attacker.Range)
                        continue;
                    if (from.StepsTo(cell) == 1 || !Board.IsRock(from + dir.Value))
                        return true;
                }

                return false;
            }

            if (attacker.IsRanged && steps > 1 && InCover(target) && !HasVantage(attacker, from))
                return false;
            if (attacker.IsRanged && steps > 1 && IsShroudedFrom(target, attacker.Team))
                return false;
            if (!attacker.IsRanged || attacker.Has(Ability.ArcaneBolt))
                return true;
            foreach (var a in Footprint(from, attacker.Size))
            foreach (var b in Cells(target))
                if (Board.HasLineOfSight(a, b))
                    return true;
            return false;
        }

        public bool CanAttack(Unit attacker, Unit target) =>
            !IsOver && attacker.IsAlive && target != null && target.IsAlive && attacker.Team != target.Team &&
            attacker.Ap >= AttackCost && !target.IsHidden && CanHitFrom(attacker, attacker.Pos, target);

        /// <summary>
        /// The cheapest tile from which the unit can walk up and attack the target, with its move cost.
        /// Ties go to the tile next to the fewest other enemies. Null if out of reach this turn or short of
        /// AP. Returns the unit's own tile at cost 0 if it can already attack.
        /// </summary>
        public (GridPos tile, int cost)? FindAttackSpot(Unit unit, Unit target)
        {
            if (IsOver || target == null || !target.IsAlive || target.Team == unit.Team)
                return null;
            if (CanAttack(unit, target))
                return (unit.Pos, 0);
            if (unit.Ap < AttackCost)
                return null;

            return GetReachable(unit)
                .Where(kv => CanHitFrom(unit, kv.Key, target))
                .OrderBy(kv => kv.Value)
                .ThenBy(kv => _units.Count(u => u.IsPlaced && u.IsAlive && u.Team != unit.Team && Distance(kv.Key, unit.Size, u) == 1))
                .ThenBy(kv => kv.Key.Y)
                .ThenBy(kv => kv.Key.X)
                .Select(kv => ((GridPos, int)?)(kv.Key, kv.Value))
                .FirstOrDefault();
        }

        /// <summary>Whether a player unit that hasn't finished could still move, attack or use an action this turn.</summary>
        public bool CanStillAct(Unit unit)
        {
            if (!unit.IsAlive || unit.IsDone || unit.Team != Team.Player || unit.IsObjective)
                return false;
            if (unit.Ap >= AttackCost && Living(Team.Enemy).Any(e => CanAttack(unit, e)))
                return true;
            if (unit.Actions.Any(a => ActionTargets(unit, a).Any()))
                return true;
            return GetReachable(unit).Count > 0;
        }

        // ---------- Special actions ----------

        public int MinesOf(Unit unit) => _mines.Count(m => m.Owner == unit);

        public static int MineDamageFor(Unit owner) => owner != null && owner.Has(Ability.BigMines) ? BigMineDamage : MineDamage;

        public static int MaxMinesFor(Unit owner) => (owner.Has(Ability.BigMines) ? BigMaxMines : MaxMinesPerUnit) + (owner.Member?.LevelMines ?? 0);

        public bool CanPlaceMine(Unit unit, GridPos tile) => CanUseAction(unit, ActionKind.PlaceMine, tile);

        public List<BattleEvent> PlayerPlaceMine(Unit unit, GridPos tile) => PlayerUseAction(unit, ActionKind.PlaceMine, tile);

        /// <summary>
        /// For Charge: whether the unit can rush in a straight line to this tile, up to <see cref="ChargeRange"/> away.
        /// Rocks and objectives of its own side block the way, it can't end on an ally, and at least one enemy must be
        /// on the way.
        /// </summary>
        public bool CanChargeTo(Unit unit, GridPos tile)
        {
            var dir = StraightDirection(unit.Pos, tile);
            var steps = unit.Pos.StepsTo(tile);
            if (dir == null || unit.Size != 1 || steps < 1 || steps > ChargeRange)
                return false;
            var enemies = 0;
            var p = unit.Pos;
            for (var step = 1; step <= steps; step++)
            {
                p += dir.Value;
                if (!Board.InBounds(p) || Board.IsObstacle(p))
                    return false;
                var occupant = UnitAt(p);
                if (occupant == null)
                    continue;
                if (occupant.Team != unit.Team)
                    enemies++;
                else if (occupant.IsObjective || step == steps)
                    return false;
            }

            return enemies > 0;
        }

        public bool CanUseAction(Unit unit, ActionKind action, GridPos tile)
        {
            var cost = CostOf(unit, action);
            if (IsOver || !unit.IsAlive || !unit.HasAction(action) || (unit.Ap < cost && !TwincastFree(unit, action)) || (cost == 0 && unit.ActedThisTurn) || !Ready(unit, action) || !Board.InBounds(tile))
                return false;
            // Slowed: once it has walked, no action that costs AP (Twincast's free one included).
            if (unit.SlowedThisTurn && unit.MovedThisTurn && cost > 0)
                return false;
            var target = UnitAt(tile);
            var steps = Distance(tile, 1, unit);
            switch (action)
            {
                case ActionKind.PlaceMine:
                    return MinesOf(unit) < MaxMinesFor(unit) && steps >= 1 && steps <= MineRange && IsFree(tile) && !HasHazard(tile);
                case ActionKind.FrostNova:
                    return tile == unit.Pos && AdjacentFoes(unit).Any(u => !u.IsObjective && !u.IsHidden);
                case ActionKind.Firewall:
                    return steps >= 1 && steps <= FirewallRange && !Board.IsObstacle(tile) && !IsShroudedFrom(tile, unit.Team);
                case ActionKind.Blizzard:
                    return steps >= 1 && steps <= BlizzardRange && !IsShroudedFrom(tile, unit.Team);
                case ActionKind.Transpose:
                    return target != null && target != unit && unit.Size == 1 && target.Size == 1 && steps <= TransposeRange &&
                           !target.IsObjective && !target.Has(Ability.Colossus) && (!target.IsHidden || target.Team == unit.Team);
                case ActionKind.CaveIn:
                    return steps >= 1 && steps <= CaveInRange && IsFree(tile) && !HasHazard(tile) && RemainsAt(tile) == null;
                case ActionKind.Hook:
                    return HookLine(unit, target).HasValue;
                case ActionKind.Demolish:
                    return steps >= 1 && steps <= DemolishRange && Board.IsObstacle(tile);
                case ActionKind.ThrowBomb:
                    return steps >= 2 && steps <= PlayerBombRange && !Board.IsObstacle(tile) && BombAt(tile) == null &&
                           !IsShroudedFrom(tile, unit.Team);
                case ActionKind.PinningShot:
                    return target != null && target.Team != unit.Team && !target.IsObjective && CanHitFrom(unit, unit.Pos, target);
                case ActionKind.ShieldBash:
                    return target != null && target.Team != unit.Team && !target.IsObjective && unit.Size == 1 && Distance(unit, target) == 1;
                case ActionKind.Sweep:
                    return target != null && target.Team != unit.Team && unit.Size == 1 && Distance(unit, target) == 1;
                case ActionKind.ShieldUp:
                case ActionKind.Battlecry:
                case ActionKind.Taunt:
                    return tile == unit.Pos;
                case ActionKind.ThrowShield:
                    return tile == unit.Pos && !unit.ShieldThrown && unit.Hp > ThrowShieldHpCost;
                case ActionKind.Vanish:
                    return tile == unit.Pos && !unit.IsHidden;
                case ActionKind.Blink:
                    return unit.Size == 1 && !IsHeld(unit) && steps >= 1 && steps <= BlinkRange && CanStand(unit, tile);
                case ActionKind.Fireball:
                    return steps >= 1 && steps <= FireballRange && !IsShroudedFrom(tile, unit.Team);
                case ActionKind.IceWall:
                    return steps >= 1 && steps <= ActionRange && IsFree(tile) && !HasHazard(tile) && RemainsAt(tile) == null;
                case ActionKind.Ward:
                    return target != null && target.Team == unit.Team && Distance(unit, target) <= ActionRange;
                case ActionKind.Mend:
                    return target != null && target.Team == unit.Team && Distance(unit, target) <= ActionRange && target.Hp < target.MaxHp;
                case ActionKind.Barricade:
                    return _barricades.Count(b => b.owner == unit) < MaxBarricades && steps == 1 && IsFree(tile) && !HasHazard(tile);
                case ActionKind.Turret:
                    return steps == 1 && IsFree(tile) && !_units.Any(u => u.Summoner == unit && u.IsAlive);
                case ActionKind.Detonate:
                    return tile == unit.Pos && _barricades.Any(b => b.owner == unit);
                case ActionKind.Charge:
                    return CanChargeTo(unit, tile);
                case ActionKind.Kick:
                    return target != null && target.Team != unit.Team && !target.IsObjective && unit.Size == 1 && Distance(unit, target) == 1;
                case ActionKind.SnareTrap:
                    return SnaresOf(unit) < MaxSnares && steps >= 1 && steps <= SnareRange && IsFree(tile) && !HasHazard(tile);
                case ActionKind.FireArrow:
                    return target != null && target.Team != unit.Team && !target.IsHidden && CanHitFrom(unit, unit.Pos, target);
                case ActionKind.CoveringFire:
                    return tile == unit.Pos;
                case ActionKind.SuppressingFire:
                    return SuppressLine(unit, tile) is List<GridPos> line && line.Any(p => UnitAt(p) is Unit u && u.Team != unit.Team && !u.IsObjective);
                case ActionKind.ThrowingKnife:
                    return target != null && target.Team != unit.Team && !target.IsHidden && CanThrowKnife(unit, target);
                case ActionKind.Shadowstep:
                    return unit.Size == 1 && !IsHeld(unit) && !unit.MovedThisTurn && steps >= 1 && steps <= ShadowstepRange && CanStand(unit, tile) &&
                           _units.Any(e => e.IsPlaced && e.IsAlive && e.Team != unit.Team && Distance(tile, 1, e) == 1);
                case ActionKind.SmokeBomb:
                    return target != null && target.Team == unit.Team && !target.IsObjective && Distance(unit, target) <= SmokeRange;
                case ActionKind.Rupture:
                    return target != null && target.Team != unit.Team && target.PoisonTurns > 0 && unit.Size == 1 && Distance(unit, target) == 1;
                case ActionKind.MarkTarget:
                    return target != null && target.Team != unit.Team && !target.IsHidden && !target.Marked && CanHitFrom(unit, unit.Pos, target);
                case ActionKind.Planted:
                    return tile == unit.Pos && !unit.IsPlanted;
                case ActionKind.Overchannel:
                    return tile == unit.Pos && !unit.Overchanneled;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Suppressing Fire: the row or column from the unit through this tile, up to its range and stopped by rocks, if
        /// the tile is on it; else null.
        /// </summary>
        public List<GridPos> SuppressLine(Unit unit, GridPos tile)
        {
            var dir = StraightDirection(unit.Pos, tile);
            if (dir == null || unit.Size != 1)
                return null;
            var line = new List<GridPos>();
            var range = RangeFrom(unit, unit.Pos);
            for (var p = unit.Pos + dir.Value; Board.InBounds(p) && !Board.IsRock(p) && unit.Pos.StepsTo(p) <= range; p += dir.Value)
                line.Add(p);
            return line.Contains(tile) ? line : null;
        }

        /// <summary>Throwing Knife: an enemy 2 to <see cref="KnifeRange"/> steps away, in line of sight, not in cover or shrouded.</summary>
        public bool CanThrowKnife(Unit unit, Unit target)
        {
            var steps = Distance(unit, target);
            return steps >= 2 && steps <= KnifeRange && !InCover(target) && !IsShroudedFrom(target, unit.Team) &&
                   Cells(unit).Any(a => Cells(target).Any(b => Board.HasLineOfSight(a, b)));
        }

        /// <summary>Every tile the action could be used on right now.</summary>
        public List<GridPos> ActionTargets(Unit unit, ActionKind action) =>
            Board.AllTiles().Where(p => CanUseAction(unit, action, p)).ToList();

        public List<BattleEvent> PlayerUseAction(Unit unit, ActionKind action, GridPos tile)
        {
            if (!CanAct(unit) || !CanUseAction(unit, action, tile))
                return null;
            ActiveUnit = unit;
            var events = new List<BattleEvent>();
            var twincast = TwincastFree(unit, action);
            UseUp(unit, action);
            if (twincast)
                events.Add(new StatusEvent(unit, unit, StatusKind.Twincast, 0));
            switch (action)
            {
                case ActionKind.PlaceMine:
                    _mines.Add(new Mine(tile, unit, MineDamageFor(unit)));
                    events.Add(new PlaceMineEvent(unit, tile));
                    break;
                case ActionKind.ThrowBomb:
                    _bombs.Add(new Bomb(tile, unit, PlayerBombDamage));
                    events.Add(new BombThrowEvent(unit, tile));
                    break;
                case ActionKind.PinningShot:
                {
                    var after = new List<BattleEvent>();
                    var hit = Damage(UnitAt(tile), PinShotDamage(unit), unit, DamageKind.Attack, after);
                    events.Add(new AttackEvent(unit, hit.Target, hit.Damage, hit.HpAfter));
                    events.AddRange(after);
                    if (hit.Target.IsAlive && !hit.Target.Has(Ability.Colossus))
                    {
                        hit.Target.SnareTurns = 1;
                        events.Add(new StatusEvent(unit, hit.Target, StatusKind.Pin, 1));
                    }

                    break;
                }
                case ActionKind.ShieldBash:
                {
                    var target = UnitAt(tile);
                    var dir = StraightDirection(unit.Pos, Cells(target).First(c => c.StepsTo(unit.Pos) == 1)).Value;
                    var after = new List<BattleEvent>();
                    var hit = Damage(target, ActionDamage(unit, target, BashDamage), unit, DamageKind.Attack, after);
                    unit.AttackedThisTurn = true;
                    unit.MarkStruck(target);
                    events.Add(new AttackEvent(unit, hit.Target, hit.Damage, hit.HpAfter));
                    events.AddRange(after);
                    Push(target, dir, unit, events, hurtsBlocker: true);
                    break;
                }
                case ActionKind.Sweep:
                    Sweep(unit, UnitAt(tile), events);
                    break;
                case ActionKind.ShieldUp:
                    unit.ShieldUpTurns = ShieldUpTurns;
                    events.Add(new StatusEvent(unit, unit, StatusKind.ShieldUp, ShieldUpReduction));
                    break;
                case ActionKind.Taunt:
                    unit.TauntTurns = TauntTurns;
                    events.Add(new StatusEvent(unit, unit, StatusKind.Taunt, TauntRange));
                    break;
                case ActionKind.Battlecry:
                    foreach (var ally in Living(unit.Team).Where(u => !u.IsObjective && Distance(unit, u) <= BattlecryRange).ToList())
                    {
                        // A second shout refreshes the bonus rather than stacking it.
                        if (ally.BattlecryTurns == 0 && !(ally.SlowedThisTurn && ally.ActedThisTurn))
                            ally.Moves += BattlecryMoves;
                        ally.BattlecryTurns = BattlecryTurns;
                        ally.CryShield = BattlecryShield;
                        events.Add(new StatusEvent(unit, ally, StatusKind.Battlecry, BattlecryShield));
                    }

                    break;
                case ActionKind.ThrowShield:
                    unit.ShieldThrown = true;
                    unit.Hp -= ThrowShieldHpCost;
                    if (!(unit.SlowedThisTurn && unit.ActedThisTurn))
                        unit.Moves += ThrowShieldMoves;
                    events.Add(new StatusEvent(unit, unit, StatusKind.ThrowShield, ThrowShieldHpCost));
                    break;
                case ActionKind.Vanish:
                    unit.HiddenTurns = VanishTurns;
                    unit.AmbushUsed = false;
                    events.Add(new StatusEvent(unit, unit, StatusKind.Vanish, VanishTurns));
                    break;
                case ActionKind.Kick:
                {
                    var target = UnitAt(tile);
                    var dir = StraightDirection(unit.Pos, Cells(target).First(c => c.StepsTo(unit.Pos) == 1)).Value;
                    ActionHit(unit, target, ActionDamage(unit, target, KickDamage, shot: false), events, "kick");
                    Push(target, dir, unit, events);
                    break;
                }
                case ActionKind.SnareTrap:
                    _snares[tile] = unit;
                    events.Add(new TileEvent("snare", tile, true, unit));
                    break;
                case ActionKind.FireArrow:
                {
                    var target = UnitAt(tile);
                    var cells = Cells(target).Where(p => !Board.IsObstacle(p)).ToList();
                    var arrow = ActionHit(unit, target, DamageAgainst(unit, target), events, "fire arrow");
                    PlantedPin(unit, new[] { arrow.Target }, events);
                    foreach (var p in cells)
                    {
                        _burning[p] = BurnRounds;
                        _burnOwners[p] = unit;
                        events.Add(new TileEvent("burn", p, true, unit));
                    }

                    break;
                }
                case ActionKind.CoveringFire:
                {
                    var tiles = SightTiles(unit);
                    _overwatch[unit] = new HashSet<GridPos>(tiles);
                    events.Add(new OverwatchEvent(unit, tiles));
                    break;
                }
                case ActionKind.SuppressingFire:
                {
                    var line = SuppressLine(unit, tile);
                    var victims = line.Select(UnitAt).Where(u => u != null && u.Team != unit.Team && !u.IsObjective).Distinct().ToList();
                    var after = new List<BattleEvent>();
                    var hits = victims.Select(v => Damage(v, PinShotDamage(unit), unit, DamageKind.Attack, after)).ToList();
                    unit.AttackedThisTurn = true;
                    foreach (var v in victims)
                        unit.MarkStruck(v);
                    SpendAmbush(unit);
                    events.Add(new AreaAttackEvent(unit, "suppressing fire", tile, line, hits));
                    events.AddRange(after);
                    foreach (var v in victims.Where(v => v.IsAlive && !v.Has(Ability.Colossus)))
                    {
                        v.SnareTurns = 1;
                        events.Add(new StatusEvent(unit, v, StatusKind.Pin, 1));
                    }

                    break;
                }
                case ActionKind.ThrowingKnife:
                {
                    var target = UnitAt(tile);
                    ActionHit(unit, target, ActionDamage(unit, target, KnifeDamage), events, "throwing knife");
                    break;
                }
                case ActionKind.Shadowstep:
                {
                    var from = unit.Pos;
                    unit.Pos = tile;
                    unit.MovedThisTurn = true;
                    unit.Moves = 0;
                    events.Add(new MoveEvent(unit, from, new List<GridPos> { tile }));
                    break;
                }
                case ActionKind.SmokeBomb:
                {
                    // The thrower and at most one ally (the one aimed at). Neither gets the ambush +2 from the smoke.
                    events.Add(new TileEvent("smoke", tile, true, unit));
                    foreach (var ally in new[] { unit, UnitAt(tile) }.Distinct().ToList())
                    {
                        if (!ally.IsHidden)
                            ally.AmbushUsed = true;
                        ally.HiddenTurns = Math.Max(ally.HiddenTurns, VanishTurns);
                        events.Add(new StatusEvent(unit, ally, StatusKind.Vanish, VanishTurns));
                    }

                    break;
                }
                case ActionKind.MarkTarget:
                {
                    var target = UnitAt(tile);
                    target.Marked = true;
                    target.MarkSpreads = unit.Has(Ability.DeathMark);
                    events.Add(new StatusEvent(unit, target, StatusKind.Mark, MarkBonus));
                    break;
                }
                case ActionKind.Planted:
                    // Pinned for the rest of this turn and on its next one; the bonus lasts as long.
                    unit.PlantedTurns = PlantedTurns;
                    unit.RootedThisTurn = true;
                    unit.Moves = 0;
                    unit.SnareTurns = Math.Max(unit.SnareTurns, 1);
                    events.Add(new StatusEvent(unit, unit, StatusKind.Planted, PlantedBonus));
                    break;
                case ActionKind.Overchannel:
                    // The cost is paid on use: slowed on its next turn even if no spell follows.
                    unit.Overchanneled = true;
                    unit.SlowNext = 1;
                    events.Add(new StatusEvent(unit, unit, StatusKind.Overchannel, 2));
                    break;
                case ActionKind.Rupture:
                {
                    var target = UnitAt(tile);
                    var burst = target.PoisonTurns * PoisonDamage;
                    target.PoisonTurns = 0;
                    var after = new List<BattleEvent>();
                    events.Add(new TickEvent("rupture", Damage(target, burst, unit, DamageKind.Tick, after), unit));
                    events.AddRange(after);
                    if (target.IsAlive && !IsOver)
                        ActionHit(unit, target, DamageAgainst(unit, target), events, "rupture");
                    else
                        unit.AttackedThisTurn = true;
                    break;
                }
                case ActionKind.Blink:
                {
                    var from = unit.Pos;
                    unit.Pos = tile;
                    unit.MovedThisTurn = true;
                    events.Add(new MoveEvent(unit, from, new List<GridPos> { tile }));
                    break;
                }
                case ActionKind.Fireball:
                    Fireball(unit, tile, events);
                    break;
                case ActionKind.IceWall:
                    Board.AddObstacle(tile);
                    _ice[tile] = IceRounds;
                    events.Add(new TileEvent("ice", tile, true, unit));
                    break;
                case ActionKind.Ward:
                {
                    var target = UnitAt(tile);
                    var shielded = new List<Unit> { target };
                    if (unit.Has(Ability.WideWard))
                        shielded.AddRange(Living(unit.Team).Where(u => u != target && Distance(u, target) == 1));
                    foreach (var u in shielded)
                    {
                        u.Shield = Math.Max(u.Shield, WardShield);
                        events.Add(new StatusEvent(unit, u, StatusKind.Ward, WardShield));
                    }

                    break;
                }
                case ActionKind.Mend:
                {
                    var target = UnitAt(tile);
                    var healed = Math.Min(MendHeal, target.MaxHp - target.Hp);
                    target.Hp += healed;
                    events.Add(new StatusEvent(unit, target, StatusKind.Heal, healed));
                    break;
                }
                case ActionKind.Barricade:
                    Board.AddObstacle(tile);
                    _barricades.Add((tile, unit));
                    _barricadeHp[tile] = BarricadeHp;
                    events.Add(new TileEvent("barricade", tile, true, unit));
                    break;
                case ActionKind.Turret:
                {
                    var turret = new Unit(_nextId++, Team.Player, UnitStats.Turret, null, unit)
                    {
                        Pos = tile, IsPlaced = true, IsDone = true,
                    };
                    _units.Add(turret);
                    events.Add(new SummonEvent(unit, turret));
                    break;
                }
                case ActionKind.Detonate:
                    foreach (var b in _barricades.Where(b => b.owner == unit).ToList())
                    {
                        if (!_barricades.Contains(b))
                            continue;
                        _barricades.Remove(b);
                        _barricadeHp.Remove(b.tile);
                        Board.RemoveObstacle(b.tile);
                        events.Add(new TileEvent("barricade", b.tile, false, unit));
                        Explode(b.tile, BarricadeBlastDamage, "barricade", unit, events);
                        AddRubble(b.tile, null, events);
                    }

                    break;
                case ActionKind.Charge:
                    Charge(unit, tile, events);
                    break;
                case ActionKind.FrostNova:
                    FrostNova(unit, events);
                    break;
                case ActionKind.Firewall:
                    Firewall(unit, tile, events);
                    break;
                case ActionKind.Blizzard:
                    Blizzard(unit, tile, events);
                    break;
                case ActionKind.Transpose:
                {
                    var other = UnitAt(tile);
                    var from = unit.Pos;
                    unit.Pos = tile;
                    other.Pos = from;
                    unit.MovedThisTurn = true;
                    events.Add(new MoveEvent(unit, from, new List<GridPos> { tile }));
                    events.Add(new MoveEvent(other, tile, new List<GridPos> { from }));
                    // An enemy put down on a mine sets it off.
                    if (other.Team == Team.Enemy && MineAt(from) is Mine mine)
                        Explode(mine.Tile, mine.Damage, "mine", mine.Owner, events);
                    break;
                }
                case ActionKind.CaveIn:
                    AddRubble(tile, unit, events);
                    break;
                case ActionKind.Hook:
                {
                    var target = UnitAt(tile);
                    var dir = HookLine(unit, target).Value;
                    events.Add(new StatusEvent(unit, target, StatusKind.Hooked, 0));
                    Push(target, dir, unit, events, distance: unit.Pos.StepsTo(target.Pos) - 1);
                    break;
                }
                case ActionKind.Demolish:
                    Demolish(unit, tile, events);
                    break;
            }

            CheckEnd();
            return events;
        }

        /// <summary>
        /// Frost Nova (the Mage's base spell): every enemy next to the caster is slowed (can't move on its next turn)
        /// and pushed 1 tile away, by the usual weight rules. A Colossus shrugs off both.
        /// </summary>
        private void FrostNova(Unit caster, List<BattleEvent> events)
        {
            events.Add(new StatusEvent(caster, caster, StatusKind.FrostNova, 0));
            foreach (var foe in AdjacentFoes(caster).Where(u => !u.IsObjective && !u.IsHidden).OrderBy(u => u.Id).ToList())
            {
                if (!foe.IsAlive || IsOver || foe.Has(Ability.Colossus))
                    continue;
                foe.SlowNext = 1;
                events.Add(new StatusEvent(caster, foe, StatusKind.Slow, 1));
                var near = Cells(foe).Where(c => c.StepsTo(caster.Pos) == 1).Cast<GridPos?>().FirstOrDefault();
                var dir = near.HasValue ? StraightDirection(caster.Pos, near.Value) : null;
                if (dir.HasValue)
                    Push(foe, dir.Value, caster, events);
            }
        }

        /// <summary>
        /// The 3 tiles a Firewall lights: the chosen tile and its two neighbours across the caster's line of aim
        /// (the longer of the two axes; a tie counts as left-right aim).
        /// </summary>
        public List<GridPos> FirewallLine(Unit caster, GridPos center)
        {
            var dx = center.X - caster.Pos.X;
            var dy = center.Y - caster.Pos.Y;
            var side = Math.Abs(dx) >= Math.Abs(dy) ? new GridPos(0, 1) : new GridPos(1, 0);
            return new[] { center + side, center, new GridPos(center.X - side.X, center.Y - side.Y) }
                .Where(p => Board.InBounds(p) && !Board.IsObstacle(p)).ToList();
        }

        /// <summary>Firewall: a line of 3 burning tiles; whoever stands there takes the damage now, friend or foe.</summary>
        private void Firewall(Unit caster, GridPos center, List<BattleEvent> events)
        {
            var line = FirewallLine(caster, center);
            var after = new List<BattleEvent>();
            var hits = _units.Where(u => u.IsPlaced && u.IsAlive && Cells(u).Any(line.Contains)).ToList()
                .Select(u => Damage(u, SpellDamage(caster, FirewallDamage), caster, DamageKind.Blast, after)).ToList();
            events.Add(new AreaAttackEvent(caster, "firewall", center, line, hits));
            caster.Overchanneled = false;
            events.AddRange(after);
            foreach (var p in line)
                SetBurning(p, caster, events);
        }

        /// <summary>Blizzard: every enemy in the plus takes the damage and is slowed. Your units in it are spared.</summary>
        private void Blizzard(Unit caster, GridPos center, List<BattleEvent> events)
        {
            var area = PlusArea(center);
            var foes = _units.Where(u => u.IsPlaced && u.IsAlive && u.Team != caster.Team && Cells(u).Any(area.Contains)).ToList();
            var after = new List<BattleEvent>();
            var hits = foes.Select(u => Damage(u, SpellDamage(caster, BlizzardDamage), caster, DamageKind.Blast, after)).ToList();
            events.Add(new AreaAttackEvent(caster, "blizzard", center, area, hits));
            caster.Overchanneled = false;
            events.AddRange(after);
            foreach (var foe in foes.Where(u => u.IsAlive && !u.IsObjective && !u.Has(Ability.Colossus)))
            {
                foe.SlowNext = 1;
                events.Add(new StatusEvent(caster, foe, StatusKind.Slow, 1));
            }
        }

        /// <summary>
        /// Grappling Hook: the pull direction (from the target towards the sapper) if the target is an enemy 2 to
        /// <see cref="HookRange"/> steps away in a straight line, in sight (rubble doesn't block it), not huge.
        /// </summary>
        public GridPos? HookLine(Unit unit, Unit target)
        {
            if (target == null || target.Team == unit.Team || target.IsObjective || target.IsHidden || target.Size != 1 || unit.Size != 1 ||
                target.Has(Ability.Colossus) || target.Stats.Weight == Weight.Huge)
                return null;
            var dir = StraightDirection(target.Pos, unit.Pos);
            var steps = unit.Pos.StepsTo(target.Pos);
            if (dir == null || steps < 2 || steps > HookRange || !Board.HasLineOfSight(unit.Pos, target.Pos))
                return null;
            return dir;
        }

        /// <summary>
        /// Demolish: blows up a rock, rubble, barricade or ice wall in range for a blast in the 3x3. A rock or barricade
        /// collapses into rubble; rubble and ice are gone.
        /// </summary>
        private void Demolish(Unit unit, GridPos tile, List<BattleEvent> events)
        {
            var leavesRubble = !IsRubble(tile) && !_ice.ContainsKey(tile);
            if (IsBarricade(tile))
            {
                _barricades.RemoveAll(b => b.tile == tile);
                _barricadeHp.Remove(tile);
                events.Add(new TileEvent("barricade", tile, false, unit));
            }
            else if (_ice.Remove(tile))
                events.Add(new TileEvent("ice", tile, false, unit));
            else if (IsRubble(tile))
                events.Add(new TileEvent("rubble", tile, false, unit));
            else
                events.Add(new TileEvent("rock", tile, false, unit));

            Board.RemoveObstacle(tile);
            Explode(tile, DemolishDamage, "demolish", unit, events);
            if (leavesRubble)
                AddRubble(tile, null, events);
        }

        /// <summary>A spell's flat damage on each target: double after Overchannel.</summary>
        private static int SpellDamage(Unit caster, int amount) => caster.Overchanneled ? amount * 2 : amount;

        /// <summary>Planted: a planted Archer's shots pin every enemy they hit (not objectives or a Colossus).</summary>
        private void PlantedPin(Unit shooter, IEnumerable<Unit> hit, List<BattleEvent> events)
        {
            if (!shooter.IsPlanted)
                return;
            foreach (var u in hit.Distinct().Where(u => u.IsAlive && u.Team != shooter.Team && !u.IsObjective && !u.Has(Ability.Colossus)).ToList())
            {
                u.SnareTurns = Math.Max(u.SnareTurns, 1);
                events.Add(new StatusEvent(shooter, u, StatusKind.Pin, 1));
            }
        }

        private void Fireball(Unit caster, GridPos center, List<BattleEvent> events)
        {
            var area = PlusArea(center);
            var after = new List<BattleEvent>();
            var hits = _units.Where(u => u.IsPlaced && u.IsAlive && Cells(u).Any(area.Contains)).ToList()
                .Select(u => Damage(u, SpellDamage(caster, FireballDamage), caster, DamageKind.Blast, after)).ToList();
            events.Add(new AreaAttackEvent(caster, "fireball", center, area, hits));
            caster.Overchanneled = false;
            events.AddRange(after);

            if (caster.Has(Ability.Burning))
            {
                foreach (var p in area.Where(p => !Board.IsObstacle(p)))
                {
                    _burning[p] = BurnRounds;
                    _burnOwners[p] = caster;
                    events.Add(new TileEvent("burn", p, true, caster));
                }
            }

            if (caster.Has(Ability.Concussive))
            {
                // Push outward, furthest first so units don't block each other.
                var pushed = _units.Where(u => u.IsPlaced && u.IsAlive && u.Size == 1 && area.Contains(u.Pos) && u.Pos != center)
                    .OrderByDescending(u => u.Pos.StepsTo(center)).ToList();
                foreach (var u in pushed)
                {
                    var dx = u.Pos.X - center.X;
                    var dy = u.Pos.Y - center.Y;
                    var dir = Math.Abs(dx) >= Math.Abs(dy) ? new GridPos(Math.Sign(dx), 0) : new GridPos(0, Math.Sign(dy));
                    Push(u, dir, caster, events);
                }

                foreach (var p in area)
                {
                    var hazard = MineAt(p);
                    if (hazard != null)
                        Explode(p, hazard.Damage, "mine", hazard.Owner, events);
                    var bomb = BombAt(p);
                    if (bomb != null)
                        Explode(p, bomb.Damage, "bomb", bomb.Owner, events);
                }
            }
        }

        /// <summary>
        /// A single-target hit from an action (Kick, Fire Arrow, Throwing Knife, Rupture): counts as an attack for
        /// Poison, Chill, Momentum and the like, and spends a hidden unit's ambush bonus.
        /// </summary>
        private Hit ActionHit(Unit unit, Unit target, int amount, List<BattleEvent> events, string via)
        {
            var after = new List<BattleEvent>();
            var hit = Damage(target, amount, unit, DamageKind.Attack, after);
            unit.AttackedThisTurn = true;
            unit.MarkStruck(target);
            SpendAmbush(unit);
            events.Add(new AttackEvent(unit, hit.Target, hit.Damage, hit.HpAfter, via));
            events.AddRange(after);
            return hit;
        }

        /// <summary>A hidden unit's first attack used its +2: it stays hidden, but later attacks don't get it.</summary>
        private static void SpendAmbush(Unit unit)
        {
            if (unit.IsHidden)
                unit.AmbushUsed = true;
        }

        /// <summary>An action's own base damage in place of the unit's attack damage; bonuses to its damage still count.</summary>
        private int ActionDamage(Unit unit, Unit target, int baseDamage, bool shot = true) =>
            DamageFrom(unit, target, Math.Max(1, EffectiveDamage(unit) - unit.Stats.Damage + baseDamage), shot);

        /// <summary>
        /// Pincer, Prey, Cheap Shot, Long Shot, Isolate, Lone Wolf, Planted, Shatter and the hidden ambush, added up and
        /// capped at <see cref="MaxConditionalBonus"/> so stacked conditions can't one-shot anything. Planted counts
        /// only for shots (<paramref name="shot"/> is false for Kick).
        /// </summary>
        public int ConditionalBonus(Unit attacker, Unit target, bool shot = true)
        {
            var bonus = 0;
            if (shot && attacker.IsPlanted)
                bonus += PlantedBonus;
            if (attacker.Has(Ability.LoneWolf) && IsExposed(attacker))
                bonus += LoneWolfBonus;
            if (attacker.Has(Ability.Pincer) && IsPinched(attacker, target))
                bonus += PincerBonus;
            if (target.IsSnared && Living(attacker.Team).Any(u => u.Has(Ability.PreyOnSnared)))
                bonus += PreyBonus;
            if (attacker.Has(Ability.CheapShot) && (target.IsSnared || target.IsSlowed))
                bonus += CheapShotBonus;
            if (attacker.Has(Ability.LongShot) && Distance(attacker, target) >= LongShotRange)
                bonus += LongShotBonus;
            if (attacker.Has(Ability.Isolate) && IsExposed(target))
                bonus += IsolateBonus;
            if (target.IsSlowed && Living(attacker.Team).Any(u => u.Has(Ability.Shatter)))
                bonus += ShatterBonus;
            if (attacker.IsHidden && !attacker.AmbushUsed)
                bonus += VanishBonus;
            return Math.Min(bonus, MaxConditionalBonus);
        }

        /// <summary>Sweep: the unit's attack damage to the target and the enemies on both sides of it.</summary>
        private void Sweep(Unit unit, Unit target, List<BattleEvent> events)
        {
            var dir = StraightDirection(unit.Pos, Cells(target).First(c => c.StepsTo(unit.Pos) == 1)).Value;
            var side = new GridPos(dir.Y, dir.X);
            var near = unit.Pos + dir;
            var line = new List<GridPos> { near + side, near, new GridPos(near.X - side.X, near.Y - side.Y) };
            var victims = line.Select(UnitAt).Where(u => u != null && u.Team != unit.Team).Distinct().ToList();
            var after = new List<BattleEvent>();
            var hits = victims.Select(v => Damage(v, DamageAgainst(unit, v), unit, DamageKind.Attack, after)).ToList();
            unit.AttackedThisTurn = true;
            foreach (var v in victims)
                unit.MarkStruck(v);
            events.Add(new AreaAttackEvent(unit, "sweep", target.Pos, line, hits));
            events.AddRange(after);
        }

        /// <summary>
        /// Charge: walks tile by tile to the chosen tile, past allies. Each enemy on the way takes the damage and is
        /// pushed to a random side (the other side if that one is blocked; a collision if both are). An enemy still
        /// in the way afterwards stops the charge in front of it.
        /// </summary>
        private void Charge(Unit unit, GridPos tile, List<BattleEvent> events)
        {
            var dir = StraightDirection(unit.Pos, tile).Value;
            var shownFrom = unit.Pos;
            var walked = new List<GridPos>();
            var pastAllies = new List<GridPos>();
            unit.MovedThisTurn = true;

            void ShowWalk()
            {
                if (walked.Count == 0)
                    return;
                events.Add(new MoveEvent(unit, shownFrom, walked.ToList()));
                shownFrom = walked[walked.Count - 1];
                walked.Clear();
            }

            for (var p = unit.Pos; p != tile && unit.IsAlive && !IsOver;)
            {
                p += dir;
                var occupant = UnitAt(p);
                if (occupant != null && occupant.Team == unit.Team)
                {
                    pastAllies.Add(p);
                    continue;
                }

                if (occupant != null)
                {
                    ShowWalk();
                    var after = new List<BattleEvent>();
                    var hit = Damage(occupant, ActionDamage(unit, occupant, ChargeDamage), unit, DamageKind.Attack, after);
                    unit.AttackedThisTurn = true;
                    unit.MarkStruck(occupant);
                    events.Add(new AttackEvent(unit, hit.Target, hit.Damage, hit.HpAfter));
                    events.AddRange(after);
                    if (occupant.IsAlive)
                        Push(occupant, ChargeSide(occupant, dir), unit, events);
                    if (UnitAt(p) != null || !unit.IsAlive)
                        break;
                }

                walked.AddRange(pastAllies);
                pastAllies.Clear();
                walked.Add(p);
                unit.Pos = p;
            }

            ShowWalk();
        }

        /// <summary>The side Charge pushes a unit to: random, but the other one if only this one is blocked.</summary>
        private GridPos ChargeSide(Unit unit, GridPos dir)
        {
            var first = new GridPos(dir.Y, dir.X);
            var second = new GridPos(-dir.Y, -dir.X);
            if (Rng.Next(2) == 0)
                (first, second) = (second, first);
            bool Open(GridPos side)
            {
                var to = unit.Pos + side;
                return unit.Size == 1 && !Board.InBounds(to) ? EdgeKills : CanStand(unit, to);
            }

            return !Open(first) && Open(second) ? second : first;
        }

        /// <summary>
        /// Tiles a push moves a unit: 1, +1 for a small unit and -1 for a huge one, +1 when your unit pushes with
        /// Battering Ram. 0 means it doesn't budge.
        /// </summary>
        public int PushDistance(Unit unit, Unit pusher)
        {
            var distance = 1;
            if (unit.Stats.Weight == Weight.Small)
                distance++;
            else if (unit.Stats.Weight == Weight.Huge)
                distance--;
            if (IsRamming(pusher))
                distance += Core.Banners.RamDistance;
            return Math.Max(0, distance);
        }

        private bool IsRamming(Unit pusher) => pusher != null && pusher.Team == Team.Player && HasBanner(Banner.BatteringRam);

        /// <summary>
        /// Shoves a unit <see cref="PushDistance"/> tiles, one at a time. If something is in the way it slams into it,
        /// stops and takes collision damage. An enemy shoved onto a mine sets it off. Battering Ram adds its damage
        /// at the end, whether the unit moved or not.
        /// </summary>
        private void Push(Unit unit, GridPos dir, Unit pusher, List<BattleEvent> events, bool hurtsBlocker = false, int? distance = null)
        {
            // The shrine is rooted to its tile, and nothing moves a Colossus.
            if (!unit.IsAlive || unit.IsObjective || unit.Has(Ability.Colossus))
                return;
            var start = unit.Pos;
            var tiles = distance ?? PushDistance(unit, pusher);
            for (var step = 0; step < tiles && unit.IsAlive; step++)
            {
                var from = unit.Pos;
                var to = from + dir;
                if (EdgeKills && unit.Size == 1 && !unit.Stats.IsBoss && !Board.InBounds(to))
                {
                    if (from != start)
                        events.Add(new PushEvent(unit, pusher, start, from, null));
                    Fall(unit, pusher, from, dir, events);
                    return;
                }

                // Crumbling Roof: pushed into a hole, it falls (huge units aren't pushed in the first place).
                if (unit.Size == 1 && !unit.Stats.IsBoss && Board.IsHole(to))
                {
                    if (from != start)
                        events.Add(new PushEvent(unit, pusher, start, from, null));
                    DropIntoHole(unit, pusher, from, to, events);
                    return;
                }

                if (!CanStand(unit, to))
                {
                    if (from != start)
                        events.Add(new PushEvent(unit, pusher, start, from, null));
                    start = from;

                    // Whoever it slams into, if the push says so (Shield Bash), takes the same damage.
                    var blockers = hurtsBlocker
                        ? Footprint(to, unit.Size).Select(UnitAt).Where(u => u != null && u != unit).Distinct().ToList()
                        : new List<Unit>();
                    // Rubble crushes: more damage, and the heap breaks.
                    var rubble = Footprint(to, unit.Size).Where(IsRubble).ToList();
                    var after = new List<BattleEvent>();
                    var hit = Damage(unit, CollisionDamage + (rubble.Count > 0 ? CrushDamage : 0), pusher, DamageKind.Collision, after);
                    events.Add(new PushEvent(unit, pusher, from, from, hit));
                    if (rubble.Count > 0)
                        events.Add(new EffectEvent(EffectKind.Crush, unit, CrushDamage, unit.Hp, pusher));
                    events.AddRange(after);
                    foreach (var r in rubble)
                        ClearRubble(r, null, events);
                    foreach (var blocker in blockers.Where(b => b.IsAlive))
                    {
                        var blockerAfter = new List<BattleEvent>();
                        events.Add(new TickEvent("collision", Damage(blocker, CollisionDamage, pusher, DamageKind.Collision, blockerAfter), pusher));
                        events.AddRange(blockerAfter);
                    }

                    // Crumbling Roof: the slam cracks the floor under it.
                    if (!unit.Fell)
                        CrackFloor(unit.Pos, events);

                    break;
                }

                unit.Pos = to;
                if (unit.Team == Team.Enemy)
                {
                    var mine = Cells(unit).Select(MineAt).FirstOrDefault(m => m != null);
                    if (mine != null)
                    {
                        // The slide so far shows before the blast.
                        events.Add(new PushEvent(unit, pusher, start, to, null));
                        start = to;
                        Explode(mine.Tile, mine.Damage, "mine", mine.Owner, events);
                    }
                }
            }

            if (unit.Pos != start)
                events.Add(new PushEvent(unit, pusher, start, unit.Pos, null));

            // Battering Ram is for pushes, not the Grappling Hook's pull (the one push with a set distance).
            if (IsRamming(pusher) && unit.IsAlive && distance == null)
            {
                var after = new List<BattleEvent>();
                events.Add(new TickEvent("ram", Damage(unit, Core.Banners.RamDamage, pusher, DamageKind.Collision, after), pusher));
                events.AddRange(after);
            }
        }

        // ---------- Enemy abilities ----------

        /// <summary>Living enemies of the unit standing next to it: the targets of an area smash.</summary>
        public List<Unit> AdjacentFoes(Unit unit) =>
            _units.Where(u => u.IsPlaced && u.IsAlive && u.Team != unit.Team && Distance(unit, u) == 1).ToList();

        public bool CanRaise(Unit raiser, Unit remains) =>
            !IsOver && raiser.IsAlive && raiser.Stats.CanRaise && raiser.Ap >= raiser.Stats.RaiseCost && Ready(raiser, ActionKind.Raise) &&
            remains != null && IsRaisableBy(raiser, remains) &&
            Distance(raiser, remains) <= raiser.Stats.RaiseRange && UnitAt(remains.Pos) == null && !Board.IsObstacle(remains.Pos);

        /// <summary>A bone pile the raiser can bring back: one of its own side's, or a fallen unit of yours with Soul Harvest.</summary>
        private static bool IsRaisableBy(Unit raiser, Unit remains) =>
            remains.IsRemains && (remains.Team == raiser.Team || (remains.LeftPile && raiser.Has(Ability.SoulHarvest)));

        /// <summary>Every bone pile the raiser could bring back, wherever it lies.</summary>
        public IEnumerable<Unit> RaisablePiles(Unit raiser) => _units.Where(u => IsRaisableBy(raiser, u));

        /// <summary>Corpse Blast: any bone pile (its side's or yours) within raise range, someone standing on it or not.</summary>
        public bool CanCorpseBlast(Unit unit, Unit pile) =>
            !IsOver && unit.IsAlive && unit.Has(Ability.CorpseBlast) && unit.Ap >= AttackCost && Ready(unit, ActionKind.CorpseBlast) &&
            pile != null && pile.IsRemains && Distance(unit, pile) <= unit.Stats.RaiseRange;

        /// <summary>Corpse Blast: the pile is used up and explodes for <see cref="CorpseBlastDamage"/> in the 3x3 around it, friend or foe.</summary>
        internal List<BattleEvent> DoCorpseBlast(Unit unit, Unit pile)
        {
            UseUp(unit, ActionKind.CorpseBlast);
            pile.PileGone = true;
            var events = new List<BattleEvent> { new CorpseBlastEvent(unit, pile) };
            Explode(pile.Pos, CorpseBlastDamage, "corpse blast", unit, events);
            CheckEnd();
            return events;
        }

        public bool CanThrow(Unit thrower, Unit target)
        {
            if (IsOver || !thrower.IsAlive || !thrower.Stats.CanThrow || thrower.Ap < thrower.Stats.ThrowCost || !Ready(thrower, ActionKind.Boulder))
                return false;
            if (target == null || !target.IsAlive || target.Team == thrower.Team || target.IsHidden)
                return false;
            var steps = Distance(thrower, target);
            return steps >= 2 && steps <= thrower.Stats.ThrowRange && !IsShroudedFrom(target, thrower.Team);
        }

        public bool CanBomb(Unit thrower, Unit target)
        {
            if (IsOver || !thrower.IsAlive || !thrower.Stats.CanBomb || thrower.Ap < thrower.Stats.BombCost || !Ready(thrower, ActionKind.Bomb))
                return false;
            if (target == null || !target.IsAlive || target.Team == thrower.Team || target.IsHidden)
                return false;
            var steps = Distance(thrower, target);
            return steps >= 2 && steps <= thrower.Stats.BombRange && BombAt(target.Pos) == null && !IsShroudedFrom(target, thrower.Team);
        }

        /// <summary>
        /// Where a boulder thrown at the target comes to rest: the free tile next to the target that is
        /// closest to the thrower. Null if there is no free tile.
        /// </summary>
        public GridPos? RockTileFor(Unit thrower, Unit target)
        {
            GridPos? best = null;
            var bestSteps = int.MaxValue;
            foreach (var n in target.Pos.Neighbours())
            {
                if (!IsFree(n))
                    continue;
                var steps = Footprint(thrower.Pos, thrower.Size).Min(c => c.StepsTo(n));
                if (steps >= bestSteps)
                    continue;
                bestSteps = steps;
                best = n;
            }

            return best;
        }

        // ---------- Placement ----------

        /// <summary>Your deployment zone; with Vanguard also the row in front of it (not in fights with their own zone).</summary>
        public bool IsDeployTile(Unit unit, GridPos p) =>
            IsPlayerZone(p) || (Board.InBounds(p) && _deployZone == null && unit.Has(Ability.Vanguard) && p.Y == PlayerZoneRows);

        public bool CanPlace(Unit unit, GridPos p)
        {
            if (Phase != Phase.Placement || unit.Team != Team.Player || unit.IsObjective || !IsDeployTile(unit, p) || Board.IsObstacle(p))
                return false;
            var occupant = UnitAt(p);
            return occupant == null || occupant == unit;
        }

        public bool Place(Unit unit, GridPos p)
        {
            if (!CanPlace(unit, p))
                return false;
            unit.Pos = p;
            unit.IsPlaced = true;
            return true;
        }

        public bool AllPlaced => _units.Where(u => u.Team == Team.Player).All(u => u.IsPlaced);

        public bool StartBattle()
        {
            if (Phase != Phase.Placement || !AllPlaced)
                return false;
            TurnNumber = 1;
            Phase = Phase.PlayerTurn;
            BeginTurn(Team.Player, OpeningEvents);
            return true;
        }

        // ---------- Player turn ----------

        public bool CanAct(Unit unit) =>
            Phase == Phase.PlayerTurn && unit.Team == Team.Player && unit.IsAlive && !unit.IsDone && !unit.IsObjective &&
            (ActiveUnit == null || ActiveUnit == unit);

        /// <summary>Moves a player unit. Returns the events, or null if the move isn't allowed.</summary>
        public List<BattleEvent> PlayerMove(Unit unit, GridPos target)
        {
            if (!CanAct(unit) || !GetReachable(unit).ContainsKey(target))
                return null;
            ActiveUnit = unit;
            return DoMove(unit, target);
        }

        /// <summary>A player unit attacks a barricade (any side's). Returns the events, or null if not allowed.</summary>
        public List<BattleEvent> PlayerAttackBarricade(Unit unit, GridPos tile)
        {
            if (!CanAct(unit) || !CanAttackBarricade(unit, tile))
                return null;
            ActiveUnit = unit;
            return DoAttackBarricade(unit, tile);
        }

        /// <summary>
        /// Whether the unit could hit the barricade on this tile now with its basic attack: within its range
        /// (ranged attacks need line of sight). Objectives and units without an attack can't.
        /// </summary>
        public bool CanAttackBarricade(Unit attacker, GridPos tile)
        {
            if (IsOver || !attacker.IsAlive || attacker.IsObjective || attacker.Stats.Damage <= 0 || attacker.Ap < AttackCost || (!IsBarricade(tile) && !IsRubble(tile)))
                return false;
            var steps = Cells(attacker).Min(c => c.StepsTo(tile));
            return steps <= attacker.Range && (steps == 1 || Cells(attacker).Any(c => Board.HasLineOfSight(c, tile)));
        }

        /// <summary>The explosion cause of a Rigged Barricade going off (for the view and the playtest log).</summary>
        public const string RiggedCause = "rigged barricade";

        /// <summary>The sapper that built the barricade on this tile, or null.</summary>
        public Unit BarricadeOwner(GridPos tile) => _barricades.FirstOrDefault(b => b.tile == tile).owner;

        /// <summary>Whether breaking the barricade on this tile would set off a Rigged Barricades blast.</summary>
        public bool IsRigged(GridPos tile) => IsBarricade(tile) && BarricadeOwner(tile) is Unit owner && owner.Has(Ability.RiggedBarricades);

        /// <summary>
        /// For the enemy AI: the HP each side would lose if the rigged barricade on this tile broke now (3x3 blast,
        /// Blast Proof sparing the sapper's side). Mines and bombs it would set off aren't counted.
        /// </summary>
        public (int own, int foe) RiggedBlastLoss(GridPos tile, Team breaker)
        {
            var owner = BarricadeOwner(tile);
            if (owner == null || !owner.Has(Ability.RiggedBarricades))
                return (0, 0);
            var area = BlastArea(tile);
            var safe = owner.Has(Ability.BlastProof) ? owner.Team : (Team?)null;
            int own = 0, foe = 0;
            foreach (var u in _units.Where(u => u.IsPlaced && u.IsAlive && !u.IsObjective && u.Team != safe && Cells(u).Any(area.Contains)))
            {
                var loss = Math.Min(u.Hp, RiggedBlastDamage);
                if (u.Team == breaker)
                    own += loss;
                else
                    foe += loss;
            }

            return (own, foe);
        }

        /// <summary>
        /// A basic attack on a barricade: the attacker's damage (no target bonuses), one hit. At 0 HP the
        /// barricade breaks and its tile is free again (a rigged one explodes first). Attacking reveals a vanished unit.
        /// </summary>
        internal List<BattleEvent> DoAttackBarricade(Unit attacker, GridPos tile)
        {
            var events = new List<BattleEvent>();
            SpendAp(attacker, AttackCost);
            attacker.AttackedThisTurn = true;
            var damage = EffectiveDamage(attacker);
            if (IsRubble(tile))
            {
                // Rubble goes with one hit.
                events.Add(new BarricadeHitEvent(attacker, tile, damage, 0));
                ClearRubble(tile, attacker, events);
            }
            else
            {
                var hp = Math.Max(0, BarricadeHpAt(tile) - damage);
                _barricadeHp[tile] = hp;
                events.Add(new BarricadeHitEvent(attacker, tile, damage, hp));
                if (hp == 0)
                {
                    // A broken barricade collapses into rubble; a rigged one blows up first (its sapper's blast).
                    var owner = BarricadeOwner(tile);
                    _barricades.RemoveAll(b => b.tile == tile);
                    _barricadeHp.Remove(tile);
                    Board.RemoveObstacle(tile);
                    events.Add(new TileEvent("barricade", tile, false, attacker));
                    if (owner != null && owner.Has(Ability.RiggedBarricades))
                        Explode(tile, RiggedBlastDamage, RiggedCause, owner, events);
                    AddRubble(tile, null, events);
                }
            }

            // Attacking doesn't reveal a hidden unit (Vanish lasts until your next turn); it only spends the +2.
            SpendAmbush(attacker);

            return events;
        }

        /// <summary>A player unit's basic attack. Returns the events, or null if not allowed.</summary>
        public List<BattleEvent> PlayerAttack(Unit unit, Unit target)
        {
            if (!CanAct(unit) || !CanAttack(unit, target))
                return null;
            ActiveUnit = unit;
            return DoAttack(unit, target);
        }

        /// <summary>Finishes this unit's turn. Its leftover AP will partly carry over.</summary>
        public void EndUnitTurn(Unit unit)
        {
            if (Phase != Phase.PlayerTurn || unit.Team != Team.Player)
                return;
            unit.IsDone = true;
            if (ActiveUnit == unit)
                ActiveUnit = null;
        }

        /// <summary>
        /// Ends the player phase and runs the whole enemy phase: start-of-turn effects, then every bomb
        /// waiting on the board explodes, then each enemy acts, then the player's start-of-turn effects.
        /// Returns what happened.
        /// </summary>
        public List<BattleEvent> EndPlayerTurn()
        {
            var events = new List<BattleEvent>();
            if (Phase != Phase.PlayerTurn)
                return events;

            foreach (var u in Living(Team.Player))
                u.IsDone = true;
            ActiveUnit = null;

            // Break the Ritual: with totems still standing, the summoning completes as the enemy turn begins.
            if (SummonTurn > 0 && TurnNumber >= SummonTurn && _totems.Any(t => t.IsAlive))
            {
                SummoningComplete = true;
                Phase = Phase.Defeat;
                return events;
            }

            Phase = Phase.EnemyTurn;
            BeginTurn(Team.Enemy, events);

            foreach (var bomb in _bombs.ToList())
            {
                if (IsOver)
                    break;
                // An earlier blast may already have set this one off.
                if (_bombs.Contains(bomb))
                    Explode(bomb.Tile, bomb.Damage, "bomb", bomb.Owner, events);
            }

            // Howlers go first, so the moves they hand out reach wolves that haven't acted yet; imps too, so a unit
            // they swap in lands next to demons that still have their turn.
            foreach (var enemy in Living(Team.Enemy).OrderBy(e => e.Has(Ability.Howl) || e.Has(Ability.Hellswap) ? 0 : 1).ToList())
            {
                if (IsOver)
                    break;
                // A skeleton raised earlier this phase waits until the next enemy turn. Totems never act.
                if (enemy.IsAlive && !enemy.IsDone && !enemy.IsObjective)
                    EnemyAI.TakeTurn(this, enemy, events);
                enemy.IsDone = true;
            }

            if (IsOver)
                return events;

            // Ritual fights: surviving the last enemy turn completes the ritual; otherwise waves arrive.
            if (RitualTurns > 0 && TurnNumber >= RitualTurns)
            {
                RitualComplete = true;
                Phase = Phase.Victory;
                return events;
            }

            StrikeStormMarks(events);
            if (IsOver)
                return events;

            SpawnWaves(events);

            TurnNumber++;
            BeginTurn(Team.Player, events);
            if (!IsOver)
                Phase = Phase.PlayerTurn;
            return events;
        }

        // ---------- Shared actions ----------

        /// <summary>
        /// Walks the path. An enemy that steps onto a mine stops there and sets it off.
        /// </summary>
        internal List<BattleEvent> DoMove(Unit unit, GridPos target)
        {
            if (!CanMove(unit))
                return null;
            var path = FindPath(unit, target);
            if (path == null || path.Count == 0 || PathCost(unit, path) > unit.Moves)
                return null;

            // Enemies stop on the first mine, snare or tile one of your archers covers (Covering Fire).
            Mine triggered = null;
            GridPos? snare = null;
            Unit watcher = null;
            if (unit.Team == Team.Enemy)
            {
                for (var i = 0; i < path.Count; i++)
                {
                    if (!CanStand(unit, path[i]))
                        continue;
                    var cells = Footprint(path[i], unit.Size).ToList();
                    triggered = cells.Select(MineAt).FirstOrDefault(m => m != null);
                    snare = cells.Where(_snares.ContainsKey).Cast<GridPos?>().FirstOrDefault();
                    watcher = cells.Select(c => WatcherOf(c, unit)).FirstOrDefault(w => w != null);
                    if (triggered == null && snare == null && watcher == null)
                        continue;
                    path = path.Take(i + 1).ToList();
                    break;
                }
            }

            // Your units stop on the first caltrops or watched tile on the way.
            var caltrop = false;
            if (unit.Team == Team.Player)
            {
                for (var i = 0; i < path.Count; i++)
                {
                    if (!CanStand(unit, path[i]))
                        continue;
                    caltrop = _caltrops.ContainsKey(path[i]);
                    watcher = WatcherOf(path[i], unit);
                    if (!caltrop && watcher == null)
                        continue;
                    path = path.Take(i + 1).ToList();
                    break;
                }
            }

            var from = unit.Pos;
            var cost = PathCost(unit, path);
            var zoc = ZocSteps(unit, path);
            var strikers = ZocStrikers(unit, from, path);
            unit.Moves -= cost;
            unit.MovedThisTurn = true;
            // Slowed: it chose to move, so it can't attack or spend AP this turn.
            if (unit.SlowedThisTurn)
                unit.Ap = 0;
            unit.Pos = path[path.Count - 1];
            var events = new List<BattleEvent> { new MoveEvent(unit, from, path, cost, zoc) };
            foreach (var striker in strikers.Where(s => unit.IsAlive && s.IsAlive))
            {
                striker.ZocStrikeUsed = true;
                var strikeAfter = new List<BattleEvent>();
                var strike = Damage(unit, DamageAgainst(striker, unit), striker, DamageKind.Attack, strikeAfter);
                striker.MarkStruck(unit);
                events.Add(new EffectEvent(EffectKind.ZocStrike, striker, 0, striker.Hp, striker));
                events.Add(new AttackEvent(striker, strike.Target, strike.Damage, strike.HpAfter));
                events.AddRange(strikeAfter);
            }

            if (triggered != null)
                Explode(triggered.Tile, triggered.Damage, "mine", triggered.Owner, events);

            if (snare.HasValue && unit.IsAlive)
            {
                // Snare Trap: it stops, takes the damage, and can't move on its next turn either.
                var setter = _snares[snare.Value];
                _snares.Remove(snare.Value);
                unit.Moves = 0;
                events.Add(new TileEvent("snare", snare.Value, false, null));
                var after = new List<BattleEvent>();
                var damage = setter != null && setter.Has(Ability.SpikedSnares) ? SpikedSnareDamage : SnareDamage;
                events.Add(new TickEvent("snare", Damage(unit, damage, setter, DamageKind.Tick, after), setter));
                events.AddRange(after);
                if (unit.IsAlive && !unit.Has(Ability.Colossus))
                {
                    unit.SnareTurns = 1;
                    events.Add(new StatusEvent(setter ?? unit, unit, StatusKind.Pin, 1));
                }
            }

            if (unit.Team == Team.Enemy && watcher != null && unit.IsAlive && watcher.IsAlive)
            {
                // Covering Fire: one shot, the enemy stops there, and the archer stops watching.
                _overwatch.Remove(watcher);
                unit.Moves = 0;
                events.Add(new EffectEvent(EffectKind.CoveringFire, watcher, 0, watcher.Hp, watcher));
                var after = new List<BattleEvent>();
                var hit = Damage(unit, DamageAgainst(watcher, unit), watcher, DamageKind.Attack, after);
                watcher.MarkStruck(unit);
                SpendAmbush(watcher);
                events.Add(new AttackEvent(watcher, hit.Target, hit.Damage, hit.HpAfter, "covering fire"));
                events.AddRange(after);
                PlantedPin(watcher, new[] { hit.Target }, events);
                watcher = null;
            }

            if (caltrop)
            {
                var dropper = _caltrops[unit.Pos];
                _caltrops.Remove(unit.Pos);
                unit.Moves = 0;
                events.Add(new TileEvent("caltrops", unit.Pos, false, null));
                var after = new List<BattleEvent>();
                events.Add(new TickEvent("caltrops", Damage(unit, CaltropsDamage, null, DamageKind.Tick, after), dropper));
                events.AddRange(after);
            }

            if (unit.Team == Team.Player && watcher != null && unit.IsAlive)
            {
                // Overwatch: one shot, then it stops watching.
                _overwatch.Remove(watcher);
                var after = new List<BattleEvent>();
                var hit = Damage(unit, DamageAgainst(watcher, unit), watcher, DamageKind.Attack, after);
                events.Add(new AttackEvent(watcher, hit.Target, hit.Damage, hit.HpAfter, "overwatch"));
                events.AddRange(after);
            }

            CheckEnd();
            return events;
        }

        /// <summary>
        /// Who attacks a unit for walking this path: each enemy with the zone-of-control passive, not used yet this
        /// round, whose reach the path leaves. Elusive units ignore it, as they ignore zone of control.
        /// </summary>
        private List<Unit> ZocStrikers(Unit unit, GridPos from, IReadOnlyList<GridPos> path)
        {
            var strikers = new List<Unit>();
            var previous = from;
            foreach (var next in path)
            {
                strikers.AddRange(EngagedBy(unit, previous).Where(s => s.Has(Ability.ZocStrike) && !s.ZocStrikeUsed && Distance(next, unit.Size, s) != 1 && !strikers.Contains(s)).ToList());
                previous = next;
            }

            return strikers;
        }

        internal List<BattleEvent> DoAttack(Unit attacker, Unit target)
        {
            var events = new List<BattleEvent>();
            SpendAp(attacker, AttackCost);
            var strikes = attacker.Has(Ability.DoubleStrike) ? 2 : 1;
            var struck = new List<Unit>();
            for (var i = 0; i < strikes && !IsOver && attacker.IsAlive && target.IsAlive; i++)
                struck.AddRange(Strike(attacker, target, events));
            attacker.AttackedThisTurn = true;
            attacker.MarkStruck(target);
            // Attacking doesn't reveal a hidden unit (Vanish lasts until your next turn); it only spends the +2.
            SpendAmbush(attacker);
            // The bolt was the overchanneled spell.
            attacker.Overchanneled = false;
            if (!IsOver)
                PlantedPin(attacker, struck, events);

            if (attacker.Has(Ability.WolfBite))
                BiteChain++;
            foreach (var hitUnit in struck)
                attacker.MarkStruck(hitUnit);

            // Searing Bolt: the target's ground catches fire.
            if (attacker.Has(Ability.SearingBolt) && !IsOver)
                foreach (var cell in Cells(target).ToList())
                    SetBurning(cell, attacker, events);

            if (!attacker.IsRanged)
            {
                foreach (var hitUnit in struck.Distinct())
                {
                    Thorns(attacker, hitUnit, events);
                    Riposte(hitUnit, attacker, events);
                }
            }

            CheckEnd();
            return events;
        }

        /// <summary>One swing or shot, shaped by the attacker's abilities. Returns the enemies it hit.</summary>
        private List<Unit> Strike(Unit attacker, Unit target, List<BattleEvent> events)
        {
            var after = new List<BattleEvent>();
            var hits = new List<Hit>();

            if (attacker.Has(Ability.Volley))
            {
                var damage = Math.Max(1, DamageAgainst(attacker, target) - 1);
                var area = PlusArea(target.Pos);
                foreach (var u in _units.Where(u => u.IsPlaced && u.IsAlive && Cells(u).Any(area.Contains)).ToList())
                    hits.Add(Damage(u, damage, attacker, DamageKind.Attack, after));
                events.Add(new AreaAttackEvent(attacker, "volley", target.Pos, area, hits));
                events.AddRange(after);
                return hits.Select(h => h.Target).Where(u => u.Team != attacker.Team).ToList();
            }

            var victims = new List<Unit> { target };
            var kind = "attack";
            var line = new List<GridPos>();
            var dir = StraightDirection(attacker.Pos, Cells(target).OrderBy(c => c.StepsTo(attacker.Pos)).First());

            if (attacker.Has(Ability.Pierce) && dir != null)
            {
                kind = "pierce";
                for (var p = attacker.Pos + dir.Value; Board.InBounds(p) && !Board.IsRock(p) && attacker.Pos.StepsTo(p) <= RangeFrom(attacker, attacker.Pos); p += dir.Value)
                    line.Add(p);
            }
            else if (attacker.Has(Ability.LongReach) && dir != null)
            {
                kind = "long reach";
                for (var p = attacker.Pos + dir.Value; Board.InBounds(p) && attacker.Pos.StepsTo(p) <= attacker.Range; p += dir.Value)
                {
                    line.Add(p);
                    if (Board.IsRock(p))
                        break;
                }
            }

            if (line.Count > 0)
            {
                victims = line.Select(UnitAt).Where(u => u != null && u.Team != attacker.Team).Distinct().ToList();
                if (!victims.Contains(target))
                    victims.Insert(0, target);
                foreach (var v in victims)
                    hits.Add(Damage(v, DamageAgainst(attacker, v), attacker, DamageKind.Attack, after));
                events.Add(new AreaAttackEvent(attacker, kind, target.Pos, line, hits));
            }
            else
            {
                var hit = Damage(target, DamageAgainst(attacker, target), attacker, DamageKind.Attack, after);
                hits.Add(hit);
                events.Add(new AttackEvent(attacker, hit.Target, hit.Damage, hit.HpAfter));
            }

            events.AddRange(after);
            return hits.Select(h => h.Target).ToList();
        }

        internal List<BattleEvent> DoSmash(Unit attacker)
        {
            var damage = EffectiveDamage(attacker);
            SpendAp(attacker, AttackCost);
            var hits = new List<Hit>();
            var after = new List<BattleEvent>();
            var targets = AdjacentFoes(attacker);
            foreach (var target in targets)
                hits.Add(Damage(target, damage, attacker, DamageKind.Attack, after));
            var events = new List<BattleEvent> { new SmashEvent(attacker, hits) };
            events.AddRange(after);
            foreach (var hitUnit in hits.Select(h => h.Target).Distinct())
            {
                Thorns(attacker, hitUnit, events);
                Riposte(hitUnit, attacker, events);
            }

            CheckEnd();
            return events;
        }

        internal List<BattleEvent> DoThrow(Unit thrower, Unit target)
        {
            UseUp(thrower, ActionKind.Boulder);
            var rock = RockTileFor(thrower, target);
            var after = new List<BattleEvent>();
            var hit = Damage(target, thrower.Stats.ThrowDamage, thrower, DamageKind.Attack, after);
            if (rock.HasValue)
            {
                Board.AddObstacle(rock.Value);
                // A boulder landing on a mine or bomb buries it.
                _mines.RemoveAll(m => m.Tile == rock.Value);
                _bombs.RemoveAll(b => b.Tile == rock.Value);
                _burning.Remove(rock.Value);
                _burnOwners.Remove(rock.Value);
            }

            var events = new List<BattleEvent> { new BoulderEvent(thrower, hit, rock) };
            events.AddRange(after);
            CheckEnd();
            return events;
        }

        /// <summary>
        /// What a goblin could fling at the target from where it stands, ignoring its AP: a bomb or mine on an empty tile
        /// next to it first (it explodes on the target's tile), else a goblin next to it that hasn't acted yet this
        /// turn (it lands on the free tile next to the target nearest the thrower). Null if it can't.
        /// </summary>
        public (Unit goblin, GridPos from, GridPos to)? FlingOption(Unit thrower, Unit target)
        {
            if (IsOver || !thrower.IsAlive || !thrower.Has(Ability.Fling) || !Ready(thrower, ActionKind.Fling) || target == null || !target.IsAlive ||
                target.Team == thrower.Team || target.IsObjective || target.IsHidden || IsShroudedFrom(target, thrower.Team))
                return null;
            var steps = Distance(thrower, target);
            if (steps < 2 || steps > FlingRange)
                return null;

            foreach (var n in thrower.Pos.Neighbours())
                if ((MineAt(n) != null || BombAt(n) != null) && UnitAt(n) == null)
                    return (null, n, target.Pos);

            var goblin = thrower.Pos.Neighbours().Select(UnitAt)
                .FirstOrDefault(u => u != null && u.Team == thrower.Team && u.Stats.Kind == UnitKind.Goblin && u.Size == 1 &&
                                     (Phase != Phase.EnemyTurn || !u.IsDone));
            if (goblin == null)
                return null;
            var landing = target.Pos.Neighbours().Where(p => IsFree(p) && !HasHazard(p))
                .OrderBy(p => p.StepsTo(thrower.Pos)).ThenBy(p => p.Y).ThenBy(p => p.X)
                .Select(p => (GridPos?)p).FirstOrDefault();
            return landing.HasValue ? (goblin, goblin.Pos, landing.Value) : ((Unit, GridPos, GridPos)?)null;
        }

        internal List<BattleEvent> DoFling(Unit thrower, Unit target)
        {
            var option = FlingOption(thrower, target);
            if (option == null || thrower.Ap < AttackCost)
                return null;
            UseUp(thrower, ActionKind.Fling);
            var (goblin, from, to) = option.Value;
            var events = new List<BattleEvent>();
            if (goblin == null)
            {
                // Picked up off the ground: it goes off on impact with its own blast.
                var mine = MineAt(from);
                var bomb = BombAt(from);
                _mines.RemoveAll(m => m.Tile == from);
                _bombs.RemoveAll(b => b.Tile == from);
                events.Add(new FlingEvent(thrower, null, from, to, target, null));
                if (mine != null)
                    Explode(to, mine.Damage, "mine", thrower, events);
                else
                    Explode(to, bomb.Damage, "bomb", thrower, events);
                return events;
            }

            goblin.Pos = to;
            var after = new List<BattleEvent>();
            var hit = Damage(target, FlingDamage, thrower, DamageKind.Attack, after);
            events.Add(new FlingEvent(thrower, goblin, from, to, target, hit));
            events.AddRange(after);
            CheckEnd();
            return events;
        }

        // ---------- Bandits ----------

        /// <summary>Behind Cover: a unit with it standing next to a rock (or ice, or a barricade) can't be hit from range.</summary>
        public bool InCover(Unit unit) =>
            unit.Has(Ability.BehindCover) && Cells(unit).Any(c => c.Neighbours().Any(Board.IsRock));

        /// <summary>Caltrops: on the cutpurse's own tile or a free tile next to it, with none there yet.</summary>
        public bool CanDropCaltrops(Unit unit, GridPos tile) =>
            !IsOver && unit.IsAlive && unit.Has(Ability.Caltrops) && unit.Ap >= AttackCost && Ready(unit, ActionKind.Caltrops) &&
            (tile == unit.Pos || (tile.StepsTo(unit.Pos) == 1 && IsFree(tile))) && !HasHazard(tile) && !Board.IsObstacle(tile);

        internal List<BattleEvent> DoCaltrops(Unit unit, GridPos tile)
        {
            UseUp(unit, ActionKind.Caltrops);
            _caltrops[tile] = unit;
            return new List<BattleEvent> { new TileEvent("caltrops", tile, true, unit) };
        }

        /// <summary>The tiles a unit could shoot at from where it stands: in range, in line of sight, not shrouded.</summary>
        private List<GridPos> SightTiles(Unit unit) =>
            Board.AllTiles().Where(p =>
            {
                var steps = Distance(p, 1, unit);
                return steps >= 1 && steps <= RangeFrom(unit, unit.Pos) && !Board.IsObstacle(p) && !IsShroudedFrom(p, unit.Team) &&
                       Cells(unit).Any(c => Board.HasLineOfSight(c, p));
            }).ToList();

        /// <summary>Overwatch: until its next turn it watches every tile it could shoot at.</summary>
        internal List<BattleEvent> DoOverwatch(Unit unit)
        {
            UseUp(unit, ActionKind.Overwatch);
            var tiles = SightTiles(unit);
            _overwatch[unit] = new HashSet<GridPos>(tiles);
            return new List<BattleEvent> { new OverwatchEvent(unit, tiles) };
        }

        /// <summary>The watcher (alive, still on overwatch) that would shoot a unit stepping onto this tile, if any.</summary>
        private Unit WatcherOf(GridPos tile, Unit mover) =>
            mover.IsHidden ? null : _overwatch.Where(o => o.Key.IsAlive && o.Key.Team != mover.Team && o.Value.Contains(tile)).Select(o => o.Key).OrderBy(u => u.Id).FirstOrDefault();

        /// <summary>Bull Rush: a melee hit on an adjacent 1x1 unit that also pushes it 1 tile.</summary>
        public bool CanBullRush(Unit unit, Unit target) =>
            unit.Has(Ability.BullRush) && Ready(unit, ActionKind.BullRush) && CanAttack(unit, target) &&
            target.Size == 1 && unit.Size == 1 && Distance(unit, target) == 1;

        internal List<BattleEvent> DoBullRush(Unit unit, Unit target)
        {
            UseUp(unit, ActionKind.BullRush);
            var events = new List<BattleEvent>();
            var after = new List<BattleEvent>();
            var hit = Damage(target, DamageAgainst(unit, target), unit, DamageKind.Attack, after);
            unit.AttackedThisTurn = true;
            events.Add(new AttackEvent(unit, hit.Target, hit.Damage, hit.HpAfter));
            events.AddRange(after);
            Thorns(unit, hit.Target, events);
            Riposte(hit.Target, unit, events);
            if (hit.Target == target && target.IsAlive && unit.IsAlive)
                Push(target, StraightDirection(unit.Pos, target.Pos).Value, unit, events, hurtsBlocker: true);
            CheckEnd();
            return events;
        }

        // ---------- Elementals and skeletons ----------

        /// <summary>Upheaval: a 1x1 unit of the other side within <see cref="UpheavalRange"/> steps, seen or not.</summary>
        public bool CanUpheave(Unit unit, Unit target) =>
            !IsOver && unit.IsAlive && unit.Has(Ability.Upheaval) && unit.Ap >= AttackCost && Ready(unit, ActionKind.Upheaval) &&
            target != null && target.IsAlive && target.IsPlaced && target.Team != unit.Team && !target.IsHidden && !target.IsObjective &&
            target.Size == 1 && Distance(unit, target) <= UpheavalRange && !IsShroudedFrom(target, unit.Team);

        /// <summary>Which way Upheaval throws the target: straight away from the caster, along the longer axis (rows on a tie).</summary>
        public static GridPos UpheavalDirection(Unit unit, Unit target)
        {
            var dx = target.Pos.X - unit.Pos.X;
            var dy = target.Pos.Y - unit.Pos.Y;
            return Math.Abs(dy) >= Math.Abs(dx) ? new GridPos(0, Math.Sign(dy)) : new GridPos(Math.Sign(dx), 0);
        }

        /// <summary>
        /// Upheaval: the ground bursts up under the target. It takes <see cref="UpheavalDamage"/> and is pushed away
        /// from the caster (collision damage if blocked). No rock is left behind (rocks were too much, 2026-10-30).
        /// </summary>
        internal List<BattleEvent> DoUpheaval(Unit unit, Unit target)
        {
            UseUp(unit, ActionKind.Upheaval);
            var dir = UpheavalDirection(unit, target);
            var after = new List<BattleEvent>();
            var hit = Damage(target, UpheavalDamage, unit, DamageKind.Blast, after);
            var events = new List<BattleEvent> { new UpheavalEvent(unit, hit) };
            events.AddRange(after);
            if (target.IsAlive)
                Push(target, dir, unit, events);

            CheckEnd();
            return events;
        }

        /// <summary>Grasp: an adjacent unit it could attack, not already pinned.</summary>
        public bool CanGrasp(Unit unit, Unit target) =>
            unit.Has(Ability.Grasp) && Ready(unit, ActionKind.Grasp) && CanAttack(unit, target) && Distance(unit, target) == 1 &&
            !target.IsObjective && target.SnareTurns == 0 && !target.Has(Ability.Colossus);

        /// <summary>Grasp: no damage, but the target is pinned.</summary>
        internal List<BattleEvent> DoGrasp(Unit unit, Unit target)
        {
            UseUp(unit, ActionKind.Grasp);
            target.SnareTurns = 1;
            return new List<BattleEvent> { new StatusEvent(unit, target, StatusKind.Grasp, 1) };
        }

        /// <summary>Howl: every wolf of its side that hasn't acted yet this turn gets <see cref="HowlMoves"/> extra moves.</summary>
        internal List<BattleEvent> DoHowl(Unit leader)
        {
            UseUp(leader, ActionKind.Howl);
            var wolves = Living(leader.Team).Where(u => u.IsWolf && !u.IsDone && CanMove(u)).ToList();
            foreach (var wolf in wolves)
                wolf.Moves += HowlMoves;
            return new List<BattleEvent> { new HowlEvent(leader, wolves, HowlMoves) };
        }

        // ---------- Demons ----------

        /// <summary>
        /// A unit just died: an imp's Blood Offering gives Fury to the nearest other demon of its side within
        /// <see cref="OfferingRange"/>, and every Bloodlust demon next to it (either side's death) gains Fury.
        /// </summary>
        private void DemonDeath(Unit dead, List<BattleEvent> after)
        {
            // A contract on a dead unit is simply gone; a dead Devil (killed, fallen or dropped through the roof) voids its own.
            dead.ContractBy = null;
            if (dead.Has(Ability.SoulContract))
                BreakContracts(dead, after);

            if (dead.Has(Ability.BloodOffering))
            {
                var heir = Living(dead.Team).Where(u => u != dead && u.IsDemon && Distance(u, dead) <= OfferingRange)
                    .OrderBy(u => Distance(u, dead)).ThenBy(u => u.Id).FirstOrDefault();
                if (heir != null)
                    GainFury(heir, OfferingFury, dead, after);
            }

            foreach (var demon in _units.Where(u => u != dead && u.IsPlaced && u.IsAlive && u.Has(Ability.Bloodlust) && Distance(u, dead) == 1).ToList())
                GainFury(demon, BloodlustFury, demon, after);
        }

        /// <summary>Adds Fury up to <see cref="MaxFury"/>; nothing is shown once it's full.</summary>
        private static void GainFury(Unit demon, int amount, Unit source, List<BattleEvent> after)
        {
            var gained = Math.Min(amount, MaxFury - demon.Fury);
            if (gained <= 0)
                return;
            demon.Fury += gained;
            after.Add(new EffectEvent(EffectKind.Fury, demon, gained, demon.Hp, source));
        }

        /// <summary>Hellswap: a 1x1 unit of the other side 2 to <see cref="HellswapRange"/> steps away, seen or not.</summary>
        public bool CanHellswap(Unit imp, Unit target) =>
            !IsOver && imp.IsAlive && imp.Has(Ability.Hellswap) && imp.Ap >= AttackCost && Ready(imp, ActionKind.Hellswap) && imp.Size == 1 &&
            target != null && target.IsAlive && target.IsPlaced && target.Team != imp.Team && !target.IsHidden && !target.IsObjective &&
            target.Size == 1 && !target.Has(Ability.Colossus) && Distance(imp, target) >= 2 && Distance(imp, target) <= HellswapRange &&
            !IsShroudedFrom(target, imp.Team);

        /// <summary>Hellswap: the imp and the target trade tiles. Nothing on either tile goes off (it's a teleport).</summary>
        internal List<BattleEvent> DoHellswap(Unit imp, Unit target)
        {
            UseUp(imp, ActionKind.Hellswap);
            var impFrom = imp.Pos;
            var targetFrom = target.Pos;
            imp.Pos = targetFrom;
            target.Pos = impFrom;
            var events = new List<BattleEvent> { new SwapEvent(imp, target, impFrom, targetFrom) };
            CheckEnd();
            return events;
        }

        /// <summary>Rending Blow: a unit next to it, while it has more HP than the blow costs.</summary>
        public bool CanRendingBlow(Unit unit, Unit target) =>
            unit.Has(Ability.RendingBlow) && Ready(unit, ActionKind.RendingBlow) && unit.Hp > RendingBlowCost &&
            CanAttack(unit, target) && Distance(unit, target) == 1;

        /// <summary>Rending Blow: it loses <see cref="RendingBlowCost"/> HP (nothing stops that), then hits for its damage +<see cref="RendingBlowBonus"/>.</summary>
        internal List<BattleEvent> DoRendingBlow(Unit unit, Unit target)
        {
            UseUp(unit, ActionKind.RendingBlow);
            unit.Hp -= RendingBlowCost;
            var events = new List<BattleEvent> { new TickEvent("blood price", new Hit(unit, RendingBlowCost, unit.Hp)) };
            var after = new List<BattleEvent>();
            var hit = Damage(target, DamageAgainst(unit, target) + RendingBlowBonus, unit, DamageKind.Attack, after);
            unit.AttackedThisTurn = true;
            unit.MarkStruck(target);
            events.Add(new AttackEvent(unit, hit.Target, hit.Damage, hit.HpAfter, "rending blow"));
            events.AddRange(after);
            Thorns(unit, hit.Target, events);
            Riposte(hit.Target, unit, events);
            CheckEnd();
            return events;
        }

        /// <summary>Soul Contract: a unit of the other side within <see cref="ContractRange"/> steps (no line of sight needed) without a contract yet.</summary>
        public bool CanSignContract(Unit devil, Unit target) =>
            !IsOver && devil.IsAlive && devil.Has(Ability.SoulContract) && devil.Ap >= AttackCost && Ready(devil, ActionKind.SoulContract) &&
            target != null && target.IsAlive && target.IsPlaced && target.Team != devil.Team && !target.IsHidden && !target.IsObjective &&
            target.ContractBy == null && Distance(devil, target) <= ContractRange && !IsShroudedFrom(target, devil.Team);

        /// <summary>Soul Contract: the target takes <see cref="ContractDamage"/> damage (reductions apply) at the start of the next enemy turn, unless the Devil takes damage first.</summary>
        internal List<BattleEvent> DoSoulContract(Unit devil, Unit target)
        {
            UseUp(devil, ActionKind.SoulContract);
            target.ContractBy = devil;
            return new List<BattleEvent> { new StatusEvent(devil, target, StatusKind.Contract, ContractDamage) };
        }

        /// <summary>The Devil took damage: every contract it signed is void.</summary>
        private void BreakContracts(Unit devil, List<BattleEvent> after)
        {
            foreach (var u in _units.Where(u => u.ContractBy == devil).OrderBy(u => u.Id).ToList())
            {
                u.ContractBy = null;
                if (u.IsAlive && u.IsPlaced)
                    after.Add(new StatusEvent(devil, u, StatusKind.ContractBroken, 0));
            }
        }

        /// <summary>Start of the enemy turn: every contract still standing comes due (if its Devil and the unit still live).</summary>
        private void CollectContracts(List<BattleEvent> events)
        {
            foreach (var u in _units.Where(u => u.ContractBy != null).OrderBy(u => u.Id).ToList())
            {
                var devil = u.ContractBy;
                u.ContractBy = null;
                if (IsOver || !u.IsAlive || !u.IsPlaced || !devil.IsAlive)
                    continue;
                var after = new List<BattleEvent>();
                events.Add(new TickEvent("contract", Damage(u, ContractDamage, devil, DamageKind.Tick, after), devil));
                events.AddRange(after);
                CheckEnd();
            }
        }

        /// <summary>
        /// The tiles a Fire Breath at the target would cover: <see cref="FireBreathLength"/> tiles in a straight line from the
        /// fiend, stopping at a rock. Null if the target isn't in that line.
        /// </summary>
        public List<GridPos> FireBreathLine(Unit fiend, Unit target) =>
            fiend.Size != 1 || target == null || target.Size != 1 ? null : FireBreathLineFrom(fiend.Pos, target.Pos);

        /// <summary>The Fire Breath line from a 1x1 fiend standing on <paramref name="from"/> at a target tile, or null (also for the intent).</summary>
        public List<GridPos> FireBreathLineFrom(GridPos from, GridPos target)
        {
            var dir = StraightDirection(from, target);
            if (dir == null || from.StepsTo(target) > FireBreathLength)
                return null;
            var line = new List<GridPos>();
            for (var p = from + dir.Value; line.Count < FireBreathLength && Board.InBounds(p) && !Board.IsRock(p); p += dir.Value)
                line.Add(p);
            return line.Contains(target) ? line : null;
        }

        /// <summary>Fire Breath: one of your units 1-2 tiles away in a straight line, with no rock in between.</summary>
        public bool CanFireBreath(Unit fiend, Unit target) =>
            !IsOver && fiend.IsAlive && fiend.Has(Ability.FireBreath) && fiend.Ap >= AttackCost && Ready(fiend, ActionKind.FireBreath) &&
            target != null && target.IsAlive && target.IsPlaced && target.Team != fiend.Team && !target.IsHidden &&
            !IsShroudedFrom(target, fiend.Team) && FireBreathLine(fiend, target) != null;

        /// <summary>The units of the other side a Fire Breath at the target would hit.</summary>
        public List<Unit> FireBreathVictims(Unit fiend, Unit target)
        {
            var line = FireBreathLine(fiend, target);
            return line == null
                ? new List<Unit>()
                : _units.Where(u => u.IsPlaced && u.IsAlive && u.Team != fiend.Team && Cells(u).Any(line.Contains)).ToList();
        }

        /// <summary>
        /// Fire Breath: <see cref="FireBreathDamage"/> (plus Fury) to each unit of the other side on the line, then the line
        /// burns for <see cref="BurnRounds"/> rounds. Demons are Hellborn, so it only hurts your side.
        /// </summary>
        internal List<BattleEvent> DoFireBreath(Unit fiend, Unit target)
        {
            UseUp(fiend, ActionKind.FireBreath);
            var line = FireBreathLine(fiend, target);
            var after = new List<BattleEvent>();
            var hits = FireBreathVictims(fiend, target).Select(u => Damage(u, FireBreathDamage + fiend.Fury, fiend, DamageKind.Blast, after)).ToList();
            fiend.AttackedThisTurn = true;
            var events = new List<BattleEvent> { new AreaAttackEvent(fiend, "fire breath", target.Pos, line, hits) };
            events.AddRange(after);
            foreach (var p in line)
            {
                _burning[p] = BurnRounds;
                _burnOwners[p] = fiend;
                events.Add(new TileEvent("burn", p, true, fiend));
            }

            CheckEnd();
            return events;
        }

        internal List<BattleEvent> DoBomb(Unit thrower, Unit target)
        {
            UseUp(thrower, ActionKind.Bomb);
            _bombs.Add(new Bomb(target.Pos, thrower, thrower.Stats.BombDamage));
            return new List<BattleEvent> { new BombThrowEvent(thrower, target.Pos) };
        }

        /// <summary>
        /// Raises the given bone pile and every other one within the raiser's range that nobody stands on,
        /// for one action. They wait until the next enemy turn to act. A fallen unit of yours (Soul Harvest) rises as a new
        /// unit on the raiser's side (<see cref="UnitStats.Harvested"/>) and its pile is used up.
        /// </summary>
        internal List<BattleEvent> DoRaise(Unit raiser, Unit remains)
        {
            UseUp(raiser, ActionKind.Raise);
            var piles = new List<Unit> { remains };
            piles.AddRange(RaisablePiles(raiser).Where(r => r != remains && Distance(raiser, r) <= raiser.Stats.RaiseRange && UnitAt(r.Pos) == null && !Board.IsObstacle(r.Pos))
                .OrderBy(r => Distance(raiser, r)).ThenBy(r => r.Id));
            var events = new List<BattleEvent>();
            foreach (var pile in piles)
            {
                if (UnitAt(pile.Pos) != null || Board.IsObstacle(pile.Pos))
                    continue;
                if (pile.Team != raiser.Team)
                {
                    pile.PileGone = true;
                    var risen = new Unit(_nextId++, raiser.Team, UnitStats.Harvested(pile.Stats)) { Pos = pile.Pos, IsPlaced = true, IsDone = true, Ap = 0 };
                    _units.Add(risen);
                    events.Add(new RaiseEvent(raiser, risen, pile));
                    continue;
                }

                pile.Hp = pile.MaxHp;
                pile.Ap = 0;
                pile.IsDone = true;
                pile.PoisonTurns = 0;
                events.Add(new RaiseEvent(raiser, pile));
            }

            return events;
        }

        /// <summary>
        /// Blows up a mine, bomb or barricade at <paramref name="center"/>: every living unit in the area
        /// (the 3x3, or just the tile for a bomb or mine) takes the damage, friend or foe (unless the owner is Blast
        /// Proof). Mines and bombs inside the area go off too, one after another.
        /// </summary>
        internal void Explode(GridPos center, int damage, string cause, Unit owner, List<BattleEvent> events)
        {
            var queue = new Queue<(GridPos center, int damage, string cause, Unit owner, int depth)>();
            queue.Enqueue((center, damage, cause, owner, 0));
            _mines.RemoveAll(m => m.Tile == center);
            _bombs.RemoveAll(b => b.Tile == center);

            while (queue.Count > 0)
            {
                var blast = queue.Dequeue();
                var area = ExplosionArea(blast.cause, blast.center);
                var safeTeam = blast.owner != null && blast.owner.Has(Ability.BlastProof) ? blast.owner.Team : (Team?)null;
                var victims = _units.Where(u => u.IsPlaced && u.IsAlive && u.Team != safeTeam && Cells(u).Any(area.Contains)).ToList();
                var hits = new List<Hit>();
                var after = new List<BattleEvent>();
                foreach (var victim in victims)
                    hits.Add(Damage(victim, blast.damage, blast.owner, DamageKind.Blast, after));
                events.Add(new ExplosionEvent(blast.cause, blast.owner, blast.center, area, hits, blast.depth));
                events.AddRange(after);

                // Incendiary: the sapper's mines, bombs and blasts leave the ground burning.
                if (blast.owner != null && blast.owner.Has(Ability.Incendiary))
                    foreach (var p in area)
                        SetBurning(p, blast.owner, events);

                foreach (var mine in _mines.Where(m => area.Contains(m.Tile)).ToList())
                {
                    _mines.Remove(mine);
                    queue.Enqueue((mine.Tile, mine.Damage, "mine", mine.Owner, blast.depth + 1));
                }

                foreach (var bomb in _bombs.Where(b => area.Contains(b.Tile)).ToList())
                {
                    _bombs.Remove(bomb);
                    queue.Enqueue((bomb.Tile, bomb.Damage, "bomb", bomb.Owner, blast.depth + 1));
                }
            }

            CheckEnd();
        }

        /// <summary>
        /// Every point of damage goes through here, so abilities apply to all sources. Guard redirects
        /// attacks, Shield Wall and Tough reduce, Dodge and wards absorb, Lucky Charm can stop a death,
        /// and a kill triggers the killer's Vampire Fang and Momentum. Attacks also carry
        /// Poison and Chill. Follow-up effects are added to <paramref name="after"/>.
        /// </summary>
        private Hit Damage(Unit target, int amount, Unit source, DamageKind kind, List<BattleEvent> after)
        {
            var hostile = source != null && source.Team != target.Team;
            amount = FavoredAmount(source, target, amount, kind);

            if (kind == DamageKind.Attack && hostile)
            {
                var guard = _units.FirstOrDefault(g => g != target && g.Team == target.Team && g.IsPlaced && g.IsAlive &&
                                                        g.Has(Ability.Guard) && Distance(g, target) == 1);
                if (guard != null)
                {
                    after.Add(new EffectEvent(EffectKind.Guard, guard, 0, guard.Hp, guard, target));
                    target = guard;
                }
            }

            // Marked (the Archer's keyword): the next hit from the other side, of any kind but ticks, deals more and uses it up.
            // Death Mark spreads whenever a marked unit dies, whatever killed it (ticks and friendly fire too).
            var spreadMark = target.Marked && target.MarkSpreads;
            if (hostile && kind != DamageKind.Tick && target.Marked && amount > 0)
            {
                amount += MarkBonus;
                target.Marked = false;
                after.Add(new EffectEvent(EffectKind.Mark, target, MarkBonus, target.Hp, source));
            }

            if (IsShielded(target))
                amount = Math.Max(1, amount - 1);
            if (target.Has(Ability.Tough))
                amount = Math.Max(1, amount - 1);
            if (target.Affix == Affix.Armored)
                amount = Math.Max(1, amount - Champions.ArmoredReduction);
            if (target.ShieldUpTurns > 0)
                amount = Math.Max(1, amount - ShieldUpReduction);

            if (kind == DamageKind.Attack && hostile && target.Has(Ability.Dodge) && !target.DodgeUsed)
            {
                target.DodgeUsed = true;
                after.Add(new EffectEvent(EffectKind.Dodge, target, 0, target.Hp, target));
                amount = 0;
            }

            // Totems are warded against their own side: goblin bombs landing around them don't hurt them.
            if (target.IsObjective && target.Team == Team.Enemy && source != null && source.Team == target.Team)
                amount = 0;

            if (target.CryShield > 0 && amount > 0)
            {
                var absorbed = Math.Min(target.CryShield, amount);
                target.CryShield -= absorbed;
                amount -= absorbed;
                after.Add(new EffectEvent(EffectKind.Absorb, target, absorbed, target.Hp, target));
            }

            if (target.MoonShield > 0 && amount > 0)
            {
                var absorbed = Math.Min(target.MoonShield, amount);
                target.MoonShield -= absorbed;
                amount -= absorbed;
                after.Add(new EffectEvent(EffectKind.Absorb, target, absorbed, target.Hp, target));
            }

            if (target.Shield > 0 && amount > 0)
            {
                var absorbed = Math.Min(target.Shield, amount);
                target.Shield -= absorbed;
                amount -= absorbed;
                after.Add(new EffectEvent(EffectKind.Absorb, target, absorbed, target.Hp, target));
            }

            var hp = target.Hp - amount;
            if (hp <= 0 && target.Has(Ability.LuckyCharm) && !target.LastStandUsed)
            {
                hp = 1;
                target.LastStandUsed = true;
                after.Add(new EffectEvent(EffectKind.LastStand, target, 0, 1, target));
            }

            // Stone Heart: a boss can lose at most one phase a round; the damage past it is lost.
            var phaseStarts = false;
            if (target.Has(Ability.StoneHeart) && target.PhaseFloor > 0 && hp <= target.PhaseFloor)
            {
                phaseStarts = target.Hp > target.PhaseFloor;
                hp = target.PhaseFloor;
                amount = Math.Max(0, target.Hp - hp);
            }

            var wasInWrath = target.InWrath;
            target.Hp = Math.Max(0, hp);
            if (phaseStarts)
                EnterPhase(target, after);
            if (!wasInWrath && target.InWrath)
                after.Add(new StatusEvent(target, target, StatusKind.Wrath, 0));

            // Vampiric banner: every attack of yours that lands heals the attacker.
            if (hostile && kind == DamageKind.Attack && amount > 0 && source.Team == Team.Player && source.IsAlive &&
                HasBanner(Banner.Vampiric) && source.Hp < source.MaxHp)
            {
                var healed = Math.Min(Core.Banners.VampiricHeal, source.MaxHp - source.Hp);
                source.Hp += healed;
                after.Add(new EffectEvent(EffectKind.Heal, source, healed, source.Hp, source));
            }

            if (!target.IsAlive && hostile && source.IsAlive)
            {
                if (source.Has(Ability.VampireFang) && source.Hp < source.MaxHp)
                {
                    var healed = Math.Min(VampireHeal, source.MaxHp - source.Hp);
                    source.Hp += healed;
                    after.Add(new EffectEvent(EffectKind.Heal, source, healed, source.Hp, source));
                }

                if (source.Has(Ability.KillRefund) && kind == DamageKind.Attack)
                {
                    source.Ap += KillRefundAp;
                    // A slowed unit chose to act this turn: the kill gives the action back, not the move.
                    if (!source.SlowedThisTurn)
                    {
                        source.Moves += KillRefundMoves;
                        source.ActedThisTurn = false;
                    }
                    after.Add(new EffectEvent(EffectKind.Refund, source, KillRefundAp, source.Hp, source));
                }

                // Into the Shadows: a kill hides the killer until its side's next turn, with no ambush bonus, at most
                // once every few turns.
                if (source.Has(Ability.IntoShadows) && kind == DamageKind.Attack && !source.IsHidden &&
                    TurnNumber - source.ShadowsTurn >= IntoShadowsCooldown)
                {
                    source.ShadowsTurn = TurnNumber;
                    source.HiddenTurns = VanishTurns;
                    source.AmbushUsed = true;
                    after.Add(new StatusEvent(source, source, StatusKind.Vanish, VanishTurns));
                }
            }

            // Soul Contract: any damage to the Devil voids the contracts it signed.
            if (amount > 0 && target.Has(Ability.SoulContract))
                BreakContracts(target, after);

            if (!target.IsAlive)
            {
                // Death Mark: the mark jumps to every unit of the dead one's side next to it.
                if (spreadMark)
                    foreach (var near in _units.Where(u => u != target && u.IsPlaced && u.IsAlive && u.Team == target.Team && !u.Marked && Distance(u, target) == 1).ToList())
                    {
                        near.Marked = true;
                        near.MarkSpreads = true;
                        after.Add(new StatusEvent(source ?? target, near, StatusKind.Mark, MarkBonus));
                    }

                BreakPackBond(target, after);
                DemonDeath(target, after);
                // Soul Harvest: one of your units dying while a harvester stands leaves a bone pile it can raise.
                if (target.Team == Team.Player && !target.IsSummon && !target.IsObjective && !target.Stats.LeavesRemains &&
                    Living(Team.Enemy).Any(u => u.Has(Ability.SoulHarvest)))
                    target.LeftPile = true;
                // One tile, one pile: the newer one replaces any older pile under it.
                if (target.IsRemains)
                    foreach (var old in _units.Where(u => u != target && u.IsRemains && u.Pos == target.Pos).ToList())
                    {
                        old.PileGone = true;
                        after.Add(new PileReplacedEvent(old));
                    }

                if (target.Has(Ability.PowderKeg))
                    Explode(target.Pos, PowderKegDamage, "powder keg", target, after);

                // Crumbling Roof: a statue crashing down cracks the floor under it.
                if (!target.Fell && target.Size == 1 && target.IsPlaced && !target.IsObjective)
                    CrackFloor(target.Pos, after);

                // A Vengeful champion hits back at everyone of the other side next to it as it dies.
                if (target.Affix == Affix.Vengeful && !target.Fell)
                {
                    var hits = new List<Hit>();
                    var blast = new List<BattleEvent>();
                    foreach (var foe in _units.Where(u => u.Team != target.Team && u.IsAlive && u.IsPlaced && !u.IsObjective && Distance(target, u) == 1).ToList())
                        hits.Add(Damage(foe, Champions.VengefulDamage, target, DamageKind.Blast, blast));
                    after.Add(new ExplosionEvent("vengeful", target, target.Pos, BlastArea(target.Pos), hits, 0));
                    after.AddRange(blast);
                }
            }

            if (!target.IsAlive && target.Stats.SplitsInto != null)
                Split(target, after);

            // A cutpurse grabs gold with its first landed hit, then runs for it.
            if (hostile && kind == DamageKind.Attack && amount > 0 && source.IsAlive && source.Stats.StealsGold > 0 && source.CarryingLoot == 0)
            {
                source.CarryingLoot = source.Stats.StealsGold;
                after.Add(new EffectEvent(EffectKind.Steal, source, source.CarryingLoot, source.Hp, source));
            }

            if (target.IsAlive && hostile && kind == DamageKind.Attack)
            {
                if (source.Has(Ability.Poison))
                {
                    target.PoisonTurns = PoisonTurns;
                    target.PoisonSource = source;
                    after.Add(new StatusEvent(source, target, StatusKind.Poison, PoisonTurns));
                }

                if (source.Has(Ability.Chill) && !target.Has(Ability.Colossus))
                {
                    target.SlowNext = 1;
                    after.Add(new StatusEvent(source, target, StatusKind.Slow, 1));
                }
            }

            return new Hit(target, amount, target.Hp);
        }

        /// <summary>
        /// A dying Elemental becomes two Shards: one on its own tile, one on a free neighbour (if any).
        /// They wait until the next enemy turn to act.
        /// </summary>
        private void Split(Unit parent, List<BattleEvent> after)
        {
            var tiles = new List<GridPos> { parent.Pos };
            tiles.AddRange(parent.Pos.Neighbours().Where(IsFree).OrderBy(p => p.Y).ThenBy(p => p.X));
            var children = new List<Unit>();
            foreach (var tile in tiles.Where(t => IsFree(t)).Take(2))
            {
                var child = new Unit(_nextId++, parent.Team, parent.Stats.SplitsInto) { Pos = tile, IsPlaced = true, IsDone = true };
                _units.Add(child);
                children.Add(child);
            }

            if (children.Count > 0)
                after.Add(new SplitEvent(parent, children));
        }

        /// <summary>Gold carried off the board by escaped cutpurses this battle.</summary>
        public int StolenGold { get; private set; }

        /// <summary>A unit leaves the board with whatever loot it carries.</summary>
        internal void Escape(Unit unit, List<BattleEvent> events)
        {
            unit.Escaped = true;
            unit.IsPlaced = false;
            StolenGold += unit.CarryingLoot;
            events.Add(new EscapeEvent(unit, unit.CarryingLoot));
            CheckEnd();
        }

        /// <summary>
        /// Slam: every enemy of the slammer within 2 tiles takes <see cref="SlamDamage"/>, then every other unit
        /// within 2 tiles is shoved 1 tile away from it.
        /// </summary>
        internal List<BattleEvent> DoSlam(Unit slammer)
        {
            UseUp(slammer, ActionKind.Slam);
            var events = new List<BattleEvent>();
            var area = Board.AllTiles().Where(p => Distance(p, 1, slammer) <= 2 && !Cells(slammer).Contains(p)).ToList();
            var after = new List<BattleEvent>();
            var hits = _units.Where(u => u.IsPlaced && u.IsAlive && u.Team != slammer.Team && !u.IsObjective && Distance(slammer, u) <= 2).ToList()
                .Select(u => Damage(u, SlamDamage, slammer, DamageKind.Blast, after)).ToList();
            events.Add(new AreaAttackEvent(slammer, "slam", slammer.Pos, area, hits));
            events.AddRange(after);
            var cx = slammer.Pos.X + (slammer.Size - 1) * 0.5;
            var cy = slammer.Pos.Y + (slammer.Size - 1) * 0.5;
            var shoved = _units.Where(u => u != slammer && u.IsPlaced && u.IsAlive && u.Size == 1 && Distance(slammer, u) <= 2)
                .OrderByDescending(u => Distance(slammer, u)).ToList();
            foreach (var u in shoved)
            {
                var dx = u.Pos.X - cx;
                var dy = u.Pos.Y - cy;
                var dir = Math.Abs(dx) >= Math.Abs(dy) ? new GridPos(Math.Sign(dx), 0) : new GridPos(0, Math.Sign(dy));
                Push(u, dir, slammer, events);
            }

            CheckEnd();
            return events;
        }

        /// <summary>Thorns (Thorned Mail, the Fiend): a melee attacker takes damage back from the unit it hit.</summary>
        private void Thorns(Unit attacker, Unit target, List<BattleEvent> events)
        {
            if (!target.Has(Ability.Thorns) || !attacker.IsAlive || target.Team == attacker.Team)
                return;
            var after = new List<BattleEvent>();
            var hit = Damage(attacker, ThornsDamage, target, DamageKind.Tick, after);
            events.Add(new EffectEvent(EffectKind.Thorns, attacker, hit.Damage, hit.HpAfter, target));
            events.AddRange(after);
        }

        /// <summary>Riposte: a defender with Counter hits a melee attacker straight back (no AP).</summary>
        private void Riposte(Unit defender, Unit attacker, List<BattleEvent> events)
        {
            if (!defender.IsAlive || !attacker.IsAlive || defender.Team == attacker.Team || Distance(defender, attacker) != 1)
                return;
            var can = defender.Has(Ability.CounterAll) || (defender.Has(Ability.Counter) && !defender.CounterUsed);
            if (!can)
                return;
            defender.CounterUsed = true;
            var after = new List<BattleEvent>();
            var hit = Damage(attacker, DamageAgainst(defender, attacker), defender, DamageKind.Blast, after);
            events.Add(new AttackEvent(defender, hit.Target, hit.Damage, hit.HpAfter));
            events.AddRange(after);
        }

        /// <summary>
        /// Start of a side's turn: poison and burning ground hurt that side's units, timed tiles count
        /// down (once per round, at the player's turn), then AP and moves are refilled (no moves when
        /// chilled), and snares take effect.
        /// </summary>
        private void BeginTurn(Team team, List<BattleEvent> events)
        {
            // The other side's turn is over: a pin or slow that held it this turn no longer counts (Prey, Cheap Shot, Shatter).
            foreach (var u in _units.Where(u => u.Team != team))
            {
                u.RootedThisTurn = false;
                u.SlowedThisTurn = false;
            }

            if (team == Team.Enemy)
            {
                StrikeSunMarks(events);
                CollectContracts(events);
            }

            foreach (var u in Living(team).ToList())
            {
                if (!u.Has(Ability.Hellborn) && (_burning.ContainsKey(u.Pos) || Cells(u).Any(_burning.ContainsKey)))
                {
                    var after = new List<BattleEvent>();
                    events.Add(new TickEvent("burn", Damage(u, BurnDamage, null, DamageKind.Tick, after), _burnOwners[Cells(u).First(_burning.ContainsKey)]));
                    events.AddRange(after);
                }

                if (u.IsAlive && u.PoisonTurns > 0)
                {
                    u.PoisonTurns--;
                    var after = new List<BattleEvent>();
                    events.Add(new TickEvent("poison", Damage(u, PoisonDamage, null, DamageKind.Tick, after), u.PoisonSource));
                    events.AddRange(after);
                }
            }

            if (team == Team.Player)
            {
                // In tile order, so a copied battle reports them in the same order.
                foreach (var p in _burning.Keys.OrderBy(p => p.Y).ThenBy(p => p.X).ToList())
                {
                    if (--_burning[p] > 0)
                        continue;
                    _burning.Remove(p);
                    _burnOwners.Remove(p);
                    events.Add(new TileEvent("burn", p, false, null));
                }

                foreach (var p in _ice.Keys.OrderBy(p => p.Y).ThenBy(p => p.X).ToList())
                {
                    if (--_ice[p] > 0)
                        continue;
                    _ice.Remove(p);
                    Board.RemoveObstacle(p);
                    events.Add(new TileEvent("ice", p, false, null));
                }

                // Vanish wears off after its enemy turns.
                foreach (var u in Living(team).Where(u => u.IsHidden).ToList())
                    if (--u.HiddenTurns == 0)
                        events.Add(new StatusEvent(u, u, StatusKind.Reveal, 0));
            }

            // Overwatch lasts until the watcher's own next turn.
            foreach (var u in Living(team))
                _overwatch.Remove(u);

            // The Warrior's timed effects count down at the start of its side's turn.
            foreach (var u in Living(team))
            {
                u.ZocStrikeUsed = false;
                u.TwincastUsed = false;
                u.Overchanneled = false;
                u.PlantedTurns = Math.Max(0, u.PlantedTurns - 1);
                u.ShieldUpTurns = Math.Max(0, u.ShieldUpTurns - 1);
                u.TauntTurns = Math.Max(0, u.TauntTurns - 1);
                if (u.BattlecryTurns > 0 && --u.BattlecryTurns == 0)
                    u.CryShield = 0;
            }

            foreach (var u in Living(team))
            {
                u.Ap = u.MaxAp + Math.Min(u.Ap, MaxCarriedAp);
                if (u.Enraged && team == Team.Enemy)
                {
                    u.Ap++;
                    u.Enraged = false;
                }
                u.SlowedThisTurn = u.SlowNext > 0;
                u.SlowNext = 0;
                u.Moves = u.MaxMoves;
                u.RootedThisTurn = u.SnareTurns > 0;
                if (u.RootedThisTurn)
                    u.SnareTurns--;
                u.IsDone = false;
                u.MovedThisTurn = false;
                u.AttackedThisTurn = false;
                u.ActedThisTurn = false;
                u.TickCooldowns();
            }

            BiteChain = 0;

            // Riposte and Dodge work once per enemy turn.
            if (team == Team.Enemy)
            {
                foreach (var u in Living(Team.Player))
                {
                    u.CounterUsed = false;
                    u.DodgeUsed = false;
                }
            }

            // Regenerating champions heal as their side's turn starts.
            foreach (var u in Living(team).Where(u => u.Affix == Affix.Regenerating && u.Hp < u.MaxHp).ToList())
            {
                var healed = Math.Min(Champions.RegenHeal, u.MaxHp - u.Hp);
                u.Hp += healed;
                events.Add(new EffectEvent(EffectKind.Heal, u, healed, u.Hp, u));
            }

            if (team == Team.Player)
            {
                ApplyOmens(events);
                ApplyField(events);
                MarkRockfall(events);
                SetPhaseFloors();
            }

            CheckEnd();
        }

        /// <summary>
        /// Start of the player's turn: last round's shrouds and moon shields fade, then each omen picks a
        /// random unit (never the objective): the Sun marks a tile under one of the other side's units, the
        /// Night shrouds the plus around one of its own side's units, the Moon shields one of its own side's units.
        /// </summary>
        private void ApplyOmens(List<BattleEvent> events)
        {
            if (IsOver || _omens.Count == 0)
                return;
            _shroud.Clear();
            foreach (var u in _units)
                u.MoonShield = 0;

            foreach (var (omen, side) in _omens)
            {
                var other = side == Team.Player ? Team.Enemy : Team.Player;
                var pool = Living(omen == Omen.Sun ? other : side).Where(u => !u.IsObjective).ToList();
                if (pool.Count == 0)
                    continue;
                var unit = pool[Rng.Next(pool.Count)];
                var blessing = side == Team.Player;
                switch (omen)
                {
                    case Omen.Sun:
                    {
                        var cells = Cells(unit).ToList();
                        var tile = cells[Rng.Next(cells.Count)];
                        _sunMarks.Add(tile);
                        events.Add(new OmenEvent(omen, blessing, unit, new[] { tile }));
                        break;
                    }
                    case Omen.Night:
                    {
                        var area = PlusArea(unit.Pos);
                        foreach (var p in area)
                            _shroud[p] = side;
                        events.Add(new OmenEvent(omen, blessing, unit, area));
                        break;
                    }
                    case Omen.Moon:
                        unit.MoonShield = MoonShield;
                        events.Add(new OmenEvent(omen, blessing, unit, new GridPos[0]));
                        break;
                }
            }
        }

        /// <summary>Start of the enemy turn: a sun beam strikes each marked tile, hurting whoever stands there now.</summary>
        private void StrikeSunMarks(List<BattleEvent> events)
        {
            foreach (var tile in _sunMarks.ToList())
            {
                if (IsOver)
                    break;
                var hits = new List<Hit>();
                var after = new List<BattleEvent>();
                var victim = UnitAt(tile);
                if (victim != null)
                    hits.Add(Damage(victim, SunDamage, null, DamageKind.Blast, after));
                events.Add(new ExplosionEvent("sun", null, tile, new[] { tile }, hits, 0));
                events.AddRange(after);
                CheckEnd();
            }

            _sunMarks.Clear();
        }

        /// <summary>
        /// End of an enemy turn in a ritual fight: the waves due now walk in on random free edge tiles,
        /// as far from the objective as possible. They act from the next enemy turn.
        /// </summary>
        private void SpawnWaves(List<BattleEvent> events)
        {
            var due = _waves.Where(w => w.afterEnemyTurn <= TurnNumber).ToList();
            if (due.Count == 0)
                return;
            var arrived = new List<Unit>();
            foreach (var wave in due)
            {
                _waves.Remove(wave);
                var tiles = Board.AllTiles()
                    .Where(p => (p.X == 0 || p.Y == 0 || p.X == Board.Width - 1 || p.Y == Board.Height - 1) && IsFree(p) && !HasHazard(p))
                    .Where(p => Objective == null || p.StepsTo(Objective.Pos) >= RitualSpawnDistance)
                    .ToList();
                if (tiles.Count == 0)
                    continue;
                var tile = tiles[Rng.Next(tiles.Count)];
                var unit = new Unit(_nextId++, Team.Enemy, wave.stats) { Pos = tile, IsPlaced = true, IsDone = true };
                _units.Add(unit);
                arrived.Add(unit);
            }

            if (arrived.Count > 0)
                events.Add(new ReinforceEvent(arrived));
        }

        /// <summary>Enemies in a ritual fight start and arrive at least this many steps from the objective.</summary>
        public const int RitualSpawnDistance = 3;

        /// <summary>
        /// Playtest shortcut: every enemy dies and the battle is won, with the whole army surviving.
        /// Units not placed yet are put on free tiles in the player zone so they count as survivors.
        /// </summary>
        public void DebugWin()
        {
            if (IsOver)
                return;
            foreach (var u in _units.Where(u => u.Team == Team.Player && !u.IsPlaced))
            {
                var free = Board.AllTiles().FirstOrDefault(p => IsPlayerZone(p) && IsFree(p));
                u.Pos = free;
                u.IsPlaced = true;
            }

            foreach (var e in _units.Where(u => u.Team == Team.Enemy))
                e.Hp = 0;
            if (TurnNumber == 0)
                TurnNumber = 1;
            WasSkipped = true;
            ActiveUnit = null;
            Phase = Phase.Victory;
        }

        private void CheckEnd()
        {
            if (IsOver || Phase == Phase.Placement)
                return;
            // Smashing the last totem wins Break the Ritual; the guards flee.
            if ((!Living(Team.Enemy).Any() && _waves.Count == 0) || (_totems.Count > 0 && _totems.All(t => !t.IsAlive)))
                Phase = Phase.Victory;
            else if (!LivingArmy.Any() || (Objective != null && !Objective.IsAlive))
                Phase = Phase.Defeat;
        }
    }
}
