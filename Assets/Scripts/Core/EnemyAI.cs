using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    public enum IntentKind
    {
        None,
        Attack,
        Smash,
        Throw,
        Raise,
        Bomb,

        /// <summary>A cutpurse running off with stolen gold.</summary>
        Flee,

        /// <summary>The Giant shoving everyone near it.</summary>
        Slam,
    }

    /// <summary>What an enemy plans to do next, for the hover display.</summary>
    public readonly struct Intent
    {
        public readonly IntentKind Kind;

        /// <summary>The player unit it wants to hit, or the bone pile it wants to raise.</summary>
        public readonly Unit Target;

        /// <summary>For a throw: where the boulder will come to rest. For a bomb: where it will land.</summary>
        public readonly GridPos? Tile;

        public Intent(IntentKind kind, Unit target, GridPos? tile = null)
        {
            Kind = kind;
            Target = target;
            Tile = tile;
        }
    }

    /// <summary>Which turn routine an enemy type runs. The gamepedia documents each one.</summary>
    public enum EnemyBehaviour
    {
        /// <summary>Walk to the target and use the basic attack (melee or ranged).</summary>
        Advance,

        /// <summary>Like Advance until it lands a hit, then runs for the far edge with the gold.</summary>
        Thief,

        /// <summary>Keeps its distance and lobs delayed bombs.</summary>
        Bomber,

        /// <summary>Slam, area smash and boulder throws.</summary>
        Giant,

        /// <summary>Raises bone piles, keeps its distance and fires bolts.</summary>
        Necromancer,
    }

    /// <summary>
    /// Enemy behaviour. Wolves, skeletons and goblins walk to the nearest player unit and attack while
    /// AP lasts. The necromancer stays back, raises bone piles and fires bolts. The ogre throws boulders
    /// from afar and smashes everything next to it up close. Goblin bombers keep their distance and lob
    /// bombs that go off next enemy turn.
    /// </summary>
    public static class EnemyAI
    {
        public const int NecromancerComfortDistance = 3;
        public const int BomberComfortDistance = 2;

        /// <summary>The turn routine a type runs; <see cref="TakeTurn"/> dispatches on this.</summary>
        public static EnemyBehaviour BehaviourOf(UnitStats stats)
        {
            if (stats.CanRaise)
                return EnemyBehaviour.Necromancer;
            if (stats.StealsGold > 0)
                return EnemyBehaviour.Thief;
            if (stats.CanBomb)
                return EnemyBehaviour.Bomber;
            if (stats.AreaMelee)
                return EnemyBehaviour.Giant;
            return EnemyBehaviour.Advance;
        }

        /// <summary>
        /// The player unit this enemy wants to attack: nearest in steps, then lowest HP, then lowest id.
        /// </summary>
        public static Unit ChooseTarget(Battle battle, Unit enemy)
        {
            // A taunting unit within range overrides everything.
            var taunter = battle.Living(Team.Player)
                .Where(u => u.Has(Ability.Taunt) && Battle.Distance(enemy, u) <= Battle.TauntRange)
                .OrderBy(u => Battle.Distance(enemy, u))
                .ThenBy(u => u.Id)
                .FirstOrDefault();
            if (taunter != null)
                return taunter;

            return battle.Living(Team.Player)
                .OrderBy(u => Battle.Distance(enemy, u))
                .ThenBy(u => u.Hp)
                .ThenBy(u => u.Id)
                .FirstOrDefault();
        }

        /// <summary>The bone pile a necromancer would raise right now, if any.</summary>
        public static Unit ChooseRemains(Battle battle, Unit raiser) =>
            battle.Remains(raiser.Team)
                .Where(r => battle.CanRaise(raiser, r))
                .OrderBy(r => Battle.Distance(raiser, r))
                .ThenBy(r => r.Id)
                .FirstOrDefault();

        /// <summary>What this enemy would do first if its turn started now. Used to show intent on hover.</summary>
        public static Intent PredictIntent(Battle battle, Unit enemy)
        {
            if (enemy.CarryingLoot > 0)
                return new Intent(IntentKind.Flee, enemy);

            if (enemy.Stats.Slams && battle.Living(Team.Player).Count(u => Battle.Distance(enemy, u) <= 2) >= 2)
                return new Intent(IntentKind.Slam, enemy);

            if (enemy.Stats.CanRaise)
            {
                // Intent is shown during the player's turn, so assume a full enemy AP pool.
                var remains = battle.Remains(enemy.Team)
                    .Where(r => Battle.Distance(enemy, r) <= enemy.Stats.RaiseRange && battle.UnitAt(r.Pos) == null)
                    .OrderBy(r => Battle.Distance(enemy, r))
                    .ThenBy(r => r.Id)
                    .FirstOrDefault();
                if (remains != null)
                    return new Intent(IntentKind.Raise, remains);
            }

            var target = ChooseTarget(battle, enemy);
            if (target == null)
                return new Intent(IntentKind.None, null);

            if (enemy.Stats.CanBomb)
            {
                var steps = Battle.Distance(enemy, target);
                if (steps >= 2 && steps <= enemy.Stats.BombRange && battle.BombAt(target.Pos) == null)
                    return new Intent(IntentKind.Bomb, target, target.Pos);
                return new Intent(IntentKind.Attack, target);
            }

            if (enemy.Stats.AreaMelee && Battle.Distance(enemy, target) == 1)
                return new Intent(IntentKind.Smash, target);
            if (enemy.Stats.CanThrow)
            {
                var steps = Battle.Distance(enemy, target);
                if (steps >= 2 && steps <= enemy.Stats.ThrowRange)
                    return new Intent(IntentKind.Throw, target, battle.RockTileFor(enemy, target));
            }

            return new Intent(enemy.Stats.AreaMelee ? IntentKind.Smash : IntentKind.Attack, target);
        }

        public static void TakeTurn(Battle battle, Unit enemy, List<BattleEvent> events)
        {
            switch (BehaviourOf(enemy.Stats))
            {
                case EnemyBehaviour.Necromancer:
                    NecromancerTurn(battle, enemy, events);
                    break;
                case EnemyBehaviour.Thief:
                    CutpurseTurn(battle, enemy, events);
                    break;
                case EnemyBehaviour.Bomber:
                    BomberTurn(battle, enemy, events);
                    break;
                case EnemyBehaviour.Giant:
                    OgreTurn(battle, enemy, events);
                    break;
                default:
                    MeleeTurn(battle, enemy, events);
                    break;
            }
        }

        // ---------- Behaviours ----------

        /// <summary>Walk up to the nearest player unit and attack. Each loop spends AP or stops.</summary>
        private static void MeleeTurn(Battle battle, Unit enemy, List<BattleEvent> events)
        {
            while (!battle.IsOver && enemy.IsAlive)
            {
                var target = ChooseTarget(battle, enemy);
                if (target == null)
                    return;

                if (battle.CanAttack(enemy, target))
                {
                    events.AddRange(battle.DoAttack(enemy, target));
                    continue;
                }

                if (!Approach(battle, enemy, target, Battle.AttackCost, events))
                    return;
            }
        }

        /// <summary>
        /// Without loot: walk up and stab like any melee unit. With loot: run for the far edge (the
        /// enemies' back row) and escape once it gets there.
        /// </summary>
        private static void CutpurseTurn(Battle battle, Unit thief, List<BattleEvent> events)
        {
            var backRow = battle.Board.Height - 1;
            while (!battle.IsOver && thief.IsAlive && !thief.Escaped)
            {
                if (thief.CarryingLoot > 0)
                {
                    if (thief.Pos.Y == backRow)
                    {
                        battle.Escape(thief, events);
                        return;
                    }

                    // Furthest row reachable, cheapest first; give up if it can't get any further this turn.
                    var best = battle.GetReachable(thief)
                        .OrderByDescending(kv => kv.Key.Y)
                        .ThenBy(kv => kv.Value)
                        .FirstOrDefault();
                    if (best.Key.Y <= thief.Pos.Y)
                        return;
                    var move = battle.DoMove(thief, best.Key);
                    if (move == null)
                        return;
                    events.AddRange(move);
                    continue;
                }

                var target = ChooseTarget(battle, thief);
                if (target == null)
                    return;
                if (battle.CanAttack(thief, target))
                {
                    events.AddRange(battle.DoAttack(thief, target));
                    continue;
                }

                if (!Approach(battle, thief, target, Battle.AttackCost, events))
                    return;
            }
        }

        /// <summary>
        /// Slam first if two or more of the player's units are within 2 tiles, then smash anyone adjacent,
        /// throw one boulder per turn from range, otherwise walk closer.
        /// </summary>
        private static void OgreTurn(Battle battle, Unit ogre, List<BattleEvent> events)
        {
            var thrown = false;
            if (ogre.Stats.Slams && ogre.Ap >= Battle.AttackCost &&
                battle.Living(Team.Player).Count(u => Battle.Distance(ogre, u) <= 2) >= 2)
                events.AddRange(battle.DoSlam(ogre));

            while (!battle.IsOver && ogre.IsAlive)
            {
                if (battle.AdjacentFoes(ogre).Count > 0)
                {
                    if (ogre.Ap < Battle.AttackCost)
                        return;
                    events.AddRange(battle.DoSmash(ogre));
                    continue;
                }

                var target = ChooseTarget(battle, ogre);
                if (target == null)
                    return;

                if (!thrown && battle.CanThrow(ogre, target))
                {
                    events.AddRange(battle.DoThrow(ogre, target));
                    thrown = true;
                    continue;
                }

                if (!Approach(battle, ogre, target, Battle.AttackCost, events))
                    return;
            }
        }

        /// <summary>
        /// Raise bone piles first, back away from anyone close, then fire bolts. With nothing to raise
        /// and nobody in range it walks up to bolt range, but never closer than its comfort distance.
        /// </summary>
        private static void NecromancerTurn(Battle battle, Unit necro, List<BattleEvent> events)
        {
            var retreated = false;
            var advanced = false;
            while (!battle.IsOver && necro.IsAlive)
            {
                var remains = ChooseRemains(battle, necro);
                if (remains != null)
                {
                    events.AddRange(battle.DoRaise(necro, remains));
                    continue;
                }

                var target = ChooseTarget(battle, necro);
                if (target == null)
                    return;

                if (!retreated && Battle.Distance(necro, target) < NecromancerComfortDistance)
                {
                    retreated = true;
                    if (Retreat(battle, necro, events))
                        continue;
                }

                if (battle.CanAttack(necro, target))
                {
                    events.AddRange(battle.DoAttack(necro, target));
                    continue;
                }

                if (!advanced)
                {
                    advanced = true;
                    if (AdvanceToRange(battle, necro, target, NecromancerComfortDistance, Battle.AttackCost,
                            tile => battle.CanHitFrom(necro, tile, target), events))
                        continue;
                }

                return;
            }
        }

        /// <summary>
        /// Back away from anyone adjacent, lob one bomb per turn at the nearest unit 2+ tiles away,
        /// otherwise walk up to bomb range (never closer than 2). Stabs only when cornered.
        /// </summary>
        private static void BomberTurn(Battle battle, Unit bomber, List<BattleEvent> events)
        {
            var retreated = false;
            var advanced = false;
            var thrown = false;
            while (!battle.IsOver && bomber.IsAlive)
            {
                var target = ChooseTarget(battle, bomber);
                if (target == null)
                    return;

                if (!retreated && Battle.Distance(bomber, target) < BomberComfortDistance)
                {
                    retreated = true;
                    if (Retreat(battle, bomber, events))
                        continue;
                }

                if (!thrown)
                {
                    // Prefer the nearest unit, but any unit in range without a bomb on it will do.
                    var bombTarget = battle.CanBomb(bomber, target)
                        ? target
                        : battle.Living(Team.Player).Where(u => battle.CanBomb(bomber, u)).OrderBy(u => Battle.Distance(bomber, u)).FirstOrDefault();
                    if (bombTarget != null)
                    {
                        events.AddRange(battle.DoBomb(bomber, bombTarget));
                        thrown = true;
                        continue;
                    }
                }

                if (battle.CanAttack(bomber, target))
                {
                    events.AddRange(battle.DoAttack(bomber, target));
                    continue;
                }

                if (!thrown && !advanced)
                {
                    advanced = true;
                    var range = bomber.Stats.BombRange;
                    if (AdvanceToRange(battle, bomber, target, BomberComfortDistance, bomber.Stats.BombCost,
                            tile =>
                            {
                                var steps = Battle.Distance(tile, bomber.Size, target);
                                return steps >= 2 && steps <= range;
                            }, events))
                        continue;
                }

                return;
            }
        }

        /// <summary>
        /// Moves to a tile from which <paramref name="canHitFrom"/> holds with <paramref name="keepAp"/> to
        /// spare, staying at least <paramref name="comfort"/> tiles from every player unit. If no such tile
        /// is reachable this turn, gets as close to the target as that distance allows. False if no move helps.
        /// </summary>
        private static bool AdvanceToRange(Battle battle, Unit unit, Unit target, int comfort, int keepAp,
            System.Func<GridPos, bool> canHitFrom, List<BattleEvent> events)
        {
            int Safety(GridPos anchor) =>
                battle.Living(Team.Player).Select(u => Battle.Distance(anchor, unit.Size, u)).DefaultIfEmpty(int.MaxValue).Min();

            var reachable = battle.GetReachable(unit)
                .Where(kv => Safety(kv.Key) >= comfort)
                .ToList();

            var shootingSpot = reachable
                .Where(kv => unit.Ap - kv.Value >= keepAp && canHitFrom(kv.Key))
                .OrderBy(kv => kv.Value)
                .Select(kv => (GridPos?)kv.Key)
                .FirstOrDefault();

            var current = Battle.Distance(unit, target);
            var destination = shootingSpot ?? reachable
                .Where(kv => Battle.Distance(kv.Key, unit.Size, target) < current)
                .OrderBy(kv => Battle.Distance(kv.Key, unit.Size, target))
                .ThenBy(kv => kv.Value)
                .Select(kv => (GridPos?)kv.Key)
                .FirstOrDefault();
            if (destination == null)
                return false;

            var move = battle.DoMove(unit, destination.Value);
            if (move == null)
                return false;
            events.AddRange(move);
            return true;
        }

        // ---------- Movement helpers ----------

        /// <summary>
        /// Moves towards a tile from which the target can be hit, keeping enough AP for the attack if
        /// possible. Returns false if no useful move exists.
        /// </summary>
        private static bool Approach(Battle battle, Unit enemy, Unit target, int keepAp, List<BattleEvent> events)
        {
            var destination = PickApproach(battle, enemy, target, keepAp);
            if (destination == null)
                return false;
            var move = battle.DoMove(enemy, destination.Value);
            if (move == null)
                return false;
            events.AddRange(move);
            return true;
        }

        private static GridPos? PickApproach(Battle battle, Unit enemy, Unit target, int keepAp)
        {
            var reachable = battle.GetReachable(enemy);
            if (reachable.Count == 0)
                return null;

            // Best case: the cheapest tile that still leaves enough AP to attack from it.
            var attackSpots = reachable
                .Where(kv => enemy.Ap - kv.Value >= keepAp && battle.CanHitFrom(enemy, kv.Key, target))
                .OrderBy(kv => kv.Value)
                .ToList();
            if (attackSpots.Count > 0)
                return attackSpots[0].Key;

            // Otherwise get as close as possible. Other units are ignored when measuring distance,
            // so an enemy will still advance when its path is temporarily blocked.
            var distance = DistanceField(battle, enemy, target);
            var current = distance.TryGetValue(enemy.Pos, out var d) ? d : int.MaxValue;
            var closer = reachable
                .Where(kv => distance.TryGetValue(kv.Key, out var dist) && dist < current)
                .OrderBy(kv => distance[kv.Key])
                .ThenBy(kv => kv.Value)
                .ToList();
            return closer.Count > 0 ? closer[0].Key : (GridPos?)null;
        }

        /// <summary>Moves to the reachable tile furthest from every player unit. False if nothing is safer.</summary>
        private static bool Retreat(Battle battle, Unit enemy, List<BattleEvent> events)
        {
            int Safety(GridPos anchor) =>
                battle.Living(Team.Player).Select(u => Battle.Distance(anchor, enemy.Size, u)).DefaultIfEmpty(0).Min();

            var current = Safety(enemy.Pos);
            var best = battle.GetReachable(enemy)
                .Select(kv => (pos: kv.Key, cost: kv.Value, safety: Safety(kv.Key)))
                .Where(t => t.safety > current)
                .OrderByDescending(t => t.safety)
                .ThenBy(t => t.cost)
                .ToList();
            if (best.Count == 0)
                return false;
            var move = battle.DoMove(enemy, best[0].pos);
            if (move == null)
                return false;
            events.AddRange(move);
            return true;
        }

        /// <summary>
        /// Walking distance, around obstacles but through units, from every anchor tile to the nearest
        /// anchor where this unit would stand next to the target.
        /// </summary>
        private static Dictionary<GridPos, int> DistanceField(Battle battle, Unit enemy, Unit target)
        {
            bool Walkable(GridPos anchor) =>
                Battle.Footprint(anchor, enemy.Size).All(c => battle.Board.InBounds(c) && !battle.Board.IsObstacle(c));

            var distance = new Dictionary<GridPos, int>();
            var frontier = new Queue<GridPos>();
            foreach (var p in battle.Board.AllTiles())
            {
                if (!Walkable(p) || Battle.Distance(p, enemy.Size, target) != 1)
                    continue;
                distance[p] = 0;
                frontier.Enqueue(p);
            }

            while (frontier.Count > 0)
            {
                var p = frontier.Dequeue();
                foreach (var n in p.Neighbours())
                {
                    if (distance.ContainsKey(n) || !Walkable(n))
                        continue;
                    distance[n] = distance[p] + 1;
                    frontier.Enqueue(n);
                }
            }

            return distance;
        }
    }
}
