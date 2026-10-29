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

        /// <summary>A Pack Leader howling: every wolf that hasn't acted gets +1 move.</summary>
        Howl,

        /// <summary>A goblin flinging another goblin, a bomb or a mine at one of your units.</summary>
        Fling,
    }

    /// <summary>What an enemy plans to do next, for the hover display.</summary>
    public readonly struct Intent
    {
        public readonly IntentKind Kind;

        /// <summary>The player unit it wants to hit, or the bone pile it wants to raise.</summary>
        public readonly Unit Target;

        /// <summary>For a throw: where the boulder will come to rest. For a bomb: where it will land. For a fling: where the goblin lands or the explosive goes off.</summary>
        public readonly GridPos? Tile;

        /// <summary>For a fling: the goblin it will throw, or null for a bomb or mine.</summary>
        public readonly Unit Thrown;

        public Intent(IntentKind kind, Unit target, GridPos? tile = null, Unit thrown = null)
        {
            Kind = kind;
            Target = target;
            Tile = tile;
            Thrown = thrown;
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

        /// <summary>Walks up and strikes; when it can't reach anyone, flings what is next to it.</summary>
        Goblin,

        /// <summary>Howls when that lets wolves reach their prey, otherwise like Advance.</summary>
        PackLeader,

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
        public const int NecromancerComfortDistance = 2;
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
            if (stats.Has(Ability.Fling))
                return EnemyBehaviour.Goblin;
            if (stats.Has(Ability.Howl))
                return EnemyBehaviour.PackLeader;
            if (stats.AreaMelee)
                return EnemyBehaviour.Giant;
            return EnemyBehaviour.Advance;
        }

        /// <summary>Break the Ritual: a player unit this close to a totem counts as threatening it.</summary>
        public const int TotemThreatRange = 2;

        /// <summary>Your units enemies can see: everyone except vanished rogues.</summary>
        private static IEnumerable<Unit> Visible(Battle battle) => battle.Living(Team.Player).Where(u => !u.IsHidden);

        /// <summary>
        /// The player unit this enemy wants to attack, among those it can see: a taunter in range first; in a
        /// ritual fight a defender next to it, else the objective; in Break the Ritual the nearest unit
        /// threatening a totem; for Blood Scent the weakest it can reach; otherwise nearest in steps, then
        /// lowest HP, then lowest id.
        /// </summary>
        public static Unit ChooseTarget(Battle battle, Unit enemy)
        {
            // A taunting unit within range overrides everything.
            var taunter = Visible(battle)
                .Where(u => u.Has(Ability.Taunt) && Battle.Distance(enemy, u) <= Battle.TauntRange)
                .OrderBy(u => Battle.Distance(enemy, u))
                .ThenBy(u => u.Id)
                .FirstOrDefault();
            if (taunter != null)
                return taunter;

            // Ritual fights: go for the objective, unless a defender is already next to this enemy.
            var objective = battle.Objective;
            if (objective != null && objective.IsAlive)
            {
                var blocker = Visible(battle)
                    .Where(u => u != objective && Battle.Distance(enemy, u) == 1)
                    .OrderBy(u => u.Hp)
                    .ThenBy(u => u.Id)
                    .FirstOrDefault();
                return blocker ?? objective;
            }

            // Break the Ritual: guards go for whoever threatens a totem (within reach of one), nearest first.
            var totems = battle.Totems.Where(t => t.IsAlive).ToList();
            if (totems.Count > 0)
            {
                var threat = Visible(battle)
                    .Where(u => totems.Any(t => Battle.Distance(t, u) <= TotemThreatRange))
                    .OrderBy(u => Battle.Distance(enemy, u))
                    .ThenBy(u => u.Hp)
                    .ThenBy(u => u.Id)
                    .FirstOrDefault();
                if (threat != null)
                    return threat;
            }

            // Blood Scent: the lowest-HP unit it can reach and attack this turn.
            if (enemy.Has(Ability.BloodScent))
            {
                var prey = Visible(battle)
                    .Where(u => !u.IsObjective && CanReachToAttack(battle, enemy, u))
                    .OrderBy(u => u.Hp)
                    .ThenBy(u => Battle.Distance(enemy, u))
                    .ThenBy(u => u.Id)
                    .FirstOrDefault();
                if (prey != null)
                    return prey;
            }

            return Visible(battle)
                .OrderBy(u => Battle.Distance(enemy, u))
                .ThenBy(u => u.Hp)
                .ThenBy(u => u.Id)
                .FirstOrDefault();
        }

        /// <summary>
        /// Runs a check as if the unit's turn were about to happen: during the enemy turn with what it has
        /// left, otherwise (hover intent) with its full AP and moves. <paramref name="extraMoves"/> is added on top.
        /// </summary>
        private static T AsIfItsTurn<T>(Battle battle, Unit unit, int extraMoves, System.Func<T> check)
        {
            var (moves, ap) = (unit.Moves, unit.Ap);
            if (battle.Phase != Phase.EnemyTurn)
            {
                unit.Moves = unit.MaxMoves;
                unit.Ap = unit.MaxAp;
            }

            unit.Moves += extraMoves;
            try
            {
                return check();
            }
            finally
            {
                (unit.Moves, unit.Ap) = (moves, ap);
            }
        }

        /// <summary>Whether the unit can attack the target this turn, walking up first if needed.</summary>
        private static bool CanReachToAttack(Battle battle, Unit unit, Unit target, int extraMoves = 0) =>
            AsIfItsTurn(battle, unit, extraMoves, () => battle.FindAttackSpot(unit, target).HasValue);

        /// <summary>
        /// Howl when the extra move lets wolves bite this turn that otherwise couldn't: at least one if the
        /// leader can't bite anyone itself, at least two if it could.
        /// </summary>
        public static bool ShouldHowl(Battle battle, Unit leader)
        {
            if (!leader.Has(Ability.Howl) || (battle.Phase == Phase.EnemyTurn && leader.Ap < Battle.AttackCost))
                return false;
            var helped = battle.Living(leader.Team)
                .Where(w => w.IsWolf && (battle.Phase != Phase.EnemyTurn || !w.IsDone) && battle.CanMove(w))
                .Count(w =>
                {
                    var prey = ChooseTarget(battle, w);
                    return prey != null && !CanReachToAttack(battle, w, prey) && CanReachToAttack(battle, w, prey, Battle.HowlMoves);
                });
            var ownTarget = ChooseTarget(battle, leader);
            var canBite = ownTarget != null && CanReachToAttack(battle, leader, ownTarget);
            return helped >= (canBite ? 2 : 1);
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
            if (enemy.IsObjective)
                return new Intent(IntentKind.None, null);

            if (enemy.CarryingLoot > 0)
                return new Intent(IntentKind.Flee, enemy);

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

            if (ShouldHowl(battle, enemy))
                return new Intent(IntentKind.Howl, enemy);

            if (BehaviourOf(enemy.Stats) == EnemyBehaviour.Goblin)
            {
                var fling = AsIfItsTurn(battle, enemy, 0, () => ChooseFling(battle, enemy, target));
                if (fling.HasValue && (fling.Value.goblin == null || !CanReachToAttack(battle, enemy, target)))
                    return new Intent(IntentKind.Fling, fling.Value.target, fling.Value.to, fling.Value.goblin);
            }

            if (enemy.Stats.CanBomb)
            {
                var steps = Battle.Distance(enemy, target);
                if (steps >= 2 && steps <= enemy.Stats.BombRange && battle.BombAt(target.Pos) == null)
                    return new Intent(IntentKind.Bomb, target, target.Pos);
                return new Intent(IntentKind.Attack, target);
            }

            if (BehaviourOf(enemy.Stats) == EnemyBehaviour.Giant)
            {
                // Intent is shown during the player's turn, so assume the Giant has its AP.
                var steps = Battle.Distance(enemy, target);
                var action = GiantAction(battle, enemy, target);
                if (action == IntentKind.Slam)
                    return new Intent(IntentKind.Slam, enemy);
                if (action == IntentKind.None && enemy.Stats.CanThrow && steps >= 2 && steps <= enemy.Stats.ThrowRange &&
                    !RockWallsOff(battle, enemy, target) && !battle.IsShroudedFrom(target.Pos, enemy.Team))
                    action = IntentKind.Throw;
                if (action == IntentKind.Throw)
                    return new Intent(IntentKind.Throw, target, battle.RockTileFor(enemy, target));
                return new Intent(IntentKind.Smash, target);
            }

            return new Intent(IntentKind.Attack, target);
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
                case EnemyBehaviour.Goblin:
                    GoblinTurn(battle, enemy, events);
                    break;
                case EnemyBehaviour.PackLeader:
                    if (ShouldHowl(battle, enemy))
                        events.AddRange(battle.DoHowl(enemy));
                    MeleeTurn(battle, enemy, events);
                    break;
                default:
                    MeleeTurn(battle, enemy, events);
                    break;
            }
        }

        // ---------- Behaviours ----------

        /// <summary>Walk up to the nearest player unit and attack. Each loop spends AP or moves, or stops.</summary>
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
        /// A bomb or mine lying next to it always gets flung. Otherwise strike its target if it can (walking up
        /// first if that reaches). If it can't reach anyone to strike this turn, fling a goblin that hasn't acted
        /// at its target, or at any of your units in range. Otherwise walk up and try again.
        /// </summary>
        private static void GoblinTurn(Battle battle, Unit goblin, List<BattleEvent> events)
        {
            while (!battle.IsOver && goblin.IsAlive)
            {
                var target = ChooseTarget(battle, goblin);
                if (target == null)
                    return;

                if (ChooseFling(battle, goblin, target) is var explosive && explosive.HasValue && explosive.Value.goblin == null)
                {
                    events.AddRange(battle.DoFling(goblin, explosive.Value.target));
                    continue;
                }

                if (battle.CanAttack(goblin, target))
                {
                    events.AddRange(battle.DoAttack(goblin, target));
                    continue;
                }

                if (goblin.Ap >= Battle.AttackCost && battle.FindAttackSpot(goblin, target) == null &&
                    ChooseFling(battle, goblin, target) is var fling && fling.HasValue)
                {
                    events.AddRange(battle.DoFling(goblin, fling.Value.target));
                    continue;
                }

                if (!Approach(battle, goblin, target, Battle.AttackCost, events))
                    return;
            }
        }

        /// <summary>A fling at the target if possible, else at the nearest of your units it can fling at.</summary>
        private static (Unit target, Unit goblin, GridPos to)? ChooseFling(Battle battle, Unit thrower, Unit target)
        {
            if (thrower.Ap < Battle.AttackCost)
                return null;
            foreach (var u in new[] { target }.Concat(Visible(battle).OrderBy(u => Battle.Distance(thrower, u)).ThenBy(u => u.Id)))
                if (battle.FlingOption(thrower, u) is var option && option.HasValue)
                    return (u, option.Value.goblin, option.Value.to);
            return null;
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

                    // Furthest row reachable, cheapest first. Cornered (it can't get any further back this
                    // turn): it hits whoever it can instead of standing still.
                    var best = battle.GetReachable(thief)
                        .OrderByDescending(kv => kv.Key.Y)
                        .ThenBy(kv => kv.Value)
                        .FirstOrDefault();
                    if (best.Key.Y <= thief.Pos.Y)
                    {
                        var cornered = ChooseTarget(battle, thief);
                        if (cornered != null && battle.CanAttack(thief, cornered))
                            events.AddRange(battle.DoAttack(thief, cornered));
                        return;
                    }
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
        /// The Giant's one action from where it stands: smash or slam, whichever deals more in total (slam only
        /// with two or more of your units within 2 tiles; ties go to smash), else a boulder at the target, else
        /// nothing (it walks closer first).
        /// </summary>
        private static IntentKind GiantAction(Battle battle, Unit ogre, Unit target)
        {
            var adjacent = battle.AdjacentFoes(ogre).Count(u => !u.IsObjective);
            var near = battle.Living(Team.Player).Count(u => !u.IsObjective && Battle.Distance(ogre, u) <= 2);
            var smash = adjacent * ogre.Stats.Damage;
            var slam = ogre.Stats.Slams && near >= 2 ? near * Battle.SlamDamage : 0;
            if (slam > smash)
                return IntentKind.Slam;
            if (adjacent > 0)
                return IntentKind.Smash;
            if (target != null && battle.CanThrow(ogre, target) && !RockWallsOff(battle, ogre, target))
                return IntentKind.Throw;
            return IntentKind.None;
        }

        /// <summary>One action a turn (see <see cref="GiantAction"/>); without one it walks closer and tries again.</summary>
        private static void OgreTurn(Battle battle, Unit ogre, List<BattleEvent> events)
        {
            while (!battle.IsOver && ogre.IsAlive)
            {
                var target = ChooseTarget(battle, ogre);
                if (target == null)
                    return;

                if (ogre.Ap >= Battle.AttackCost)
                {
                    switch (GiantAction(battle, ogre, target))
                    {
                        case IntentKind.Slam:
                            events.AddRange(battle.DoSlam(ogre));
                            continue;
                        case IntentKind.Smash:
                            events.AddRange(battle.DoSmash(ogre));
                            continue;
                        case IntentKind.Throw:
                            events.AddRange(battle.DoThrow(ogre, target));
                            continue;
                    }
                }

                if (!Approach(battle, ogre, target, Battle.AttackCost, events))
                    return;
            }
        }

        /// <summary>
        /// True if the boulder thrown at the target would leave the thrower with no walking route to it. On the
        /// small board a 2x2 Giant can otherwise wall itself in with its own rocks.
        /// </summary>
        private static bool RockWallsOff(Battle battle, Unit thrower, Unit target)
        {
            var rock = battle.RockTileFor(thrower, target);
            if (rock == null || !DistanceField(battle, thrower, target).ContainsKey(thrower.Pos))
                return false;
            battle.Board.AddObstacle(rock.Value);
            var cut = !DistanceField(battle, thrower, target).ContainsKey(thrower.Pos);
            battle.Board.RemoveObstacle(rock.Value);
            return cut;
        }

        /// <summary>
        /// Back away from anyone close, then raise bone piles, else fire bolts. With nothing to raise
        /// and nobody in range it walks up to bolt range, but never closer than its comfort distance.
        /// </summary>
        private static void NecromancerTurn(Battle battle, Unit necro, List<BattleEvent> events)
        {
            var retreated = false;
            var advanced = false;
            while (!battle.IsOver && necro.IsAlive)
            {
                // Back off first: once it raises or shoots it can't move any more this turn.
                var target = ChooseTarget(battle, necro);
                if (target != null && !retreated && Battle.Distance(necro, target) < NecromancerComfortDistance)
                {
                    retreated = true;
                    if (Retreat(battle, necro, events))
                        continue;
                }

                var remains = ChooseRemains(battle, necro);
                if (remains != null)
                {
                    events.AddRange(battle.DoRaise(necro, remains));
                    continue;
                }

                if (target == null)
                    return;

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
        /// Moves to a tile from which <paramref name="canHitFrom"/> holds (if it has <paramref name="keepAp"/> to use there),
        /// staying at least <paramref name="comfort"/> tiles from every player unit. If no such tile
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
                .Where(kv => unit.Ap >= keepAp && canHitFrom(kv.Key))
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
        /// Moves towards a tile from which the target can be hit, if it has the AP to attack from there;
        /// otherwise as close as it can. Returns false if no useful move exists.
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

            // Best case: the cheapest tile it can attack from, when it has the AP to.
            var attackSpots = reachable
                .Where(kv => enemy.Ap >= keepAp && battle.CanHitFrom(enemy, kv.Key, target))
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
