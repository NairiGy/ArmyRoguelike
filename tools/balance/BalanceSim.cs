// Not part of the Unity project: compiled by tools/dev.sh sim.
// Plays battles on Core with crude bots and writes a balance report. Bots only move and basic-attack: they don't
// use actions, so results are a floor for the army's strength and a comparison between fights, not real win rates.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ArmyRoguelike.Core;

public static class BalanceSim
{
    private enum Bot { Random, Greedy, Smart, Smart2 }

    private sealed class Result
    {
        public int Outcome;
        public int Turns;
        public int Survivors;
        public string Stuck;
    }

    private sealed class Tally
    {
        public int Games, Wins, Timeouts, Turns;

        public void Add(Result r)
        {
            Games++;
            Turns += r.Turns;
            if (r.Outcome > 0) Wins++;
            if (r.Outcome == 0) Timeouts++;
        }

        public string WinRate => Games == 0 ? "-" : $"{100.0 * Wins / Games:F0}%";
        public string AvgTurns => Games == 0 ? "-" : $"{(double)Turns / Games:F1}";
    }

    private const int TurnCap = 40;

    private static readonly Encounter[] Fights =
    {
        Encounter.Wolves, Encounter.Goblins, Encounter.Bandits, Encounter.Elementals, Encounter.Demons,
        Encounter.EliteWolves, Encounter.EliteGoblins, Encounter.EliteBandits, Encounter.EliteElementals, Encounter.EliteDemons,
        Encounter.Giant, Encounter.Necromancer, Encounter.MoonShrine, Encounter.BreakRitual,
    };

    private static List<UnitStats[]> AllDrafts()
    {
        var bases = UnitStats.BaseUnits.ToArray();
        var drafts = new List<UnitStats[]>();
        for (var i = 0; i < bases.Length; i++)
            for (var j = i + 1; j < bases.Length; j++)
                for (var k = j + 1; k < bases.Length; k++)
                    drafts.Add(new[] { bases[i], bases[j], bases[k] });
        return drafts;
    }

    private static string DraftName(UnitStats[] draft) => string.Join("/", draft.Select(s => s.Label));

    private static Result Play(UnitStats[] draft, Encounter fight, int seed, Bot bot, int level, Battlefield? field)
    {
        var rng = new Random(seed);
        var army = draft.Select(s => new ArmyMember(s)).ToList();
        for (var l = 0; l < level; l++)
            foreach (var member in army)
            {
                var options = Technique.LearnableBy(member).ToList();
                if (options.Count > 0)
                    member.Learn(options[rng.Next(options.Count)]);
            }

        // As if every faction was beaten: the Necromancer raises one of each, as it does late in a real run.
        var battle = fight.CreateBattle(army, new Random(seed + 1), Encounter.Regular);
        if (field.HasValue)
            battle.SetField(field);
        var botRng = new Random(seed + 2);
        if (bot == Bot.Smart || bot == Bot.Smart2)
            SmartBot.Deploy(battle, botRng);
        else
            Deploy(battle);
        if (!battle.StartBattle())
            throw new InvalidOperationException("couldn't start " + fight.Name);

        while (!battle.IsOver && battle.TurnNumber <= TurnCap)
        {
            if (bot == Bot.Smart || bot == Bot.Smart2)
            {
                SmartBot.TakeTurn(battle, bot == Bot.Smart ? 1 : 2);
                if (!battle.IsOver)
                    battle.EndPlayerTurn();
                continue;
            }

            foreach (var unit in battle.Living(Team.Player).Where(u => !u.IsObjective).ToList())
            {
                if (battle.IsOver)
                    break;
                if (!unit.IsAlive)
                    continue;
                if (bot == Bot.Random)
                    RandomTurn(battle, unit, botRng);
                else
                    GreedyTurn(battle, unit);
                if (!battle.IsOver && unit.IsAlive)
                    battle.EndUnitTurn(unit);
            }
            if (!battle.IsOver)
                battle.EndPlayerTurn();
        }

        var outcome = battle.Phase == Phase.Victory ? 1 : battle.Phase == Phase.Defeat ? -1 : 0;
        return new Result
        {
            Outcome = outcome,
            Turns = battle.TurnNumber,
            Survivors = battle.Units.Count(u => u.Team == Team.Player && !u.IsObjective && u.IsAlive),
            Stuck = outcome != 0 ? null : string.Join(" + ", battle.Living(Team.Enemy).Select(u => u.Stats.Name).OrderBy(n => n)) +
                " vs " + string.Join(" + ", battle.Living(Team.Player).Select(u => u.Stats.Name).OrderBy(n => n)),
        };
    }

