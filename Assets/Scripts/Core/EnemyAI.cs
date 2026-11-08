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

        /// <summary>A crossbowman aiming: it shoots the first of your units that walks onto the marked tiles.</summary>
        Overwatch,

        /// <summary>An elemental bursting the ground up under its target: damage and a push.</summary>
        Upheaval,

        /// <summary>A necromancer blowing up a bone pile your units stand on or next to.</summary>
        CorpseBlast,

        /// <summary>A skeleton grabbing its target so it can't move next turn.</summary>
        Grasp,

        /// <summary>A brute about to hit and shove its target 1 tile.</summary>
        BullRush,

        // Demons

        /// <summary>An imp about to trade places with its target; the target lands on <see cref="Intent.Tile"/>.</summary>
        Hellswap,

        /// <summary>A demon paying its own HP for a much harder hit on its target.</summary>
        RendingBlow,

        /// <summary>A devil signing a contract on its target: HP lost next enemy turn unless the Devil is hurt first.</summary>
        SoulContract,

        /// <summary>A fiend breathing fire down a line at its target, from <see cref="Intent.Tile"/>.</summary>
        FireBreath,
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

        /// <summary>Shoots from cover; with no shot it walks closer and goes on overwatch.</summary>
        Shooter,

        /// <summary>Walks up and strikes; when it can't reach anyone, flings what is next to it.</summary>
        Goblin,

        /// <summary>Howls when that lets wolves reach their prey, otherwise like Advance.</summary>
        PackLeader,

        /// <summary>Walks up and strikes; when it can't reach anyone, walks closer and raises a rock under one of your units.</summary>
        Elemental,

        /// <summary>Raises bone piles, keeps its distance and fires bolts.</summary>
        Necromancer,

        // Demons

        /// <summary>Swaps one of your units next to its allies when it can, otherwise like Advance.</summary>
        Imp,

        /// <summary>Keeps its distance, signs Soul Contracts and strikes with its trident.</summary>
        Devil,
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
            if (stats.Has(Ability.SoulContract))
                return EnemyBehaviour.Devil;
            if (stats.Has(Ability.Hellswap))
                return EnemyBehaviour.Imp;
            if (stats.StealsGold > 0)
                return EnemyBehaviour.Thief;
            if (stats.CanBomb)
                return EnemyBehaviour.Bomber;
            if (stats.Has(Ability.Overwatch))
                return EnemyBehaviour.Shooter;
            if (stats.Has(Ability.Fling))
                return EnemyBehaviour.Goblin;
            if (stats.Has(Ability.Howl))
                return EnemyBehaviour.PackLeader;
            if (stats.Has(Ability.Upheaval))
                return EnemyBehaviour.Elemental;
            if (stats.AreaMelee)
                return EnemyBehaviour.Giant;
            return EnemyBehaviour.Advance;
        }

        /// <summary>Break the Ritual: a player unit this close to a totem counts as threatening it.</summary>
        public const int TotemThreatRange = 2;

        /// <summary>Your units enemies can see: everyone except vanished rogues.</summary>
        private static IEnumerable<Unit> Visible(Battle battle) => battle.Living(Team.Player).Where(u => !u.IsHidden);

        /// <summary>The nearest visible unit taunting this enemy, or null.</summary>
        private static Unit Taunter(Battle battle, Unit enemy) =>
            Visible(battle)
                .Where(u => u.IsTaunting && Battle.Distance(enemy, u) <= Battle.TauntRange)
                .OrderBy(u => Battle.Distance(enemy, u))
                .ThenBy(u => u.Id)
                .FirstOrDefault();

        /// <summary>The units this enemy may aim an attack or ability at: only its taunter if it has one.</summary>
        private static IEnumerable<Unit> Aimable(Battle battle, Unit enemy)
        {
            var taunter = Taunter(battle, enemy);
            return taunter != null ? new[] { taunter } : Visible(battle);
        }

        /// <summary>
        /// The player unit this enemy wants to attack, among those it can see: a taunter in range first; in a
        /// ritual fight a defender next to it, else the objective; in Break the Ritual the nearest unit
        /// threatening a totem; for Blood Scent the weakest it can reach; otherwise nearest in steps, then
        /// lowest HP, then lowest id.
        /// </summary>
        public static Unit ChooseTarget(Battle battle, Unit enemy)
        {
            // A taunting unit within range overrides everything.
            var taunter = Taunter(battle, enemy);
            if (taunter != null)
                return taunter;

            // Ritual fights: go for the objective, unless a defender is already next to this enemy.
            var objective = battle.Objective;
            if (objective != null && objective.IsAlive)
            {
                var blocker = Visible(battle)
                    .Where(u => u != objective && Battle.Distance(enemy, u) == 1)
                    .OrderBy(HasShield)
                    .ThenBy(u => u.Hp)
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
                    .OrderBy(HasShield)
                    .ThenBy(u => Battle.Distance(enemy, u))
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
                    .OrderBy(HasShield)
                    .ThenBy(u => u.Hp)
                    .ThenBy(u => Battle.Distance(enemy, u))
                    .ThenBy(u => u.Id)
                    .FirstOrDefault();
                if (prey != null)
                    return prey;
            }

            var nearest = Visible(battle)
                .OrderBy(u => Battle.Distance(enemy, u))
                .ThenBy(u => u.Hp)
                .ThenBy(u => u.Id)
                .ToList();
            // A shield would soak the hit: if the nearest has one, go for an unshielded unit it can reach this turn.
            if (nearest.Count > 0 && HasShield(nearest[0]))
                return nearest.FirstOrDefault(u => !HasShield(u) && !u.IsObjective && CanReachToAttack(battle, enemy, u)) ?? nearest[0];
            return nearest.FirstOrDefault();
        }

        private static bool HasShield(Unit unit) => unit.Shield > 0 || unit.MoonShield > 0;

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
            if (!leader.Has(Ability.Howl) || !battle.Ready(leader, ActionKind.Howl) || (battle.Phase == Phase.EnemyTurn && leader.Ap < Battle.AttackCost))
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
            battle.RaisablePiles(raiser)
                .Where(r => battle.CanRaise(raiser, r))
                .OrderBy(r => Battle.Distance(raiser, r))
                .ThenBy(r => r.Id)
                .FirstOrDefault();

        /// <summary>
        /// The bone pile a necromancer would blow up right now, if any: one whose 3x3 holds more of your units than of
        /// its own side (itself included), at least one. Most of yours first, then nearest.
        /// </summary>
        public static Unit ChooseCorpseBlast(Battle battle, Unit necro)
        {
            int Score(Unit pile)
            {
                var area = battle.BlastArea(pile.Pos);
                var hit = battle.Units.Where(u => u.IsPlaced && u.IsAlive && Battle.Cells(u).Any(area.Contains)).ToList();
                var yours = hit.Count(u => u.Team != necro.Team && !u.IsObjective);
                return yours == 0 ? 0 : yours - hit.Count(u => u.Team == necro.Team);
            }

            return battle.Units
                .Where(p => battle.CanCorpseBlast(necro, p))
                .Select(p => (pile: p, score: Score(p)))
                .Where(s => s.score > 0)
                .OrderByDescending(s => s.score)
                .ThenBy(s => Battle.Distance(necro, s.pile))
                .ThenBy(s => s.pile.Id)
                .Select(s => s.pile)
                .FirstOrDefault();
        }

        /// <summary>What this enemy would do first if its turn started now. Used to show intent on hover.</summary>
        public static Intent PredictIntent(Battle battle, Unit enemy)
        {
            if (enemy.IsObjective)
                return new Intent(IntentKind.None, null);

            if (enemy.CarryingLoot > 0)
                return new Intent(IntentKind.Flee, enemy);

            if (enemy.Has(Ability.CorpseBlast) && AsIfItsTurn(battle, enemy, 0, () => ChooseCorpseBlast(battle, enemy)) is Unit blast)
                return new Intent(IntentKind.CorpseBlast, blast, blast.Pos);

            if (enemy.Stats.CanRaise && battle.Ready(enemy, ActionKind.Raise))
            {
                // Intent is shown during the player's turn, so assume a full enemy AP pool.
                var remains = battle.RaisablePiles(enemy)
                    .Where(r => Battle.Distance(enemy, r) <= enemy.Stats.RaiseRange && battle.UnitAt(r.Pos) == null)
                    .OrderBy(r => Battle.Distance(enemy, r))
                    .ThenBy(r => r.Id)
                    .FirstOrDefault();
                if (remains != null)
                    return new Intent(IntentKind.Raise, remains);
            }

            var edge = AsIfItsTurn(battle, enemy, 0, () => EdgeKill(battle, enemy));
            if (edge.HasValue)
                return new Intent(edge.Value.kind, edge.Value.target);

            var target = ChooseTarget(battle, enemy);
            if (target == null)
                return new Intent(IntentKind.None, null);

            if (ShouldHowl(battle, enemy))
                return new Intent(IntentKind.Howl, enemy);

            // Demons.
            if (BehaviourOf(enemy.Stats) == EnemyBehaviour.Imp && AsIfItsTurn(battle, enemy, 0, () => ChooseHellswap(battle, enemy)) is var swap && swap.HasValue)
                return new Intent(IntentKind.Hellswap, swap.Value.target, swap.Value.from);
            if (BehaviourOf(enemy.Stats) == EnemyBehaviour.Devil && AsIfItsTurn(battle, enemy, 0, () => ChooseContract(battle, enemy)) is Unit signee)
                return new Intent(IntentKind.SoulContract, signee);
            if (enemy.Has(Ability.FireBreath) && AsIfItsTurn(battle, enemy, 0, () => ChooseBreath(battle, enemy)) is var breath && breath.HasValue)
                return new Intent(IntentKind.FireBreath, breath.Value.target, breath.Value.from);
            if (enemy.Has(Ability.RendingBlow) && battle.Ready(enemy, ActionKind.RendingBlow) && enemy.Hp > Battle.RendingBlowCost &&
                CanReachToAttack(battle, enemy, target))
                return new Intent(IntentKind.RendingBlow, target);

            if (BehaviourOf(enemy.Stats) == EnemyBehaviour.Shooter && !CanReachToAttack(battle, enemy, target) &&
                battle.Ready(enemy, ActionKind.Overwatch))
                return new Intent(IntentKind.Overwatch, target);

            if (enemy.Has(Ability.BullRush) && battle.Ready(enemy, ActionKind.BullRush) && CanReachToAttack(battle, enemy, target))
                return new Intent(IntentKind.BullRush, target);

            if (WantsToGrasp(battle, enemy, target) && CanReachToAttack(battle, enemy, target))
                return new Intent(IntentKind.Grasp, target);

            // Upheaval: it can't reach the target to strike, but can walk to within Upheaval range of it.
            if (enemy.Has(Ability.Upheaval) && battle.Ready(enemy, ActionKind.Upheaval) && !CanReachToAttack(battle, enemy, target) &&
                target.Size == 1 && !target.IsObjective && Battle.Distance(enemy, target) <= Battle.UpheavalRange + enemy.MaxMoves)
                return new Intent(IntentKind.Upheaval, target);

            if (BehaviourOf(enemy.Stats) == EnemyBehaviour.Goblin)
            {
                var fling = AsIfItsTurn(battle, enemy, 0, () => ChooseFling(battle, enemy, target));
                if (fling.HasValue && (fling.Value.goblin == null || !CanReachToAttack(battle, enemy, target)))
                    return new Intent(IntentKind.Fling, fling.Value.target, fling.Value.to, fling.Value.goblin);
            }

            if (enemy.Stats.CanBomb)
            {
                var steps = Battle.Distance(enemy, target);
                if (battle.Ready(enemy, ActionKind.Bomb) && steps >= 2 && steps <= enemy.Stats.BombRange && battle.BombAt(target.Pos) == null)
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
                if (action == IntentKind.None && enemy.Stats.CanThrow && battle.Ready(enemy, ActionKind.Boulder) && steps >= 2 && steps <= enemy.Stats.ThrowRange &&
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
            // Storm: first step off a marked tile, towards a spot to attack its target from if it can.
            if (battle.OnStormMark(enemy))
                StepOffStorm(battle, enemy, events);

            // Edge of the World: a push that throws one of your units off the tower comes before anything else.
            var edge = EdgeKill(battle, enemy);
            if (edge.HasValue)
                events.AddRange(edge.Value.kind == IntentKind.BullRush
                    ? battle.DoBullRush(enemy, edge.Value.target)
                    : battle.DoUpheaval(enemy, edge.Value.target));
            if (battle.IsOver || !enemy.IsAlive)
                return;

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
                case EnemyBehaviour.Shooter:
                    ShooterTurn(battle, enemy, events);
                    break;
                case EnemyBehaviour.Goblin:
                    GoblinTurn(battle, enemy, events);
                    break;
                case EnemyBehaviour.Elemental:
                    ElementalTurn(battle, enemy, events);
                    break;
                case EnemyBehaviour.Imp:
                    ImpTurn(battle, enemy, events);
                    break;
                case EnemyBehaviour.Devil:
                    DevilTurn(battle, enemy, events);
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

            BreakBarricade(battle, enemy, events);
        }

        /// <summary>
        /// An enemy that spent no AP this turn (nobody to hit) attacks a barricade or rubble in its reach instead, the
        /// weakest first, so walls don't hold it off forever.
        /// </summary>
        private static void BreakBarricade(Battle battle, Unit enemy, List<BattleEvent> events)
        {
            if (battle.IsOver || !enemy.IsAlive || enemy.ActedThisTurn)
                return;
            var tile = battle.Barricades.Concat(battle.Rubble).Where(t => battle.CanAttackBarricade(enemy, t))
                .OrderBy(t => battle.BarricadeHpAt(t)).ThenBy(t => t.X).ThenBy(t => t.Y)
                .Cast<GridPos?>().FirstOrDefault();
            if (tile.HasValue)
                events.AddRange(battle.DoAttackBarricade(enemy, tile.Value));
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

                if (battle.CanBullRush(enemy, target))
                {
                    events.AddRange(battle.DoBullRush(enemy, target));
                    continue;
                }

                if (WantsToGrasp(battle, enemy, target) && battle.CanGrasp(enemy, target))
                {
                    events.AddRange(battle.DoGrasp(enemy, target));
                    continue;
                }

                if (battle.CanRendingBlow(enemy, target))
                {
                    events.AddRange(battle.DoRendingBlow(enemy, target));
                    continue;
                }

                // Fire Breath beats a strike (same damage, plus a second unit and the burning), from here or a tile it can reach.
                if (enemy.Has(Ability.FireBreath) && ChooseBreath(battle, enemy) is var breath && breath.HasValue)
                {
                    if (breath.Value.from != enemy.Pos && battle.DoMove(enemy, breath.Value.from) is var walk && walk != null)
                        events.AddRange(walk);
                    if (battle.CanFireBreath(enemy, breath.Value.target))
                    {
                        events.AddRange(battle.DoFireBreath(enemy, breath.Value.target));
                        continue;
                    }
                }

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
        /// A skeleton grasps rather than strikes a unit that would get away from it: a ranged one (it wants to back off
        /// and shoot) or a Swift one. Only when Grasp is ready and the target isn't already held.
        /// </summary>
        public static bool WantsToGrasp(Battle battle, Unit enemy, Unit target) =>
            enemy.Has(Ability.Grasp) && battle.Ready(enemy, ActionKind.Grasp) && target != null && !target.IsObjective &&
            target.SnareTurns == 0 && (target.IsRanged || target.MaxMoves > Battle.BaseMoves);

        /// <summary>
        /// Strike its target if it can (walking up first if that reaches). If it can't reach anyone to strike this turn,
        /// walk as close as it can, then raise a rock under its target, or under the nearest of your units in range.
        /// </summary>
        private static void ElementalTurn(Battle battle, Unit elemental, List<BattleEvent> events)
        {
            while (!battle.IsOver && elemental.IsAlive)
            {
                var target = ChooseTarget(battle, elemental);
                if (target == null)
                    return;

                if (battle.CanAttack(elemental, target))
                {
                    events.AddRange(battle.DoAttack(elemental, target));
                    continue;
                }

                if (battle.FindAttackSpot(elemental, target) == null)
                {
                    if (elemental.Moves > 0 && Approach(battle, elemental, target, Battle.AttackCost, events))
                        continue;
                    var victim = new[] { target }.Concat(Aimable(battle, elemental).OrderBy(u => Battle.Distance(elemental, u)).ThenBy(u => u.Id))
                        .FirstOrDefault(u => battle.CanUpheave(elemental, u));
                    if (victim == null)
                        return;
                    events.AddRange(battle.DoUpheaval(elemental, victim));
                    continue;
                }

                if (!Approach(battle, elemental, target, Battle.AttackCost, events))
                    return;
            }
        }

        // ---------- Demons ----------

        /// <summary>Devils keep at least this many tiles from your units, like the Necromancer.</summary>
        public const int DevilComfortDistance = 2;

        /// <summary>
        /// Runs a check as if the unit stood on another tile, then puts it back. The board is tiny and nothing else
        /// changes, so this is the cheapest way to ask "from there, could it...?".
        /// </summary>
        private static T AsIfStandingAt<T>(Unit unit, GridPos tile, System.Func<T> check)
        {
            var original = unit.Pos;
            unit.Pos = tile;
            try
            {
                return check();
            }
            finally
            {
                unit.Pos = original;
            }
        }

        /// <summary>Its own tile (cost 0) and every tile it can still walk to this turn.</summary>
        private static List<(GridPos tile, int cost)> StandingSpots(Battle battle, Unit unit)
        {
            var spots = new List<(GridPos, int)> { (unit.Pos, 0) };
            spots.AddRange(battle.GetReachable(unit).OrderBy(kv => kv.Value).ThenBy(kv => kv.Key.Y).ThenBy(kv => kv.Key.X).Select(kv => (kv.Key, kv.Value)));
            return spots;
        }

        /// <summary>
        /// The Hellswap an imp would make now: from its tile or one it can walk to that is next to another fighting ally,
        /// swap with one of your units that isn't already in an enemy's reach, so your unit lands next to that ally. Ranged
        /// units first (they least want to be in melee), then the tile next to the most allies, then the weakest unit,
        /// then the shortest walk. Null if none.
        /// </summary>
        public static (Unit target, GridPos from)? ChooseHellswap(Battle battle, Unit imp)
        {
            if (!imp.Has(Ability.Hellswap) || imp.Ap < Battle.AttackCost || !battle.Ready(imp, ActionKind.Hellswap) || imp.Size != 1)
                return null;
            (Unit target, GridPos from)? best = null;
            (int ranged, int allies, int hp, int cost, int id) bestScore = default;
            foreach (var (tile, cost) in StandingSpots(battle, imp))
            {
                var allies = battle.Living(imp.Team).Count(a => a != imp && !a.IsObjective && a.Stats.Damage > 0 && Battle.Distance(tile, 1, a) == 1);
                if (allies == 0)
                    continue;
                foreach (var u in AsIfStandingAt(imp, tile, () => Aimable(battle, imp)
                             .Where(u => battle.CanHellswap(imp, u) && !battle.EngagedBy(u, u.Pos).Any()).ToList()))
                {
                    var score = (u.IsRanged ? 0 : 1, -allies, u.Hp, cost, u.Id);
                    if (best == null || score.CompareTo(bestScore) < 0)
                    {
                        best = (u, tile);
                        bestScore = score;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// The unit a devil would sign a Soul Contract on now: one the contract would kill first, then the hardest hitter,
        /// then the weakest. Null if none in range.
        /// </summary>
        public static Unit ChooseContract(Battle battle, Unit devil) =>
            Aimable(battle, devil)
                .Where(u => battle.CanSignContract(devil, u))
                .OrderBy(u => u.Hp <= Battle.ContractDamage ? 0 : 1)
                .ThenByDescending(u => battle.EffectiveDamage(u))
                .ThenBy(u => u.Hp)
                .ThenBy(u => u.Id)
                .FirstOrDefault();

        /// <summary>
        /// The Fire Breath a fiend would use now, from its tile or one it can walk to: the one that hits the most of your
        /// units, then the shortest walk. Null if it can't breathe at anyone this turn.
        /// </summary>
        public static (Unit target, GridPos from)? ChooseBreath(Battle battle, Unit fiend)
        {
            if (!fiend.Has(Ability.FireBreath) || fiend.Ap < Battle.AttackCost || !battle.Ready(fiend, ActionKind.FireBreath))
                return null;
            (Unit target, GridPos from)? best = null;
            (int hits, int cost, int id) bestScore = default;
            foreach (var (tile, cost) in StandingSpots(battle, fiend))
            {
                foreach (var (u, hits) in AsIfStandingAt(fiend, tile, () => Aimable(battle, fiend)
                             .Where(u => battle.CanFireBreath(fiend, u))
                             .Select(u => (u, battle.FireBreathVictims(fiend, u).Count(v => !v.IsObjective))).ToList()))
                {
                    var score = (-hits, cost, u.Id);
                    if (hits > 0 && (best == null || score.CompareTo(bestScore) < 0))
                    {
                        best = (u, tile);
                        bestScore = score;
                    }
                }
            }

            return best;
        }

        /// <summary>Hellswap first if it has a good one (walking to the tile it swaps from), then like Walk up and hit.</summary>
        private static void ImpTurn(Battle battle, Unit imp, List<BattleEvent> events)
        {
            if (ChooseHellswap(battle, imp) is var swap && swap.HasValue)
            {
                if (swap.Value.from != imp.Pos && battle.DoMove(imp, swap.Value.from) is var walk && walk != null)
                    events.AddRange(walk);
                if (!battle.IsOver && imp.IsAlive && battle.CanHellswap(imp, swap.Value.target))
                    events.AddRange(battle.DoHellswap(imp, swap.Value.target));
            }

            MeleeTurn(battle, imp, events);
        }

        /// <summary>
        /// Back away from anyone next to it, then sign a Soul Contract if one is ready, else strike with the trident,
        /// else walk up to where it can strike or sign one (never closer than its comfort distance).
        /// </summary>
        private static void DevilTurn(Battle battle, Unit devil, List<BattleEvent> events)
        {
            var retreated = false;
            var advanced = false;
            while (!battle.IsOver && devil.IsAlive)
            {
                var target = ChooseTarget(battle, devil);
                if (target != null && !retreated && Battle.Distance(devil, target) < DevilComfortDistance)
                {
                    retreated = true;
                    if (Retreat(battle, devil, events))
                        continue;
                }

                var signee = ChooseContract(battle, devil);
                if (signee != null)
                {
                    events.AddRange(battle.DoSoulContract(devil, signee));
                    continue;
                }

                if (target == null)
                    return;

                if (battle.CanAttack(devil, target))
                {
                    events.AddRange(battle.DoAttack(devil, target));
                    continue;
                }

                if (!advanced)
                {
                    advanced = true;
                    var contractReady = devil.Has(Ability.SoulContract) && battle.Ready(devil, ActionKind.SoulContract);
                    if (AdvanceToRange(battle, devil, target, DevilComfortDistance, Battle.AttackCost,
                            tile => battle.CanHitFrom(devil, tile, target) ||
                                    (contractReady && Aimable(battle, devil).Any(u => !u.IsObjective && u.ContractBy == null &&
                                                                                    Battle.Distance(tile, devil.Size, u) <= Battle.ContractRange)),
                            events))
                        continue;
                }

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
            foreach (var u in new[] { target }.Concat(Aimable(battle, thrower).OrderBy(u => Battle.Distance(thrower, u)).ThenBy(u => u.Id)))
                if (battle.FlingOption(thrower, u) is var option && option.HasValue)
                    return (u, option.Value.goblin, option.Value.to);
            return null;
        }

        /// <summary>Where a cutpurse drops caltrops: the free tile next to it nearest your units, else its own tile.</summary>
        private static GridPos? CaltropsTile(Battle battle, Unit thief)
        {
            var nearest = Visible(battle).OrderBy(u => Battle.Distance(thief, u)).FirstOrDefault();
            return thief.Pos.Neighbours().Concat(new[] { thief.Pos })
                .Where(p => battle.CanDropCaltrops(thief, p))
                .OrderBy(p => nearest == null ? 0 : Battle.Distance(p, 1, nearest))
                .Select(p => (GridPos?)p)
                .FirstOrDefault();
        }

        /// <summary>
        /// Shoot its target if it can from where it stands. Otherwise walk to the cheapest tile with a shot,
        /// preferring one next to a rock (Behind Cover), and shoot. With no shot this turn: walk closer, ending
        /// next to a rock if it can, then go on overwatch.
        /// </summary>
        private static void ShooterTurn(Battle battle, Unit shooter, List<BattleEvent> events)
        {
            while (!battle.IsOver && shooter.IsAlive)
            {
                var target = ChooseTarget(battle, shooter);
                if (target == null)
                    return;

                if (battle.CanAttack(shooter, target))
                {
                    events.AddRange(battle.DoAttack(shooter, target));
                    continue;
                }

                bool Covered(GridPos p) => p.Neighbours().Any(battle.Board.IsRock);
                var reachable = battle.GetReachable(shooter);
                if (shooter.Ap >= Battle.AttackCost)
                {
                    var spot = reachable.Where(kv => battle.CanHitFrom(shooter, kv.Key, target))
                        .OrderBy(kv => Covered(kv.Key) ? 0 : 1).ThenBy(kv => kv.Value)
                        .Select(kv => (GridPos?)kv.Key).FirstOrDefault();
                    if (spot.HasValue && battle.DoMove(shooter, spot.Value) is var walk && walk != null)
                    {
                        events.AddRange(walk);
                        continue;
                    }
                }

                // No shot this turn: one step closer (into cover if it can), then watch the approach.
                if (reachable.Count > 0)
                {
                    var distance = Battle.Distance(shooter, target);
                    var step = reachable.Where(kv => Battle.Distance(kv.Key, shooter.Size, target) < distance)
                        .OrderBy(kv => Covered(kv.Key) ? 0 : 1).ThenBy(kv => Battle.Distance(kv.Key, shooter.Size, target))
                        .Select(kv => (GridPos?)kv.Key).FirstOrDefault();
                    if (step.HasValue && battle.DoMove(shooter, step.Value) is var move && move != null)
                    {
                        events.AddRange(move);
                        continue;
                    }
                }

                if (shooter.Has(Ability.Overwatch) && shooter.Ap >= Battle.AttackCost && battle.Ready(shooter, ActionKind.Overwatch))
                    events.AddRange(battle.DoOverwatch(shooter));
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

                    // Furthest row reachable, cheapest first. Once it can't get any further back this turn: if it
                    // ran, it drops caltrops behind it; if it's cornered, it hits whoever it can.
                    var best = battle.GetReachable(thief)
                        .OrderByDescending(kv => kv.Key.Y)
                        .ThenBy(kv => kv.Value)
                        .FirstOrDefault();
                    if (best.Key.Y <= thief.Pos.Y)
                    {
                        var cornered = ChooseTarget(battle, thief);
                        var trap = CaltropsTile(battle, thief);
                        if (thief.MovedThisTurn && trap.HasValue)
                            events.AddRange(battle.DoCaltrops(thief, trap.Value));
                        else if (cornered != null && battle.CanAttack(thief, cornered))
                            events.AddRange(battle.DoAttack(thief, cornered));
                        else if (trap.HasValue)
                            events.AddRange(battle.DoCaltrops(thief, trap.Value));
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
            var slam = ogre.Stats.Slams && battle.Ready(ogre, ActionKind.Slam) && near >= 2 ? near * Battle.SlamDamage : 0;
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

                var blast = ChooseCorpseBlast(battle, necro);
                if (blast != null)
                {
                    events.AddRange(battle.DoCorpseBlast(necro, blast));
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
                        : Aimable(battle, bomber).Where(u => battle.CanBomb(bomber, u)).OrderBy(u => Battle.Distance(bomber, u)).FirstOrDefault();
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

        // ---------- Battlefields ----------

        /// <summary>
        /// Edge of the World: one of your units this enemy can push off the board right now, with Bull Rush (next to it)
        /// or Upheaval. Null if there is none, or on any other battlefield.
        /// </summary>
        public static (IntentKind kind, Unit target)? EdgeKill(Battle battle, Unit enemy)
        {
            if (!battle.EdgeKills)
                return null;
            foreach (var u in Aimable(battle, enemy).Where(u => !u.IsObjective).OrderBy(u => u.Id))
            {
                var dir = Battle.StraightDirection(enemy.Pos, u.Pos);
                if (dir.HasValue && battle.CanBullRush(enemy, u) && battle.WouldFall(u, dir.Value, enemy))
                    return (IntentKind.BullRush, u);
                if (battle.CanUpheave(enemy, u) && battle.WouldFall(u, Battle.UpheavalDirection(enemy, u), enemy))
                    return (IntentKind.Upheaval, u);
            }

            return null;
        }

        /// <summary>Storm: moves off the marked tile, to an attack spot for its target if one is in reach, else closer, else anywhere.</summary>
        private static void StepOffStorm(Battle battle, Unit enemy, List<BattleEvent> events)
        {
            var target = ChooseTarget(battle, enemy);
            var destination = target != null ? PickApproach(battle, enemy, target, Battle.AttackCost) : null;
            if (destination == null)
            {
                var reachable = battle.GetReachable(enemy);
                if (reachable.Count == 0)
                    return;
                destination = reachable
                    .OrderBy(kv => target != null ? Battle.Distance(kv.Key, enemy.Size, target) : 0)
                    .ThenBy(kv => kv.Value)
                    .First().Key;
            }

            var move = battle.DoMove(enemy, destination.Value);
            if (move != null)
                events.AddRange(move);
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
