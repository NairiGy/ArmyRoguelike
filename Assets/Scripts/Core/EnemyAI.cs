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
    }

    /// <summary>What an enemy plans to do next, for the hover display.</summary>
    public readonly struct Intent
    {
        public readonly IntentKind Kind;

        /// <summary>The player unit it wants to hit, or the bone pile it wants to raise.</summary>
        public readonly Unit Target;

        /// <summary>For a throw: where the boulder will come to rest.</summary>
        public readonly GridPos? RockTile;

        public Intent(IntentKind kind, Unit target, GridPos? rockTile = null)
        {
            Kind = kind;
            Target = target;
            RockTile = rockTile;
        }
    }

    /// <summary>
    /// Enemy behaviour. Wolves and skeletons walk to the nearest player unit and attack while AP lasts.
    /// The necromancer stays back, raises bone piles and fires bolts. The ogre throws boulders from
    /// afar and smashes everything next to it up close.
    /// </summary>
    public static class EnemyAI
    {
        private const int NecromancerComfortDistance = 3;

        /// <summary>
        /// The player unit this enemy wants to attack: nearest in steps, then lowest HP, then lowest id.
        /// </summary>
        public static Unit ChooseTarget(Battle battle, Unit enemy)
        {
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
            if (enemy.Stats.CanRaise)
                NecromancerTurn(battle, enemy, events);
            else if (enemy.Stats.AreaMelee)
                OgreTurn(battle, enemy, events);
            else
                MeleeTurn(battle, enemy, events);
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
                    events.Add(battle.DoAttack(enemy, target));
                    continue;
                }

                if (!Approach(battle, enemy, target, Battle.AttackCost, events))
                    return;
            }
        }

        /// <summary>Smash anyone adjacent, throw one boulder per turn from range, otherwise walk closer.</summary>
        private static void OgreTurn(Battle battle, Unit ogre, List<BattleEvent> events)
        {
            var thrown = false;
            while (!battle.IsOver && ogre.IsAlive)
            {
                if (battle.AdjacentFoes(ogre).Count > 0)
                {
                    if (ogre.Ap < Battle.AttackCost)
                        return;
                    events.Add(battle.DoSmash(ogre));
                    continue;
                }

                var target = ChooseTarget(battle, ogre);
                if (target == null)
                    return;

                if (!thrown && battle.CanThrow(ogre, target))
                {
                    events.Add(battle.DoThrow(ogre, target));
                    thrown = true;
                    continue;
                }

                if (!Approach(battle, ogre, target, Battle.AttackCost, events))
                    return;
            }
        }

        /// <summary>Raise bone piles first, back away from anyone close, then fire bolts. Never advances.</summary>
        private static void NecromancerTurn(Battle battle, Unit necro, List<BattleEvent> events)
        {
            var retreated = false;
            while (!battle.IsOver && necro.IsAlive)
            {
                var remains = ChooseRemains(battle, necro);
                if (remains != null)
                {
                    events.Add(battle.DoRaise(necro, remains));
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
                    events.Add(battle.DoAttack(necro, target));
                    continue;
                }

                return;
            }
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
            events.Add(move);
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
            events.Add(move);
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
