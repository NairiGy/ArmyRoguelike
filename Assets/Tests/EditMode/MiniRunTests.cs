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
                run.SkipReward();
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
                run.SkipReward();
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
                    if (run.State == RunState.Reward)
                        run.SkipReward();
                    break;
                case RunState.Shop:
                    run.LeaveShop();
                    break;
                case RunState.Event:
                    switch (run.CurrentEvent)
                    {
                        case EventKind.WanderingRecruit:
                            run.AcceptRecruit();
                            break;
                        case EventKind.AbandonedCart:
                            run.TakeCart(null);
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
        public void Map_Act1_TwelveSteps_FightsGuaranteed_BossLast()
        {
            bool IsFight(MapNode n) => n.Kind == NodeKind.Battle || n.Kind == NodeKind.Elite || n.Kind == NodeKind.Ritual;
            for (var seed = 0; seed < 50; seed++)
            {
                var run = NewRun(seed);
                Assert.AreEqual(12, run.Map.Count);
                Assert.IsTrue(run.Map[4].Any(n => n.Kind == NodeKind.Shop) && run.Map[4].Any(n => n.Kind == NodeKind.Event), "step 5: shop or ?");
                Assert.IsTrue(run.Map[8].Any(n => n.Kind == NodeKind.Event) && run.Map[8].Any(n => n.Kind == NodeKind.Battle), "step 9: ? or battle");
                foreach (var step in Run.FightSteps)
                    Assert.IsTrue(run.Map[step].All(IsFight), $"step {step + 1} is two fights");
                Assert.IsTrue(run.Map[0].All(n => n.Kind == NodeKind.Battle), "the run opens with a fight");
                Assert.AreNotEqual(run.Map[0][0].Encounter, run.Map[0][1].Encounter);
                Assert.IsTrue(run.Map[0].All(n => Encounter.Regular.Contains(n.Encounter)), "step 1 is filled in from the start");
                Assert.IsTrue(run.Map.Skip(1).SelectMany(s => s).Where(n => n.Kind == NodeKind.Battle || n.Kind == NodeKind.Elite)
                    .All(n => n.Encounter == null), "later fights are decided when the run gets there");
                Assert.AreEqual(2, run.Map.SelectMany(s => s).Count(n => n.Kind == NodeKind.Elite));
                for (var i = 0; i < Run.Steps - 1; i++)
                    Assert.AreEqual(2, run.Map[i].Count);

                var last = run.Map[Run.Steps - 1].Single();
                Assert.AreEqual(NodeKind.Boss, last.Kind);
                Assert.IsTrue(Encounter.Bosses.Contains(last.Encounter));
            }
        }

        [Test]
        public void Map_BattlesBringNewFactions_ThenRematches_ElitesRematchBeatenOnes()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var run = NewRun(seed);
                var met = new System.Collections.Generic.List<Encounter>();
                while (run.State == RunState.Map && !run.IsFinalStep)
                {
                    var choices = run.CurrentChoices;
                    var fights = choices.Where(n => n.Kind == NodeKind.Battle || n.Kind == NodeKind.Elite).ToList();
                    foreach (var n in fights)
                        Assert.IsNotNull(n.Encounter, $"seed {seed} step {run.StepIndex + 1}");
                    if (fights.Count == 2)
                        Assert.AreNotEqual(fights[0].Encounter.Base ?? fights[0].Encounter, fights[1].Encounter.Base ?? fights[1].Encounter);
                    foreach (var n in choices.Where(n => n.Kind == NodeKind.Battle))
                    {
                        Assert.IsTrue(Encounter.Regular.Contains(n.Encounter));
                        Assert.AreEqual(Encounter.Regular.All(run.Fought.Contains), n.IsRematch);
                        if (!n.IsRematch)
                            Assert.IsFalse(run.Fought.Contains(n.Encounter), "a new faction while there are some left");
                    }

                    foreach (var n in choices.Where(n => n.Kind == NodeKind.Elite))
                    {
                        Assert.IsTrue(n.Encounter.IsElite);
                        Assert.IsTrue(run.Fought.Contains(n.Encounter.Base), "an elite is a faction already beaten");
                    }

                    // Prefer fights, so the run beats factions and reaches the rematch rules.
                    var pick = run.CurrentChoices.ToList().FindIndex(n => n.Kind == NodeKind.Battle);
                    run.ChooseNode(pick >= 0 ? pick : 0);
                    PlayThroughNode(run);
                }

                Assert.AreEqual(Run.Steps - 1, run.StepIndex, $"seed {seed}");
            }
        }

        [Test]
        public void Elite_Won_PaysGoldAndAnItemPick()
        {
            var run = NewRun(2);
            while (!run.CurrentChoices.Any(n => n.Kind == NodeKind.Elite))
            {
                var battle = run.CurrentChoices.ToList().FindIndex(n => n.Kind == NodeKind.Battle);
                run.ChooseNode(battle >= 0 ? battle : 0);
                PlayThroughNode(run);
            }

            var gold = run.Gold;
            run.ChooseNode(run.CurrentChoices.ToList().FindIndex(n => n.Kind == NodeKind.Elite));
            Assert.IsTrue(run.CurrentEncounter.IsElite);
            WinCurrentBattle(run, 3);
            var earned = run.FinishBattle();
            Assert.AreEqual(gold + earned, run.Gold);
            Assert.Greater(earned, 0);
            Assert.AreEqual(RunState.Reward, run.State);
            Assert.IsTrue(run.RewardOffers.Any(t => t.Rarity == TechniqueRarity.Rare), "an elite's pick holds a rare technique");
            Assert.IsFalse(run.Fought.Contains(run.CurrentEncounter), "the elite itself isn't a faction for the Necromancer");
        }

        [Test]
        public void EliteEncounters_MoreOfTheSameEnemies()
        {
            for (var seed = 0; seed < 30; seed++)
            {
                int Count(Encounter e, UnitStats s) =>
                    e.CreateBattle(ArmyMember.StartingArmy(), new System.Random(seed)).Living(Team.Enemy).Count(u => u.Stats == s);
                Assert.AreEqual(2, Count(Encounter.EliteWolves, UnitStats.PackLeader), $"seed {seed}");
                Assert.AreEqual(4, Count(Encounter.EliteWolves, UnitStats.Wolf), $"seed {seed}");
                Assert.AreEqual(4, Count(Encounter.EliteGoblins, UnitStats.GoblinBomber));
                Assert.AreEqual(3, Count(Encounter.EliteGoblins, UnitStats.Goblin));
                Assert.AreEqual(3, Count(Encounter.EliteBandits, UnitStats.Crossbowman), $"seed {seed}");
                Assert.AreEqual(2, Count(Encounter.EliteBandits, UnitStats.Cutpurse));
                Assert.AreEqual(2, Count(Encounter.EliteBandits, UnitStats.Brute), $"seed {seed}");
                Assert.AreEqual(4, Count(Encounter.EliteElementals, UnitStats.Elemental), $"seed {seed}");
            }

            foreach (var faction in Encounter.Regular)
                Assert.AreEqual(faction, Encounter.EliteOf(faction).Base);
        }

        [Test]
        public void Map_BothScenarios_AtSteps4And8_NextToABattle()
        {
            var shrineSteps = new System.Collections.Generic.HashSet<int>();
            for (var seed = 0; seed < 50; seed++)
            {
                var run = NewRun(seed);
                int StepOf(Encounter e) => Enumerable.Range(0, Run.Steps).Single(i => run.Map[i].Any(n => n.Kind == NodeKind.Ritual && n.Encounter == e));
                var shrine = StepOf(Encounter.MoonShrine);
                var ritual = StepOf(Encounter.BreakRitual);
                CollectionAssert.AreEquivalent(new[] { 3, 7 }, new[] { shrine, ritual }, $"seed {seed}");
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
        public void BreakRitual_Won_PickATechniqueForAUnit()
        {
            var run = RunAtScenarioStep(4, Encounter.BreakRitual);
            var step = run.StepIndex;
            run.ChooseNode(ScenarioIndex(run, Encounter.BreakRitual));
            WinCurrentBattle(run, 3);
            Assert.IsTrue(run.CurrentBattle.TotemsBroken);
            Assert.Greater(run.FinishBattle(), 0);
            Assert.AreEqual(RunState.Reward, run.State);
            Assert.AreEqual(Run.RewardCount, run.RewardOffers.Distinct().Count());

            var technique = run.RewardOffers[0];
            var member = run.Army.First(technique.CanLearn);
            var level = member.Level;
            Assert.IsTrue(run.TakeReward(technique, member));
            Assert.IsTrue(member.Knows(technique));
            Assert.AreEqual(level + 1, member.Level);
            Assert.AreEqual(RunState.Map, run.State);
            Assert.AreEqual(step + 1, run.StepIndex);
            Assert.AreEqual(0, run.RewardOffers.Count);
        }

        [Test]
        public void Reward_OffersOnlyLearnable_AndOnlyToUnitsThatCanLearn()
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var run = NewRun(seed);
                run.ChooseNode(0);
                WinCurrentBattle(run, 3);
                run.FinishBattle();
                Assert.AreEqual(RunState.Reward, run.State);
                Assert.AreEqual(Run.RewardCount, run.RewardOffers.Distinct().Count());
                Assert.IsTrue(run.RewardOffers.All(t => run.Army.Any(t.CanLearn)), $"seed {seed}");
                var technique = run.RewardOffers.First(t => !t.IsShared);
                var outsider = run.Army.FirstOrDefault(m => !technique.CanLearn(m));
                if (outsider != null)
                    Assert.IsFalse(run.TakeReward(technique, outsider), "another line's technique");
            }
        }

        [Test]
        public void Reward_SkipPaysGold_AndMovesOn()
        {
            var run = NewRun(1);
            run.ChooseNode(0);
            WinCurrentBattle(run, 3);
            run.FinishBattle();
            var gold = run.Gold;
            run.SkipReward();
            Assert.AreEqual(gold + Run.SkipGold, run.Gold);
            Assert.AreEqual(RunState.Map, run.State);
            Assert.AreEqual(1, run.StepIndex);
            Assert.IsTrue(run.Army.All(m => m.Level == 0));
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
                while (run.State == RunState.Map && run.StepIndex < 8)
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
        public void Necromancer_RaisesTheFactionsYouBeat()
        {
            var fought = new[] { Encounter.Wolves, Encounter.Goblins };
            var b = Encounter.Necromancer.CreateBattle(ArmyMember.StartingArmy(), new System.Random(3), fought);
            var minions = b.Units.Where(u => u.Team == Team.Enemy && !u.Stats.CanRaise).ToList();
            Assert.AreEqual(1, minions.Count(u => u.Stats == UnitStats.Raised(UnitStats.Wolf)), "one of each faction on the small board");
            Assert.AreEqual(1, minions.Count(u => u.Stats == UnitStats.Raised(UnitStats.GoblinBomber)));
            Assert.AreEqual(2, minions.Count(u => u.Stats == UnitStats.Skeleton), "topped up with skeletons to 4");
            Assert.IsTrue(minions.All(u => u.Stats.LeavesRemains), "they can all be raised again");

            var none = Encounter.Necromancer.CreateBattle(ArmyMember.StartingArmy(), new System.Random(3));
            Assert.AreEqual(4, none.Units.Count(u => u.Stats == UnitStats.Skeleton), "skeletons when nothing was beaten");
        }

        [Test]
        public void Layouts_FollowTheirRules()
        {
            foreach (var enc in Encounter.Regular.Concat(Encounter.Elites).Concat(Encounter.Bosses).Concat(new[] { Encounter.MoonShrine, Encounter.BreakRitual }))
            {
                for (var seed = 0; seed < 30; seed++)
                {
                    var b = enc.CreateBattle(ArmyMember.StartingArmy(), new System.Random(seed), Encounter.Regular);
                    for (var x = 0; x < Encounter.Size; x++)
                    for (var y = 0; y < Battle.PlayerZoneRows; y++)
                        Assert.IsFalse(b.Board.IsObstacle(new GridPos(x, y)), $"{enc.Name}: player rows clear");
                    Assert.IsTrue(b.Units.Any(u => u.Team == Team.Enemy), enc.Name);
                    // At most 2 rocks unless the layout needs more (the broken ridge, one cover rock per elite crossbowman), none touching.
                    var rocks = b.Board.AllTiles().Where(b.Board.IsObstacle).ToList();
                    var max = enc.Layout == "Ridge" || enc == Encounter.EliteBandits ? 3 : 2;
                    Assert.LessOrEqual(rocks.Count, max, $"{enc.Name}: rocks");
                    Assert.IsFalse(rocks.Any(r => rocks.Any(o => o != r && System.Math.Abs(o.X - r.X) <= 1 && System.Math.Abs(o.Y - r.Y) <= 1)), $"{enc.Name}: rocks touch");
                }
            }

            // The broken ridge is always three rocks, one every other column.
            var ridge = Encounter.Goblins.CreateBattle(ArmyMember.StartingArmy(), new System.Random(5));
            Assert.AreEqual(Encounter.Size - 3, Enumerable.Range(0, Encounter.Size).Count(x => ridge.Board.IsObstacle(new GridPos(x, 3))));
        }

        [Test]
        public void Run_GoldScalesWithSurvivors_ThenTechniquePick()
        {
            var run = NewRun(1);
            run.ChooseNode(0);
            WinCurrentBattle(run, 2);
            Assert.AreEqual(Run.GoldFor(2), run.FinishBattle());
            Assert.AreEqual(20, run.Gold);
            Assert.AreEqual(RunState.Reward, run.State);
            Assert.AreEqual(0, run.StepIndex);
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
        public void Shop_BuyTeachesTheTechnique_AndSpendsGold()
        {
            var run = RunInShop();
            Assert.IsTrue(run.Offers.All(t => run.Army.Any(t.CanLearn)), "only learnable techniques");
            var offer = run.Offers.OrderBy(o => o.Price).First();
            var buyer = run.Army.First(offer.CanLearn);
            var gold = run.Gold;
            Assume.That(gold >= offer.Price);
            Assert.IsTrue(run.Buy(offer, buyer));
            Assert.IsTrue(buyer.Knows(offer));
            Assert.AreEqual(1, buyer.Level);
            Assert.AreEqual(gold - offer.Price, run.Gold);
            Assert.IsFalse(run.Offers.Contains(offer), "bought techniques leave the shop");
        }

        [Test]
        public void Shop_ItemReplacesSlot_AndSpendsGold()
        {
            var run = RunInShop();
            Assert.AreEqual(Run.ShopItemCount, run.ItemOffers.Distinct().Count());
            var warrior = run.Army[0];
            var items = run.ItemOffers.OrderBy(o => o.Price).ToList();
            Assert.IsTrue(run.BuyItem(items[0], warrior));
            Assert.AreEqual(items[0], warrior.Equipment);
            Assert.AreEqual(0, warrior.Level, "items don't level a unit");
            Assert.IsFalse(run.ItemOffers.Contains(items[0]), "bought items leave the shop");
            var gold = run.Gold;
            if (run.BuyItem(items[1], warrior))
            {
                Assert.AreEqual(items[1], warrior.Equipment, "one slot: the new item replaces the old");
                Assert.AreEqual(gold - items[1].Price, run.Gold);
            }
        }

        [Test]
        public void Shop_ForgetCostsGold_KeepsTheLevel()
        {
            var run = RunInShop();
            var mage = run.Army[0];
            mage.Stats = UnitStats.Apprentice;
            mage.Learn(Technique.Ward);
            mage.Learn(Technique.Mend);
            var gold = run.Gold;
            Assert.IsTrue(run.Forget(mage, Technique.Ward));
            Assert.AreEqual(gold - Run.ForgetPrice, run.Gold);
            Assert.IsFalse(mage.Knows(Technique.Mend), "Mend needs Ward");
            Assert.AreEqual(2, mage.Level);
            Assert.IsFalse(run.Forget(mage, Technique.Ward), "not known any more");
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
            run.SkipReward();
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

        // ---------- Draft ----------

        [Test]
        public void Draft_ThreeDifferentBaseUnits()
        {
            var run = new Run(1);
            Assert.AreEqual(RunState.Draft, run.State);
            Assert.IsFalse(run.Draft(UnitStats.Warrior, UnitStats.Warrior, UnitStats.Rogue), "no duplicates");
            Assert.IsFalse(run.Draft(UnitStats.Warrior, UnitStats.Wolf, UnitStats.Rogue), "player units only");
            Assert.IsFalse(run.Draft(UnitStats.Warrior, UnitStats.Rogue), "exactly three");
            Assert.IsTrue(run.Draft(UnitStats.Apprentice, UnitStats.Sapper, UnitStats.Rogue));
            Assert.AreEqual(RunState.Map, run.State);
            Assert.AreEqual(3, run.Army.Count);
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
        public void Event_Cart_TeachesATechniqueOrGivesAnItem()
        {
            var run = RunAtEvent(EventKind.AbandonedCart);
            Assert.IsTrue(run.CartTechnique == null ^ run.CartItem == null, "a technique or an item, never both");
            if (run.CartTechnique != null)
            {
                var technique = run.CartTechnique;
                var member = run.Army.First(technique.CanLearn);
                Assert.IsTrue(run.TakeCart(member));
                Assert.IsTrue(member.Knows(technique));
                Assert.AreEqual(1, member.Level);
            }
            else
            {
                var item = run.CartItem;
                Assert.IsTrue(run.TakeCart(run.Army[0]));
                Assert.AreEqual(item, run.Army[0].Equipment);
            }
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

        // ---------- Walk up and attack ----------

        [Test]
        public void AttackSpot_CheapestTileInReach()
        {
            var b = Started(Army(UnitStats.Rogue), new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)) });
            var rogue = Player(b);
            var wolf = Enemy(b);
            var spot = b.FindAttackSpot(rogue, wolf);
            Assert.IsTrue(spot.HasValue);
            Assert.AreEqual(new GridPos(3, 3), spot.Value.tile);
            Assert.AreEqual(2, spot.Value.cost, "two moves, and walking costs no AP");
        }

        [Test]
        public void AttackSpot_NullWhenTooFarOrShortOfAp()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)) });
            Assert.IsNull(b.FindAttackSpot(Player(b), Enemy(b)), "one move can't reach (3,3)");

            var close = Started(Army(UnitStats.Warrior), new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 3)) });
            Assert.IsNotNull(close.FindAttackSpot(Player(close), Enemy(close)));
            Player(close).Ap = 0;
            Assert.IsNull(close.FindAttackSpot(Player(close), Enemy(close)), "no AP left for the attack");
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
        public void CanStillAct_FalseWhenDoneOrOutOfApAndMoves()
        {
            var b = Started(Army(UnitStats.Warrior, UnitStats.Rogue), new[] { new GridPos(0, 0), new GridPos(5, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            var warrior = Player(b, 0);
            var rogue = Player(b, 1);
            Assert.IsTrue(b.CanStillAct(warrior));
            b.EndUnitTurn(warrior);
            Assert.IsFalse(b.CanStillAct(warrior));
            rogue.Ap = 0;
            Assert.IsTrue(b.CanStillAct(rogue), "it can still walk");
            rogue.Moves = 0;
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
            Assert.IsNull(b.PlayerMove(rogue, new GridPos(3, 2)), "it attacked, so it can't move this turn");
            b.EndPlayerTurn();
            Assert.IsNotNull(b.PlayerMove(rogue, new GridPos(3, 2)), "the pile doesn't block");
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
        public void SoulHarvest_YourFallenRiseAsBoneVersionsWithOnlyTheirAttack()
        {
            var b = Started(Army(UnitStats.Warrior, UnitStats.Archer), new[] { new GridPos(0, 0), new GridPos(3, 1) },
                new[] { (UnitStats.Necromancer, new GridPos(3, 4)) });
            var archer = Player(b, 1);
            archer.Hp = 1;
            b.EndPlayerTurn();
            Assert.IsFalse(archer.IsAlive, "bolted");
            Assert.IsTrue(archer.IsRemains, "left a bone pile");

            var raise = b.EndPlayerTurn().OfType<RaiseEvent>().Single();
            Assert.AreEqual(archer, raise.From);
            var bone = raise.Raised;
            Assert.AreEqual(Team.Enemy, bone.Team);
            Assert.AreEqual("Bone Archer", bone.Stats.Name);
            Assert.AreEqual(UnitStats.Archer.Damage, bone.Stats.Damage);
            Assert.AreEqual(0, bone.Stats.Abilities.Count, "only its basic attack");
            Assert.AreEqual(archer.Pos, bone.Pos);
            Assert.IsFalse(archer.IsRemains, "the pile is used up");
        }

        [Test]
        public void CorpseBlast_PunishesStandingOnAPile()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Necromancer, new GridPos(3, 5)), (UnitStats.Skeleton, new GridPos(3, 2)) });
            var necro = Enemy(b, 0);
            var skeleton = Enemy(b, 1);
            skeleton.Hp = 0;
            var warrior = Player(b);
            b.PlayerMove(warrior, new GridPos(3, 2));
            Assert.AreEqual(IntentKind.CorpseBlast, EnemyAI.PredictIntent(b, necro).Kind);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<CorpseBlastEvent>().Any());
            Assert.AreEqual(warrior.MaxHp - Battle.CorpseBlastDamage, warrior.Hp);
            Assert.IsFalse(skeleton.IsRemains, "the pile is used up");
        }

        [Test]
        public void Colossus_GiantCantBePinnedOrBashed()
        {
            var b = Started(Army(UnitStats.Archer, UnitStats.Warrior), new[] { new GridPos(3, 1), new GridPos(0, 0) },
                new[] { (UnitStats.Ogre, new GridPos(3, 4)) });
            var archer = Player(b, 0);
            var warrior = Player(b, 1);
            var giant = Enemy(b);
            Assert.IsNotNull(b.PlayerUseAction(archer, ActionKind.PinningShot, giant.Pos));
            Assert.AreEqual(giant.MaxHp - Battle.PinDamage, giant.Hp, "the shot still hurts");
            Assert.IsFalse(giant.IsSnared);
            warrior.Pos = new GridPos(3, 3);
            Assert.IsFalse(b.CanUseAction(warrior, ActionKind.ShieldBash, giant.Pos));
        }

        [Test]
        public void Wrath_GiantActsTwiceAtHalfHp()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(3, 1) }, new[] { (UnitStats.Ogre, new GridPos(3, 4)) });
            var warrior = Player(b);
            var giant = Enemy(b);
            giant.Hp = giant.MaxHp / 2 + 1;
            warrior.Pos = new GridPos(3, 3);
            var hit = b.PlayerAttack(warrior, giant);
            Assert.IsTrue(hit.OfType<StatusEvent>().Any(s => s.Kind == StatusKind.Wrath));
            Assert.AreEqual(2, giant.MaxAp);
            var events = b.EndPlayerTurn();
            Assert.AreEqual(2, events.OfType<SmashEvent>().Count(), "two smashes in one turn");
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
            Enemy(b, 0).Hp = UnitStats.Rogue.Damage;
            b.PlayerAttack(rogue, Enemy(b, 0));
            Assert.AreEqual(Phase.Victory, b.Phase);
        }

        [Test]
        public void Necromancer_BacksAwayFromCloseUnits()
        {
            var b = Started(Army(UnitStats.Warrior), new[] { new GridPos(4, 1) },
                new[] { (UnitStats.Necromancer, new GridPos(4, 2)) });
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
            warrior.Pos = new GridPos(3, 3);
            archer.Pos = new GridPos(6, 5);
            Assert.AreEqual(IntentKind.Slam, EnemyAI.PredictIntent(b, Enemy(b)).Kind);
            var events = b.EndPlayerTurn();
            var slam = events.OfType<AreaAttackEvent>().Single(a => a.Kind == "slam");
            Assert.AreEqual(2, slam.Hits.Count);
            Assert.AreEqual(warrior.MaxHp - Battle.SlamDamage, warrior.Hp);
            var pushes = events.OfType<PushEvent>().Where(p => p.Pusher == Enemy(b)).ToList();
            Assert.IsTrue(pushes.Any(p => p.Unit == warrior) && pushes.Any(p => p.Unit == archer));
            Assert.IsFalse(events.OfType<SmashEvent>().Any(), "one action a turn");
        }

        [Test]
        public void Giant_SmashesWhenThatDealsMore()
        {
            var b = Started(Army(UnitStats.Warrior, UnitStats.Archer), new[] { new GridPos(0, 0), new GridPos(1, 0) },
                new[] { (UnitStats.Ogre, new GridPos(3, 5)) });
            Player(b, 0).Pos = new GridPos(3, 4);
            Player(b, 1).Pos = new GridPos(6, 5);
            // Smash: 5 to the adjacent warrior. Slam: 3 each to both = 6, so it slams.
            Assert.AreEqual(IntentKind.Slam, EnemyAI.PredictIntent(b, Enemy(b)).Kind);
            Player(b, 1).Pos = new GridPos(5, 6);
            // Both adjacent now: smash 10 beats slam 6.
            Assert.AreEqual(IntentKind.Smash, EnemyAI.PredictIntent(b, Enemy(b)).Kind);
            var events = b.EndPlayerTurn();
            Assert.AreEqual(1, events.OfType<SmashEvent>().Count());
            Assert.IsFalse(events.OfType<AreaAttackEvent>().Any(a => a.Kind == "slam"));
        }

        [Test]
        public void Necromancer_RaisesEveryPileInRangeAtOnce()
        {
            var b = Started(Army(UnitStats.Rogue), new[] { new GridPos(0, 1) },
                new[]
                {
                    (UnitStats.Necromancer, new GridPos(4, 7)), (UnitStats.Skeleton, new GridPos(3, 5)),
                    (UnitStats.Skeleton, new GridPos(5, 6)), (UnitStats.Skeleton, new GridPos(0, 3)),
                });
            foreach (var s in b.Units.Where(u => u.Stats == UnitStats.Skeleton))
                s.Hp = 0;
            var events = b.EndPlayerTurn();
            Assert.AreEqual(2, events.OfType<RaiseEvent>().Count(), "the two within 4 tiles, not the far one");
            Assert.IsFalse(b.Units.Single(u => u.Pos == new GridPos(0, 3)).IsAlive);
            Assert.IsFalse(events.OfType<AttackEvent>().Any(), "raising was its action");
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
            ogre.Moves = ogre.MaxMoves;
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
                Assert.IsTrue(b.IsOver, $"{encounter.Name} should end against a passive player: " + string.Join("; ", b.Units.Where(u => u.IsAlive && u.IsPlaced)) + " rocks " + string.Join(",", b.Board.AllTiles().Where(b.Board.IsObstacle)));
            }
        }
    }
}
