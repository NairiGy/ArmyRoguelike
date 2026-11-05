using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>Banners (army-wide rules for the run) and unit weight.</summary>
    public class BannerTests
    {
        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        /// <summary>A started battle with these banners, players standing where told.</summary>
        private static Battle Setup(Banner[] banners, UnitStats[] army, GridPos[] at, params (UnitStats, GridPos)[] enemies)
        {
            var b = new Battle(new Board(8, 8), army.Select(s => new ArmyMember(s)), enemies);
            foreach (var banner in banners)
                b.AddBanner(banner);
            for (var i = 0; i < army.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), P(i, 0)));
            Assert.IsTrue(b.StartBattle());
            for (var i = 0; i < army.Length; i++)
                Player(b, i).Pos = at[i];
            return b;
        }

        private static readonly Banner[] None = new Banner[0];

        // ---------- Weight ----------

        [Test]
        public void Weight_HugeUnitsShrugOffAPlainPush()
        {
            var b = Setup(None, new[] { UnitStats.Warrior }, new[] { P(3, 1) }, (UnitStats.Brute, P(3, 2)), (UnitStats.Wolf, P(7, 7)));
            var brute = Enemy(b);
            Assert.AreEqual(0, b.PushDistance(brute, Player(b)));
            b.PlayerUseAction(Player(b), ActionKind.ShieldBash, brute.Pos);
            Assert.AreEqual(P(3, 2), brute.Pos, "doesn't budge");
            Assert.AreEqual(UnitStats.Brute.MaxHp - Battle.BashDamage, brute.Hp, "only the bash: no collision damage");
        }

        [Test]
        public void Weight_SmallUnitsStopAtWhatBlocksThem()
        {
            var b = Setup(None, new[] { UnitStats.Warrior }, new[] { P(3, 1) }, (UnitStats.Wolf, P(3, 2)), (UnitStats.Crossbowman, P(3, 4)));
            var wolf = Enemy(b);
            b.PlayerUseAction(Player(b), ActionKind.ShieldBash, wolf.Pos);
            Assert.AreEqual(P(3, 3), wolf.Pos, "one tile, then slams into the crossbowman");
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.BashDamage - Battle.CollisionDamage, wolf.Hp);
        }

        // ---------- Battering Ram ----------

        [Test]
        public void BatteringRam_YourPushesGoFurtherAndHurt()
        {
            var b = Setup(new[] { Banner.BatteringRam }, new[] { UnitStats.Warrior }, new[] { P(3, 1) },
                (UnitStats.Crossbowman, P(3, 2)), (UnitStats.Wolf, P(7, 7)));
            var crossbow = Enemy(b);
            b.PlayerUseAction(Player(b), ActionKind.ShieldBash, crossbow.Pos);
            Assert.AreEqual(P(3, 4), crossbow.Pos, "medium: two tiles");
            Assert.AreEqual(UnitStats.Crossbowman.MaxHp - Battle.BashDamage - Banners.RamDamage, crossbow.Hp);

            var huge = Setup(new[] { Banner.BatteringRam }, new[] { UnitStats.Warrior }, new[] { P(3, 1) },
                (UnitStats.Brute, P(3, 2)), (UnitStats.Wolf, P(7, 7)));
            var brute = Enemy(huge);
            huge.PlayerUseAction(Player(huge), ActionKind.ShieldBash, brute.Pos);
            Assert.AreEqual(P(3, 3), brute.Pos, "huge: one tile");
            Assert.AreEqual(UnitStats.Brute.MaxHp - Battle.BashDamage - Banners.RamDamage, brute.Hp);
        }

        [Test]
        public void BatteringRam_EnemyPushesAreUnchanged()
        {
            var b = Setup(new[] { Banner.BatteringRam }, new[] { UnitStats.Warrior }, new[] { P(3, 2) }, (UnitStats.Brute, P(3, 3)));
            var warrior = Player(b);
            b.EndPlayerTurn();
            Assert.AreEqual(P(3, 1), warrior.Pos, "the Bull Rush still pushes one tile");
            Assert.AreEqual(warrior.MaxHp - UnitStats.Brute.Damage, warrior.Hp, "and adds no damage");
        }

        // ---------- Vampiric ----------

        [Test]
        public void Vampiric_AttacksHealAndTheSunHuntsYou()
        {
            var b = Setup(new[] { Banner.Vampiric }, new[] { UnitStats.Archer }, new[] { P(3, 0) }, (UnitStats.Wolf, P(3, 3)), (UnitStats.Wolf, P(7, 7)));
            Assert.IsTrue(b.Omens.Contains((Omen.Sun, Team.Enemy)), "Curse of the Sun");
            var archer = Player(b);
            archer.Hp = 3;
            b.PlayerAttack(archer, Enemy(b));
            Assert.AreEqual(3 + Banners.VampiricHeal, archer.Hp);
        }

        [Test]
        public void Vampiric_NoHealAboveMaxOrForEnemies()
        {
            var b = Setup(new[] { Banner.Vampiric }, new[] { UnitStats.Archer }, new[] { P(3, 0) }, (UnitStats.Wolf, P(3, 3)), (UnitStats.Wolf, P(7, 7)));
            var archer = Player(b);
            b.PlayerAttack(archer, Enemy(b));
            Assert.AreEqual(archer.MaxHp, archer.Hp);

            var wolfFight = Setup(new[] { Banner.Vampiric }, new[] { UnitStats.Warrior }, new[] { P(3, 2) }, (UnitStats.Wolf, P(3, 3)));
            var wolf = Enemy(wolfFight);
            wolf.Hp = 3;
            wolfFight.EndPlayerTurn();
            Assert.LessOrEqual(wolf.Hp, 3, "enemies don't heal from your banner");
        }

        // ---------- Third Wheel ----------

        [Test]
        public void ThirdWheel_ExtraApAndMove()
        {
            var b = Setup(new[] { Banner.ThirdWheel }, new[] { UnitStats.Warrior, UnitStats.Rogue }, new[] { P(3, 1), P(5, 1) }, (UnitStats.Wolf, P(7, 7)));
            Assert.AreEqual(2, Player(b, 0).Ap);
            Assert.AreEqual(2, Player(b, 0).Moves);
            Assert.AreEqual(3, Player(b, 1).Moves, "Swift and Third Wheel add up");
            Assert.AreEqual(1, Enemy(b).MaxAp, "enemies don't get it");
        }

        private static Run RunWithStartBanner(Banner banner)
        {
            for (var seed = 0; seed < 500; seed++)
            {
                var run = new Run(seed);
                if (run.StartBanners.Contains(banner))
                {
                    Assert.IsTrue(run.ChooseStartBanner(banner));
                    return run;
                }
            }

            Assert.Fail($"no seed offers {banner}");
            return null;
        }

        [Test]
        public void StartBanner_ThreeDifferentChoices_PickedOnce()
        {
            var run = new Run(7);
            Assert.AreEqual(Banners.StartChoices, run.StartBanners.Distinct().Count());
            Assert.IsFalse(run.StartBannerChosen);
            Assert.IsTrue(run.ChooseStartBanner(run.StartBanners[0]));
            Assert.IsFalse(run.ChooseStartBanner(run.StartBanners[1]), "only one");
            CollectionAssert.AreEqual(new[] { run.StartBanners[0] }, run.Banners);
        }

        [Test]
        public void ThirdWheel_AtTheStart_DraftsTwo()
        {
            var run = RunWithStartBanner(Banner.ThirdWheel);
            Assert.AreEqual(2, run.DraftCount);
            Assert.IsFalse(run.Draft(UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue));
            Assert.IsTrue(run.Draft(UnitStats.Warrior, UnitStats.Archer));
        }

        /// <summary>A run in a shop at step 2, with Warrior, Archer and Rogue (found by trying seeds).</summary>
        private static Run RunInShop(Banner? start = null)
        {
            for (var seed = 0; seed < 500; seed++)
            {
                var run = new Run(seed);
                if (start.HasValue && !run.ChooseStartBanner(start.Value))
                    continue;
                run.Draft(UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue);
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

        private static void WinCurrentBattle(Run run, int survivors)
        {
            var b = run.StartBattle();
            foreach (var u in b.Units.Where(u => u.Team == Team.Player && !u.IsObjective))
                b.Place(u, b.Board.AllTiles().First(p => b.CanPlace(u, p) && b.UnitAt(p) == null));
            b.StartBattle();
            var players = b.Units.Where(u => u.Team == Team.Player && !u.IsObjective && !u.IsMercenary).ToList();
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

        [Test]
        public void ThirdWheel_BoughtWithAFullArmy_ChooseWhoStays()
        {
            var run = RunInShop();
            run.ShopBanner = Banner.ThirdWheel;
            run.Gold = 100;
            Assert.IsTrue(run.BuyBanner());
            Assert.AreEqual(RunState.Dismiss, run.State);
            Assert.IsFalse(run.Dismiss(run.Army.Take(1).ToList()), "must keep exactly 2");
            var keep = run.Army.Take(2).ToList();
            Assert.IsTrue(run.Dismiss(keep));
            CollectionAssert.AreEqual(keep, run.Army);
            Assert.AreEqual(RunState.Shop, run.State, "back to the shop");
            Assert.IsFalse(run.CanBuyRecruit, "no room for a recruit");
        }

        [Test]
        public void ShopBanner_OnlyOnesYouDontHold()
        {
            var run = RunInShop(Banner.Loser);
            Assert.IsTrue(run.ShopBanner.HasValue);
            Assert.AreNotEqual(Banner.Loser, run.ShopBanner.Value);
            run.Gold = Banners.Price - 1;
            Assert.IsFalse(run.CanBuyBanner);
        }

        // ---------- Loser ----------

        [Test]
        public void Loser_TheFallenGrowForTheRun()
        {
            Run run = null;
            for (var seed = 0; seed < 500 && run == null; seed++)
            {
                var r = new Run(seed);
                if (r.ChooseStartBanner(Banner.Loser))
                    run = r;
            }

            run.Draft(UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue);
            run.ChooseNode(0);
            WinCurrentBattle(run, 2);
            run.FinishBattle();
            var rogue = run.Army[2];
            Assert.AreEqual(Banners.LoserHp, rogue.BonusMaxHp);
            Assert.AreEqual(Banners.LoserDamage, rogue.BonusDamage);
            Assert.AreEqual(0, run.Army[0].BonusDamage, "survivors don't");
            CollectionAssert.AreEqual(new[] { rogue }, run.LastLoserGains);
        }

        // ---------- Mercenaries ----------

        [Test]
        public void Mercenaries_HireForOneFight_NotASurvivor()
        {
            var run = RunWithStartBanner(Banner.Mercenaries);
            run.Draft(UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue);
            run.Gold = 20;
            run.ChooseNode(0);
            Assert.IsTrue(run.MercPending);
            Assert.AreEqual(Banners.MercOffers, run.MercOffers.Distinct().Count());
            var hire = run.MercOffers[0];
            Assert.IsTrue(run.HireMerc(hire));
            Assert.AreEqual(20 - Banners.MercPrice, run.Gold);
            Assert.IsFalse(run.MercPending);

            var b = run.StartBattle();
            Assert.AreEqual(4, b.Units.Count(u => u.Team == Team.Player));
            Assert.AreEqual(hire, b.Units.Single(u => u.IsMercenary).Stats);
            Assert.AreEqual(3, run.Army.Count, "the army itself is unchanged");
        }

        [Test]
        public void Mercenaries_GoneAfterTheFight_AndDontCountForGold()
        {
            var run = RunWithStartBanner(Banner.Mercenaries);
            run.Draft(UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue);
            run.Gold = 20;
            run.ChooseNode(0);
            run.HireMerc(run.MercOffers[0]);
            WinCurrentBattle(run, 3);
            Assert.AreEqual(3, run.CurrentBattle.LivingArmy.Count(), "the merc isn't counted");
            run.FinishBattle();
            Assert.IsNull(run.Merc);
        }

        [Test]
        public void Mercenaries_DecliningStartsWithoutOne()
        {
            var run = RunWithStartBanner(Banner.Mercenaries);
            run.Draft(UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue);
            run.ChooseNode(0);
            run.DeclineMerc();
            Assert.IsFalse(run.MercPending);
            Assert.AreEqual(3, run.StartBattle().Units.Count(u => u.Team == Team.Player));
        }

        // ---------- Elite reward ----------

        [Test]
        public void EliteBanner_TakenInsteadOfATechnique()
        {
            var run = RunInShop();
            run.LeaveShop();
            // Pretend the last fight was an elite with a banner on offer.
            typeof(Run).GetProperty(nameof(Run.State)).SetValue(run, RunState.Reward);
            run.RewardBanner = Banner.ThirdWheel;
            Assert.IsTrue(run.TakeRewardBanner());
            CollectionAssert.Contains(run.Banners, Banner.ThirdWheel);
            Assert.AreEqual(RunState.Dismiss, run.State, "three units: choose who stays first");
            Assert.IsTrue(run.Dismiss(run.Army.Take(2).ToList()));
            Assert.AreEqual(RunState.Map, run.State);
        }
    }
}
