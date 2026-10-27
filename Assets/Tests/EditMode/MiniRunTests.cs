using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    public class MiniRunTests
    {
        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static Battle Started(ArmyMember[] army, GridPos[] placements, (UnitStats, GridPos)[] enemies, params GridPos[] obstacles)
        {
            var b = new Battle(new Board(8, 8, obstacles), army, enemies);
            for (var i = 0; i < placements.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), placements[i]));
            Assert.IsTrue(b.StartBattle());
            return b;
        }

        private static ArmyMember[] Army(params UnitStats[] stats) => stats.Select(s => new ArmyMember(s)).ToArray();

        // ---------- Items ----------

        [Test]
        public void Items_OnlyRuleBending_EachGrantsAnAbility()
        {
            Assert.AreEqual(4, Equipment.Catalog.Count);
            Assert.IsTrue(Equipment.Catalog.All(e => e.Ability != Ability.None));
            var unit = new Unit(0, Team.Player, UnitStats.Warrior, new ArmyMember(UnitStats.Warrior, Equipment.ThornedMail));
            Assert.IsTrue(unit.Has(Ability.ThornedMail));
            Assert.AreEqual(UnitStats.Warrior.MaxHp, unit.MaxHp, "items no longer change stats");
        }

        // ---------- Run and shop ----------

        private static Run NewRun(int seed)
        {
            var run = new Run(seed);
            Assert.IsTrue(run.Draft(UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue));
            return run;
        }

        private static void WinCurrentBattle(Run run, int survivors)
        {
            var b = run.StartBattle();
            foreach (var u in b.Units.Where(u => u.Team == Team.Player && !u.IsObjective))
                b.Place(u, b.Board.AllTiles().First(p => b.CanPlace(u, p) && b.UnitAt(p) == null));
            b.StartBattle();
            var players = b.Units.Where(u => u.Team == Team.Player && !u.IsObjective).ToList();
            for (var i = survivors; i < players.Count; i++)
                players[i].Hp = 0;
            // Elementals split when they die, so keep going until nothing is left standing. In a ritual
            // fight more arrive later, so end turns until the ritual completes.
            for (var guard = 0; guard < 200 && !b.IsOver; guard++)
            {
                var e = b.Living(Team.Enemy).FirstOrDefault();
                if (e == null)
                {
                    b.EndPlayerTurn();
                    continue;
                }

                e.Hp = 1;
                var killer = players[0];
                killer.Ap = 99;
                // Teleport next to the enemy for the test and hit it once.
                killer.Pos = new GridPos(e.Pos.X, e.Pos.Y - 1);
                b.PlayerAttack(killer, e);
            }

            Assert.AreEqual(Phase.Victory, b.Phase);
        }

        /// <summary>A run whose second map step offers a shop, after winning the first battle with all units.</summary>
        private static Run RunInShop()
        {
            for (var seed = 0; seed < 500; seed++)
            {
                var run = NewRun(seed);
                var shop = run.Map[1].ToList().FindIndex(n => n.Kind == NodeKind.Shop);
                if (shop < 0)
                    continue;
                run.ChooseNode(0);
                WinCurrentBattle(run, 3);
                run.FinishBattle();
                run.ChooseNode(shop);
                return run;
            }

            Assert.Fail("no seed with a shop at step 2");
            return null;
        }

        /// <summary>A run currently at the given ? event (found by trying seeds).</summary>
        private static Run RunAtEvent(EventKind kind)
        {
            for (var seed = 0; seed < 2000; seed++)
            {
                var run = NewRun(seed);
                var ev = run.Map[1].ToList().FindIndex(n => n.Kind == NodeKind.Event);
                if (ev < 0)
                    continue;
                run.ChooseNode(0);
                WinCurrentBattle(run, 3);
                run.FinishBattle();
                run.ChooseNode(ev);
                if (run.CurrentEvent == kind)
                    return run;
            }

            Assert.Fail($"no seed with {kind} at step 2");
            return null;
        }

        /// <summary>Resolves whatever node the run is on in the simplest way.</summary>
        private static void PlayThroughNode(Run run)
        {
            switch (run.State)
            {
                case RunState.Battle:
                    WinCurrentBattle(run, 3);
                    run.FinishBattle();
                    if (run.State == RunState.Blessing)
                        run.ChooseBlessing(run.BlessingChoices[0]);
                    if (run.State == RunState.ItemReward)
                        run.SkipRewardItem();
                    break;
                case RunState.Shop:
                    run.LeaveShop();
                    break;
                case RunState.Trainer:
                    foreach (var m in run.Army)
                        if (m.Stats.Evolutions.Count > 0)
                            run.Evolve(m, m.Stats.Evolutions[0]);
                    run.LeaveTrainer();
                    break;
                case RunState.Event:
                    switch (run.CurrentEvent)
                    {
                        case EventKind.WanderingRecruit:
                            run.AcceptRecruit();
                            break;
                        case EventKind.AbandonedCart:
                            run.TakeCartItem(null);
                            break;
                        case EventKind.Gambler:
                            run.Gamble(false);
                            break;
                        case EventKind.Ambush:
                            run.StartAmbush();
                            PlayThroughNode(run);
                            break;
                        case EventKind.Shrine:
                            run.PrayAtShrine(run.Army[0]);
                            break;
                    }

                    break;
            }
        }

        [Test]
        public void Map_Act1_EachFactionOnce_BossLast()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var run = NewRun(seed);
                Assert.AreEqual(Run.Steps, run.Map.Count);
                foreach (var step in Run.TrainerSteps)
                    Assert.IsTrue(run.Map[step].Any(n => n.Kind == NodeKind.Trainer), $"trainer at step {step + 1}");
                Assert.IsTrue(run.Map[0].All(n => n.Kind == NodeKind.Battle), "the run opens with a fight");
                Assert.AreNotEqual(run.Map[0][0].Encounter, run.Map[0][1].Encounter);
                for (var i = 0; i < Run.Steps - 1; i++)
                {
                    Assert.AreEqual(2, run.Map[i].Count);
                    var a = run.Map[i][0];
                    var b = run.Map[i][1];
                    Assert.IsFalse(a.Kind == b.Kind && a.Encounter == b.Encounter, "no identical pair");
                }

                var last = run.Map[Run.Steps - 1].Single();
                Assert.AreEqual(NodeKind.Boss, last.Kind);
                Assert.IsTrue(Encounter.Bosses.Contains(last.Encounter));

                var battles = run.Map.Take(Run.Steps - 1).SelectMany(s => s).Where(n => n.Kind == NodeKind.Battle).Select(n => n.Encounter).ToList();
                CollectionAssert.AreEquivalent(Encounter.Regular, battles, "every faction exactly once on the map");
            }
        }

        [Test]
        public void Map_BothScenarios_AtSteps3And5_NextToABattle()
        {
            var shrineSteps = new System.Collections.Generic.HashSet<int>();
            for (var seed = 0; seed < 50; seed++)
            {
                var run = NewRun(seed);
                int StepOf(Encounter e) => Enumerable.Range(0, Run.Steps).Single(i => run.Map[i].Any(n => n.Kind == NodeKind.Ritual && n.Encounter == e));
                var shrine = StepOf(Encounter.MoonShrine);
                var ritual = StepOf(Encounter.BreakRitual);
                CollectionAssert.AreEquivalent(new[] { 2, 4 }, new[] { shrine, ritual }, $"seed {seed}");
                foreach (var step in new[] { shrine, ritual })
                    Assert.IsTrue(run.Map[step].Any(n => n.Kind == NodeKind.Battle));
                shrineSteps.Add(shrine);
            }

            Assert.AreEqual(2, shrineSteps.Count, "the shrine comes up at both spots");
        }

        /// <summary>A run standing at the step that offers a scenario fight, having played through everything before it.</summary>
        private static Run RunAtScenarioStep(int seed, Encounter scenario)
        {
            var run = NewRun(seed);
            while (!run.CurrentChoices.Any(n => n.Encounter == scenario))
            {
                run.ChooseNode(run.CurrentChoices.ToList().FindIndex(n => n.Kind != NodeKind.Ritual));
                PlayThroughNode(run);
            }

            return run;
        }

        private static Run RunAtShrineStep(int seed) => RunAtScenarioStep(seed, Encounter.MoonShrine);

        private static int ShrineIndex(Run run) => run.CurrentChoices.ToList().FindIndex(n => n.Encounter == Encounter.MoonShrine);

        private static int ScenarioIndex(Run run, Encounter scenario) => run.CurrentChoices.ToList().FindIndex(n => n.Encounter == scenario);

        [Test]
        public void BreakRitual_Won_PickAnItemForAUnit()
        {
            var run = RunAtScenarioStep(4, Encounter.BreakRitual);
            var step = run.StepIndex;
            run.ChooseNode(ScenarioIndex(run, Encounter.BreakRitual));
            WinCurrentBattle(run, 3);
            Assert.IsTrue(run.CurrentBattle.TotemsBroken);
            Assert.Greater(run.FinishBattle(), 0);
            Assert.AreEqual(RunState.ItemReward, run.State);
            Assert.AreEqual(Run.RewardItemCount, run.RewardItems.Distinct().Count());

            var item = run.RewardItems[0];
            var member = run.Army[1];
            Assert.IsTrue(run.TakeRewardItem(item, member));
            Assert.AreEqual(item, member.Equipment);
            Assert.AreEqual(RunState.Map, run.State);
            Assert.AreEqual(step + 1, run.StepIndex);
            Assert.AreEqual(0, run.RewardItems.Count);
        }

        [Test]
        public void BreakRitual_SummoningCompletes_CurseAndTheRunGoesOn()
        {
            var run = RunAtScenarioStep(6, Encounter.BreakRitual);
            var step = run.StepIndex;
            run.ChooseNode(ScenarioIndex(run, Encounter.BreakRitual));
            var b = run.StartBattle();
            foreach (var u in b.Units.Where(u => u.Team == Team.Player))
                b.Place(u, b.Board.AllTiles().First(p => b.CanPlace(u, p) && b.UnitAt(p) == null));
            b.StartBattle();
            // Keep the army out of harm's way so only the clock decides.
            foreach (var u in b.Units.Where(u => u.Team == Team.Player))
                u.Hp = 999;
            for (var i = 0; i < Encounter.SummonTurn && !b.IsOver; i++)
                b.EndPlayerTurn();
            Assert.IsTrue(b.SummoningComplete);
            Assert.IsTrue(b.ScenarioLost);

            Assert.AreEqual(0, run.FinishBattle());
            Assert.AreEqual(1, run.Curses.Count);
            Assert.AreEqual(RunState.Map, run.State);
            Assert.AreEqual(step + 1, run.StepIndex);
        }

        [Test]
        public void LosingBothScenarios_TwoDifferentCurses()
        {
            for (var seed = 0; seed < 10; seed++)
            {
                var run = NewRun(seed);
                while (run.State == RunState.Map && run.StepIndex < 5)
                {
                    var scenario = run.CurrentChoices.ToList().FindIndex(n => n.Kind == NodeKind.Ritual);
                    run.ChooseNode(scenario >= 0 ? scenario : 0);
                    if (scenario < 0)
                    {
                        PlayThroughNode(run);
                        continue;
                    }

                    var b = run.StartBattle();
                    foreach (var u in b.Units.Where(u => u.Team == Team.Player && !u.IsObjective))
                        b.Place(u, b.Board.AllTiles().First(p => b.CanPlace(u, p) && b.UnitAt(p) == null));
                    b.StartBattle();
                    if (b.Objective != null)
                        b.Objective.Hp = 0;
                    foreach (var u in b.LivingArmy)
                        u.Hp = 999;
                    for (var i = 0; i < 10 && !b.IsOver; i++)
                        b.EndPlayerTurn();
                    Assert.IsTrue(b.ScenarioLost, $"seed {seed}");
                    run.FinishBattle();
                }

                Assert.AreEqual(2, run.Curses.Count, $"seed {seed}");
                Assert.AreNotEqual(run.Curses[0], run.Curses[1]);
            }
        }

        [Test]
        public void MoonShrine_Held_PickABlessing_InPlayInLaterBattles()
        {
            var run = RunAtShrineStep(3);
            var step = run.StepIndex;
            var gold = run.Gold;
            run.ChooseNode(ShrineIndex(run));
            Assert.AreEqual(RunState.Battle, run.State);
            WinCurrentBattle(run, 3);
            var earned = run.FinishBattle();
            Assert.AreEqual(gold + earned, run.Gold);
            Assert.AreEqual(RunState.Blessing, run.State);
            Assert.IsFalse(run.Fought.Contains(Encounter.MoonShrine));

            Assert.IsTrue(run.ChooseBlessing(Omen.Night));
            Assert.AreEqual(Omen.Night, run.Blessing);
            Assert.AreEqual(RunState.Map, run.State);
            Assert.AreEqual(step + 1, run.StepIndex);

            while (run.State == RunState.Map)
            {
                var fight = run.CurrentChoices.ToList().FindIndex(n => n.Kind == NodeKind.Battle || n.Kind == NodeKind.Boss);
                run.ChooseNode(fight >= 0 ? fight : 0);
                if (run.State != RunState.Battle)
                    PlayThroughNode(run);
            }

            CollectionAssert.Contains(run.StartBattle().Omens, (Omen.Night, Team.Player));
        }

        [Test]
        public void MoonShrine_Lost_RandomCurse_RunGoesOn()
        {
            var run = RunAtShrineStep(5);
            var step = run.StepIndex;
            var gold = run.Gold;
            run.ChooseNode(ShrineIndex(run));
            var b = run.StartBattle();
            foreach (var u in b.Units.Where(u => u.Team == Team.Player && !u.IsObjective))
                b.Place(u, b.Board.AllTiles().First(p => b.CanPlace(u, p) && b.UnitAt(p) == null));
            b.StartBattle();
            b.Objective.Hp = 0;
            b.EndPlayerTurn();
            Assert.IsTrue(b.ObjectiveLost);

            Assert.AreEqual(0, run.FinishBattle());
            Assert.AreEqual(gold, run.Gold);
            Assert.AreEqual(1, run.Curses.Count);
            Assert.IsNull(run.Blessing);
            Assert.AreEqual(RunState.Map, run.State);
            Assert.AreEqual(step + 1, run.StepIndex);
        }

        [Test]
        public void Map_BothBossesComeUp()
        {
            var bosses = Enumerable.Range(0, 40).Select(s => NewRun(s).Map[Run.Steps - 1][0].Encounter).Distinct().Count();
            Assert.AreEqual(2, bosses);
        }

        [Test]
        public void Trainer_FirstEvolutionFree_ThenPaid()
        {
            for (var seed = 0; seed < 300; seed++)
            {
                var run = NewRun(seed);
                while (run.State == RunState.Map && run.StepIndex < Run.TrainerSteps[0])
                {
                    run.ChooseNode(0);
                    PlayThroughNode(run);
                }

                if (run.State != RunState.Map || run.Gold < Run.SecondEvolutionPrice)
                    continue;
                run.ChooseNode(run.CurrentChoices.ToList().FindIndex(n => n.Kind == NodeKind.Trainer));
                var gold = run.Gold;
                Assert.AreEqual(0, run.NextEvolutionPrice);
                Assert.IsTrue(run.Evolve(run.Army[0], run.Army[0].Stats.Evolutions[0]));
                Assert.AreEqual(gold, run.Gold, "first is free");
                Assert.AreEqual(Run.SecondEvolutionPrice, run.NextEvolutionPrice);
                Assert.IsTrue(run.Evolve(run.Army[1], run.Army[1].Stats.Evolutions[0]));
                Assert.AreEqual(gold - Run.SecondEvolutionPrice, run.Gold);
                Assert.AreEqual(Run.LaterEvolutionPrice, run.NextEvolutionPrice);
                return;
            }

            Assert.Fail("never reached a trainer with enough gold");
        }

        [Test]
        public void Necromancer_RaisesTheFactionsYouBeat()
        {
            var fought = new[] { Encounter.Wolves, Encounter.Goblins };
            var b = Encounter.Necromancer.CreateBattle(ArmyMember.StartingArmy(), new System.Random(3), fought);
            var minions = b.Units.Where(u => u.Team == Team.Enemy && !u.Stats.CanRaise).ToList();
            Assert.AreEqual(2, minions.Count(u => u.Stats == UnitStats.Raised(UnitStats.Wolf)));
            Assert.AreEqual(2, minions.Count(u => u.Stats == UnitStats.Raised(UnitStats.GoblinBomber)));
            Assert.IsTrue(minions.All(u => u.Stats.LeavesRemains), "they can all be raised again");

            var none = Encounter.Necromancer.CreateBattle(ArmyMember.StartingArmy(), new System.Random(3));
            Assert.AreEqual(4, none.Units.Count(u => u.Stats == UnitStats.Skeleton), "skeletons when nothing was beaten");
        }

        [Test]
        public void Layouts_FollowTheirRules()
        {
            foreach (var enc in Encounter.Regular.Concat(Encounter.Bosses))
            {
                for (var seed = 0; seed < 30; seed++)
                {
                    var b = enc.CreateBattle(ArmyMember.StartingArmy(), new System.Random(seed), Encounter.Regular);
                    for (var x = 0; x < 8; x++)
                    for (var y = 0; y < Battle.PlayerZoneRows; y++)
                        Assert.IsFalse(b.Board.IsObstacle(new GridPos(x, y)), $"{enc.Name}: player rows clear");
                    Assert.IsTrue(b.Units.Any(u => u.Team == Team.Enemy), enc.Name);
                }
            }

            // The ridge always has exactly three gaps.
            var ridge = Encounter.Goblins.CreateBattle(ArmyMember.StartingArmy(), new System.Random(5));
            Assert.AreEqual(5, Enumerable.Range(0, 8).Count(x => ridge.Board.IsObstacle(new GridPos(x, 4))));
        }

        [Test]
        public void Run_GoldScalesWithSurvivors_ThenBackToMap()
        {
            var run = NewRun(1);
            run.ChooseNode(0);
            WinCurrentBattle(run, 2);
            Assert.AreEqual(Run.GoldFor(2), run.FinishBattle());
            Assert.AreEqual(20, run.Gold);
            Assert.AreEqual(RunState.Map, run.State);
            Assert.AreEqual(1, run.StepIndex);
        }

        [Test]
        public void Shop_HasDistinctOffersAndARecruit()
        {
            var run = RunInShop();
            Assert.AreEqual(Run.ShopSize, run.Offers.Count);
            Assert.AreEqual(Run.ShopSize, run.Offers.Distinct().Count());
            Assert.IsTrue(run.ShopHasRecruit);
        }

        [Test]
        public void Shop_RecruitAddsBaseUnit_UpToFour()
        {
            var run = RunInShop();
            Assert.IsTrue(run.Gold >= Run.RecruitPrice);
            var recruit = run.ShopRecruit;
            Assert.IsTrue(UnitStats.BaseUnits.Contains(recruit));
            Assert.IsTrue(run.BuyRecruit());
            Assert.AreEqual(4, run.Army.Count);
            Assert.AreEqual(recruit, run.Army[3].Stats);
            Assert.IsFalse(run.CanBuyRecruit, "only one recruit per shop, and the army is full");
        }

        [Test]
        public void Shop_BuyReplacesSlot_AndSpendsGold()
        {
            var run = RunInShop();
            var warrior = run.Army[0];
            var affordable = run.Offers.Where(o => o.CanEquip(warrior.Stats)).OrderBy(o => o.Price).ToList();
            Assume.That(affordable.Count >= 2);
            Assert.IsTrue(run.Buy(affordable[0], warrior));
            Assert.AreEqual(affordable[0], warrior.Equipment);
            Assert.IsFalse(run.Offers.Contains(affordable[0]), "bought items leave the shop");
            var goldBefore = run.Gold;
            if (run.Buy(affordable[1], warrior))
            {
                Assert.AreEqual(affordable[1], warrior.Equipment, "one slot: the new item replaces the old");
                Assert.AreEqual(goldBefore - affordable[1].Price, run.Gold);
            }
        }

        [Test]
        public void Shop_CantBuyWithoutGold()
        {
            var run = RunInShop();
            // Spend gold until nothing more fits, then every remaining offer must be refused.
            var archer = run.Army[1];
            while (run.Offers.Any(o => run.CanBuy(o, archer)))
                run.Buy(run.Offers.First(o => run.CanBuy(o, archer)), archer);
            foreach (var offer in run.Offers)
                Assert.IsFalse(run.Buy(offer, archer));
        }

        [Test]
        public void Run_WholeMap_ThenWonAfterOgre()
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var run = NewRun(seed);
                for (var step = 0; step < Run.Steps; step++)
                {
                    Assert.AreEqual(RunState.Map, run.State);
                    run.ChooseNode(step % run.CurrentChoices.Count);
                    PlayThroughNode(run);
                }

                Assert.AreEqual(RunState.Won, run.State, $"seed {seed}");
            }
        }

        [Test]
        public void Run_DefeatEndsRun()
        {
            var run = NewRun(9);
            run.ChooseNode(0);
            var b = run.StartBattle();
            var x = 0;
            foreach (var u in b.Units.Where(u => u.Team == Team.Player))
                b.Place(u, new GridPos(x++, 0));
            b.StartBattle();
            for (var turn = 0; turn < 60 && !b.IsOver; turn++)
                b.EndPlayerTurn();
            Assert.AreEqual(Phase.Defeat, b.Phase);
            Assert.AreEqual(0, run.FinishBattle());
            Assert.AreEqual(RunState.Lost, run.State);
        }

        [Test]
        public void FreshBattle_FullHpAndDeadUnitsBack()
        {
            var run = NewRun(11);
            run.ChooseNode(0);
            WinCurrentBattle(run, 1);
            run.FinishBattle();
            // Second fight of the run, wherever it comes up.
            while (run.State == RunState.Map)
            {
                var battle = run.CurrentChoices.ToList().FindIndex(n => n.Kind != NodeKind.Shop && n.Kind != NodeKind.Event);
                run.ChooseNode(battle >= 0 ? battle : 0);
                if (run.State != RunState.Battle)
                    PlayThroughNode(run);
            }

            var b = run.StartBattle();
            Assert.IsTrue(b.Units.Where(u => u.Team == Team.Player).All(u => u.Hp == u.MaxHp));
            Assert.AreEqual(run.Army.Count, b.Units.Count(u => u.Team == Team.Player));
        }

        // ---------- Events ----------

        [Test]
        public void Event_Shrine_AddsMaxHpForTheRun()
        {
            var run = RunAtEvent(EventKind.Shrine);
            var warrior = run.Army[0];
            Assert.IsTrue(run.PrayAtShrine(warrior));
            Assert.AreEqual(Run.ShrineHp, warrior.BonusMaxHp);
            Assert.AreEqual(RunState.Map, run.State);
            Assert.AreEqual(UnitStats.Warrior.MaxHp + Run.ShrineHp, new Unit(0, Team.Player, warrior.Stats, warrior).MaxHp);
        }

        [Test]
        public void Event_WanderingRecruit_Joins()
        {
            var run = RunAtEvent(EventKind.WanderingRecruit);
            var recruit = run.EventRecruit;
            run.AcceptRecruit();
            Assert.AreEqual(4, run.Army.Count);
            Assert.AreEqual(recruit, run.Army[3].Stats);
        }

        // ---------- Draft and trainer ----------

        [Test]
        public void Draft_ThreeDifferentBaseUnits()
        {
            var run = new Run(1);
            Assert.AreEqual(RunState.Draft, run.State);
            Assert.IsFalse(run.Draft(UnitStats.Warrior, UnitStats.Warrior, UnitStats.Rogue), "no duplicates");
            Assert.IsFalse(run.Draft(UnitStats.Warrior, UnitStats.Swordsman, UnitStats.Rogue), "base units only");
            Assert.IsFalse(run.Draft(UnitStats.Warrior, UnitStats.Rogue), "exactly three");
            Assert.IsTrue(run.Draft(UnitStats.Apprentice, UnitStats.Sapper, UnitStats.Rogue));
            Assert.AreEqual(RunState.Map, run.State);
            Assert.AreEqual(3, run.Army.Count);
        }

        [Test]
        public void Trainer_EveryUnitUpOneTierPerVisit_KeepsBonuses()
        {
            for (var seed = 0; seed < 300; seed++)
            {
                var run = NewRun(seed);
                // Walk to the first trainer, taking it.
                while (run.State == RunState.Map && run.StepIndex < Run.TrainerSteps[0])
                {
                    run.ChooseNode(0);
                    PlayThroughNode(run);
                }

                if (run.State != RunState.Map)
                    continue;
                var trainer = run.CurrentChoices.ToList().FindIndex(n => n.Kind == NodeKind.Trainer);
                run.ChooseNode(trainer);
                Assert.AreEqual(RunState.Trainer, run.State);

                var warrior = run.Army.First(m => m.Stats.Root == UnitStats.Warrior);
                warrior.BonusMaxHp = 2;
                Assert.IsFalse(run.CanEvolve(warrior, UnitStats.Juggernaut), "one tier at a time");
                Assert.IsTrue(run.Evolve(warrior, UnitStats.Shielder));
                Assert.AreEqual(UnitStats.Shielder, warrior.Stats);
                Assert.IsFalse(run.Evolve(warrior, UnitStats.Juggernaut), "once per visit");
                Assert.AreEqual(2, warrior.BonusMaxHp);
                Assert.IsTrue(run.Army.Where(m => m != warrior).All(m => run.CanEvolve(m, m.Stats.Evolutions[0])));
                run.LeaveTrainer();
                Assert.AreEqual(RunState.Map, run.State);
                return;
            }

            Assert.Fail("never reached a trainer");
        }

        [Test]
        public void Trees_ThreeTiers_TwoBranchesEach_StrongerAsTheyGo()
        {
            Assert.AreEqual(5, UnitStats.BaseUnits.Count);
            // 7 per tree, except the mage apprentice, which has three tier-2 branches: 4 x 7 + 10.
            Assert.AreEqual(38, UnitStats.AllPlayerTypes.Count());
            foreach (var type in UnitStats.AllPlayerTypes)
            {
                var branches = type == UnitStats.Apprentice ? 3 : type.Tier < 3 ? 2 : 0;
                Assert.AreEqual(branches, type.Evolutions.Count, type.Name);
                foreach (var next in type.Evolutions)
                {
                    Assert.AreEqual(type.Tier + 1, next.Tier);
                    Assert.GreaterOrEqual(next.MaxHp, type.MaxHp, $"{next.Name} HP");
                    Assert.GreaterOrEqual(next.Damage, type.Damage, $"{next.Name} damage");
                    Assert.IsTrue(next.Abilities.Count + next.Actions.Count > type.Abilities.Count + type.Actions.Count ||
                                  next.Abilities.Except(type.Abilities).Any(), $"{next.Name} adds something");
                }
            }
        }

        [Test]
        public void Event_Gambler_CostsGoldToPlay()
        {
            var run = RunAtEvent(EventKind.Gambler);
            var before = run.Gold;
            var won = run.Gamble(true);
            Assert.AreEqual(before - Run.GambleCost + (won ? Run.GambleWin : 0), run.Gold);
        }

        [Test]
        public void Event_Cart_EquipsItem()
        {
            var run = RunAtEvent(EventKind.AbandonedCart);
            var member = run.Army.First(m => run.CartItem.CanEquip(m.Stats));
            Assert.IsTrue(run.TakeCartItem(member));
            Assert.AreEqual(run.CartItem, member.Equipment);
        }

        [Test]
        public void Event_Ambush_IsAFreshFactionThatPaysExtra()
        {
            var run = RunAtEvent(EventKind.Ambush);
            var ahead = run.Map.Skip(run.StepIndex + 1).SelectMany(s => s).Select(n => n.Encounter).ToList();
            run.StartAmbush();
            Assert.AreEqual(RunState.Battle, run.State);
            Assert.IsTrue(Encounter.Regular.Contains(run.CurrentEncounter));
            Assert.IsFalse(run.Fought.Contains(run.CurrentEncounter), "not one you've fought");
            Assert.IsFalse(ahead.Contains(run.CurrentEncounter), "not one still waiting on the map");
            var before = run.Gold;
            WinCurrentBattle(run, 3);
            run.FinishBattle();
            Assert.AreEqual(before + Run.GoldFor(3) + Run.AmbushBonusGold, run.Gold);
        }

        [Test]
        public void Bloodthirst_DamageCarriesToNextBattle()
        {
            var run = NewRun(4);
            run.ChooseNode(0);
            var b = run.StartBattle();
            var x = 0;
            foreach (var u in b.Units.Where(u => u.Team == Team.Player))
                b.Place(u, new GridPos(x++, 0));
            b.StartBattle();
            var rogue = b.Units.Single(u => u.Stats == UnitStats.Rogue);
            // Elementals split when they die, so keep going until nothing is left standing.
            for (var e = b.Living(Team.Enemy).FirstOrDefault(); e != null && !b.IsOver; e = b.Living(Team.Enemy).FirstOrDefault())
            {
                e.Hp = 1;
                rogue.Ap = 99;
                rogue.Pos = new GridPos(e.Pos.X, e.Pos.Y - 1);
                b.PlayerAttack(rogue, e);
            }

            var kills = b.Units.Count(u => u.Team == Team.Enemy);
            run.FinishBattle();
            Assert.AreEqual(kills, run.Army.Single(m => m.Stats == UnitStats.Rogue).BonusDamage);
        }

        // ---------- Walk up and attack ----------

        [Test]
        public void AttackSpot_CheapestTileThatLeavesAttackAp()
        {
            var b = Started(Army(UnitStats.Rogue), new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 5)) });
            var rogue = Player(b);
            var wolf = Enemy(b);
            var spot = b.FindAttackSpot(rogue, wolf);
            Assert.IsTrue(spot.HasValue);
            Assert.AreEqual(new GridPos(3, 4), spot.Value.tile);
            Assert.AreEqual(3, spot.Value.cost);
        }

        [Test]
        public void AttackSpot_NullWhenAttackCantBeAfforded()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 5)) });
            // Warrior has 3 AP: 3 tiles to reach (3,4) would leave nothing for the attack.
            Assert.IsNull(b.FindAttackSpot(Player(b), Enemy(b)));
        }

        [Test]
        public void AttackSpot_OwnTileWhenAlreadyInRange()
        {
            var b = Started(Army(UnitStats.Archer), new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)) });
            var spot = b.FindAttackSpot(Player(b), Enemy(b));
            Assert.AreEqual(new GridPos(3, 1), spot.Value.tile);
            Assert.AreEqual(0, spot.Value.cost);
        }

        [Test]
        public void CanStillAct_FalseWhenDoneOrOutOfAp()
        {
            var b = Started(Army(UnitStats.Warrior, UnitStats.Rogue), new[] { new GridPos(0, 0), new GridPos(5, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            var warrior = Player(b, 0);
            var rogue = Player(b, 1);
            Assert.IsTrue(b.CanStillAct(warrior));
            b.EndUnitTurn(warrior);
            Assert.IsFalse(b.CanStillAct(warrior));
            rogue.Ap = 0;
            Assert.IsFalse(b.CanStillAct(rogue));
        }

        // ---------- Necromancer ----------

        [Test]
        public void Skeleton_LeavesBonePile_ThatCanBeWalkedOn()
        {
            var b = Started(Army(UnitStats.Rogue), new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Skeleton, new GridPos(3, 2)), (UnitStats.Wolf, new GridPos(7, 7)) });
            var rogue = Player(b);
            var skeleton = Enemy(b);
            b.PlayerAttack(rogue, skeleton);
            Assert.IsFalse(skeleton.IsAlive);
            Assert.AreEqual(skeleton, b.RemainsAt(new GridPos(3, 2)));
            Assert.IsNotNull(b.PlayerMove(rogue, new GridPos(3, 2)));
        }

        [Test]
        public void Necromancer_RaisesBonePile()
        {
            var b = Started(Army(UnitStats.Rogue), new[] { new GridPos(0, 1) },
                new[] { (UnitStats.Necromancer, new GridPos(4, 7)), (UnitStats.Skeleton, new GridPos(3, 5)) });
            var necro = Enemy(b, 0);
            var skeleton = Enemy(b, 1);
            skeleton.Hp = 0;
            Assert.AreEqual(IntentKind.Raise, EnemyAI.PredictIntent(b, necro).Kind);

            var events = b.EndPlayerTurn();
            var raise = events.OfType<RaiseEvent>().Single();
            Assert.AreEqual(skeleton, raise.Raised);
            Assert.AreEqual(skeleton.MaxHp, skeleton.Hp);
            Assert.IsFalse(events.Any(e => e.Actor == skeleton), "a raised skeleton waits a turn");
        }

        [Test]
        public void StandingOnBonePile_BlocksRaise()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Necromancer, new GridPos(4, 7)), (UnitStats.Skeleton, new GridPos(3, 2)) });
            var necro = Enemy(b, 0);
            var skeleton = Enemy(b, 1);
            skeleton.Hp = 0;
            var warrior = Player(b);
            b.PlayerMove(warrior, new GridPos(3, 2));
            Assert.IsFalse(b.CanRaise(necro, skeleton));
            var events = b.EndPlayerTurn();
            Assert.IsFalse(events.OfType<RaiseEvent>().Any());
            Assert.IsFalse(skeleton.IsAlive);
        }

        [Test]
        public void DeadNecromancer_BonesStayDead_AndBattleCanBeWon()
        {
            var b = Started(Army(UnitStats.Rogue), new[] { new GridPos(4, 1) },
                new[] { (UnitStats.Necromancer, new GridPos(4, 2)), (UnitStats.Skeleton, new GridPos(0, 7)) });
            var rogue = Player(b);
            Enemy(b, 1).Hp = 0;
            Enemy(b, 0).Hp = 6;
            b.PlayerAttack(rogue, Enemy(b, 0));
            b.PlayerAttack(rogue, Enemy(b, 0));
            Assert.AreEqual(Phase.Victory, b.Phase);
        }

        [Test]
        public void Necromancer_BacksAwayFromCloseUnits()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(4, 1) },
                new[] { (UnitStats.Necromancer, new GridPos(4, 3)) });
            var necro = Enemy(b);
            var before = Battle.Distance(necro, Player(b));
            b.EndPlayerTurn();
            Assert.Greater(Battle.Distance(necro, Player(b)), before);
        }

        [Test]
        public void Necromancer_WithNothingToRaise_WalksToBoltRangeAndShoots()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(4, 1) },
                new[] { (UnitStats.Necromancer, new GridPos(4, 7)) });
            var necro = Enemy(b);
            var warrior = Player(b);
            warrior.Pos = new GridPos(4, 3);
            var events = b.EndPlayerTurn();
            Assert.AreEqual(3, Battle.Distance(necro, warrior), "stops at bolt range, not closer");
            var bolt = events.OfType<AttackEvent>().Single();
            Assert.AreEqual(warrior, bolt.Target);
        }

        [Test]
        public void Necromancer_FarAway_ApproachesButNeverCloserThanBoltRange()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(4, 0) },
                new[] { (UnitStats.Necromancer, new GridPos(4, 7)) });
            var necro = Enemy(b);
            var warrior = Player(b);
            var before = Battle.Distance(necro, warrior);
            b.EndPlayerTurn();
            var after = Battle.Distance(necro, warrior);
            Assert.Less(after, before, "it comes closer");
            Assert.GreaterOrEqual(after, 3, "but keeps its distance");
        }

        // ---------- Ogre ----------

        [Test]
        public void Ogre_Occupies2x2()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(0, 0) },
                new[] { (UnitStats.Ogre, new GridPos(3, 5)) });
            var ogre = Enemy(b);
            Assert.AreEqual(ogre, b.UnitAt(new GridPos(4, 6)));
            Assert.AreEqual(ogre, b.UnitAt(new GridPos(3, 5)));
            Assert.IsNull(b.UnitAt(new GridPos(5, 5)));
            Assert.AreEqual(1, Battle.Distance(new GridPos(5, 6), 1, ogre));
        }

        [Test]
        public void Ogre_SmashHitsEveryAdjacentUnit()
        {
            var b = Started(Army(UnitStats.Warrior, UnitStats.Archer), new[] { new GridPos(0, 0), new GridPos(1, 0) },
                new[] { (UnitStats.Ogre, new GridPos(3, 5)) });
            var warrior = Player(b, 0);
            var archer = Player(b, 1);
            warrior.Pos = new GridPos(3, 4);
            archer.Pos = new GridPos(5, 6);
            // Called directly: on its own turn the Giant would slam them away first.
            var ogre = Enemy(b);
            ogre.Ap = ogre.MaxAp;
            var smash = b.DoSmash(ogre).OfType<SmashEvent>().Single();
            Assert.AreEqual(2, smash.Hits.Count);
            Assert.AreEqual(UnitStats.Ogre.Damage, smash.Hits[0].Damage);
        }

        [Test]
        public void Giant_SlamsWhenCrowded()
        {
            var b = Started(Army(UnitStats.Warrior, UnitStats.Archer), new[] { new GridPos(0, 0), new GridPos(1, 0) },
                new[] { (UnitStats.Ogre, new GridPos(3, 5)) });
            var warrior = Player(b, 0);
            var archer = Player(b, 1);
            warrior.Pos = new GridPos(3, 4);
            archer.Pos = new GridPos(5, 6);
            Assert.AreEqual(IntentKind.Slam, EnemyAI.PredictIntent(b, Enemy(b)).Kind);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<AreaAttackEvent>().Any(a => a.Kind == "slam"));
            var pushes = events.OfType<PushEvent>().Where(p => p.Pusher == Enemy(b)).ToList();
            Assert.IsTrue(pushes.Any(p => p.Unit == warrior) && pushes.Any(p => p.Unit == archer));
        }

        [Test]
        public void Ogre_BoulderDamages_AndLeavesRock()
        {
            var b = Started(Army(UnitStats.Archer), new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Ogre, new GridPos(3, 5)) });
            var archer = Player(b);
            var ogre = Enemy(b);
            var intent = EnemyAI.PredictIntent(b, ogre);
            Assert.AreEqual(IntentKind.Throw, intent.Kind);
            Assert.AreEqual(new GridPos(3, 2), intent.Tile, "the rock lands on the target's ogre side");

            var events = b.EndPlayerTurn();
            var boulder = events.OfType<BoulderEvent>().Single();
            Assert.AreEqual(archer, boulder.Hit.Target);
            Assert.AreEqual(archer.MaxHp - UnitStats.Ogre.ThrowDamage, archer.Hp);
            Assert.IsTrue(b.Board.IsObstacle(new GridPos(3, 2)));
        }

        [Test]
        public void Ogre_WalksAround2x2()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(0, 0) },
                new[] { (UnitStats.Ogre, new GridPos(5, 5)) });
            var ogre = Enemy(b);
            ogre.Ap = ogre.MaxAp;
            var reach = b.GetReachable(ogre);
            Assert.IsFalse(reach.ContainsKey(new GridPos(7, 5)), "the footprint would leave the board");
            Assert.IsTrue(reach.ContainsKey(new GridPos(5, 4)));
        }

        [Test]
        public void MiniRun_EveryBattleTerminates()
        {
            foreach (var encounter in Encounter.Regular.Concat(Encounter.Bosses))
            {
                var b = encounter.CreateBattle(ArmyMember.StartingArmy());
                var x = 2;
                foreach (var u in b.Units.Where(u => u.Team == Team.Player))
                    b.Place(u, new GridPos(x++, 1));
                b.StartBattle();
                for (var turn = 0; turn < 80 && !b.IsOver; turn++)
                    b.EndPlayerTurn();
                Assert.IsTrue(b.IsOver, $"{encounter.Name} should end against a passive player");
            }
        }
    }
}