    /// <summary>Longest reach at the back, the shortest as far forward as the zone allows, near the middle.</summary>
    private static void Deploy(Battle battle)
    {
        var tiles = battle.Board.AllTiles().OrderByDescending(p => p.Y).ThenBy(p => Math.Abs(p.X * 2 - 5)).ToList();
        foreach (var unit in battle.Units.Where(u => u.Team == Team.Player && !u.IsObjective).OrderBy(u => u.Stats.Range))
        {
            var spot = tiles.Where(p => battle.CanPlace(unit, p) && battle.UnitAt(p) == null).Cast<GridPos?>().FirstOrDefault();
            if (spot == null || !battle.Place(unit, spot.Value))
                throw new InvalidOperationException("no deployment tile");
        }
    }

    private static void RandomTurn(Battle battle, Unit unit, Random rng)
    {
        var moves = battle.GetReachable(unit).Keys.ToList();
        if (moves.Count > 0 && battle.CanAct(unit))
            battle.PlayerMove(unit, moves[rng.Next(moves.Count)]);
        if (battle.IsOver || !unit.IsAlive)
            return;
        var targets = battle.Living(Team.Enemy).Where(e => battle.CanAct(unit) && battle.CanAttack(unit, e)).ToList();
        if (targets.Count > 0)
            battle.PlayerAttack(unit, targets[rng.Next(targets.Count)]);
    }

    /// <summary>Takes a kill if it can, otherwise hits the weakest target in reach, otherwise walks closer.</summary>
    internal static void GreedyTurn(Battle battle, Unit unit)
    {
        var foes = battle.Living(Team.Enemy).Where(e => !e.IsHidden).ToList();
        if (foes.Count == 0)
            return;

        Unit best = null;
        GridPos bestTile = unit.Pos;
        var bestScore = int.MinValue;
        foreach (var foe in foes)
        {
            var spot = battle.FindAttackSpot(unit, foe);
            if (spot == null)
                continue;
            var kills = battle.PredictHpLoss(unit, foe, out _) >= foe.Hp;
            var score = (kills ? 100 : 0) - foe.Hp - spot.Value.cost + (foe.Stats.IsObjective ? 50 : 0);
            if (score > bestScore)
            {
                bestScore = score;
                best = foe;
                bestTile = spot.Value.tile;
            }
        }

        if (best != null)
        {
            if (bestTile != unit.Pos)
                battle.PlayerMove(unit, bestTile);
            if (!battle.IsOver && unit.IsAlive && best.IsAlive && battle.CanAttack(unit, best))
                battle.PlayerAttack(unit, best);
            return;
        }

        var moves = battle.GetReachable(unit).Keys.ToList();
        if (moves.Count == 0)
            return;
        var steps = WalkingSteps(battle, foes);
        int StepsFrom(GridPos p) => steps.TryGetValue(p, out var n) ? n : int.MaxValue;
        var nearest = moves.OrderBy(StepsFrom).ThenBy(p => p.X).First();
        if (StepsFrom(nearest) < StepsFrom(unit.Pos))
            battle.PlayerMove(unit, nearest);
    }

