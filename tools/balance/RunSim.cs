// Not part of the Unity project: compiled by tools/dev.sh sim.
// Plays whole runs through Core's Run with the smart bot in battles and a simple, partly random player between them:
// techniques and items are picked at random, so comparing runs that got one with runs that didn't is a fair test.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArmyRoguelike.Core;

public static class RunSim
{
    private const int TurnCap = 40;
    private const int TechniqueCheckStep = 6;

    /// <summary>How a run treats banners: null = none at all; Natural = takes them as offered; else only that one, from the start.</summary>
    private sealed class Mode
    {
        public string Name;
        public bool Natural;
        public Banner? Forced;
    }

    private sealed class FightLog
    {
        public string Name;
        public int Step;
        public bool Won;
        public bool TimedOut;
    }

    private sealed class RunLog
    {
        public string Draft;
        public string Boss;
        public bool Won;
        public int StepReached;
        public int GoldEarned;
        public int GoldLeft;
        public int Bought;
        public int LevelsAtEnd;
        public int ArmyAtEnd;
        public readonly List<FightLog> Fights = new List<FightLog>();
        public readonly int[] GoldAtStep = new int[Run.Steps];
        public readonly int[] LevelsAtStep = new int[Run.Steps];
        public HashSet<string> KnownAtCheck;
    }

    // ---------- Playing a run ----------

