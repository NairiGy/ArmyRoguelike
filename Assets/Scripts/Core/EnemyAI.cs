using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    /// <summary>
    /// Simple enemy behaviour: pick the nearest player unit, walk towards it and attack while AP lasts.
    /// </summary>
    public static class EnemyAI
    {
        /// <summary>
        /// The player unit this enemy wants to attack: nearest in steps, then lowest HP, then lowest id.
        /// Also used by the view to show intent.
        /// </summary>
        public static Unit ChooseTarget(Battle battle, Unit enemy)
        {
            return battle.Living(Team.Player)
                .OrderBy(u => u.Pos.StepsTo(enemy.Pos))
                .ThenBy(u => u.Hp)
                .ThenBy(u => u.Id)
                .FirstOrDefault();
        }

        public static void TakeTurn(Battle battle, Unit enemy, List<BattleEvent> events)
        {
            // Each loop spends AP or stops, so this always ends.
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

                var destination = PickMove(battle, enemy, target);
                if (destination == null)
                    return;
                var move = battle.DoMove(enemy, destination.Value);
                if (move == null)
                    return;
                events.Add(move);
            }
        }

        private static GridPos? PickMove(Battle battle, Unit enemy, Unit target)
        {
            var reachable = battle.GetReachable(enemy);
            if (reachable.Count == 0)
                return null;

            // Best case: the cheapest tile that still leaves enough AP to attack from it.
            var attackSpots = reachable
                .Where(kv => enemy.Ap - kv.Value >= Battle.AttackCost && battle.CanHitFrom(enemy, kv.Key, target))
                .OrderBy(kv => kv.Value)
                .ToList();
            if (attackSpots.Count > 0)
                return attackSpots[0].Key;

            // Otherwise get as close as possible. Other units are ignored when measuring distance,
            // so a wolf will still advance when its path is temporarily blocked.
            var distance = DistanceField(battle, target.Pos);
            var current = distance.TryGetValue(enemy.Pos, out var d) ? d : int.MaxValue;
            var closer = reachable
                .Where(kv => distance.TryGetValue(kv.Key, out var dist) && dist < current)
                .OrderBy(kv => distance[kv.Key])
                .ThenBy(kv => kv.Value)
                .ToList();
            return closer.Count > 0 ? closer[0].Key : (GridPos?)null;
        }

        /// <summary>Walking distance from every tile to the goal, around obstacles but through units.</summary>
        private static Dictionary<GridPos, int> DistanceField(Battle battle, GridPos goal)
        {
            var distance = new Dictionary<GridPos, int> { [goal] = 0 };
            var frontier = new Queue<GridPos>();
            frontier.Enqueue(goal);
            while (frontier.Count > 0)
            {
                var p = frontier.Dequeue();
                foreach (var n in p.Neighbours())
                {
                    if (distance.ContainsKey(n) || !battle.Board.InBounds(n) || battle.Board.IsObstacle(n))
                        continue;
                    distance[n] = distance[p] + 1;
                    frontier.Enqueue(n);
                }
            }

            return distance;
        }
    }
}
