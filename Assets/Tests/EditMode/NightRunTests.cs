using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>Run-level rules from the overnight pass: cracks, mending and the events with a price.</summary>
    public class NightRunTests
    {
        private static Run NewRun(int seed)
        {
            var run = new Run(seed);
            Assert.IsTrue(run.Draft(UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue));
            return run;
        }

        /// <summary>Wins the current fight; army units from index <paramref name="survivors"/> on fall at the start.</summary>
        private static void WinCurrentBattle(Run run, int survivors)
        {
            var b = run.StartBattle();
            foreach (var u in b.Units.Where(u => u.Team == Team.Player && !u.IsObjective))
                b.Place(u, b.Board.AllTiles().First(p => b.CanPlace(u, p) && b.UnitAt(p) == null));
            b.StartBattle();
            var players = b.Units.Where(u => u.Team == Team.Player && !u.IsObjective).ToList();
            for (var i = survivors; i < players.Count; i++)
                players[i].Hp = 0;
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
                killer.Pos = new GridPos(e.Pos.X, e.Pos.Y - 1);
                b.PlayerAttack(killer, e);
            }

            Assert.AreEqual(Phase.Victory, b.Phase);
        }

        private static Run RunAtEvent(EventKind kind, System.Action<Run> before = null)
        {
            for (var seed = 0; seed < 4000; seed++)
            {
                var run = NewRun(seed);
                var ev = run.Map[1].ToList().FindIndex(n => n.Kind == NodeKind.Event);
                if (ev < 0)
                    continue;
                run.ChooseNode(0);
                WinCurrentBattle(run, 3);
                run.FinishBattle();
                run.SkipReward();
                before?.Invoke(run);
                run.ChooseNode(ev);
                if (run.CurrentEvent == kind)
                    return run;
            }

            Assert.Fail($"no seed with {kind} at step 2");
            return null;
        }

        // ---------- Cracks ----------

        [Test]
        public void Cracks_FallenUnitIsCracked_AndLosesMaxHp()
        {
            var run = NewRun(1);
            run.ChooseNode(0);
            WinCurrentBattle(run, 2);
            run.FinishBattle();
            var rogue = run.Army.Single(m => m.Stats == UnitStats.Rogue);
            Assert.AreEqual(1, rogue.Cracks, "the unit that fell is cracked");
            Assert.AreEqual(0, run.Army.Single(m => m.Stats == UnitStats.Warrior).Cracks, "survivors aren't");
            CollectionAssert.AreEqual(new[] { rogue }, run.LastCracked.ToArray());
            var unit = new Unit(0, Team.Player, UnitStats.Rogue, rogue);
            Assert.AreEqual(UnitStats.Rogue.MaxHp - Run.CrackHp, unit.MaxHp);
        }

        [Test]
        public void Cracks_LastCrackShattersTheUnit_AndItLeavesTheArmy()
        {
            var run = NewRun(1);
            var rogue = run.Army.Single(m => m.Stats == UnitStats.Rogue);
            rogue.Cracks = Run.CracksToShatter - 1;
            run.ChooseNode(0);
            WinCurrentBattle(run, 2);
            run.FinishBattle();
            Assert.IsFalse(run.Army.Contains(rogue));
            CollectionAssert.AreEqual(new[] { rogue }, run.LastShattered.ToArray());
            Assert.AreEqual(2, run.Army.Count);
        }

        [Test]
        public void Cracks_MendingAtTheShop_CostsGold()
        {
            for (var seed = 0; seed < 500; seed++)
            {
                var run = NewRun(seed);
                var shop = run.Map[1].ToList().FindIndex(n => n.Kind == NodeKind.Shop);
                if (shop < 0)
                    continue;
                run.ChooseNode(0);
                WinCurrentBattle(run, 2);
                run.FinishBattle();
                run.SkipReward();
                run.ChooseNode(shop);
                var rogue = run.Army.Single(m => m.Stats == UnitStats.Rogue);
                var gold = run.Gold;
                Assert.IsTrue(run.CanMend(rogue));
                Assert.IsTrue(run.Mend(rogue));
                Assert.AreEqual(0, rogue.Cracks);
                Assert.AreEqual(gold - Run.MendPrice, run.Gold);
                Assert.IsFalse(run.CanMend(rogue), "nothing left to mend");
                return;
            }

            Assert.Fail("no seed with a shop at step 2");
        }

        // ---------- Events with a price ----------

        [Test]
        public void Mason_MendsEveryCrack()
        {
            var run = RunAtEvent(EventKind.Mason, r => r.Army[0].Cracks = 2);
            Assert.IsTrue(run.VisitMason(null));
            Assert.IsTrue(run.Army.All(m => m.Cracks == 0));
            Assert.AreEqual(RunState.Map, run.State);
        }

        [Test]
        public void Mason_WithNoCracks_ReinforcesOneUnit()
        {
            var run = RunAtEvent(EventKind.Mason);
            var warrior = run.Army[0];
            var hp = warrior.BonusMaxHp;
            Assert.IsTrue(run.VisitMason(warrior));
            Assert.AreEqual(hp + Run.MasonHp, warrior.BonusMaxHp);
        }

        [Test]
        public void Altar_TeachesARareForACrack_NotIfItWouldShatter()
        {
            var run = RunAtEvent(EventKind.Altar);
            var altar = run.AltarTechnique;
            Assert.IsNotNull(altar);
            Assert.AreEqual(TechniqueRarity.Rare, altar.Rarity);
            var taker = run.Army.First(m => altar.CanLearn(m));
            taker.Cracks = Run.CracksToShatter - 1;
            Assert.IsFalse(run.CanTakeAltar(taker), "one crack from shattering");
            taker.Cracks = 0;
            Assert.IsTrue(run.TakeAltar(taker));
            Assert.IsTrue(taker.Knows(altar));
            Assert.AreEqual(1, taker.Cracks);
        }

        [Test]
        public void Sculptor_SwapsATechniqueForARareOfTheLine()
        {
            var run = RunAtEvent(EventKind.Sculptor, r => r.Army[0].Learn(Technique.Sweep));
            var warrior = run.Army[0];
            var level = warrior.Level;
            Assert.IsTrue(run.CanSculpt(warrior, Technique.Sweep));
            Assert.IsTrue(run.Sculpt(warrior, Technique.Sweep));
            Assert.IsFalse(warrior.Knows(Technique.Sweep));
            Assert.IsNotNull(run.SculptedInto);
            Assert.AreEqual(TechniqueRarity.Rare, run.SculptedInto.Rarity);
            Assert.AreEqual(UnitStats.Warrior, run.SculptedInto.Line);
            Assert.IsTrue(warrior.Knows(run.SculptedInto));
            Assert.AreEqual(level + 1, warrior.Level);
        }

        [Test]
        public void Bargain_GoldForACurse()
        {
            var run = RunAtEvent(EventKind.Bargain);
            var gold = run.Gold;
            Assert.IsTrue(run.Bargain(true));
            Assert.AreEqual(gold + Run.BargainGold, run.Gold);
            Assert.AreEqual(1, run.Curses.Count);
        }
    }
}
