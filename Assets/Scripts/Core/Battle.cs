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

    /// <summary>A sapper's mine on a tile. Enemies stepping on it set it off.</summary>
    public sealed class Mine
    {
        public readonly GridPos Tile;
        public readonly Unit Owner;

        public Mine(GridPos tile, Unit owner)
        {
            Tile = tile;
            Owner = owner;
        }
    }

    /// <summary>A goblin bomb waiting on a tile. It explodes at the start of the next enemy turn.</summary>
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
        public const int MineCost = 2;
        public const int MineDamage = 4;
        public const int MaxMinesPerUnit = 3;
        public const int VampireHeal = 2;
        public const int ThornsDamage = 1;

        public readonly Board Board;

        private readonly List<Unit> _units = new List<Unit>();
        private readonly List<Mine> _mines = new List<Mine>();
        private readonly List<Bomb> _bombs = new List<Bomb>();

        public IReadOnlyList<Unit> Units => _units;
        public IReadOnlyList<Mine> Mines => _mines;
        public IReadOnlyList<Bomb> Bombs => _bombs;

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
                _units.Add(new Unit(id++, Team.Player, member.Stats, member));
            foreach (var (stats, pos) in enemies)
                _units.Add(new Unit(id++, Team.Enemy, stats) { Pos = pos, IsPlaced = true });
        }

        public Battle(Board board, IEnumerable<UnitStats> playerArmy, IEnumerable<(UnitStats stats, GridPos pos)> enemies)
            : this(board, playerArmy.Select(s => new ArmyMember(s)), enemies)
        {
        }

        /// <summary>A standalone wolf fight with the starting army.</summary>
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

        /// <summary>The 3x3 blast area around a tile, clipped to the board.</summary>
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

        // ---------- Queries ----------

        public IEnumerable<Unit> Living(Team team) => _units.Where(u => u.Team == team && u.IsAlive && u.IsPlaced);

        public Unit UnitAt(GridPos p) =>
            _units.FirstOrDefault(u => u.IsPlaced && u.IsAlive && Cells(u).Contains(p));

        /// <summary>The bone pile on a tile, if any.</summary>
        public Unit RemainsAt(GridPos p) => _units.FirstOrDefault(u => u.IsRemains && u.Pos == p);

        public IEnumerable<Unit> Remains(Team team) => _units.Where(u => u.Team == team && u.IsRemains);

        public Mine MineAt(GridPos p) => _mines.FirstOrDefault(m => m.Tile == p);

        public Bomb BombAt(GridPos p) => _bombs.FirstOrDefault(b => b.Tile == p);

        /// <summary>A tile with no rock and no living unit. Bone piles, mines and bombs don't block.</summary>
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

            if (unit.Has(Ability.SteadyAim) && !unit.MovedThisTurn)
                damage += 1;
            return Math.Max(1, damage);
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

        /// <summary>Every anchor tile the unit can walk to with its current AP, with the AP cost. Excludes its own tile.</summary>
        public Dictionary<GridPos, int> GetReachable(Unit unit)
        {
            var costs = new Dictionary<GridPos, int>();
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

        /// <summary>Whether a player unit that hasn't finished could still move, attack or place a mine this turn.</summary>
        public bool CanStillAct(Unit unit)
        {
            if (!unit.IsAlive || unit.IsDone || unit.Team != Team.Player)
                return false;
            if (unit.Ap >= AttackCost && Living(Team.Enemy).Any(e => CanAttack(unit, e)))
                return true;
            if (unit.Pos.Neighbours().Any(p => CanPlaceMine(unit, p)))
                return true;
            return GetReachable(unit).Count > 0;
        }

        // ---------- Mines and bombs ----------

        public int MinesOf(Unit unit) => _mines.Count(m => m.Owner == unit);

        public bool CanPlaceMine(Unit unit, GridPos tile) =>
            !IsOver && unit.IsAlive && unit.Has(Ability.Mines) && unit.Ap >= MineCost &&
            MinesOf(unit) < MaxMinesPerUnit && Distance(tile, 1, unit) == 1 && IsFree(tile) &&
            MineAt(tile) == null && BombAt(tile) == null;

        public bool CanBomb(Unit thrower, Unit target)
        {
            if (IsOver || !thrower.IsAlive || !thrower.Stats.CanBomb || thrower.Ap < thrower.Stats.BombCost)
                return false;
            if (target == null || !target.IsAlive || target.Team == thrower.Team)
                return false;
            var steps = Distance(thrower, target);
            return steps >= 2 && steps <= thrower.Stats.BombRange && BombAt(target.Pos) == null;
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

        public List<BattleEvent> PlayerPlaceMine(Unit unit, GridPos tile)
        {
            if (!CanAct(unit) || !CanPlaceMine(unit, tile))
                return null;
            ActiveUnit = unit;
            unit.Ap -= MineCost;
            _mines.Add(new Mine(tile, unit));
            return new List<BattleEvent> { new PlaceMineEvent(unit, tile) };
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
        /// Ends the player phase and runs the whole enemy phase: first every bomb thrown last enemy
        /// turn explodes, then each enemy acts. Returns what happened.
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
            BeginTurn(Team.Enemy);

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
            BeginTurn(Team.Player);
            Phase = Phase.PlayerTurn;
            return events;
        }

        // ---------- Shared actions ----------

        /// <summary>
        /// Walks the path. An enemy that steps onto a mine stops there and sets it off.
        /// </summary>
        internal List<BattleEvent> DoMove(Unit unit, GridPos target)
        {
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
                Explode(triggered.Tile, MineDamage, "mine", triggered.Owner, events);
            return events;
        }

        internal List<BattleEvent> DoAttack(Unit attacker, Unit target)
        {
            var events = new List<BattleEvent>();
            var after = new List<BattleEvent>();
            attacker.Ap -= AttackCost;
            var hit = Damage(target, EffectiveDamage(attacker), attacker, after);
            events.Add(new AttackEvent(attacker, target, hit.Damage, hit.HpAfter));
            events.AddRange(after);
            if (!attacker.IsRanged)
                Thorns(attacker, target, events);
            CheckEnd();
            return events;
        }

        internal List<BattleEvent> DoSmash(Unit attacker)
        {
            var damage = EffectiveDamage(attacker);
            attacker.Ap -= AttackCost;
            var hits = new List<Hit>();
            var after = new List<BattleEvent>();
            var targets = AdjacentFoes(attacker);
            foreach (var target in targets)
                hits.Add(Damage(target, damage, attacker, after));
            var events = new List<BattleEvent> { new SmashEvent(attacker, hits) };
            events.AddRange(after);
            foreach (var target in targets)
                Thorns(attacker, target, events);
            CheckEnd();
            return events;
        }

        internal List<BattleEvent> DoThrow(Unit thrower, Unit target)
        {
            thrower.Ap -= thrower.Stats.ThrowCost;
            var rock = RockTileFor(thrower, target);
            var after = new List<BattleEvent>();
            var hit = Damage(target, thrower.Stats.ThrowDamage, thrower, after);
            if (rock.HasValue)
            {
                Board.AddObstacle(rock.Value);
                // A boulder landing on a mine or bomb buries it.
                _mines.RemoveAll(m => m.Tile == rock.Value);
                _bombs.RemoveAll(b => b.Tile == rock.Value);
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
            return new List<BattleEvent> { new RaiseEvent(raiser, remains) };
        }

        /// <summary>
        /// Blows up a mine or bomb at <paramref name="center"/>: every living unit in the 3x3 area takes
        /// the damage, friend or foe. Mines and bombs inside the area go off too, one after another.
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
                var victims = _units.Where(u => u.IsPlaced && u.IsAlive && Cells(u).Any(area.Contains)).ToList();
                var hits = new List<Hit>();
                var after = new List<BattleEvent>();
                foreach (var victim in victims)
                    hits.Add(Damage(victim, blast.damage, blast.owner, after));
                events.Add(new ExplosionEvent(blast.cause, blast.owner, blast.center, area, hits, blast.depth));
                events.AddRange(after);

                foreach (var mine in _mines.Where(m => area.Contains(m.Tile)).ToList())
                {
                    _mines.Remove(mine);
                    queue.Enqueue((mine.Tile, MineDamage, "mine", mine.Owner, blast.depth + 1));
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
        /// Every point of damage goes through here, so abilities apply to all sources: Shield Wall
        /// reduces it, Lucky Charm can stop a death, and a kill triggers the killer's Bloodthirst
        /// and Vampire Fang. Follow-up effects are added to <paramref name="after"/>.
        /// </summary>
        private Hit Damage(Unit target, int amount, Unit source, List<BattleEvent> after)
        {
            if (IsShielded(target))
                amount = Math.Max(1, amount - 1);

            var hp = target.Hp - amount;
            if (hp <= 0 && target.Has(Ability.LuckyCharm) && !target.LastStandUsed)
            {
                hp = 1;
                target.LastStandUsed = true;
                after.Add(new EffectEvent(EffectKind.LastStand, target, 0, 1, target));
            }

            target.Hp = Math.Max(0, hp);
            if (!target.IsAlive && source != null && source.IsAlive && source.Team != target.Team)
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
            }

            return new Hit(target, amount, target.Hp);
        }

        /// <summary>Thorned Mail: a melee attacker takes damage back from the unit it hit.</summary>
        private void Thorns(Unit attacker, Unit target, List<BattleEvent> events)
        {
            if (!target.Has(Ability.ThornedMail) || !attacker.IsAlive)
                return;
            var after = new List<BattleEvent>();
            var hit = Damage(attacker, ThornsDamage, target, after);
            events.Add(new EffectEvent(EffectKind.Thorns, attacker, hit.Damage, hit.HpAfter, target));
            events.AddRange(after);
        }

        /// <summary>True if the battle was ended with <see cref="DebugWin"/> instead of being played.</summary>
        public bool WasSkipped { get; private set; }

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

        private void BeginTurn(Team team)
        {
            foreach (var u in Living(team))
            {
                u.Ap = u.MaxAp + Math.Min(u.Ap, MaxCarriedAp);
                u.IsDone = false;
                u.MovedThisTurn = false;
                u.FreeStepUsed = false;
            }
        }

        private void CheckEnd()
        {
            if (IsOver)
                return;
            if (!Living(Team.Enemy).Any())
                Phase = Phase.Victory;
            else if (!Living(Team.Player).Any())
                Phase = Phase.Defeat;
        }
    }
}
