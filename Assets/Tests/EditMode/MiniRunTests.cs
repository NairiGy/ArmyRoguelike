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
            var armored = new Unit(0, Team.Player, UnitStats.Warrior, Equipment.HeavyArmor);
            Assert.AreEqual(18, armored.MaxHp);
            Assert.AreEqual(18, armored.Hp);
            Assert.AreEqual(2, armored.MaxAp);

            var berserk = new Unit(1, Team.Player, UnitStats.Rogue, Equipment.BerserkerAxe);
            Assert.AreEqual(2, berserk.MaxHp);
            Assert.AreEqual(6, berserk.BaseDamage);

            var archer = new Unit(2, Team.Player, UnitStats.Archer, Equipment.Longbow);
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

        [Test]
        public void Run_GoldScalesWithSurvivors_AndOpensShop()
        {
            var run = new Run(1);
            WinCurrentBattle(run, 2);
            Assert.AreEqual(Run.GoldFor(2), run.FinishBattle());
            Assert.AreEqual(20, run.Gold);
            Assert.AreEqual(RunState.Shop, run.State);
            Assert.AreEqual(Run.ShopSize, run.Offers.Count);
            Assert.AreEqual(Run.ShopSize, run.Offers.Distinct().Count());
        }

        [Test]
        public void Shop_BuyReplacesSlot_AndSpendsGold()
        {
            var run = new Run(3);
            WinCurrentBattle(run, 3);
            run.FinishBattle();
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
            var run = new Run(5);
            WinCurrentBattle(run, 1);
            run.FinishBattle();
            // Spend gold until nothing more fits, then every remaining offer must be refused.
            var archer = run.Army[1];
            while (run.Offers.Any(o => run.CanBuy(o, archer)))
                run.Buy(run.Offers.First(o => run.CanBuy(o, archer)), archer);
            foreach (var offer in run.Offers)
                Assert.IsFalse(run.Buy(offer, archer));
        }

        [Test]
        public void Run_ThreeBattles_ThenWon()
        {
            var run = new Run(7);
            for (var i = 0; i < 3; i++)
            {
                Assert.AreEqual(Encounter.MiniRun[i], run.CurrentEncounter);
                WinCurrentBattle(run, 3);
                run.FinishBattle();
                if (i < 2)
                {
                    Assert.AreEqual(RunState.Shop, run.State);
                    run.LeaveShop();
                }
            }

            Assert.AreEqual(RunState.Won, run.State);
        }

        [Test]
        public void Run_DefeatEndsRun()
        {
            var run = new Run(9);
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
            WinCurrentBattle(run, 1);
            run.FinishBattle();
            run.LeaveShop();
            var b = run.StartBattle();
            Assert.IsTrue(b.Units.Where(u => u.Team == Team.Player).All(u => u.Hp == u.MaxHp));
            Assert.AreEqual(3, b.Units.Count(u => u.Team == Team.Player));
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
            Assert.AreEqual(new GridPos(3, 2), intent.RockTile, "the rock lands on the target's ogre side");

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
            foreach (var encounter in Encounter.MiniRun)
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
