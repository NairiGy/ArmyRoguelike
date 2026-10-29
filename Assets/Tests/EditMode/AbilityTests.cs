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
            Assert.AreEqual(archer.MaxHp - 1, archer.Hp, "one wolf bite of 2, reduced to 1");
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
            var b = Started(new[] { M(UnitStats.Assassin) }, new[] { P(3, 1) },
                new[] { (UnitStats.Wolf, P(3, 2)), (UnitStats.Wolf, P(7, 7)) });
            Enemy(b).Hp = 1;
            var events = b.PlayerAttack(Player(b), Enemy(b));
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Bloodthirst));
            Assert.AreEqual(UnitStats.Assassin.Damage + 1, Player(b).BaseDamage);
        }

        [Test]
        public void Bloodthirst_StopsAtTheCapForTheRun()
        {
            var rogue = M(UnitStats.Assassin);
            rogue.BonusDamage = Battle.BloodthirstCap - 1;
            var b = Started(new[] { rogue }, new[] { P(3, 1) },
                new[] { (UnitStats.Wolf, P(3, 2)), (UnitStats.Wolf, P(4, 1)), (UnitStats.Wolf, P(7, 7)) });
            var unit = Player(b);
            unit.Ap = 99;
            Enemy(b, 0).Hp = 1;
            b.PlayerAttack(unit, Enemy(b, 0));
            Assert.AreEqual(1, unit.GainedDamage, "one more kill still counts");

            Enemy(b, 1).Hp = 1;
            var events = b.PlayerAttack(unit, Enemy(b, 1));
            Assert.AreEqual(1, unit.GainedDamage, "the cap is reached");
            Assert.IsFalse(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Bloodthirst));
            Assert.AreEqual(UnitStats.Assassin.Damage + Battle.BloodthirstCap, unit.BaseDamage);
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
            Assert.AreEqual(1, events.OfType<EffectEvent>().Count(e => e.Kind == EffectKind.Thorns), "one bite a turn");
            Assert.AreEqual(wolf.MaxHp - Battle.ThornsDamage, wolf.Hp);
        }

        [Test]
        public void SpringBoots_GiveAnExtraMove()
        {
            var b = Started(new[] { M(UnitStats.Warrior, Equipment.SpringBoots) }, new[] { P(3, 0) },
                new[] { (UnitStats.Wolf, P(7, 7)) });
            var warrior = Player(b);
            Assert.AreEqual(2, warrior.Moves);
            var reach = b.GetReachable(warrior);
            Assert.AreEqual(2, reach[P(3, 2)]);
            Assert.IsFalse(reach.ContainsKey(P(3, 3)), "only 2 moves");
            b.PlayerMove(warrior, P(3, 1));
            Assert.AreEqual(1, warrior.Ap, "moving costs no AP");
            Assert.AreEqual(1, warrior.Moves);
        }

        [Test]
        public void LuckyCharm_SurvivesOnceAtOneHp()
        {
            var b = Started(new[] { M(UnitStats.Rogue, Equipment.LuckyCharm) }, new[] { P(3, 1) },
                new[] { (UnitStats.PackLeader, P(3, 2)) });
            var rogue = Player(b);
            rogue.Hp = UnitStats.PackLeader.Damage;
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
            Assert.AreEqual(1 - Battle.MineCost, sapper.Ap);
            Assert.IsFalse(b.CanPlaceMine(sapper, P(2, 1)), "one action a turn");
            sapper.Ap = 99;
            Assert.IsFalse(b.CanPlaceMine(sapper, P(3, 2)), "already mined");
            Assert.IsNotNull(b.PlayerPlaceMine(sapper, P(2, 1)));
            Assert.IsNotNull(b.PlayerPlaceMine(sapper, P(4, 1)));
            Assert.IsFalse(b.CanPlaceMine(sapper, P(3, 0)), "at most 3 mines");
            Assert.AreEqual(3, b.Mines.Count);
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
            sapper.Ap = 2;
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
            b.PlayerMove(warrior, P(2, 1));
            var events = b.EndPlayerTurn();
            var blast = events.OfType<ExplosionEvent>().First();
            Assert.IsFalse(blast.Hits.Any(h => h.Target == warrior), "one step off the tile is enough");
        }

        [Test]
        public void Bomb_HitsOnlyItsOwnTile()
        {
            var b = Started(new[] { M(UnitStats.Warrior), M(UnitStats.Warrior) }, new[] { P(3, 1), P(2, 1) },
                new[] { (UnitStats.GoblinBomber, P(3, 4)) });
            b.EndPlayerTurn();
            Assert.IsNotNull(b.BombAt(P(3, 1)));
            var events = b.EndPlayerTurn();
            var blast = events.OfType<ExplosionEvent>().First(e => e.Cause == "bomb");
            CollectionAssert.AreEqual(new[] { P(3, 1) }, blast.Area.ToList());
            CollectionAssert.AreEqual(new[] { Player(b, 0) }, blast.Hits.Select(h => h.Target).ToList());
        }

        [Test]
        public void Bomber_BacksAwayWhenAdjacent()
        {
            var b = Started(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { (UnitStats.GoblinBomber, P(3, 2)) });
            b.EndPlayerTurn();
            Assert.GreaterOrEqual(Battle.Distance(Enemy(b), Player(b)), 2);
        }

        // ---------- Bandits and elementals ----------

        [Test]
        public void Cutpurse_StealsOnHit_ThenRunsAndEscapes()
        {
            var b = Started(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { (UnitStats.Cutpurse, P(3, 2)) });
            var thief = Enemy(b);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Steal));
            Assert.AreEqual(UnitStats.Cutpurse.StealsGold, thief.CarryingLoot);
            Assert.AreEqual(Intent(b, thief), IntentKind.Flee);
            for (var i = 0; i < 4 && !b.IsOver; i++)
                b.EndPlayerTurn();
            Assert.IsTrue(thief.Escaped);
            Assert.AreEqual(UnitStats.Cutpurse.StealsGold, b.StolenGold);
            Assert.AreEqual(Phase.Victory, b.Phase, "the last enemy left the board");
        }

        [Test]
        public void Cutpurse_KilledWithLoot_StealsNothing()
        {
            var b = Started(new[] { M(UnitStats.Rogue) }, new[] { P(3, 1) }, new[] { (UnitStats.Cutpurse, P(3, 2)), (UnitStats.Wolf, P(7, 7)) });
            var thief = Enemy(b);
            thief.CarryingLoot = 5;
            thief.Hp = 1;
            b.PlayerAttack(Player(b), thief);
            Assert.IsFalse(thief.IsAlive);
            Assert.AreEqual(0, b.StolenGold);
        }

        [Test]
        public void Elemental_SplitsIntoTwoShards()
        {
            var b = Started(new[] { M(UnitStats.Rogue) }, new[] { P(3, 1) }, new[] { (UnitStats.Elemental, P(3, 2)) });
            var elemental = Enemy(b);
            elemental.Hp = 1;
            var events = b.PlayerAttack(Player(b), elemental);
            var split = events.OfType<SplitEvent>().Single();
            Assert.AreEqual(2, split.Children.Count);
            Assert.IsTrue(split.Children.All(c => c.Stats == UnitStats.Shard && c.IsAlive));
            Assert.AreEqual(Phase.PlayerTurn, b.Phase, "not won yet: the shards are still standing");
            Assert.AreEqual(P(3, 2), split.Children[0].Pos, "one shard where the elemental stood");
        }

        private static IntentKind Intent(Battle b, Unit u) => EnemyAI.PredictIntent(b, u).Kind;

        // ---------- Zone of control ----------

        [Test]
        public void ZoneOfControl_LeavingEnemyReachUsesUpMoves()
        {
            var b = Started(new[] { M(UnitStats.Rogue) }, new[] { P(3, 1) }, new[] { (UnitStats.Ogre, P(3, 2)) });
            var rogue = Player(b);
            Assert.AreEqual(2, rogue.Moves, "Swift");
            var reach = b.GetReachable(rogue);
            Assert.AreEqual(2, reach[P(2, 1)], "the step out of the ogre's reach uses up both moves");
            Assert.IsFalse(reach.ContainsKey(P(2, 0)), "can't keep walking after leaving its reach");
            var events = b.PlayerMove(rogue, P(2, 1));
            var move = events.OfType<MoveEvent>().Single();
            Assert.AreEqual(1, move.ZocSteps);
            Assert.AreEqual(0, rogue.Moves);
            Assert.AreEqual(1, rogue.Ap, "moving costs no AP");
        }

        [Test]
        public void ZoneOfControl_OneMoveUnitCanStillBackOff()
        {
            var b = Started(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { (UnitStats.Ogre, P(3, 2)) });
            var warrior = Player(b);
            Assert.AreEqual(1, b.GetReachable(warrior)[P(3, 0)]);
            Assert.AreEqual(0, b.CostlyMoveTiles(warrior).Count, "with one move, leaving costs nothing extra");
        }

        [Test]
        public void ZoneOfControl_MovingAwayFromNobodyIsNormal()
        {
            var b = Started(new[] { M(UnitStats.Rogue) }, new[] { P(3, 1) }, new[] { (UnitStats.Wolf, P(7, 7)) });
            Assert.AreEqual(1, b.GetReachable(Player(b))[P(2, 1)]);
        }

        [Test]
        public void ZoneOfControl_AppliesToEnemiesToo()
        {
            var b = Started(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { (UnitStats.Wolf, P(3, 2)) });
            var wolf = Enemy(b);
            wolf.Moves = wolf.MaxMoves;
            var reach = b.GetReachable(wolf);
            Assert.AreEqual(2, reach[P(3, 3)]);
            Assert.IsFalse(reach.ContainsKey(P(3, 4)));
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
