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

        // ---------- Equipment ----------

        [Test]
        public void Equipment_ChangesStats()
        {
            var armored = new Unit(0, Team.Player, UnitStats.Warrior, new ArmyMember(UnitStats.Warrior, Equipment.HeavyArmor));
            Assert.AreEqual(18, armored.MaxHp);
            Assert.AreEqual(18, armored.Hp);
            Assert.AreEqual(2, armored.MaxAp);

            var berserk = new Unit(1, Team.Player, UnitStats.Rogue, new ArmyMember(UnitStats.Rogue, Equipment.BerserkerAxe));
            Assert.AreEqual(2, berserk.MaxHp);
            Assert.AreEqual(6, berserk.BaseDamage);

            var archer = new Unit(2, Team.Player, UnitStats.Archer, new ArmyMember(UnitStats.Archer, Equipment.Longbow));
            Assert.AreEqual(5, archer.Range);
        }

        [Test]
        public void Longbow_OnlyForRangedUnits()
        {
            Assert.IsTrue(Equipment.Longbow.CanEquip(UnitStats.Archer));
            Assert.IsFalse(Equipment.Longbow.CanEquip(UnitStats.Warrior));
        }

        [Test]
        public void EquippedArmy_StartsBattleWithItems()
        {
            var army = ArmyMember.StartingArmy();
            army[0].Equipment = Equipment.SwiftBoots;
            var b = Encounter.Wolves.CreateBattle(army);
            b.Place(Player(b, 0), new GridPos(0, 0));
            b.Place(Player(b, 1), new GridPos(1, 0));
            b.Place(Player(b, 2), new GridPos(2, 0));
            b.StartBattle();
            Assert.AreEqual(4, Player(b, 0).Ap);
        }

        // ---------- Run and shop ----------

        private static void WinCurrentBattle(Run run, int survivors)
        {
            var b = run.StartBattle();
            var x = 0;
            foreach (var u in b.Units.Where(u => u.Team == Team.Player))
                b.Place(u, new GridPos(x++, 0));
            b.StartBattle();
            var players = b.Units.Where(u => u.Team == Team.Player).ToList();
            for (var i = survivors; i < players.Count; i++)
                players[i].Hp = 0;
            foreach (var e in b.Units.Where(u => u.Team == Team.Enemy).ToList())
            {
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
                var run = new Run(seed);
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
                var run = new Run(seed);
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
                    break;
                case RunState.Shop:
                    run.LeaveShop();
                    break;
                case RunState.Event:
                    switch (run.CurrentEvent)
                    {
                        case EventKind.WanderingSapper:
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
        public void Map_SixSteps_TwoChoicesEach_OgreLast()
        {
            for (var seed = 0; seed < 50; seed++)
            {
                var run = new Run(seed);
                Assert.AreEqual(Run.Steps, run.Map.Count);
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
                Assert.AreEqual(Encounter.Ogre, last.Encounter);
            }
        }

        [Test]
        public void Run_GoldScalesWithSurvivors_ThenBackToMap()
        {
            var run = new Run(1);
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
        public void Shop_RecruitAddsSapper_UpToFour()
        {
            var run = RunInShop();
            Assert.IsTrue(run.Gold >= Run.RecruitPrice);
            Assert.IsTrue(run.BuyRecruit());
            Assert.AreEqual(4, run.Army.Count);
            Assert.AreEqual(UnitStats.Sapper, run.Army[3].Stats);
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
                var run = new Run(seed);
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
            var run = new Run(9);
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
            var run = new Run(11);
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
        public void Event_WanderingSapper_Joins()
        {
            var run = RunAtEvent(EventKind.WanderingSapper);
            run.AcceptRecruit();
            Assert.AreEqual(4, run.Army.Count);
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
        public void Event_Ambush_IsAWolfFightThatPaysExtra()
        {
            var run = RunAtEvent(EventKind.Ambush);
            run.StartAmbush();
            Assert.AreEqual(RunState.Battle, run.State);
            Assert.AreEqual(Encounter.Wolves, run.CurrentEncounter);
            var before = run.Gold;
            WinCurrentBattle(run, 3);
            run.FinishBattle();
            Assert.AreEqual(before + Run.GoldFor(3) + Run.AmbushBonusGold, run.Gold);
        }

        [Test]
        public void Bloodthirst_DamageCarriesToNextBattle()
        {
            var run = new Run(4);
            run.ChooseNode(0);
            var b = run.StartBattle();
            var x = 0;
            foreach (var u in b.Units.Where(u => u.Team == Team.Player))
                b.Place(u, new GridPos(x++, 0));
            b.StartBattle();
            var rogue = b.Units.Single(u => u.Stats == UnitStats.Rogue);
            foreach (var e in b.Units.Where(u => u.Team == Team.Enemy).ToList())
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
            var events = b.EndPlayerTurn();
            var smash = events.OfType<SmashEvent>().First();
            Assert.AreEqual(2, smash.Hits.Count);
            Assert.AreEqual(UnitStats.Ogre.Damage, smash.Hits[0].Damage);
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
            foreach (var encounter in Encounter.Regular.Append(Encounter.Ogre))
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
