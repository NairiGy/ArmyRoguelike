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
            Assert.IsTrue(b.CanPlace(warrior, new GridPos(7, 1)));
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
            Assert.AreEqual(3, Player(b, 0).Ap);
        }

        [Test]
        public void Move_CostsOneApPerTileAroundObstacles()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(0, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) }, new GridPos(0, 1));
            var rogue = Player(b);
            var reach = b.GetReachable(rogue);
            // (0,2) is straight ahead but (0,1) is a rock: right, up, up, left = 4.
            Assert.AreEqual(4, reach[new GridPos(0, 2)]);
            Assert.IsFalse(reach.ContainsKey(new GridPos(0, 1)));

            var move = b.PlayerMove(rogue, new GridPos(0, 2))?.OfType<MoveEvent>().Single();
            Assert.IsNotNull(move);
            Assert.AreEqual(4, move.Path.Count);
            Assert.AreEqual(1, rogue.Ap);
        }

        [Test]
        public void Move_NoDiagonals()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            Assert.AreEqual(2, b.GetReachable(Player(b))[new GridPos(4, 1)]);
        }

        [Test]
        public void Attack_CostsTwoApAndDealsDamage()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 2)) });
            var rogue = Player(b);
            var wolf = Enemy(b);
            var hit = b.PlayerAttack(rogue, wolf)?.OfType<AttackEvent>().Single();
            Assert.AreEqual(4, hit.Damage);
            Assert.AreEqual(2, wolf.Hp);
            Assert.AreEqual(3, rogue.Ap);
            b.PlayerAttack(rogue, wolf);
            Assert.IsFalse(wolf.IsAlive);
            Assert.AreEqual(Phase.Victory, b.Phase);
        }

        [Test]
        public void Archer_RangeCountsStepsAndNeedsLineOfSight()
        {
            var b = Started(new[] { UnitStats.Archer }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 5)), (UnitStats.Wolf, new GridPos(5, 4)) },
                new GridPos(0, 0));
            var archer = Player(b);
            Assert.IsTrue(b.CanAttack(archer, Enemy(b, 0)), "4 steps straight ahead");
            Assert.IsFalse(b.CanAttack(archer, Enemy(b, 1)), "5 steps away");

            var blocked = Started(new[] { UnitStats.Archer }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 5)) }, new GridPos(3, 3));
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
        public void UnusedAp_CarriesOverUpToTwo()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(0, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            var rogue = Player(b);
            b.EndPlayerTurn();
            Assert.AreEqual(5 + 2, rogue.Ap);
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
        public void PackLeader_BuffsNearbyWolves_AndDeathWeakensThem()
        {
            var b = Started(new[] { UnitStats.Rogue, UnitStats.Rogue }, new[] { new GridPos(0, 1), new GridPos(1, 1) },
                new[]
                {
                    (UnitStats.PackLeader, new GridPos(0, 2)),
                    (UnitStats.Wolf, new GridPos(1, 3)),
                    (UnitStats.Wolf, new GridPos(6, 6)),
                });
            var leader = Enemy(b, 0);
            var near = Enemy(b, 1);
            var far = Enemy(b, 2);
            Assert.AreEqual(3, b.EffectiveDamage(near));
            Assert.AreEqual(2, b.EffectiveDamage(far));

            var first = Player(b, 0);
            b.PlayerAttack(first, leader);
            b.PlayerAttack(first, leader);
            b.EndUnitTurn(first);
            var second = Player(b, 1);
            Assert.IsNotNull(b.PlayerMove(second, new GridPos(1, 2)));
            Assert.IsNotNull(b.PlayerAttack(second, leader));
            Assert.IsFalse(leader.IsAlive);
            Assert.AreEqual(1, b.EffectiveDamage(near));
            Assert.AreEqual(1, b.EffectiveDamage(far));
            Assert.AreEqual(PackState.Leaderless, b.GetPackState(far));
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
            Assert.AreEqual(new GridPos(3, 2), wolf.Pos);
            var attack = events.OfType<AttackEvent>().Single();
            Assert.AreEqual(2, attack.Damage);
            Assert.AreEqual(10, Player(b).Hp);
            Assert.AreEqual(Phase.PlayerTurn, b.Phase);
        }

        [Test]
        public void Enemy_AdjacentAttacksTwice()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 2)) });
            var events = b.EndPlayerTurn();
            Assert.AreEqual(2, events.OfType<AttackEvent>().Count());
            Assert.AreEqual(8, Player(b).Hp);
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
    }
}
