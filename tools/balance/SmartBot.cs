// Not part of the Unity project: compiled by tools/dev.sh sim.
// A bot that uses every action and looks ahead. For each option it copies the battle, finishes the turn with the
// greedy bot, plays the enemy's real response and scores the board. It can see the enemy's next moves and dice, so it
// plans better than a player for one turn, but it doesn't plan further than Depth rounds.
using System;
using System.Collections.Generic;
using System.Linq;
using ArmyRoguelike.Core;

public static class SmartBot
{
    private const int DeployTries = 16;
    private const int PlayoutTurns = 15;
    private const int StepsPerTurn = 30;

    private sealed class Plan
    {
        public int UnitId;
        public GridPos? Move;
        public int? AttackId;
        public ActionKind? Action;
        public GridPos Tile;
        public bool Free;
    }

    private static Unit Same(Battle battle, int id) => battle.Units.First(u => u.Id == id);

    // ---------- Deployment ----------

    /// <summary>Tries the sensible layout and some random ones, each played out by the greedy bot, and places the best.</summary>
    public static void Deploy(Battle battle, Random rng)
    {
        var army = battle.Units.Where(u => u.Team == Team.Player && !u.IsObjective).ToList();
        var tiles = battle.Board.AllTiles().Where(p => army.Any(u => battle.CanPlace(u, p))).ToList();

        var layouts = new List<List<GridPos>>();
        var sensible = tiles.OrderByDescending(p => p.Y).ThenBy(p => Math.Abs(p.X * 2 - 5)).ToList();
        var byReach = army.OrderBy(u => u.Stats.Range).ToList();
        // Each unit takes the first tile in the order that it may stand on (Vanguard's extra row is the Warrior's only).
        List<GridPos> Assign(IEnumerable<Unit> order, List<GridPos> candidates)
        {
            var layout = army.Select(_ => default(GridPos)).ToList();
            var used = new HashSet<GridPos>();
            foreach (var u in order)
            {
                var tile = candidates.Where(p => !used.Contains(p) && battle.CanPlace(u, p)).Cast<GridPos?>().FirstOrDefault();
                if (!tile.HasValue)
                    return new List<GridPos>();
                used.Add(tile.Value);
                layout[army.IndexOf(u)] = tile.Value;
            }

            return layout;
        }

        layouts.Add(Assign(byReach, sensible));
        for (var i = 0; i < DeployTries; i++)
            layouts.Add(Assign(army.OrderBy(_ => rng.Next()), tiles.OrderBy(_ => rng.Next()).ToList()));

        List<GridPos> best = null;
        var bestScore = double.MinValue;
        foreach (var layout in layouts)
        {
            if (layout.Count < army.Count)
                continue;
            var copy = battle.Clone();
            var placed = true;
            for (var i = 0; i < army.Count; i++)
                placed &= copy.Place(Same(copy, army[i].Id), layout[i]);
            if (!placed || !copy.StartBattle())
                continue;
            for (var t = 0; t < PlayoutTurns && !copy.IsOver; t++)
                FinishRound(copy);
            var score = Score(copy);
            if (score > bestScore)
            {
                bestScore = score;
                best = layout;
            }
        }

        if (best == null)
            throw new InvalidOperationException("no deployment");
        for (var i = 0; i < army.Count; i++)
            battle.Place(army[i], best[i]);
    }

    // ---------- The turn ----------

