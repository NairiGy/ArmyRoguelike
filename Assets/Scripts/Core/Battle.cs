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

        public bool IsPlayerZone(GridPos p) => Board.InBounds(p) && p.Y < PlayerZoneRows;

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

        /// <summary>AP cost of walking the given number of tiles, counting a free Spring Boots step.</summary>
        public int MoveCostFor(Unit unit, int tiles)
        {
            var free = unit.Has(Ability.SpringBoots) && !unit.FreeStepUsed ? 1 : 0;
            return Math.Max(0, tiles - free) * MoveCost;
        }

        public bool CanMove(Unit unit) => !unit.Has(Ability.Immobile) && !unit.RootedThisTurn;

        /// <summary>Every anchor tile the unit can walk to with its current AP, with the AP cost. Excludes its own tile.</summary>
        public Dictionary<GridPos, int> GetReachable(Unit unit)
        {
            var costs = new Dictionary<GridPos, int>();
            if (!CanMove(unit))
                return costs;
            var start = unit.Pos;
            var frontier = new Queue<GridPos>();
            var steps = new Dictionary<GridPos, int> { [start] = 0 };
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                var p = frontier.Dequeue();
                var next = steps[p] + 1;
                var cost = MoveCostFor(unit, next);
                if (cost > unit.Ap)
                    continue;
                foreach (var n in p.Neighbours())
                {
                    if (steps.ContainsKey(n) || !CanStand(unit, n))
                        continue;
                    steps[n] = next;
                    costs[n] = cost;
                    frontier.Enqueue(n);
                }
            }

            return costs;
        }

        /// <summary>Shortest walkable path to an anchor tile, without the start tile. Null if unreachable.</summary>
        public List<GridPos> FindPath(Unit unit, GridPos target)
        {
            var start = unit.Pos;
            var parent = new Dictionary<GridPos, GridPos>();
            var frontier = new Queue<GridPos>();
            var seen = new HashSet<GridPos> { start };
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                var p = frontier.Dequeue();
                if (p == target)
                {
                    var path = new List<GridPos>();
                    for (var c = target; c != start; c = parent[c])
                        path.Add(c);
                    path.Reverse();
                    return path;
                }

                foreach (var n in p.Neighbours())
                {
                    if (seen.Contains(n) || !CanStand(unit, n))
                        continue;
                    seen.Add(n);
                    parent[n] = p;
                    frontier.Enqueue(n);
                }
            }

            return null;
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
            if (!unit.IsAlive || unit.IsDone || unit.Team != Team.Player)
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
                    return steps >= 2 && steps <= PlayerBombRange && !Board.IsObstacle(tile) && BombAt(tile) == null;
                case ActionKind.Snare:
                    return target != null && target.Team != unit.Team && steps <= ActionRange;
                case ActionKind.Fireball:
                    return steps >= 1 && steps <= FireballRange;
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
            if (!unit.IsAlive)
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
            return steps >= 2 && steps <= thrower.Stats.ThrowRange;
        }

        public bool CanBomb(Unit thrower, Unit target)
        {
            if (IsOver || !thrower.IsAlive || !thrower.Stats.CanBomb || thrower.Ap < thrower.Stats.BombCost)
                return false;
            if (target == null || !target.IsAlive || target.Team == thrower.Team)
                return false;
            var steps = Distance(thrower, target);
            return steps >= 2 && steps <= thrower.Stats.BombRange && BombAt(target.Pos) == null;
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
            if (Phase != Phase.Placement || unit.Team != Team.Player || !IsPlayerZone(p) || Board.IsObstacle(p))
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
            BeginTurn(Team.Player, new List<BattleEvent>());
            Phase = Phase.PlayerTurn;
            return true;
        }

        // ---------- Player turn ----------

        public bool CanAct(Unit unit) =>
            Phase == Phase.PlayerTurn && unit.Team == Team.Player && unit.IsAlive && !unit.IsDone &&
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
            if (path == null || path.Count == 0 || MoveCostFor(unit, path.Count) > unit.Ap)
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
            unit.Ap -= MoveCostFor(unit, path.Count);
            if (unit.Has(Ability.SpringBoots))
                unit.FreeStepUsed = true;
            unit.MovedThisTurn = true;
            unit.Pos = path[path.Count - 1];
            var events = new List<BattleEvent> { new MoveEvent(unit, from, path) };
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

            CheckEnd();
        }

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
            if (!Living(Team.Enemy).Any())
                Phase = Phase.Victory;
            else if (!LivingArmy.Any())
                Phase = Phase.Defeat;
        }
    }
}
