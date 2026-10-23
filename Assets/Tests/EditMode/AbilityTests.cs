using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    public class AbilityTests
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

        private static ArmyMember M(UnitStats stats, Equipment item = null) => new ArmyMember(stats, item);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        // ---------- Built-in abilities ----------

        [Test]
        public void ShieldWall_AdjacentAllyTakesOneLess()
        {
            var b = Started(new[] { M(UnitStats.Warrior), M(UnitStats.Archer) }, new[] { P(3, 1), P(4, 1) },
                new[] { (UnitStats.Wolf, P(4, 2)) });
            var archer = Player(b, 1);
            Assert.IsTrue(b.IsShielded(archer));
            Assert.IsFalse(b.IsShielded(Player(b, 0)), "the warrior doesn't shield itself");
            b.EndPlayerTurn();
            Assert.AreEqual(archer.MaxHp - 2, archer.Hp, "two wolf bites of 2, each reduced to 1");
        }

        [Test]
        public void SteadyAim_BonusOnlyWithoutMoving()
        {
            var b = Started(new[] { M(UnitStats.Archer) }, new[] { P(3, 1) }, new[] { (UnitStats.Wolf, P(3, 7)) });
            var archer = Player(b);
            Assert.AreEqual(3, b.EffectiveDamage(archer));
            b.PlayerMove(archer, P(3, 2));
            Assert.AreEqual(2, b.EffectiveDamage(archer));
        }

        [Test]
        public void Bloodthirst_GainsDamageOnKill()
        {
            var b = Started(new[] { M(UnitStats.Rogue) }, new[] { P(3, 1) },
                new[] { (UnitStats.Wolf, P(3, 2)), (UnitStats.Wolf, P(7, 7)) });
            Enemy(b).Hp = 1;
            var events = b.PlayerAttack(Player(b), Enemy(b));
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Bloodthirst));
            Assert.AreEqual(5, Player(b).BaseDamage);
        }

        // ---------- Item abilities ----------

        [Test]
        public void VampireFang_HealsOnKill()
        {
            var b = Started(new[] { M(UnitStats.Warrior, Equipment.VampireFang) }, new[] { P(3, 1) },
                new[] { (UnitStats.Wolf, P(3, 2)), (UnitStats.Wolf, P(7, 7)) });
            var warrior = Player(b);
            warrior.Hp = 5;
            Enemy(b).Hp = 1;
            b.PlayerAttack(warrior, Enemy(b));
            Assert.AreEqual(5 + Battle.VampireHeal, warrior.Hp);
        }

        [Test]
        public void ThornedMail_HurtsMeleeAttackers()
        {
            var b = Started(new[] { M(UnitStats.Warrior, Equipment.ThornedMail) }, new[] { P(3, 1) },
                new[] { (UnitStats.Wolf, P(3, 2)) });
            var wolf = Enemy(b);
            var events = b.EndPlayerTurn();
            Assert.AreEqual(2, events.OfType<EffectEvent>().Count(e => e.Kind == EffectKind.Thorns));
            Assert.AreEqual(wolf.MaxHp - 2 * Battle.ThornsDamage, wolf.Hp);
        }

        [Test]
        public void SpringBoots_FirstStepFree()
        {
            var b = Started(new[] { M(UnitStats.Warrior, Equipment.SpringBoots) }, new[] { P(3, 0) },
                new[] { (UnitStats.Wolf, P(7, 7)) });
            var warrior = Player(b);
            var reach = b.GetReachable(warrior);
            Assert.AreEqual(0, reach[P(3, 1)]);
            Assert.AreEqual(3, reach[P(3, 4)], "4 tiles for 3 AP");
            b.PlayerMove(warrior, P(3, 1));
            Assert.AreEqual(3, warrior.Ap);
            Assert.AreEqual(1, b.GetReachable(warrior)[P(3, 2)], "only the first step of the turn is free");
        }

        [Test]
        public void LuckyCharm_SurvivesOnceAtOneHp()
        {
            var b = Started(new[] { M(UnitStats.Rogue, Equipment.LuckyCharm) }, new[] { P(3, 1) },
                new[] { (UnitStats.PackLeader, P(3, 2)) });
            var rogue = Player(b);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(rogue.IsAlive);
            Assert.AreEqual(1, rogue.Hp);
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.LastStand));
            Assert.IsTrue(rogue.LastStandUsed);
        }

        // ---------- Sapper mines ----------

        [Test]
        public void Mine_PlacedNextToSapper_WithLimits()
        {
            var b = Started(new[] { M(UnitStats.Sapper) }, new[] { P(3, 1) }, new[] { (UnitStats.Wolf, P(7, 7)) });
            var sapper = Player(b);
            Assert.IsFalse(b.CanPlaceMine(sapper, P(3, 3)), "not adjacent");
            Assert.IsNotNull(b.PlayerPlaceMine(sapper, P(3, 2)));
            Assert.AreEqual(4 - Battle.MineCost, sapper.Ap);
            Assert.IsFalse(b.CanPlaceMine(sapper, P(3, 2)), "already mined");
            Assert.IsNotNull(b.PlayerPlaceMine(sapper, P(2, 1)));
            Assert.IsFalse(b.CanPlaceMine(sapper, P(4, 1)), "out of AP");
            Assert.AreEqual(2, b.Mines.Count);
        }

        [Test]
        public void Mine_EnemyStepsOn_StopsAndExplodes_HittingEveryoneAround()
        {
            var b = Started(new[] { M(UnitStats.Sapper) }, new[] { P(3, 1) }, new[] { (UnitStats.Wolf, P(3, 4)) });
            var sapper = Player(b);
            var wolf = Enemy(b);
            b.PlayerPlaceMine(sapper, P(3, 2));
            var events = b.EndPlayerTurn();
            var blast = events.OfType<ExplosionEvent>().First();
            Assert.AreEqual("mine", blast.Cause);
            Assert.AreEqual(P(3, 2), wolf.Pos, "the wolf stops on the mine");
            Assert.AreEqual(wolf.MaxHp - Battle.MineDamage, wolf.Hp);
            Assert.AreEqual(sapper.MaxHp - Battle.MineDamage - 2, sapper.Hp, "friendly fire, then the wolf bites once");
            Assert.AreEqual(0, b.Mines.Count);
        }

        [Test]
        public void Mine_ChainReaction()
        {
            var b = Started(new[] { M(UnitStats.Sapper) }, new[] { P(3, 1) }, new[] { (UnitStats.Wolf, P(3, 4)) });
            var sapper = Player(b);
            b.PlayerPlaceMine(sapper, P(3, 2));
            // A second mine diagonally next to the first, inside its blast.
            b.PlayerPlaceMine(sapper, P(2, 1));
            var events = b.EndPlayerTurn();
            var blasts = events.OfType<ExplosionEvent>().ToList();
            Assert.AreEqual(2, blasts.Count);
            Assert.AreEqual(0, blasts[0].ChainDepth);
            Assert.AreEqual(1, blasts[1].ChainDepth);
        }

        [Test]
        public void MineKill_CountsForTheSapper()
        {
            var b = Started(new[] { M(UnitStats.Sapper, Equipment.VampireFang) }, new[] { P(3, 1) },
                new[] { (UnitStats.Wolf, P(3, 4)), (UnitStats.Wolf, P(7, 7)) });
            var sapper = Player(b);
            b.PlayerPlaceMine(sapper, P(3, 2));
            Enemy(b).Hp = 1;
            var events = b.EndPlayerTurn();
            Assert.IsFalse(Enemy(b).IsAlive);
            // Caught in its own blast (6 - 4), then healed by the Vampire Fang for the kill.
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Heal && e.Unit == sapper));
            Assert.AreEqual(sapper.MaxHp - Battle.MineDamage + Battle.VampireHeal, sapper.Hp);
        }

        // ---------- Goblin bombs ----------

        [Test]
        public void Bomb_LandsThisTurn_ExplodesNextEnemyTurn()
        {
            var b = Started(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { (UnitStats.GoblinBomber, P(3, 4)) });
            var warrior = Player(b);
            Assert.AreEqual(IntentKind.Bomb, EnemyAI.PredictIntent(b, Enemy(b)).Kind);
            var first = b.EndPlayerTurn();
            Assert.AreEqual(P(3, 1), first.OfType<BombThrowEvent>().Single().Tile);
            Assert.AreEqual(warrior.MaxHp, warrior.Hp, "nothing yet");
            Assert.IsNotNull(b.BombAt(P(3, 1)));

            var second = b.EndPlayerTurn();
            var blast = second.OfType<ExplosionEvent>().First();
            Assert.AreEqual("bomb", blast.Cause);
            Assert.AreEqual(warrior.MaxHp - UnitStats.GoblinBomber.BombDamage, warrior.Hp);
        }

        [Test]
        public void Bomb_CanBeDodged()
        {
            var b = Started(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { (UnitStats.GoblinBomber, P(3, 4)) });
            var warrior = Player(b);
            b.EndPlayerTurn();
            b.PlayerMove(warrior, P(5, 0));
            var events = b.EndPlayerTurn();
            var blast = events.OfType<ExplosionEvent>().First();
            Assert.IsFalse(blast.Hits.Any(h => h.Target == warrior));
        }

        [Test]
        public void Bomb_SetsOffMines()
        {
            var b = Started(new[] { M(UnitStats.Sapper) }, new[] { P(3, 1) }, new[] { (UnitStats.GoblinBomber, P(3, 4)) });
            var sapper = Player(b);
            b.EndPlayerTurn();
            Assert.IsNotNull(b.BombAt(P(3, 1)));
            b.PlayerPlaceMine(sapper, P(3, 2));
            b.PlayerMove(sapper, P(6, 0));
            var events = b.EndPlayerTurn();
            var blasts = events.OfType<ExplosionEvent>().ToList();
            Assert.IsTrue(blasts.Any(e => e.Cause == "bomb" && e.ChainDepth == 0));
            Assert.IsTrue(blasts.Any(e => e.Cause == "mine" && e.ChainDepth == 1));
        }

        [Test]
        public void Bomber_BacksAwayWhenAdjacent()
        {
            var b = Started(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { (UnitStats.GoblinBomber, P(3, 2)) });
            b.EndPlayerTurn();
            Assert.GreaterOrEqual(Battle.Distance(Enemy(b), Player(b)), 2);
        }

        [Test]
        public void DebugWin_WinsWithWholeArmy_EvenDuringPlacement()
        {
            var b = Battle.CreatePrototype();
            b.DebugWin();
            Assert.AreEqual(Phase.Victory, b.Phase);
            Assert.IsTrue(b.WasSkipped);
            Assert.AreEqual(3, b.Living(Team.Player).Count());
            Assert.AreEqual(0, b.Living(Team.Enemy).Count());
        }

        [Test]
        public void CanStillAct_CountsMinePlacement()
        {
            var b = Started(new[] { M(UnitStats.Sapper) }, new[] { P(0, 0) }, new[] { (UnitStats.Wolf, P(7, 7)) },
                P(1, 0), P(0, 1));
            // Boxed into the corner: it can't move, but it can't place a mine either (no free neighbour).
            Assert.IsFalse(b.CanStillAct(Player(b)));
        }
    }
}
