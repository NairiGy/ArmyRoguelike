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

        public Battle(Board board, IEnumerable<UnitStats> playerArmy, IEnumerable<(UnitStats stats, GridPos pos)> enemies)
        {
            Board = board;
            var id = 0;
            foreach (var stats in playerArmy)
                _units.Add(new Unit(id++, Team.Player, stats));
            foreach (var (stats, pos) in enemies)
                _units.Add(new Unit(id++, Team.Enemy, stats) { Pos = pos, IsPlaced = true });
        }

        /// <summary>The fixed Prototype 1 battle: warrior, archer and rogue against a wolf pack on 8x8.</summary>
        public static Battle CreatePrototype()
        {
            var board = new Board(8, 8, new[]
            {
                new GridPos(1, 3), new GridPos(2, 3), new GridPos(5, 4), new GridPos(6, 4),
            });
            var army = new[] { UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue };
            var pack = new[]
            {
                (UnitStats.PackLeader, new GridPos(3, 7)),
                (UnitStats.Wolf, new GridPos(2, 6)),
                (UnitStats.Wolf, new GridPos(4, 6)),
                (UnitStats.Wolf, new GridPos(5, 7)),
            };
            return new Battle(board, army, pack);
        }

        // ---------- Queries ----------

        public IEnumerable<Unit> Living(Team team) => _units.Where(u => u.Team == team && u.IsAlive && u.IsPlaced);

        public Unit UnitAt(GridPos p) => _units.FirstOrDefault(u => u.IsPlaced && u.IsAlive && u.Pos == p);

        public bool IsFree(GridPos p) => Board.InBounds(p) && !Board.IsObstacle(p) && UnitAt(p) == null;

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
            return leader.Pos.StepsTo(unit.Pos) <= LeaderAuraRange ? PackState.NearLeader : PackState.None;
        }

        public int EffectiveDamage(Unit unit)
        {
            var damage = unit.Stats.Damage;
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

        /// <summary>Every tile the unit can walk to with its current AP, with the AP cost. Excludes its own tile.</summary>
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
                    if (seen.ContainsKey(n) || !IsFree(n))
                        continue;
                    seen[n] = cost;
                    costs[n] = cost;
                    frontier.Enqueue(n);
                }
            }

            return costs;
        }

        /// <summary>Shortest walkable path to a free tile, without the start tile. Null if unreachable.</summary>
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
                    if (seen.Contains(n) || !IsFree(n))
                        continue;
                    seen.Add(n);
                    parent[n] = p;
                    frontier.Enqueue(n);
                }
            }

            return null;
        }

        /// <summary>Whether the attacker could hit the target from a given tile, ignoring AP.</summary>
        public bool CanHitFrom(Unit attacker, GridPos from, Unit target)
        {
            var steps = from.StepsTo(target.Pos);
            if (steps < 1 || steps > attacker.Stats.Range)
                return false;
            return !attacker.Stats.IsRanged || Board.HasLineOfSight(from, target.Pos);
        }

        public bool CanAttack(Unit attacker, Unit target) =>
            !IsOver && attacker.IsAlive && target != null && target.IsAlive && attacker.Team != target.Team &&
            attacker.Ap >= AttackCost && CanHitFrom(attacker, attacker.Pos, target);

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
                if (enemy.IsAlive)
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
            unit.Ap -= path.Count * MoveCost;
            unit.Pos = target;
            return new MoveEvent(unit, path);
        }

        internal AttackEvent DoAttack(Unit attacker, Unit target)
        {
            var damage = EffectiveDamage(attacker);
            attacker.Ap -= AttackCost;
            target.Hp = Math.Max(0, target.Hp - damage);
            CheckEnd();
            return new AttackEvent(attacker, target, damage, target.Hp);
        }

        private void BeginTurn(Team team)
        {
            foreach (var u in Living(team))
            {
                u.Ap = u.Stats.MaxAp + Math.Min(u.Ap, MaxCarriedAp);
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