    public static void TakeTurn(Battle battle, int depth)
    {
        for (var step = 0; step < StepsPerTurn && !battle.IsOver; step++)
        {
            var units = battle.ActiveUnit != null
                ? new List<Unit> { battle.ActiveUnit }
                : battle.Living(Team.Player).Where(u => !u.IsObjective && !u.IsDone).ToList();
            if (units.Count == 0)
                break;

            Plan best = null;
            var bestScore = double.MinValue;
            foreach (var unit in units)
                foreach (var (plan, score) in Options(battle, unit, depth))
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = plan;
                    }

            if (best == null)
                break;
            Apply(battle, best);
        }
    }

    /// <summary>Every move-then-act for this unit, scored. Free actions come first and leave the unit to act on.</summary>
    private static IEnumerable<(Plan, double)> Options(Battle battle, Unit unit, int depth)
    {
        var id = unit.Id;

        foreach (var action in unit.Actions.Where(a => Battle.CostOf(unit, a) == 0))
            foreach (var tile in battle.ActionTargets(unit, action))
                yield return Rate(battle, new Plan { UnitId = id, Action = action, Tile = tile, Free = true }, depth);

        var starts = new List<GridPos?> { null };
        starts.AddRange(battle.GetReachable(unit).Keys.Select(p => (GridPos?)p));
        foreach (var start in starts)
        {
            var moved = battle.Clone();
            var self = Same(moved, id);
            if (start.HasValue && moved.PlayerMove(self, start.Value) == null)
                continue;

            yield return Rate(battle, new Plan { UnitId = id, Move = start }, depth);
            if (moved.IsOver || !self.IsAlive)
                continue;

            foreach (var foe in moved.Living(Team.Enemy).Where(e => moved.CanAct(self) && moved.CanAttack(self, e)).Select(e => e.Id).ToList())
                yield return Rate(battle, new Plan { UnitId = id, Move = start, AttackId = foe }, depth);

            foreach (var action in self.Actions.Where(a => Battle.CostOf(self, a) > 0).ToList())
                foreach (var tile in Worthwhile(moved, self, action))
                    yield return Rate(battle, new Plan { UnitId = id, Move = start, Action = action, Tile = tile }, depth);
        }
    }

    /// <summary>The tiles an action can go on; area attacks only near an enemy, to keep the search small.</summary>
    private static IEnumerable<GridPos> Worthwhile(Battle battle, Unit unit, ActionKind action)
    {
        var tiles = battle.ActionTargets(unit, action);
        if (action != ActionKind.Fireball && action != ActionKind.ThrowBomb && action != ActionKind.Blizzard && action != ActionKind.Firewall && action != ActionKind.CaveIn)
            return tiles;
        var foes = battle.Living(Team.Enemy).ToList();
        return tiles.Where(t => foes.Any(f => Battle.Distance(t, 1, f) <= 1));
    }

    private static (Plan, double) Rate(Battle battle, Plan plan, int depth)
    {
        var copy = battle.Clone();
        if (!Apply(copy, plan))
            return (plan, double.MinValue);
        for (var round = 0; round < depth && !copy.IsOver; round++)
            FinishRound(copy);
        return (plan, Score(copy));
    }

    private static bool Apply(Battle battle, Plan plan)
    {
        var unit = Same(battle, plan.UnitId);
        if (plan.Move.HasValue && battle.PlayerMove(unit, plan.Move.Value) == null)
            return false;
        if (battle.IsOver || !unit.IsAlive)
            return true;

        var acted = false;
        if (plan.AttackId.HasValue)
        {
            if (battle.PlayerAttack(unit, Same(battle, plan.AttackId.Value)) == null)
                return false;
            acted = true;
        }
        else if (plan.Action.HasValue)
        {
            if (battle.PlayerUseAction(unit, plan.Action.Value, plan.Tile) == null)
                return false;
            acted = true;
        }

        // A free action, or a kill that gave the AP back, leaves the unit able to go on.
        if (battle.IsOver || !unit.IsAlive)
            return true;
        // Twincast: an attack first still leaves a free spell, so the unit isn't done yet.
        var twincastLeft = unit.Has(Ability.Twincast) && !unit.TwincastUsed && unit.Actions.Any(a => battle.ActionTargets(unit, a).Count > 0);
        if (!plan.Free && !(acted && (unit.Ap > 0 || twincastLeft)))
            battle.EndUnitTurn(unit);
        return true;
    }

    /// <summary>The greedy bot finishes the player's turn, then the enemy phase runs.</summary>
    private static void FinishRound(Battle battle)
    {
        var active = battle.ActiveUnit;
        if (active != null)
            FinishUnit(battle, active);
        foreach (var unit in battle.Living(Team.Player).Where(u => !u.IsObjective && !u.IsDone).ToList())
        {
            if (battle.IsOver)
                return;
            if (unit.IsAlive)
                FinishUnit(battle, unit);
        }
        if (!battle.IsOver)
            battle.EndPlayerTurn();
    }

    private static void FinishUnit(Battle battle, Unit unit)
    {
        BalanceSim.GreedyTurn(battle, unit);
        if (!battle.IsOver && unit.IsAlive)
            battle.EndUnitTurn(unit);
    }

    // ---------- Scoring ----------

    private static double Score(Battle battle)
    {
        if (battle.Phase == Phase.Victory)
            return 1000 + battle.LivingArmy.Sum(u => 50.0 + u.Hp);
        if (battle.Phase == Phase.Defeat)
            return battle.ScenarioLost ? -400 + battle.LivingArmy.Count() * 50 : -1000;

        // A player finishes a fight rather than kiting forever: enemy HP and distance weigh more as the turns pass.
        var urgency = 1 + battle.TurnNumber * 0.15;
        var score = 0.0;
        var foes = battle.Living(Team.Enemy).ToList();
        foreach (var unit in battle.Living(Team.Player))
        {
            if (unit.IsObjective)
                score += unit.Hp * 4;
            else if (unit.Stats.IsSummon)
                score += 3 + unit.Hp * 0.5;
            else
            {
                score += 20 + unit.Hp * 1.5;
                if (foes.Count > 0)
                    score -= 0.2 * urgency * foes.Min(f => Battle.Distance(unit, f));
            }
        }
        foreach (var foe in foes)
            score -= foe.IsObjective ? 15 + foe.Hp * 3 : 8 + foe.Stats.Damage * 3 + foe.Hp * 1.5 * urgency;
        return score;
    }
}
