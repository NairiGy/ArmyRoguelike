using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    public class BattleTests
    {
        private static Battle Make(UnitStats[] army, (UnitStats, GridPos)[] enemies, params GridPos[] obstacles) =>
            new Battle(new Board(8, 8, obstacles), army, enemies);

        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static Battle Started(UnitStats[] army, GridPos[] placements, (UnitStats, GridPos)[] enemies, params GridPos[] obstacles)
        {
            var b = Make(army, enemies, obstacles);
            for (var i = 0; i < placements.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), placements[i]));
            Assert.IsTrue(b.StartBattle());
            return b;
        }

        [Test]
        public void Placement_OnlyInBackTwoRows()
        {
            var b = Battle.CreatePrototype();
            var warrior = Player(b);
            Assert.IsTrue(b.CanPlace(warrior, new GridPos(0, 0)));
            Assert.IsTrue(b.CanPlace(warrior, new GridPos(5, 1)));
            Assert.IsFalse(b.CanPlace(warrior, new GridPos(0, 2)));
        }

        [Test]
        public void Placement_TileCanHoldOnlyOneUnit()
        {
            var b = Battle.CreatePrototype();
            Assert.IsTrue(b.Place(Player(b, 0), new GridPos(3, 0)));
            Assert.IsFalse(b.Place(Player(b, 1), new GridPos(3, 0)));
        }

        [Test]
        public void StartBattle_RequiresWholeArmyPlaced()
        {
            var b = Battle.CreatePrototype();
            b.Place(Player(b, 0), new GridPos(0, 0));
            Assert.IsFalse(b.StartBattle());
            b.Place(Player(b, 1), new GridPos(1, 0));
            b.Place(Player(b, 2), new GridPos(2, 0));
            Assert.IsTrue(b.StartBattle());
            Assert.AreEqual(Phase.PlayerTurn, b.Phase);
            Assert.AreEqual(1, Player(b, 0).Ap);
        }

        [Test]
        public void Move_CostsOneMovePerTileAroundObstacles()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(0, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) }, new GridPos(0, 1));
            var rogue = Player(b);
            Assert.AreEqual(2, rogue.Moves, "Swift");
            rogue.Moves = 4;
            var reach = b.GetReachable(rogue);
            // (0,2) is straight ahead but (0,1) is a rock: right, up, up, left = 4.
            Assert.AreEqual(4, reach[new GridPos(0, 2)]);
            Assert.IsFalse(reach.ContainsKey(new GridPos(0, 1)));

            var move = b.PlayerMove(rogue, new GridPos(0, 2))?.OfType<MoveEvent>().Single();
            Assert.IsNotNull(move);
            Assert.AreEqual(4, move.Path.Count);
            Assert.AreEqual(0, rogue.Moves);
            Assert.AreEqual(1, rogue.Ap, "moving costs no AP");
        }

        [Test]
        public void Move_OneTilePerTurnByDefault()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            var warrior = Player(b);
            Assert.AreEqual(1, warrior.Moves);
            CollectionAssert.AreEquivalent(new[] { new GridPos(2, 0), new GridPos(4, 0), new GridPos(3, 1) }, b.GetReachable(warrior).Keys);
            b.PlayerMove(warrior, new GridPos(3, 1));
            Assert.AreEqual(0, b.GetReachable(warrior).Count);
            b.EndPlayerTurn();
            Assert.AreEqual(1, warrior.Moves, "moves refill and don't carry over");
        }

        [Test]
        public void Move_NoDiagonals()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            Player(b).Moves = 2;
            Assert.AreEqual(2, b.GetReachable(Player(b))[new GridPos(4, 1)]);
        }

        [Test]
        public void Attack_UsesTheOneApAndDealsDamage()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 2)) });
            var rogue = Player(b);
            var wolf = Enemy(b);
            var hit = b.PlayerAttack(rogue, wolf)?.OfType<AttackEvent>().Single();
            Assert.AreEqual(4, hit.Damage);
            Assert.AreEqual(2, wolf.Hp);
            Assert.AreEqual(0, rogue.Ap);
            Assert.IsNull(b.PlayerAttack(rogue, wolf), "one attack a turn");
            rogue.Ap = 1;
            b.PlayerAttack(rogue, wolf);
            Assert.IsFalse(wolf.IsAlive);
            Assert.AreEqual(Phase.Victory, b.Phase);
        }

        [Test]
        public void Archer_RangeCountsStepsAndNeedsLineOfSight()
        {
            var b = Started(new[] { UnitStats.Archer }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)), (UnitStats.Wolf, new GridPos(4, 4)) },
                new GridPos(0, 0));
            var archer = Player(b);
            Assert.IsTrue(b.CanAttack(archer, Enemy(b, 0)), "3 steps straight ahead");
            Assert.IsFalse(b.CanAttack(archer, Enemy(b, 1)), "4 steps away");

            var blocked = Started(new[] { UnitStats.Archer }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)) }, new GridPos(3, 3));
            Assert.IsFalse(blocked.CanAttack(Player(blocked), Enemy(blocked)), "rock in the way");
        }

        [Test]
        public void LineOfSight_SqueezesPastSingleCorner()
        {
            var board = new Board(8, 8, new[] { new GridPos(1, 0) });
            Assert.IsTrue(board.HasLineOfSight(new GridPos(0, 0), new GridPos(2, 2)));
            var walled = new Board(8, 8, new[] { new GridPos(1, 0), new GridPos(0, 1) });
            Assert.IsFalse(walled.HasLineOfSight(new GridPos(0, 0), new GridPos(1, 1)));
        }

        [Test]
        public void UnusedAp_DoesntCarryOver()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(0, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            var rogue = Player(b);
            b.EndPlayerTurn();
            Assert.AreEqual(1, rogue.Ap);
        }

        [Test]
        public void OnlyOneUnitActsAtATime()
        {
            var b = Started(new[] { UnitStats.Warrior, UnitStats.Rogue }, new[] { new GridPos(0, 0), new GridPos(5, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            var warrior = Player(b, 0);
            var rogue = Player(b, 1);
            Assert.IsNotNull(b.PlayerMove(warrior, new GridPos(0, 1)));
            Assert.IsFalse(b.CanAct(rogue));
            b.EndUnitTurn(warrior);
            Assert.IsFalse(b.CanAct(warrior));
            Assert.IsTrue(b.CanAct(rogue));
        }

        [Test]
        public void PackBond_WolvesHaveExtraHp_AndLoseItWhenTheLeaderDies()
        {
            var b = Started(new[] { UnitStats.Rogue, UnitStats.Rogue }, new[] { new GridPos(0, 1), new GridPos(1, 1) },
                new[]
                {
                    (UnitStats.PackLeader, new GridPos(0, 2)),
                    (UnitStats.Wolf, new GridPos(1, 3)),
                    (UnitStats.Wolf, new GridPos(6, 6)),
                });
            var leader = Enemy(b, 0);
            var healthy = Enemy(b, 1);
            var wounded = Enemy(b, 2);
            Assert.AreEqual(UnitStats.Wolf.MaxHp + 1, healthy.MaxHp, "anywhere on the board, no range");
            Assert.AreEqual(UnitStats.Wolf.MaxHp + 1, healthy.Hp);
            Assert.AreEqual(PackState.Bonded, b.GetPackState(wounded));
            Assert.AreEqual(UnitStats.Wolf.Damage, b.EffectiveDamage(healthy), "no damage aura any more");
            wounded.Hp = 2;

            leader.Hp = UnitStats.Rogue.Damage;
            var events = b.PlayerAttack(Player(b, 0), leader);
            Assert.IsFalse(leader.IsAlive);
            Assert.AreEqual(2, events.OfType<TickEvent>().Count(t => t.Cause == "leader lost"));
            Assert.AreEqual(UnitStats.Wolf.MaxHp - 1, healthy.MaxHp);
            Assert.AreEqual(UnitStats.Wolf.MaxHp - 1, healthy.Hp, "lost 2");
            Assert.IsFalse(wounded.IsAlive, "a wolf on 2 HP dies with its leader");
            Assert.AreEqual(PackState.Leaderless, b.GetPackState(healthy));
        }

        [Test]
        public void WolfBite_EachBiteThisTurnHitsHarder_ResetsNextTurn()
        {
            var b = Started(new[] { UnitStats.Bastion }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 2)), (UnitStats.Wolf, new GridPos(2, 1)), (UnitStats.Wolf, new GridPos(4, 1)) });
            Player(b).Hp = 50;
            var events = b.EndPlayerTurn();
            var bites = events.OfType<AttackEvent>().Where(a => a.Attacker.Team == Team.Enemy).Select(a => a.Damage).ToList();
            // 2, 3, 4 before Bastion's Tough (-1) and Shield Wall doesn't cover itself.
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, bites);
            Assert.AreEqual(0, b.BiteChain, "the chain resets at the start of the next turn");
        }

        [Test]
        public void BloodScent_GoesForTheWeakestItCanReach()
        {
            var b = Started(new[] { UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue },
                new[] { new GridPos(3, 1), new GridPos(4, 1), new GridPos(0, 0) },
                new[] { (UnitStats.Wolf, new GridPos(3, 3)) });
            var wolf = Enemy(b);
            Player(b, 1).Hp = 3;
            Player(b, 2).Hp = 1;
            Assert.AreEqual(Player(b, 1), EnemyAI.ChooseTarget(b, wolf), "the wounded archer, not the nearer warrior; the rogue is out of reach");
        }

        [Test]
        public void Howl_GivesWolvesAnExtraMoveWhenItHelps()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 0) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)), (UnitStats.PackLeader, new GridPos(0, 5)) });
            var wolf = Enemy(b, 0);
            var leader = Enemy(b, 1);
            // The wolf needs 3 moves to reach (3,1); it has 2, so the howl lets it bite.
            Assert.AreEqual(IntentKind.Howl, EnemyAI.PredictIntent(b, leader).Kind);
            var events = b.EndPlayerTurn();
            var howl = events.OfType<HowlEvent>().Single();
            CollectionAssert.AreEqual(new[] { wolf }, howl.Wolves.ToList());
            Assert.IsTrue(events.IndexOf(howl) < events.FindIndex(e => e is AttackEvent), "the leader goes first");
            Assert.AreEqual(new GridPos(3, 1), wolf.Pos);
            Assert.Less(Player(b).Hp, Player(b).MaxHp);
        }

        [Test]
        public void Enemy_TargetsNearestUnit()
        {
            var b = Started(new[] { UnitStats.Warrior, UnitStats.Archer }, new[] { new GridPos(0, 0), new GridPos(6, 1) },
                new[] { (UnitStats.Wolf, new GridPos(6, 5)) });
            Assert.AreEqual(Player(b, 1), EnemyAI.ChooseTarget(b, Enemy(b)));
        }

        [Test]
        public void Enemy_WalksUpAndAttacks()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)) });
            var events = b.EndPlayerTurn();
            var wolf = Enemy(b);
            Assert.AreEqual(new GridPos(3, 2), wolf.Pos, "a Swift wolf walks 2 tiles");
            Assert.AreEqual(1, events.OfType<AttackEvent>().Count(), "walking costs no AP, then one bite");
            Assert.AreEqual(10, Player(b).Hp);
            Assert.AreEqual(Phase.PlayerTurn, b.Phase);
        }

        [Test]
        public void Enemy_AdjacentAttacksOnce()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 2)) });
            var events = b.EndPlayerTurn();
            Assert.AreEqual(1, events.OfType<AttackEvent>().Count());
            Assert.AreEqual(10, Player(b).Hp);
        }

        [Test]
        public void Enemy_KillingLastUnitIsDefeat()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.PackLeader, new GridPos(3, 2)), (UnitStats.Wolf, new GridPos(4, 1)) });
            b.EndPlayerTurn();
            Assert.AreEqual(Phase.Defeat, b.Phase);
        }

        [Test]
        public void Prototype_FullBattleTerminates()
        {
            var b = Battle.CreatePrototype();
            b.Place(Player(b, 0), new GridPos(3, 1));
            b.Place(Player(b, 1), new GridPos(3, 0));
            b.Place(Player(b, 2), new GridPos(4, 1));
            b.StartBattle();
            for (var turn = 0; turn < 50 && !b.IsOver; turn++)
                b.EndPlayerTurn();
            Assert.AreEqual(Phase.Defeat, b.Phase, "a passive player should lose");
        }

        [Test]
        public void ZoneOfControlDisplay_ReachTiles_CostlyMoves_EngagedBy()
        {
            // Rogue at (3,3) next to a wolf at (3,4); a rock at (2,4) next to the wolf.
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(3, 0) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)) }, new GridPos(2, 4));
            var rogue = Player(b);
            var wolf = Enemy(b);
            rogue.Pos = new GridPos(3, 3);

            var reach = b.EnemyReachTiles(rogue);
            CollectionAssert.AreEquivalent(new[] { new GridPos(3, 3), new GridPos(4, 4), new GridPos(3, 5) }, reach,
                "free tiles next to the wolf (the rock is left out), plus the rogue's own");
            CollectionAssert.AreEqual(new[] { wolf }, b.EngagedBy(rogue, rogue.Pos).ToList());

            var costly = b.CostlyMoveTiles(rogue);
            Assert.IsTrue(costly.Contains(new GridPos(3, 2)), "stepping away pays zone of control");
            Assert.IsTrue(b.GetReachable(rogue).Keys.All(costly.Contains), "every move starts by leaving the wolf's reach");

            rogue.Pos = new GridPos(0, 0);
            Assert.AreEqual(0, b.EngagedBy(rogue, rogue.Pos).Count());
            Assert.AreEqual(0, b.CostlyMoveTiles(rogue).Count, "nothing costs extra away from enemies");
        }
    }
}
