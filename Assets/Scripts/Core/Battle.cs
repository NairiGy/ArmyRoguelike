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
        NearLeader,
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
        public const int MoveCost = 1;
        public const int AttackCost = 2;
        public const int MaxCarriedAp = 2;
        public const int PlayerZoneRows = 2;
        public const int LeaderAuraRange = 2;

        // Mines and bombs.
        public const int MineCost = 2;
        public const int MineDamage = 4;
        public const int BigMineDamage = 5;
        public const int MaxMinesPerUnit = 3;
        public const int BigMaxMines = 5;
        public const int PlayerBombRange = 4;
        public const int PlayerBombDamage = 4;

        // Ability numbers (placeholders).
        public const int VampireHeal = 2;
        public const int ThornsDamage = 1;
        public const int PincerBonus = 3;
        public const int PreyBonus = 2;
        public const int KillRefundAp = 2;
        public const int PoisonDamage = 2;
        public const int PoisonTurns = 2;
        public const int ChillAp = 2;
        public const int CollisionDamage = 2;
        public const int TauntRange = 3;

        /// <summary>Zone of control: extra AP for each step out of a tile next to an enemy.</summary>
        public const int ZocCost = 2;

        // Actions (placeholders).
        public const int ActionRange = 3;
        public const int FireballRange = 4;
        public const int FireballDamage = 3;
        public const int BurnRounds = 2;
        public const int BurnDamage = 1;
        public const int IceRounds = 2;
        public const int WardShield = 3;
        public const int MendHeal = 3;
        public const int MaxBarricades = 3;
        public const int BarricadeBlastDamage = 3;
        public const int ChargeRange = 4;

        // Omens (placeholders).
        public const int SunDamage = 3;
        public const int MoonShield = 4;

        public static int ActionCost(ActionKind action)
        {
            switch (action)
            {
                case ActionKind.Fireball:
                case ActionKind.Turret:
                case ActionKind.Charge:
                    return 3;
                default:
                    return 2;
            }
        }

        public readonly Board Board;

        private readonly List<Unit> _units = new List<Unit>();
        private readonly List<Mine> _mines = new List<Mine>();
        private readonly List<Bomb> _bombs = new List<Bomb>();
        private readonly List<(GridPos tile, Unit owner)> _barricades = new List<(GridPos, Unit)>();
        private readonly Dictionary<GridPos, int> _ice = new Dictionary<GridPos, int>();
        private readonly Dictionary<GridPos, int> _burning = new Dictionary<GridPos, int>();
        private int _nextId;

        // Ritual fights: where the player deploys (null = the back rows) and enemies still to arrive.
        private HashSet<GridPos> _deployZone;
        private readonly List<(int afterEnemyTurn, UnitStats stats)> _waves = new List<(int, UnitStats)>();

        // Omens: which side each one helps, the tiles a sun beam will strike, and shrouded tiles with the side they protect.
        private readonly List<(Omen omen, Team side)> _omens = new List<(Omen, Team)>();
        private readonly List<GridPos> _sunMarks = new List<GridPos>();
        private readonly Dictionary<GridPos, Team> _shroud = new Dictionary<GridPos, Team>();

        /// <summary>Random picks during the battle (omen targets, where reinforcements arrive).</summary>
        internal Random Rng { get; set; } = new Random(0);

        public IReadOnlyList<Unit> Units => _units;
        public IReadOnlyList<Mine> Mines => _mines;
        public IReadOnlyList<Bomb> Bombs => _bombs;
        public IEnumerable<GridPos> Barricades => _barricades.Select(b => b.tile);
        public IReadOnlyDictionary<GridPos, int> Ice => _ice;
        public IReadOnlyDictionary<GridPos, int> BurningTiles => _burning;

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

        // ---------- Omens ----------

        /// <summary>Omens in play: each one and the side it helps (the player for a blessing, the enemy for a curse).</summary>
        public IReadOnlyList<(Omen omen, Team side)> Omens => _omens;

        /// <summary>Tiles a sun beam will strike at the start of the enemy turn.</summary>
        public IReadOnlyList<GridPos> SunMarks => _sunMarks;

        /// <summary>Shrouded tiles and the side each one protects from the other side's ranged attacks.</summary>
        public IReadOnlyDictionary<GridPos, Team> Shroud => _shroud;

        /// <summary>Puts an omen in play for this battle, helping <paramref name="side"/>. Call before the battle starts.</summary>
        public void AddOmen(Omen omen, Team side) => _omens.Add((omen, side));

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
        }

        public Battle(Board board, IEnumerable<UnitStats> playerArmy, IEnumerable<(UnitStats stats, GridPos pos)> enemies)
            : this(board, playerArmy.Select(s => new ArmyMember(s)), enemies)
        {
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

        /// <summary>The orthogonal direction from one tile to another in the same row or column, else null.</summary>
        private static GridPos? StraightDirection(GridPos from, GridPos to)
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

        /// <summary>Living army units, not counting summons such as turrets.</summary>
        public IEnumerable<Unit> LivingArmy => Living(Team.Player).Where(u => !u.IsSummon);

        public Unit UnitAt(GridPos p) =>
            _units.FirstOrDefault(u => u.IsPlaced && u.IsAlive && Cells(u).Contains(p));

        /// <summary>The bone pile on a tile, if any.</summary>
        public Unit RemainsAt(GridPos p) => _units.FirstOrDefault(u => u.IsRemains && u.Pos == p);

        public IEnumerable<Unit> Remains(Team team) => _units.Where(u => u.Team == team && u.IsRemains);

        public Mine MineAt(GridPos p) => _mines.FirstOrDefault(m => m.Tile == p);

        public Bomb BombAt(GridPos p) => _bombs.FirstOrDefault(b => b.Tile == p);

        public bool IsBarricade(GridPos p) => _barricades.Any(b => b.tile == p);

        /// <summary>A tile with no rock and no living unit. Bone piles, mines and bombs don't block.</summary>
        public bool IsFree(GridPos p) => Board.InBounds(p) && !Board.IsObstacle(p) && UnitAt(p) == null;

        private bool HasHazard(GridPos p) => MineAt(p) != null || BombAt(p) != null;

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

        /// <summary>Where the player may deploy: the back two rows, or the ritual's zone around the objective.</summary>
        public bool IsPlayerZone(GridPos p) => Board.InBounds(p) && (_deployZone != null ? _deployZone.Contains(p) : p.Y < PlayerZoneRows);

        public PackState GetPackState(Unit unit)
        {
            if (!unit.IsWolf)
                return PackState.None;
            var leader = _units.FirstOrDefault(u => u.Team == unit.Team && u.IsPackLeader);
            if (leader == null)
                return PackState.None;
            if (!leader.IsAlive)
                return PackState.Leaderless;
            return Distance(leader, unit) <= LeaderAuraRange ? PackState.NearLeader : PackState.None;
        }

        /// <summary>The unit's attack damage before target-specific bonuses.</summary>
        public int EffectiveDamage(Unit unit)
        {
            var damage = unit.BaseDamage;
            switch (GetPackState(unit))
            {
                case PackState.NearLeader:
                    damage += 1;
                    break;
                case PackState.Leaderless:
                    damage -= 1;
                    break;
            }

            if (!unit.MovedThisTurn)
            {
                if (unit.Has(Ability.SteadyAimPlus))
                    damage += 2;
                else if (unit.Has(Ability.SteadyAim))
                    damage += 1;
            }

            return Math.Max(1, damage);
        }

        /// <summary>Damage of an attack on this particular target: Pincer, Prey, Shatter and the opening shot.</summary>
        public int DamageAgainst(Unit attacker, Unit target)
        {
            var damage = EffectiveDamage(attacker);
            if (attacker.Has(Ability.Pincer) && IsPinched(attacker, target))
                damage += PincerBonus;
            if (target.IsSnared && Living(attacker.Team).Any(u => u.Has(Ability.PreyOnSnared)))
                damage += PreyBonus;
            if (attacker.Has(Ability.Shatter) && target.IsSlowed)
                damage *= 2;
            if (attacker.Has(Ability.FirstShotDouble) && !attacker.AttackedThisTurn)
                damage *= 2;
            return damage;
        }

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

        /// <summary>True if the unit, standing with its anchor here, is next to a living enemy (inside its reach).</summary>
        public bool InEnemyReach(Unit unit, GridPos anchor) =>
            _units.Any(u => u.IsPlaced && u.IsAlive && u.Team != unit.Team && Distance(anchor, unit.Size, u) == 1);

        /// <summary>
        /// AP for one step. Zone of control: stepping out of a tile next to an enemy costs
        /// <see cref="ZocCost"/> extra. Spring Boots make the first step of the turn's base cost free.
        /// </summary>
        public int StepCost(Unit unit, GridPos from, bool firstStep)
        {
            var cost = firstStep && unit.Has(Ability.SpringBoots) && !unit.FreeStepUsed ? 0 : MoveCost;
            if (InEnemyReach(unit, from))
                cost += ZocCost;
            return cost;
        }

        /// <summary>Total AP for walking a path from the unit's tile (path excludes the start).</summary>
        public int PathCost(Unit unit, IReadOnlyList<GridPos> path)
        {
            var cost = 0;
            var from = unit.Pos;
            for (var i = 0; i < path.Count; i++)
            {
                cost += StepCost(unit, from, i == 0);
                from = path[i];
            }

            return cost;
        }

        /// <summary>How many steps of a path leave an enemy's reach (each costs <see cref="ZocCost"/> extra).</summary>
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

        public bool CanMove(Unit unit) => !unit.Has(Ability.Immobile) && !unit.RootedThisTurn;

        /// <summary>
        /// Cheapest AP cost to every reachable anchor tile (Dijkstra, since steps cost different amounts),
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
                    if (closed.Contains(n) || !CanStand(unit, n))
                        continue;
                    var c = cost[p] + StepCost(unit, p, p == unit.Pos);
                    if (c > budget || (cost.TryGetValue(n, out var old) && old <= c))
                        continue;
                    cost[n] = c;
                    parent[n] = p;
                    open.Add(n);
                }
            }

            return (cost, parent);
        }

        /// <summary>Every anchor tile the unit can walk to with its current AP, with the AP cost. Excludes its own tile.</summary>
        public Dictionary<GridPos, int> GetReachable(Unit unit)
        {
            if (!CanMove(unit))
                return new Dictionary<GridPos, int>();
            var routes = CheapestRoutes(unit, unit.Ap).cost;
            routes.Remove(unit.Pos);
            return routes;
        }

        /// <summary>Cheapest walkable path to an anchor tile, without the start tile. Null if unreachable.</summary>
        public List<GridPos> FindPath(Unit unit, GridPos target)
        {
            var (cost, parent) = CheapestRoutes(unit, int.MaxValue);
            if (!cost.ContainsKey(target) || target == unit.Pos)
                return null;
            var path = new List<GridPos>();
            for (var c = target; c != unit.Pos; c = parent[c])
                path.Add(c);
            path.Reverse();
            return path;
        }

        /// <summary>Whether the attacker's basic attack could hit the target from a given anchor, ignoring AP.</summary>
        public bool CanHitFrom(Unit attacker, GridPos from, Unit target)
        {
            var steps = Distance(from, attacker.Size, target);
            if (steps < 1 || steps > attacker.Range)
                return false;

            if (attacker.Has(Ability.LongReach))
            {
                // Straight line only, and a rock in between blocks the second tile.
                foreach (var cell in Cells(target))
                {
                    var dir = StraightDirection(from, cell);
                    if (dir == null || from.StepsTo(cell) > attacker.Range)
                        continue;
                    if (from.StepsTo(cell) == 1 || !Board.IsObstacle(from + dir.Value))
                        return true;
                }

                return false;
            }

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
            attacker.Ap >= AttackCost && CanHitFrom(attacker, attacker.Pos, target);

        /// <summary>
        /// The cheapest tile from which the unit can walk up and still afford to attack the target,
        /// with its move cost. Ties go to the tile next to the fewest other enemies. Null if out of reach
        /// this turn. Returns the unit's own tile at cost 0 if it can already attack.
        /// </summary>
        public (GridPos tile, int cost)? FindAttackSpot(Unit unit, Unit target)
        {
            if (IsOver || target == null || !target.IsAlive || target.Team == unit.Team)
                return null;
            if (CanAttack(unit, target))
                return (unit.Pos, 0);

            return GetReachable(unit)
                .Where(kv => unit.Ap - kv.Value >= AttackCost && CanHitFrom(unit, kv.Key, target))
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
            if (unit.Stats.Actions.Any(a => ActionTargets(unit, a).Any()))
                return true;
            return GetReachable(unit).Count > 0;
        }

        // ---------- Special actions ----------

        public int MinesOf(Unit unit) => _mines.Count(m => m.Owner == unit);

        public static int MineDamageFor(Unit owner) => owner != null && owner.Has(Ability.BigMines) ? BigMineDamage : MineDamage;

        public static int MaxMinesFor(Unit owner) => owner.Has(Ability.BigMines) ? BigMaxMines : MaxMinesPerUnit;

        public bool CanPlaceMine(Unit unit, GridPos tile) => CanUseAction(unit, ActionKind.PlaceMine, tile);

        public List<BattleEvent> PlayerPlaceMine(Unit unit, GridPos tile) => PlayerUseAction(unit, ActionKind.PlaceMine, tile);

        /// <summary>For Charge: the enemy the unit would run into along a straight line through this tile, and where it stops.</summary>
        public (Unit target, GridPos stop, GridPos dir)? ChargeTarget(Unit unit, GridPos tile)
        {
            var dir = StraightDirection(unit.Pos, tile);
            if (dir == null || unit.Size != 1)
                return null;
            var p = unit.Pos;
            for (var step = 1; step <= ChargeRange; step++)
            {
                var next = p + dir.Value;
                if (!Board.InBounds(next) || Board.IsObstacle(next))
                    return null;
                var occupant = UnitAt(next);
                if (occupant != null)
                {
                    // The clicked tile must be the enemy itself, at least 2 tiles away (one step of run-up).
                    if (occupant.Team == unit.Team || step < 2 || !Cells(occupant).Contains(tile))
                        return null;
                    return (occupant, p, dir.Value);
                }

                p = next;
            }

            return null;
        }

        public bool CanUseAction(Unit unit, ActionKind action, GridPos tile)
        {
            if (IsOver || !unit.IsAlive || !unit.Stats.HasAction(action) || unit.Ap < ActionCost(action) || !Board.InBounds(tile))
                return false;
            var target = UnitAt(tile);
            var steps = Distance(tile, 1, unit);
            switch (action)
            {
                case ActionKind.PlaceMine:
                    return MinesOf(unit) < MaxMinesFor(unit) && steps == 1 && IsFree(tile) && !HasHazard(tile);
                case ActionKind.ThrowBomb:
                    return steps >= 2 && steps <= PlayerBombRange && !Board.IsObstacle(tile) && BombAt(tile) == null &&
                           !IsShroudedFrom(tile, unit.Team);
                case ActionKind.Snare:
                    return target != null && target.Team != unit.Team && steps <= ActionRange;
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
                    return ChargeTarget(unit, tile).HasValue;
                default:
                    return false;
            }
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
            unit.Ap -= ActionCost(action);
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
                case ActionKind.Snare:
                {
                    var target = UnitAt(tile);
                    target.SnareTurns = 1;
                    events.Add(new StatusEvent(unit, target, StatusKind.Snare, 1));
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
                        Board.RemoveObstacle(b.tile);
                        events.Add(new TileEvent("barricade", b.tile, false, unit));
                        Explode(b.tile, BarricadeBlastDamage, "barricade", unit, events);
                    }

                    break;
                case ActionKind.Charge:
                    Charge(unit, tile, events);
                    break;
            }

            CheckEnd();
            return events;
        }

        private void Fireball(Unit caster, GridPos center, List<BattleEvent> events)
        {
            var area = BlastArea(center);
            var after = new List<BattleEvent>();
            var hits = _units.Where(u => u.IsPlaced && u.IsAlive && Cells(u).Any(area.Contains)).ToList()
                .Select(u => Damage(u, FireballDamage, caster, DamageKind.Blast, after)).ToList();
            events.Add(new AreaAttackEvent(caster, "fireball", center, area, hits));
            events.AddRange(after);

            if (caster.Has(Ability.Burning))
            {
                foreach (var p in area.Where(p => !Board.IsObstacle(p)))
                {
                    _burning[p] = BurnRounds;
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

        private void Charge(Unit unit, GridPos tile, List<BattleEvent> events)
        {
            var charge = ChargeTarget(unit, tile).Value;
            var path = new List<GridPos>();
            for (var p = unit.Pos; p != charge.stop;)
            {
                p += charge.dir;
                path.Add(p);
            }

            var from = unit.Pos;
            unit.Pos = charge.stop;
            unit.MovedThisTurn = true;
            events.Add(new MoveEvent(unit, from, path));

            var after = new List<BattleEvent>();
            var hit = Damage(charge.target, DamageAgainst(unit, charge.target), unit, DamageKind.Attack, after);
            unit.AttackedThisTurn = true;
            events.Add(new AttackEvent(unit, hit.Target, hit.Damage, hit.HpAfter));
            events.AddRange(after);
            if (hit.Target.IsAlive)
                Push(hit.Target, charge.dir, unit, events);
        }

        /// <summary>
        /// Shoves a unit one tile. If something is in the way it slams into it and takes collision damage.
        /// An enemy shoved onto a mine sets it off.
        /// </summary>
        private void Push(Unit unit, GridPos dir, Unit pusher, List<BattleEvent> events)
        {
            // The shrine is rooted to its tile.
            if (!unit.IsAlive || unit.IsObjective)
                return;
            var from = unit.Pos;
            var to = from + dir;
            if (!CanStand(unit, to))
            {
                var after = new List<BattleEvent>();
                var hit = Damage(unit, CollisionDamage, pusher, DamageKind.Collision, after);
                events.Add(new PushEvent(unit, pusher, from, from, hit));
                events.AddRange(after);
                return;
            }

            unit.Pos = to;
            events.Add(new PushEvent(unit, pusher, from, to, null));
            if (unit.Team == Team.Enemy)
            {
                var mine = Cells(unit).Select(MineAt).FirstOrDefault(m => m != null);
                if (mine != null)
                    Explode(mine.Tile, mine.Damage, "mine", mine.Owner, events);
            }
        }

        // ---------- Enemy abilities ----------

        /// <summary>Living enemies of the unit standing next to it: the targets of an area smash.</summary>
        public List<Unit> AdjacentFoes(Unit unit) =>
            _units.Where(u => u.IsPlaced && u.IsAlive && u.Team != unit.Team && Distance(unit, u) == 1).ToList();

        public bool CanRaise(Unit raiser, Unit remains) =>
            !IsOver && raiser.IsAlive && raiser.Stats.CanRaise && raiser.Ap >= raiser.Stats.RaiseCost &&
            remains != null && remains.IsRemains && remains.Team == raiser.Team &&
            Distance(raiser, remains) <= raiser.Stats.RaiseRange && UnitAt(remains.Pos) == null;

        public bool CanThrow(Unit thrower, Unit target)
        {
            if (IsOver || !thrower.IsAlive || !thrower.Stats.CanThrow || thrower.Ap < thrower.Stats.ThrowCost)
                return false;
            if (target == null || !target.IsAlive || target.Team == thrower.Team)
                return false;
            var steps = Distance(thrower, target);
            return steps >= 2 && steps <= thrower.Stats.ThrowRange && !IsShroudedFrom(target, thrower.Team);
        }

        public bool CanBomb(Unit thrower, Unit target)
        {
            if (IsOver || !thrower.IsAlive || !thrower.Stats.CanBomb || thrower.Ap < thrower.Stats.BombCost)
                return false;
            if (target == null || !target.IsAlive || target.Team == thrower.Team)
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

        public bool CanPlace(Unit unit, GridPos p)
        {
            if (Phase != Phase.Placement || unit.Team != Team.Player || unit.IsObjective || !IsPlayerZone(p) || Board.IsObstacle(p))
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

            foreach (var enemy in Living(Team.Enemy).ToList())
            {
                if (IsOver)
                    break;
                // A skeleton raised earlier this phase waits until the next enemy turn.
                if (enemy.IsAlive && !enemy.IsDone)
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
            if (path == null || path.Count == 0 || PathCost(unit, path) > unit.Ap)
                return null;

            Mine triggered = null;
            if (unit.Team == Team.Enemy)
            {
                for (var i = 0; i < path.Count; i++)
                {
                    triggered = Footprint(path[i], unit.Size).Select(MineAt).FirstOrDefault(m => m != null);
                    if (triggered == null)
                        continue;
                    path = path.Take(i + 1).ToList();
                    break;
                }
            }

            var from = unit.Pos;
            var cost = PathCost(unit, path);
            var zoc = ZocSteps(unit, path);
            unit.Ap -= cost;
            if (unit.Has(Ability.SpringBoots))
                unit.FreeStepUsed = true;
            unit.MovedThisTurn = true;
            unit.Pos = path[path.Count - 1];
            var events = new List<BattleEvent> { new MoveEvent(unit, from, path, cost, zoc) };
            if (triggered != null)
                Explode(triggered.Tile, triggered.Damage, "mine", triggered.Owner, events);
            return events;
        }

        internal List<BattleEvent> DoAttack(Unit attacker, Unit target)
        {
            var events = new List<BattleEvent>();
            attacker.Ap -= AttackCost;
            var strikes = attacker.Has(Ability.DoubleStrike) ? 2 : 1;
            var struck = new List<Unit>();
            for (var i = 0; i < strikes && !IsOver && attacker.IsAlive && target.IsAlive; i++)
                struck.AddRange(Strike(attacker, target, events));
            attacker.AttackedThisTurn = true;

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
                var area = BlastArea(target.Pos);
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
                for (var p = attacker.Pos + dir.Value; Board.InBounds(p) && !Board.IsObstacle(p) && attacker.Pos.StepsTo(p) <= attacker.Range; p += dir.Value)
                    line.Add(p);
            }
            else if (attacker.Has(Ability.LongReach) && dir != null)
            {
                kind = "long reach";
                for (var p = attacker.Pos + dir.Value; Board.InBounds(p) && attacker.Pos.StepsTo(p) <= attacker.Range; p += dir.Value)
                {
                    line.Add(p);
                    if (Board.IsObstacle(p))
                        break;
                }
            }
            else if (attacker.Has(Ability.Sweep) && dir != null)
            {
                kind = "sweep";
                var side = new GridPos(dir.Value.Y, dir.Value.X);
                var near = attacker.Pos + dir.Value;
                line.Add(near + side);
                line.Add(near);
                line.Add(new GridPos(near.X - side.X, near.Y - side.Y));
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
            attacker.Ap -= AttackCost;
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
            thrower.Ap -= thrower.Stats.ThrowCost;
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
            }

            var events = new List<BattleEvent> { new BoulderEvent(thrower, hit, rock) };
            events.AddRange(after);
            CheckEnd();
            return events;
        }

        internal List<BattleEvent> DoBomb(Unit thrower, Unit target)
        {
            thrower.Ap -= thrower.Stats.BombCost;
            _bombs.Add(new Bomb(target.Pos, thrower, thrower.Stats.BombDamage));
            return new List<BattleEvent> { new BombThrowEvent(thrower, target.Pos) };
        }

        internal List<BattleEvent> DoRaise(Unit raiser, Unit remains)
        {
            raiser.Ap -= raiser.Stats.RaiseCost;
            remains.Hp = remains.MaxHp;
            remains.Ap = 0;
            remains.IsDone = true;
            remains.PoisonTurns = 0;
            return new List<BattleEvent> { new RaiseEvent(raiser, remains) };
        }

        /// <summary>
        /// Blows up a mine, bomb or barricade at <paramref name="center"/>: every living unit in the 3x3
        /// takes the damage, friend or foe (unless the owner is Blast Proof). Mines and bombs inside the
        /// area go off too, one after another.
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
                var area = BlastArea(blast.center);
                var safeTeam = blast.owner != null && blast.owner.Has(Ability.BlastProof) ? blast.owner.Team : (Team?)null;
                var victims = _units.Where(u => u.IsPlaced && u.IsAlive && u.Team != safeTeam && Cells(u).Any(area.Contains)).ToList();
                var hits = new List<Hit>();
                var after = new List<BattleEvent>();
                foreach (var victim in victims)
                    hits.Add(Damage(victim, blast.damage, blast.owner, DamageKind.Blast, after));
                events.Add(new ExplosionEvent(blast.cause, blast.owner, blast.center, area, hits, blast.depth));
                events.AddRange(after);

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
        /// and a kill triggers the killer's Bloodthirst, Vampire Fang and Momentum. Attacks also carry
        /// Poison and Chill. Follow-up effects are added to <paramref name="after"/>.
        /// </summary>
        private Hit Damage(Unit target, int amount, Unit source, DamageKind kind, List<BattleEvent> after)
        {
            var hostile = source != null && source.Team != target.Team;

            if (kind == DamageKind.Attack && hostile)
            {
                var guard = _units.FirstOrDefault(g => g != target && g.Team == target.Team && g.IsPlaced && g.IsAlive &&
                                                        g.Has(Ability.Guard) && Distance(g, target) == 1);
                if (guard != null)
                {
                    after.Add(new EffectEvent(EffectKind.Guard, guard, 0, guard.Hp, guard));
                    target = guard;
                }
            }

            if (IsShielded(target))
                amount = Math.Max(1, amount - 1);
            if (target.Has(Ability.Tough))
                amount = Math.Max(1, amount - 1);

            if (kind == DamageKind.Attack && hostile && target.Has(Ability.Dodge) && !target.DodgeUsed)
            {
                target.DodgeUsed = true;
                after.Add(new EffectEvent(EffectKind.Dodge, target, 0, target.Hp, target));
                amount = 0;
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

            target.Hp = Math.Max(0, hp);

            if (!target.IsAlive && hostile && source.IsAlive)
            {
                if (source.Has(Ability.Bloodthirst))
                {
                    source.GainedDamage++;
                    after.Add(new EffectEvent(EffectKind.Bloodthirst, source, 1, source.Hp, source));
                }

                if (source.Has(Ability.VampireFang) && source.Hp < source.MaxHp)
                {
                    var healed = Math.Min(VampireHeal, source.MaxHp - source.Hp);
                    source.Hp += healed;
                    after.Add(new EffectEvent(EffectKind.Heal, source, healed, source.Hp, source));
                }

                if (source.Has(Ability.KillRefund) && kind == DamageKind.Attack)
                {
                    source.Ap += KillRefundAp;
                    after.Add(new EffectEvent(EffectKind.Refund, source, KillRefundAp, source.Hp, source));
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
                    after.Add(new StatusEvent(source, target, StatusKind.Poison, PoisonTurns));
                }

                if (source.Has(Ability.Chill))
                {
                    target.SlowNext = ChillAp;
                    after.Add(new StatusEvent(source, target, StatusKind.Slow, ChillAp));
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

        /// <summary>Slam: every other unit within 2 tiles of the slammer is shoved 1 tile away from it.</summary>
        internal List<BattleEvent> DoSlam(Unit slammer)
        {
            slammer.Ap -= AttackCost;
            var events = new List<BattleEvent>();
            var area = Board.AllTiles().Where(p => Distance(p, 1, slammer) <= 2 && !Cells(slammer).Contains(p)).ToList();
            events.Add(new AreaAttackEvent(slammer, "slam", slammer.Pos, area, new List<Hit>()));
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

        /// <summary>Thorned Mail: a melee attacker takes damage back from the unit it hit.</summary>
        private void Thorns(Unit attacker, Unit target, List<BattleEvent> events)
        {
            if (!target.Has(Ability.ThornedMail) || !attacker.IsAlive || target.Team == attacker.Team)
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
        /// down (once per round, at the player's turn), then AP is refilled minus any Chill, and snares
        /// take effect.
        /// </summary>
        private void BeginTurn(Team team, List<BattleEvent> events)
        {
            if (team == Team.Enemy)
                StrikeSunMarks(events);

            foreach (var u in Living(team).ToList())
            {
                if (_burning.ContainsKey(u.Pos) || Cells(u).Any(_burning.ContainsKey))
                {
                    var after = new List<BattleEvent>();
                    events.Add(new TickEvent("burn", Damage(u, BurnDamage, null, DamageKind.Tick, after)));
                    events.AddRange(after);
                }

                if (u.IsAlive && u.PoisonTurns > 0)
                {
                    u.PoisonTurns--;
                    var after = new List<BattleEvent>();
                    events.Add(new TickEvent("poison", Damage(u, PoisonDamage, null, DamageKind.Tick, after)));
                    events.AddRange(after);
                }
            }

            if (team == Team.Player)
            {
                foreach (var p in _burning.Keys.ToList())
                {
                    if (--_burning[p] > 0)
                        continue;
                    _burning.Remove(p);
                    events.Add(new TileEvent("burn", p, false, null));
                }

                foreach (var p in _ice.Keys.ToList())
                {
                    if (--_ice[p] > 0)
                        continue;
                    _ice.Remove(p);
                    Board.RemoveObstacle(p);
                    events.Add(new TileEvent("ice", p, false, null));
                }
            }

            foreach (var u in Living(team))
            {
                u.Ap = Math.Max(0, u.MaxAp + Math.Min(u.Ap, MaxCarriedAp) - u.SlowNext);
                u.SlowedThisTurn = u.SlowNext > 0;
                u.SlowNext = 0;
                u.RootedThisTurn = u.SnareTurns > 0;
                if (u.RootedThisTurn)
                    u.SnareTurns--;
                u.IsDone = false;
                u.MovedThisTurn = false;
                u.FreeStepUsed = false;
                u.AttackedThisTurn = false;
            }

            // Riposte and Dodge work once per enemy turn.
            if (team == Team.Enemy)
            {
                foreach (var u in Living(Team.Player))
                {
                    u.CounterUsed = false;
                    u.DodgeUsed = false;
                }
            }

            if (team == Team.Player)
                ApplyOmens(events);

            CheckEnd();
        }

        /// <summary>
        /// Start of the player's turn: last round's shrouds and moon shields fade, then each omen picks a
        /// random unit (never the objective): the Sun marks a tile under one of the other side's units, the
        /// Night shrouds the 3x3 around one of its own side's units, the Moon shields one of its own side's units.
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
                        var area = BlastArea(unit.Pos);
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
        public const int RitualSpawnDistance = 4;

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
            if (!Living(Team.Enemy).Any() && _waves.Count == 0)
                Phase = Phase.Victory;
            else if (!LivingArmy.Any() || (Objective != null && !Objective.IsAlive))
                Phase = Phase.Defeat;
        }
    }
}