    /// <summary>Steps from each tile to the nearest foe, walking around rocks and ignoring other units.</summary>
    private static Dictionary<GridPos, int> WalkingSteps(Battle battle, IEnumerable<Unit> foes)
    {
        var steps = new Dictionary<GridPos, int>();
        var queue = new Queue<GridPos>();
        foreach (var cell in foes.SelectMany(Battle.Cells))
        {
            steps[cell] = 0;
            queue.Enqueue(cell);
        }
        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();
            foreach (var next in tile.Neighbours())
            {
                if (!battle.Board.InBounds(next) || battle.Board.IsObstacle(next) || steps.ContainsKey(next))
                    continue;
                steps[next] = steps[tile] + 1;
                queue.Enqueue(next);
            }
        }
        return steps;
    }

    private static Tally Sweep(IEnumerable<UnitStats[]> drafts, IEnumerable<Encounter> fights, int seeds, Bot bot, int level, Battlefield? field, Dictionary<string, int> stuck = null)
    {
        var jobs = (from draft in drafts from fight in fights from s in Enumerable.Range(0, seeds) select (draft, fight, s)).ToList();
        var results = new Result[jobs.Count];
        System.Threading.Tasks.Parallel.For(0, jobs.Count, i =>
            results[i] = Play(jobs[i].draft, jobs[i].fight, jobs[i].s * 13 + 5, bot, level, field));

        var tally = new Tally();
        for (var i = 0; i < jobs.Count; i++)
        {
            tally.Add(results[i]);
            if (results[i].Stuck != null && stuck != null)
            {
                var key = jobs[i].fight.Name + ": " + results[i].Stuck;
                stuck[key] = stuck.TryGetValue(key, out var n) ? n + 1 : 1;
            }
        }
        return tally;
    }

    /// <summary>The look-ahead bot: the numbers to tune against. Report with its strength check against a deeper search.</summary>
    private static void SmartReport(TextWriter o, int seeds)
    {
        var drafts = AllDrafts();
        var factions = Fights.Take(Encounter.Regular.Count).ToArray();
        var levels = new[] { 0, 1, 2, 3 };
        var stuck = new Dictionary<string, int>();

        o.WriteLine("# Balance report: smart bot");
        o.WriteLine();
        o.WriteLine($"The bot uses every action and sees the enemy's next turn, with random techniques and no items, banners or omens. {seeds} seeds per draft and fight.");
        o.WriteLine();

        o.WriteLine("## Fights: win rate (timeouts, average turns)");
        o.WriteLine();
        o.WriteLine("| Fight | " + string.Join(" | ", levels.Select(l => "L" + l)) + " |");
        o.WriteLine("|---|" + string.Concat(levels.Select(_ => "---|")));
        foreach (var fight in Fights)
        {
            o.WriteLine($"| {fight.Name} | " + string.Join(" | ", levels.Select(l =>
            {
                var t = Sweep(drafts, new[] { fight }, seeds, Bot.Smart, l, null, stuck);
                return $"{t.WinRate} ({t.Timeouts}, {t.AvgTurns})";
            })) + " |");
            o.Flush();
        }
        o.WriteLine();

        o.WriteLine("## Timeouts: who was left");
        o.WriteLine();
        foreach (var kv in stuck.OrderByDescending(kv => kv.Value).Take(12))
            o.WriteLine($"- {kv.Value}x {kv.Key}");
        if (stuck.Count == 0)
            o.WriteLine("- none");
        o.WriteLine();

        o.WriteLine("## Drafts: win rate over the regular factions");
        o.WriteLine();
        o.WriteLine("| Draft | L0 | L1 | L2 |");
        o.WriteLine("|---|---|---|---|");
        foreach (var draft in drafts)
        {
            o.WriteLine($"| {DraftName(draft)} | " + string.Join(" | ", new[] { 0, 1, 2 }.Select(l =>
                Sweep(new[] { draft }, factions, seeds, Bot.Smart, l, null).WinRate)) + " |");
            o.Flush();
        }
        o.WriteLine();

        var fields = new Battlefield?[] { null }.Concat(Enum.GetValues(typeof(Battlefield)).Cast<Battlefield>().Select(f => (Battlefield?)f)).ToArray();
        var fieldFights = factions.Concat(new[] { Encounter.Giant, Encounter.Necromancer }).ToArray();
        o.WriteLine("## Battlefields: win rate at L1");
        o.WriteLine();
        o.WriteLine("| Fight | " + string.Join(" | ", fields.Select(f => f?.ToString() ?? "none")) + " |");
        o.WriteLine("|---|" + string.Concat(fields.Select(_ => "---|")));
        foreach (var fight in fieldFights)
        {
            o.WriteLine($"| {fight.Name} | " + string.Join(" | ", fields.Select(f =>
                Sweep(drafts, new[] { fight }, seeds, Bot.Smart, 1, f).WinRate)) + " |");
            o.Flush();
        }
        o.WriteLine();

        o.WriteLine("## Strength check: one round of look-ahead against two, L0");
        o.WriteLine();
        o.WriteLine("| Fight | Greedy | Smart | Smart, two rounds |");
        o.WriteLine("|---|---|---|---|");
        foreach (var fight in factions)
        {
            var few = Math.Max(1, seeds / 2);
            o.WriteLine($"| {fight.Name} | " + string.Join(" | ", new[] { Bot.Greedy, Bot.Smart, Bot.Smart2 }.Select(b =>
                Sweep(drafts, new[] { fight }, few, b, 0, null).WinRate)) + " |");
            o.Flush();
        }
    }

    /// <summary>Hits needed for each attacker's plain attack to kill each target. Ignores passives and bonuses.</summary>
    private static void HitsToKill(TextWriter o)
    {
        var enemies = typeof(UnitStats).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(UnitStats)).Select(f => (UnitStats)f.GetValue(null))
            .Where(s => s.Damage > 0 && !UnitStats.BaseUnits.Contains(s) && s != UnitStats.Turret && !s.Name.StartsWith("Bone"))
            .OrderBy(s => s.MaxHp).ToList();

        o.WriteLine("## Your hits to kill each enemy (plain damage, by level)");
        o.WriteLine();
        o.WriteLine("| Unit, level (damage) | " + string.Join(" | ", enemies.Select(e => $"{e.Name} {e.MaxHp}")) + " |");
        o.WriteLine("|---|" + string.Concat(enemies.Select(_ => "---|")));
        foreach (var unit in UnitStats.BaseUnits)
        {
            var last = -1;
            for (var level = 0; level <= 6; level++)
            {
                var damage = unit.Damage + Growth.Damage(unit, level);
                if (damage == last)
                    continue;
                last = damage;
                o.WriteLine($"| {unit.Name} L{level} ({damage}) | " + string.Join(" | ", enemies.Select(e => Hits(e.MaxHp, damage).ToString())) + " |");
            }
        }

        o.WriteLine();
        o.WriteLine("## Enemy hits to kill your units (plain damage, by level 0 / 3 / 6 HP)");
        o.WriteLine();
        o.WriteLine("| Enemy (damage) | " + string.Join(" | ", UnitStats.BaseUnits.Select(u => $"{u.Name} {u.MaxHp}")) + " |");
        o.WriteLine("|---|" + string.Concat(UnitStats.BaseUnits.Select(_ => "---|")));
        foreach (var enemy in enemies.OrderBy(e => e.Damage))
            o.WriteLine($"| {enemy.Name} ({enemy.Damage}) | " + string.Join(" | ", UnitStats.BaseUnits.Select(u =>
                string.Join(" / ", new[] { 0, 3, 6 }.Select(l => Hits(u.MaxHp + Growth.Hp(u, l), enemy.Damage))))) + " |");
        o.WriteLine();
    }

    private static int Hits(int hp, int damage) => (hp + damage - 1) / damage;

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var outPath = args.Length > 0 ? args[0] : "balance-report.md";
        var seeds = args.Length > 1 ? int.Parse(args[1]) : 50;

        // Fill Core's Raised and Harvested caches now: they aren't safe to write to from several threads.
        foreach (var faction in Encounter.Regular)
            UnitStats.Raised(faction.RaisedRepresentative);
        foreach (var unit in UnitStats.BaseUnits)
            UnitStats.Harvested(unit);

        var clock = System.Diagnostics.Stopwatch.StartNew();
        if (args.Length > 2 && args[2] == "smart")
        {
            using (var report = new StreamWriter(outPath))
                SmartReport(report, seeds);
            Console.WriteLine($"wrote {outPath} in {clock.Elapsed.TotalSeconds:F0}s");
            return 0;
        }

        if (args.Length > 2 && args[2] == "runs")
        {
            using (var report = new StreamWriter(outPath))
                RunSim.Report(report, AllDrafts(), seeds);
            Console.WriteLine($"wrote {outPath} in {clock.Elapsed.TotalSeconds:F0}s");
            return 0;
        }

        if (args.Length > 2 && args[2] == "fights")
        {
            // Quick check of chosen fights, for trying a number change: tools/dev.sh sim 10 fights "Elementals,Giant"
            var chosen = args.Length > 3 ? Fights.Where(f => args[3].Split(',').Contains(f.Name)).ToArray() : Fights;
            foreach (var fight in chosen)
                Console.WriteLine($"{fight.Name,-22}" + string.Join("  ", new[] { 0, 1, 2, 3 }.Select(l =>
                {
                    var t = Sweep(AllDrafts(), new[] { fight }, seeds, Bot.Smart, l, null);
                    return $"L{l} {t.WinRate,4} ({t.Timeouts,2} to, {t.AvgTurns,4}t)";
                })));
            return 0;
        }

        var drafts = AllDrafts();
        var factions = Fights.Take(Encounter.Regular.Count).ToArray();
        using (var o = new StreamWriter(outPath))
        {
            o.WriteLine("# Balance report");
            o.WriteLine();
            o.WriteLine($"Bots only move and basic-attack, on random techniques (actions unused). {seeds} seeds per draft and fight. Compare fights with each other; these aren't real win rates.");
            o.WriteLine();
            HitsToKill(o);

            var setups = new[] { (Bot.Random, 0), (Bot.Greedy, 0), (Bot.Greedy, 2), (Bot.Greedy, 4) };
            var stuck = new Dictionary<string, int>();
            o.WriteLine("## Fights: win rate (timeouts at turn " + TurnCap + ", average turns)");
            o.WriteLine();
            o.WriteLine("| Fight | " + string.Join(" | ", setups.Select(s => $"{s.Item1} L{s.Item2}")) + " |");
            o.WriteLine("|---|" + string.Concat(setups.Select(_ => "---|")));
            foreach (var fight in Fights)
            {
                var cells = setups.Select(s =>
                {
                    var t = Sweep(drafts, new[] { fight }, seeds, s.Item1, s.Item2, null, s.Item1 == Bot.Greedy ? stuck : null);
                    return $"{t.WinRate} ({t.Timeouts}, {t.AvgTurns})";
                });
                o.WriteLine($"| {fight.Name} | " + string.Join(" | ", cells) + " |");
            }
            o.WriteLine();

            o.WriteLine("## Drafts: Greedy win rate over the regular factions");
            o.WriteLine();
            o.WriteLine("| Draft | L0 | L2 | L4 |");
            o.WriteLine("|---|---|---|---|");
            foreach (var draft in drafts)
                o.WriteLine($"| {DraftName(draft)} | " + string.Join(" | ", new[] { 0, 2, 4 }.Select(l =>
                    Sweep(new[] { draft }, factions, seeds, Bot.Greedy, l, null).WinRate)) + " |");
            o.WriteLine();

            var fields = new Battlefield?[] { null }.Concat(Enum.GetValues(typeof(Battlefield)).Cast<Battlefield>().Select(f => (Battlefield?)f)).ToArray();
            var fieldFights = factions.Concat(new[] { Encounter.Giant, Encounter.Necromancer }).ToArray();
            o.WriteLine("## Battlefields: Greedy win rate at L2");
            o.WriteLine();
            o.WriteLine("| Fight | " + string.Join(" | ", fields.Select(f => f?.ToString() ?? "none")) + " |");
            o.WriteLine("|---|" + string.Concat(fields.Select(_ => "---|")));
            foreach (var fight in fieldFights)
                o.WriteLine($"| {fight.Name} | " + string.Join(" | ", fields.Select(f =>
                    Sweep(drafts, new[] { fight }, seeds, Bot.Greedy, 2, f).WinRate)) + " |");
            o.WriteLine();

            o.WriteLine("## Timeouts: who was left (Greedy)");
            o.WriteLine();
            foreach (var kv in stuck.OrderByDescending(kv => kv.Value).Take(15))
                o.WriteLine($"- {kv.Value}x {kv.Key}");
            if (stuck.Count == 0)
                o.WriteLine("- none");
        }
        Console.WriteLine($"wrote {outPath} in {clock.Elapsed.TotalSeconds:F0}s");
        return 0;
    }
}
