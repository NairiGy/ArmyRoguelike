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

    /// <summary>
    /// The rules of one battle. Pure C# with no Unity dependency, so it can be unit-tested and
    /// queried by the AI and the view without side effects.
    /// </summary>
    public sealed class Battle
    {
        public const int MoveCost = 1;
        public const int AttackCost = 2;
        public const int MaxCarriedAp = 2;
        public const int PlayerZoneRows = 2;
        public const int LeaderAuraRange = 2;

        public readonly Board Board;

        private readonly List<Unit> _units = new List<Unit>();

        public IReadOnlyList<Unit> Units => _units;

        public Phase Phase { get; private set; } = Phase.Placement;

        /// <summary>The player unit that has started acting this turn. Others can't act until it is done.</summary>
        public Unit ActiveUnit { get; private set; }

        public int TurnNumber { get; private set; }

        public bool IsOver => Phase == Phase.Victory || Phase == Phase.Defeat;

        public Battle(Board board, IEnumerable<ArmyMember> playerArmy, IEnumerable<(UnitStats stats, GridPos pos)> enemies)
        {
            Board = board;
            var id = 0;
            foreach (var member in playerArmy)
                _units.Add(new Unit(id++, Team.Player, member.Stats, member.Equipment));
            foreach (var (stats, pos) in enemies)
                _units.Add(new Unit(id++, Team.Enemy, stats) { Pos = pos, IsPlaced = true });
        }

        public Battle(Board board, IEnumerable<UnitStats> playerArmy, IEnumerable<(UnitStats stats, GridPos pos)> enemies)
            : this(board, playerArmy.Select(s => new ArmyMember(s)), enemies)
        {
        }

        /// <summary>The first battle of the run: warrior, archer and rogue against a wolf pack.</summary>
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

        // ---------- Queries ----------

        public IEnumerable<Unit> Living(Team team) => _units.Where(u => u.Team == team && u.IsAlive && u.IsPlaced);

        public Unit UnitAt(GridPos p) =>
            _units.FirstOrDefault(u => u.IsPlaced && u.IsAlive && Cells(u).Contains(p));

        /// <summary>The bone pile on a tile, if any.</summary>
        public Unit RemainsAt(GridPos p) => _units.FirstOrDefault(u => u.IsRemains && u.Pos == p);

        public IEnumerable<Unit> Remains(Team team) => _units.Where(u => u.Team == team && u.IsRemains);

        /// <summary>A tile with no rock and no living unit. Bone piles don't block.</summary>
        public bool IsFree(GridPos p) => Board.InBounds(p) && !Board.IsObstacle(p) && UnitAt(p) == null;

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

            return Math.Max(1, damage);
        }

        /// <summary>Every anchor tile the unit can walk to with its current AP, with the AP cost. Excludes its own tile.</summary>
        public Dictionary<GridPos, int> GetReachable(Unit unit)
        {
            var costs = new Dictionary<GridPos, int>();
            var start = unit.Pos;
            var frontier = new Queue<GridPos>();
            var seen = new Dictionary<GridPos, int> { [start] = 0 };
            frontier.Enqueue(start);
            while (frontier.Count > 0)
            {
                var p = frontier.Dequeue();
                var cost = seen[p] + MoveCost;
                if (cost > unit.Ap)
                    continue;
                foreach (var n in p.Neighbours())
                {
                    if (seen.ContainsKey(n) || !CanStand(unit, n))
                        continue;
                    seen[n] = cost;
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
            if (!attacker.IsRanged)
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
            BeginTurn(Team.Player);
            Phase = Phase.PlayerTurn;
            return true;
        }

        // ---------- Player turn ----------

        public bool CanAct(Unit unit) =>
            Phase == Phase.PlayerTurn && unit.Team == Team.Player && unit.IsAlive && !unit.IsDone &&
            (ActiveUnit == null || ActiveUnit == unit);

        public MoveEvent PlayerMove(Unit unit, GridPos target)
        {
            if (!CanAct(unit) || !GetReachable(unit).ContainsKey(target))
                return null;
            ActiveUnit = unit;
            return DoMove(unit, target);
        }

        public AttackEvent PlayerAttack(Unit unit, Unit target)
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

        /// <summary>Ends the player phase and runs the whole enemy phase. Returns what the enemies did.</summary>
        public List<BattleEvent> EndPlayerTurn()
        {
            var events = new List<BattleEvent>();
            if (Phase != Phase.PlayerTurn)
                return events;

            foreach (var u in Living(Team.Player))
                u.IsDone = true;
            ActiveUnit = null;

            Phase = Phase.EnemyTurn;
            BeginTurn(Team.Enemy);
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
            BeginTurn(Team.Player);
            Phase = Phase.PlayerTurn;
            return events;
        }

        // ---------- Shared actions ----------

        internal MoveEvent DoMove(Unit unit, GridPos target)
        {
            var path = FindPath(unit, target);
            if (path == null || path.Count * MoveCost > unit.Ap)
                return null;
            var from = unit.Pos;
            unit.Ap -= path.Count * MoveCost;
            unit.Pos = target;
            return new MoveEvent(unit, from, path);
        }

        internal AttackEvent DoAttack(Unit attacker, Unit target)
        {
            var damage = EffectiveDamage(attacker);
            attacker.Ap -= AttackCost;
            target.Hp = Math.Max(0, target.Hp - damage);
            CheckEnd();
            return new AttackEvent(attacker, target, damage, target.Hp);
        }

        internal SmashEvent DoSmash(Unit attacker)
        {
            var damage = EffectiveDamage(attacker);
            attacker.Ap -= AttackCost;
            var hits = new List<Hit>();
            foreach (var target in AdjacentFoes(attacker))
            {
                target.Hp = Math.Max(0, target.Hp - damage);
                hits.Add(new Hit(target, damage, target.Hp));
            }

            CheckEnd();
            return new SmashEvent(attacker, hits);
        }

        internal BoulderEvent DoThrow(Unit thrower, Unit target)
        {
            var damage = thrower.Stats.ThrowDamage;
            thrower.Ap -= thrower.Stats.ThrowCost;
            var rock = RockTileFor(thrower, target);
            target.Hp = Math.Max(0, target.Hp - damage);
            if (rock.HasValue)
                Board.AddObstacle(rock.Value);
            CheckEnd();
            return new BoulderEvent(thrower, new Hit(target, damage, target.Hp), rock);
        }

        internal RaiseEvent DoRaise(Unit raiser, Unit remains)
        {
            raiser.Ap -= raiser.Stats.RaiseCost;
            remains.Hp = remains.MaxHp;
            remains.Ap = 0;
            remains.IsDone = true;
            return new RaiseEvent(raiser, remains);
        }

        private void BeginTurn(Team team)
        {
            foreach (var u in Living(team))
            {
                u.Ap = u.MaxAp + Math.Min(u.Ap, MaxCarriedAp);
                u.IsDone = false;
            }
        }

        private void CheckEnd()
        {
            if (!Living(Team.Enemy).Any())
                Phase = Phase.Victory;
            else if (!Living(Team.Player).Any())
                Phase = Phase.Defeat;
        }
    }
}