    private static RunLog Play(UnitStats[] draft, int seed, Mode mode)
    {
        var run = new Run(seed);
        var rng = new Random(seed ^ 0x3c6ef372);
        var log = new RunLog { Draft = string.Join("/", draft.Select(s => s.Label)), Boss = run.Map[Run.Steps - 1][0].Encounter.Name };

        if (mode.Forced.HasValue)
        {
            var offers = (List<Banner>)typeof(Run).GetField("_startBanners", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(run);
            offers.Clear();
            offers.Add(mode.Forced.Value);
            run.ChooseStartBanner(mode.Forced.Value);
        }
        else if (mode.Natural)
        {
            run.ChooseStartBanner(Banners.All.Where(b => run.StartBanners.Contains(b)).OrderBy(_ => rng.Next()).First());
        }
        if (!run.Draft(draft.Take(run.DraftCount).ToList()))
            throw new InvalidOperationException("draft failed");

        var goldBefore = 0;
        for (var guard = 0; guard < 500 && run.State != RunState.Won && run.State != RunState.Lost; guard++)
        {
            var step = Math.Min(run.StepIndex, Run.Steps - 1);
            log.GoldAtStep[step] = run.Gold;
            log.LevelsAtStep[step] = run.Army.Sum(m => m.Level);
            log.StepReached = run.StepIndex;
            if (log.KnownAtCheck == null && run.StepIndex >= TechniqueCheckStep)
                log.KnownAtCheck = new HashSet<string>(run.Army.SelectMany(m => m.Techniques.Select(t => t.Name))
                    .Concat(run.Army.Where(m => m.Equipment != null).Select(m => "Item: " + m.Equipment.Name)));

            switch (run.State)
            {
                case RunState.Map:
                    run.ChooseNode(ChooseNode(run));
                    break;
                case RunState.Battle:
                    goldBefore = run.Gold;
                    Fight(run, rng, log);
                    log.GoldEarned += Math.Max(0, run.Gold - goldBefore);
                    break;
                case RunState.Shop:
                    Shop(run, rng, log, mode);
                    break;
                case RunState.Event:
                    Event(run, rng, log);
                    break;
                case RunState.Reward:
                    Reward(run, rng, mode);
                    break;
                case RunState.Blessing:
                    run.ChooseBlessing(run.BlessingChoices[rng.Next(run.BlessingChoices.Count)]);
                    break;
                case RunState.Dismiss:
                    run.Dismiss(run.Army.OrderByDescending(m => m.Level).Take(run.ArmyLimit).ToList());
                    break;
            }
        }

        log.Won = run.State == RunState.Won;
        log.GoldLeft = run.Gold;
        log.LevelsAtEnd = run.Army.Sum(m => m.Level);
        log.ArmyAtEnd = run.Army.Count;
        return log;
    }

    /// <summary>A sensible route: new fights, Moon Shrine, elites and the ritual once it has a few levels; shops when it has gold.</summary>
    private static int ChooseNode(Run run)
    {
        var levels = run.Army.Average(m => (double)m.Level);
        double Score(MapNode node)
        {
            switch (node.Kind)
            {
                case NodeKind.Elite: return levels >= 1.5 ? 5 : 1;
                case NodeKind.Battle: return 3.5;
                case NodeKind.Ritual: return node.Encounter == Encounter.MoonShrine || levels >= 1 ? 4 : 2;
                case NodeKind.Shop: return run.Gold >= 15 ? 4 : 2;
                default: return 3;
            }
        }
        var choices = run.CurrentChoices.ToList();
        return choices.IndexOf(choices.OrderByDescending(Score).First());
    }

    private static void Fight(Run run, Random rng, RunLog log)
    {
        if (run.MercPending)
        {
            var offers = run.MercOffers.Where(run.CanHire).ToList();
            if (offers.Count > 0)
                run.HireMerc(offers[rng.Next(offers.Count)]);
            else
                run.DeclineMerc();
        }

        var encounter = run.CurrentEncounter;
        var battle = run.StartBattle();
        SmartBot.Deploy(battle, rng);
        if (!battle.StartBattle())
            throw new InvalidOperationException("couldn't start " + encounter.Name);
        while (!battle.IsOver && battle.TurnNumber <= TurnCap)
        {
            SmartBot.TakeTurn(battle, 1);
            if (!battle.IsOver)
                battle.EndPlayerTurn();
        }

        // A stand-off the bot can't finish: a player would, so it counts as won and is reported as a timeout.
        var timedOut = !battle.IsOver;
        if (timedOut)
            typeof(Battle).GetProperty("Phase").SetValue(battle, Phase.Victory);
        log.Fights.Add(new FightLog { Name = encounter.Name, Step = run.StepIndex, Won = battle.Phase == Phase.Victory, TimedOut = timedOut });
        run.FinishBattle();
    }

    private static ArmyMember Learner(Run run, Technique technique, Random rng) =>
        run.Army.Where(technique.CanLearn).OrderBy(m => m.Level).ThenBy(_ => rng.Next()).FirstOrDefault();

    private static void Reward(Run run, Random rng, Mode mode)
    {
        if (mode.Natural && run.RewardBanner.HasValue)
        {
            run.TakeRewardBanner();
            return;
        }
        var offers = run.RewardOffers.ToList();
        if (offers.Count == 0)
        {
            run.SkipReward();
            return;
        }
        var pick = offers[rng.Next(offers.Count)];
        var learner = Learner(run, pick, rng);
        if (learner == null || !run.TakeReward(pick, learner))
            run.SkipReward();
    }

    private static void Shop(Run run, Random rng, RunLog log, Mode mode)
    {
        // Mends a unit one crack from shattering first.
        foreach (var member in run.Army.Where(m => m.Cracks >= Run.CracksToShatter - 1).ToList())
            run.Mend(member);
        if (run.CanBuyRecruit && run.BuyRecruit())
            log.Bought++;
        if (mode.Natural && run.CanBuyBanner)
        {
            run.BuyBanner();
            log.Bought++;
            if (run.State != RunState.Shop)
                return;
        }

        foreach (var technique in run.Offers.OrderBy(_ => rng.Next()).ToList())
        {
            var learner = Learner(run, technique, rng);
            if (learner != null && run.Buy(technique, learner))
                log.Bought++;
        }
        foreach (var item in run.ItemOffers.OrderBy(_ => rng.Next()).ToList())
        {
            var wearer = run.Army.Where(m => m.Equipment == null && item.CanEquip(m.Stats)).OrderBy(_ => rng.Next()).FirstOrDefault();
            if (wearer != null && run.BuyItem(item, wearer))
                log.Bought++;
        }
        run.LeaveShop();
    }

    private static void Event(Run run, Random rng, RunLog log)
    {
        switch (run.CurrentEvent)
        {
            case EventKind.WanderingRecruit:
                run.AcceptRecruit();
                break;
            case EventKind.AbandonedCart:
                ArmyMember taker = null;
                if (run.CartTechnique != null)
                    taker = Learner(run, run.CartTechnique, rng);
                else if (run.CartItem != null)
                    taker = run.Army.Where(m => m.Equipment == null && run.CartItem.CanEquip(m.Stats)).OrderBy(_ => rng.Next()).FirstOrDefault();
                run.TakeCart(taker);
                break;
            case EventKind.Gambler:
                run.Gamble(run.CanGamble);
                break;
            case EventKind.Ambush:
                run.StartAmbush();
                break;
            case EventKind.Shrine:
                run.PrayAtShrine(run.Army[rng.Next(run.Army.Count)]);
                break;
            case EventKind.Mason:
                run.VisitMason(run.Army[rng.Next(run.Army.Count)]);
                break;
            case EventKind.Altar:
                // Takes it on a unit with no cracks yet, else leaves.
                run.TakeAltar(run.Army.Where(m => m.Cracks == 0 && run.CanTakeAltar(m)).OrderBy(_ => rng.Next()).FirstOrDefault());
                break;
            case EventKind.Sculptor:
                var sculpt = (from m in run.Army from t in m.Techniques where run.CanSculpt(m, t) && t.Rarity == TechniqueRarity.Common select (m, t))
                    .OrderBy(_ => rng.Next()).FirstOrDefault();
                run.Sculpt(sculpt.m, sculpt.t);
                break;
            case EventKind.Bargain:
                run.Bargain(run.CanBargain && run.Curses.Count == 0);
                break;
        }
    }

    // ---------- Report ----------

    private static List<RunLog> PlayAll(List<UnitStats[]> drafts, int seedsPerDraft, Mode mode, int firstSeed)
    {
        var jobs = (from draft in drafts from s in Enumerable.Range(0, seedsPerDraft) select (draft, s)).ToList();
        var logs = new RunLog[jobs.Count];
        System.Threading.Tasks.Parallel.For(0, jobs.Count, i => logs[i] = Play(jobs[i].draft, firstSeed + jobs[i].s * 101, mode));
        return logs.ToList();
    }

    private static string Pct(int part, int all) => all == 0 ? "-" : $"{100.0 * part / all:F0}%";

    public static void Report(TextWriter o, List<UnitStats[]> drafts, int seeds)
    {
        var plain = PlayAll(drafts, seeds * 5, new Mode { Name = "no banners" }, 1000);

        o.WriteLine("# Balance report: whole runs");
        o.WriteLine();
        o.WriteLine($"Smart bot in battles (sees the enemy's next turn). Between battles it picks techniques and items at random, on a sensible route. Stand-offs at turn {TurnCap} count as won. Compare these with each other; a real player plans further but sees less.");
        o.WriteLine();
        o.WriteLine($"## No banners: {plain.Count} runs");
        o.WriteLine();
        o.WriteLine($"Win rate **{Pct(plain.Count(r => r.Won), plain.Count)}**. Timeouts: {plain.Sum(r => r.Fights.Count(f => f.TimedOut))} of {plain.Sum(r => r.Fights.Count)} fights. Fights per run: {plain.Average(r => r.Fights.Count):F1}.");
        o.WriteLine();

        o.WriteLine("### By draft and boss");
        o.WriteLine();
        o.WriteLine("| | Runs | Win rate |");
        o.WriteLine("|---|---|---|");
        foreach (var g in plain.GroupBy(r => r.Draft).OrderByDescending(g => g.Count(r => r.Won)))
            o.WriteLine($"| {g.Key} | {g.Count()} | {Pct(g.Count(r => r.Won), g.Count())} |");
        foreach (var g in plain.GroupBy(r => r.Boss))
            o.WriteLine($"| Boss: {g.Key} | {g.Count()} | {Pct(g.Count(r => r.Won), g.Count())} |");
        o.WriteLine();

        o.WriteLine("### Every fight in context (enemy scaling and levels as they really come)");
        o.WriteLine();
        o.WriteLine("| Fight | Fought | Won | Average step | Run-ending losses |");
        o.WriteLine("|---|---|---|---|---|");
        var fights = plain.SelectMany(r => r.Fights).ToList();
        foreach (var g in fights.GroupBy(f => f.Name).OrderBy(g => g.Average(f => f.Step)))
            o.WriteLine($"| {g.Key} | {g.Count()} | {Pct(g.Count(f => f.Won), g.Count())} | {g.Average(f => f.Step + 1):F1} | {plain.Count(r => !r.Won && r.Fights.Count > 0 && r.Fights.Last().Name == g.Key && !r.Fights.Last().Won)} |");
        o.WriteLine();

        o.WriteLine("### The run step by step");
        o.WriteLine();
        o.WriteLine("| Step | Runs alive | Gold | Total levels |");
        o.WriteLine("|---|---|---|---|");
        for (var s = 0; s < Run.Steps; s++)
        {
            var alive = plain.Where(r => r.StepReached >= s).ToList();
            if (alive.Count > 0)
                o.WriteLine($"| {s + 1} | {Pct(alive.Count, plain.Count)} | {alive.Average(r => r.GoldAtStep[s]):F0} | {alive.Average(r => r.LevelsAtStep[s]):F1} |");
        }
        var finished = plain.Where(r => r.Won).ToList();
        if (finished.Count > 0)
            o.WriteLine($"\nWon runs: {finished.Average(r => r.GoldEarned):F0} gold earned in fights, {finished.Average(r => r.GoldLeft):F0} left at the end, {finished.Average(r => r.Bought):F1} purchases, {finished.Average(r => r.LevelsAtEnd):F1} total levels, {finished.Average(r => r.ArmyAtEnd):F1} units.");
        o.WriteLine();

        var checkedRuns = plain.Where(r => r.KnownAtCheck != null).ToList();
        o.WriteLine($"### Techniques and items held by step {TechniqueCheckStep + 1} (they came at random)");
        o.WriteLine();
        o.WriteLine($"Among the {checkedRuns.Count} runs that got that far, whose overall win rate is {Pct(checkedRuns.Count(r => r.Won), checkedRuns.Count)}. A difference under about 10 points means little.");
        o.WriteLine();
        o.WriteLine("| Technique or item | Runs with it | Win rate with | Without | Difference |");
        o.WriteLine("|---|---|---|---|---|");
        var names = checkedRuns.SelectMany(r => r.KnownAtCheck).Distinct();
        var rows = names.Select(name =>
        {
            var with = checkedRuns.Where(r => r.KnownAtCheck.Contains(name)).ToList();
            var without = checkedRuns.Where(r => !r.KnownAtCheck.Contains(name)).ToList();
            var a = with.Count == 0 ? 0 : 100.0 * with.Count(r => r.Won) / with.Count;
            var b = without.Count == 0 ? 0 : 100.0 * without.Count(r => r.Won) / without.Count;
            return (name, with.Count, a, b, diff: a - b);
        }).OrderByDescending(r => r.diff);
        foreach (var r in rows)
            o.WriteLine($"| {r.name} | {r.Count} | {r.a:F0}% | {r.b:F0}% | {r.diff:+0;-0;0} |");
        o.WriteLine();
        o.Flush();

        o.WriteLine("## Banners: each held from the start, then all as offered");
        o.WriteLine();
        o.WriteLine("| Banner | Runs | Win rate | Total levels at the end | Units at the end |");
        o.WriteLine("|---|---|---|---|---|");
        var modes = Banners.All.Select(b => new Mode { Name = b.ToString(), Forced = b }).Concat(new[] { new Mode { Name = "As offered", Natural = true } });
        foreach (var mode in modes)
        {
            var runs = PlayAll(drafts, seeds, mode, 5000);
            o.WriteLine($"| {mode.Name} | {runs.Count} | {Pct(runs.Count(r => r.Won), runs.Count)} | {runs.Average(r => r.LevelsAtEnd):F1} | {runs.Average(r => r.ArmyAtEnd):F1} |");
            o.Flush();
        }
    }
}
