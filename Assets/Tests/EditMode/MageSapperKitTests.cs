using System.Collections.Generic;
using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>The Mage and Sapper technique pools from the 2026-11-08 night pass (see docs/night/mage-sapper.md).</summary>
    public class MageSapperKitTests
    {
        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        private static ArmyMember M(UnitStats stats, params Technique[] techniques) => new ArmyMember(stats, null, techniques);

        private static (UnitStats, GridPos) E(UnitStats s, int x, int y) => (s, P(x, y));

        private static (UnitStats, GridPos) Far => (UnitStats.Wolf, P(7, 7));

        private static Battle Setup(ArmyMember[] army, GridPos[] at, (UnitStats, GridPos)[] enemies, params GridPos[] rocks)
        {
            var b = new Battle(new Board(8, 8, rocks), army, enemies);
            for (var i = 0; i < army.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), P(i, 0)));
            Assert.IsTrue(b.StartBattle());
            for (var i = 0; i < army.Length; i++)
                Player(b, i).Pos = at[i];
            return b;
        }

        // ---------- Pools ----------

        [Test]
        public void Pools_AreBigEnough_AndParkedOnesAreNotOffered()
        {
            var mage = Technique.All.Where(t => t.Line == UnitStats.Apprentice).ToList();
            var sapper = Technique.All.Where(t => t.Line == UnitStats.Sapper).ToList();
            Assert.AreEqual(13, mage.Count);
            Assert.AreEqual(12, sapper.Count);
            Assert.IsFalse(mage.Contains(Technique.WideWard));
            Assert.IsFalse(mage.Contains(Technique.Blink));
            CollectionAssert.AreEqual(new[] { ActionKind.FrostNova }, UnitStats.Apprentice.Actions.ToArray());
            Assert.AreEqual(7, UnitStats.Sapper.MaxHp);
        }

        // ---------- Mage ----------

        [Test]
        public void FrostNova_SlowsAndPushesEveryAdjacentEnemy()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Crossbowman, 4, 1), Far });
            var mage = Player(b);
            Assert.IsFalse(b.CanUseAction(mage, ActionKind.FrostNova, P(3, 2)), "aimed at itself");
            Assert.IsNotNull(b.PlayerUseAction(mage, ActionKind.FrostNova, mage.Pos));
            Assert.AreEqual(P(3, 4), Enemy(b, 0).Pos, "a small wolf flies 2 tiles");
            Assert.AreEqual(P(5, 1), Enemy(b, 1).Pos);
            Assert.IsTrue(Enemy(b, 0).IsSlowed);
            Assert.IsTrue(Enemy(b, 1).IsSlowed);
            Assert.AreEqual(UnitStats.Wolf.MaxHp, Enemy(b, 0).Hp, "no damage without a collision");
            Assert.Greater(mage.CooldownLeft(ActionKind.FrostNova), 0);
        }

        [Test]
        public void FrostNova_NeedsAnAdjacentEnemy()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 3), Far });
            Assert.IsFalse(b.CanUseAction(Player(b), ActionKind.FrostNova, P(3, 1)));
        }

        [Test]
        public void Firewall_LightsALineAcrossTheAim_EnemiesWalkThroughButNeverStop()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Firewall) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Crossbowman, 4, 2), E(UnitStats.Wolf, 3, 3), Far });
            Assert.IsNotNull(b.PlayerUseAction(Player(b), ActionKind.Firewall, P(3, 2)));
            CollectionAssert.AreEquivalent(new[] { P(2, 2), P(3, 2), P(4, 2) }, b.BurningTiles.Keys);
            Assert.AreEqual(UnitStats.Crossbowman.MaxHp - Battle.FirewallDamage, Enemy(b, 0).Hp);

            var wolf = Enemy(b, 1);
            wolf.Moves = 2;
            var reach = b.GetReachable(wolf);
            Assert.IsFalse(reach.ContainsKey(P(3, 2)), "never stops on burning ground");
            Assert.IsTrue(reach.ContainsKey(P(3, 1)), "but walks through it");
        }

        [Test]
        public void SearingBolt_SetsTheTargetsTileBurning()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.SearingBolt) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Crossbowman, 3, 3), Far });
            var mage = Player(b);
            b.PlayerAttack(mage, Enemy(b));
            Assert.IsTrue(b.BurningTiles.ContainsKey(P(3, 3)));
            b.EndUnitTurn(mage);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<TickEvent>().Any(t => t.Cause == "burn" && t.Hit.Target == Enemy(b)), "it burns at the start of its turn");
        }

        [Test]
        public void Blizzard_HurtsAndSlowsEnemiesOnly()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Blizzard), M(UnitStats.Warrior) }, new[] { P(3, 0), P(3, 2) },
                new[] { E(UnitStats.Wolf, 3, 3), E(UnitStats.Crossbowman, 4, 3), Far });
            Assert.IsNotNull(b.PlayerUseAction(Player(b), ActionKind.Blizzard, P(3, 3)));
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.BlizzardDamage, Enemy(b, 0).Hp);
            Assert.AreEqual(UnitStats.Crossbowman.MaxHp - Battle.BlizzardDamage, Enemy(b, 1).Hp);
            Assert.IsTrue(Enemy(b, 0).IsSlowed && Enemy(b, 1).IsSlowed);
            Assert.AreEqual(Player(b, 1).MaxHp, Player(b, 1).Hp);
            Assert.IsFalse(Player(b, 1).IsSlowed);
        }

        [Test]
        public void Shatter_EveryAllyHitsSlowedEnemiesHarder()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Shatter), M(UnitStats.Warrior) }, new[] { P(3, 1), P(2, 2) },
                new[] { E(UnitStats.Brute, 3, 2), Far });
            var warrior = Player(b, 1);
            var brute = Enemy(b);
            var plain = b.DamageAgainst(warrior, brute);
            b.PlayerUseAction(Player(b), ActionKind.FrostNova, P(3, 1));
            Assert.AreEqual(P(3, 2), brute.Pos, "huge: slowed but not moved");
            Assert.IsTrue(brute.IsSlowed);
            Assert.AreEqual(plain + Battle.ShatterBonus, b.DamageAgainst(warrior, brute));
        }

        [Test]
        public void Transpose_SwapsPlaces_AnEnemyPutOnAMineSetsItOff()
        {
            var b = Setup(new[] { M(UnitStats.Sapper), M(UnitStats.Apprentice, Technique.Transpose) }, new[] { P(0, 1), P(3, 1) },
                new[] { E(UnitStats.Wolf, 2, 3), Far });
            var sapper = Player(b, 0);
            var mage = Player(b, 1);
            Assert.IsNotNull(b.PlayerPlaceMine(sapper, P(2, 1)));
            b.EndUnitTurn(sapper);
            mage.Pos = P(2, 1);
            var wolf = Enemy(b);
            Assert.IsNotNull(b.PlayerUseAction(mage, ActionKind.Transpose, wolf.Pos));
            Assert.AreEqual(P(2, 3), mage.Pos);
            Assert.AreEqual(P(2, 1), wolf.Pos);
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.MineDamage, wolf.Hp);
            Assert.IsNull(b.MineAt(P(2, 1)));
        }

        [Test]
        public void Transpose_WorksOnAllies_NotOnTheGiant()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Transpose), M(UnitStats.Warrior) }, new[] { P(3, 0), P(3, 2) },
                new[] { E(UnitStats.Ogre, 4, 4) });
            var mage = Player(b);
            Assert.IsFalse(b.CanUseAction(mage, ActionKind.Transpose, P(4, 4)));
            Assert.IsNotNull(b.PlayerUseAction(mage, ActionKind.Transpose, P(3, 2)));
            Assert.AreEqual(P(3, 2), mage.Pos);
            Assert.AreEqual(P(3, 0), Player(b, 1).Pos);
        }

        [Test]
        public void Twincast_FirstActionIsFree_ThenItCanStillAttack_ButNotMove()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Fireball, Technique.Twincast) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Brute, 3, 3), Far });
            var mage = Player(b);
            Assert.IsNotNull(b.PlayerUseAction(mage, ActionKind.Fireball, P(3, 3)));
            Assert.AreEqual(1, mage.Ap, "no AP spent");
            Assert.IsFalse(b.CanMove(mage), "but it has committed");
            Assert.IsTrue(b.CanAttack(mage, Enemy(b)));
            Assert.IsNotNull(b.PlayerAttack(mage, Enemy(b)));
            Assert.AreEqual(0, mage.Ap);
            b.EndUnitTurn(mage);
            b.EndPlayerTurn();
            Assert.IsFalse(mage.TwincastUsed, "ready again next turn");
        }

        [Test]
        public void Twincast_AlsoWorksAfterTheAttack()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Fireball, Technique.Twincast) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Brute, 3, 3), Far });
            var mage = Player(b);
            b.PlayerAttack(mage, Enemy(b));
            Assert.AreEqual(0, mage.Ap);
            Assert.IsTrue(b.CanUseAction(mage, ActionKind.Fireball, P(3, 3)));
            Assert.IsNotNull(b.PlayerUseAction(mage, ActionKind.Fireball, P(3, 3)));
        }

        // ---------- Sapper ----------

        [Test]
        public void CaveIn_RubbleBlocksWalkingButNotShots_AndGivesNoCover()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.CaveIn), M(UnitStats.Archer) }, new[] { P(2, 1), P(3, 1) },
                new[] { E(UnitStats.Crossbowman, 3, 3), Far });
            var sapper = Player(b, 0);
            Assert.IsNotNull(b.PlayerUseAction(sapper, ActionKind.CaveIn, P(3, 2)));
            Assert.IsTrue(b.IsRubble(P(3, 2)));
            Assert.IsTrue(b.Board.IsObstacle(P(3, 2)));
            Assert.IsFalse(b.CanStand(Player(b, 1), P(3, 2)));
            Assert.IsTrue(b.Board.HasLineOfSight(P(3, 1), P(3, 3)), "shots pass over rubble");
            Assert.IsFalse(b.InCover(Enemy(b)), "rubble is no cover");
            b.EndUnitTurn(sapper);
            Assert.IsTrue(b.CanAttack(Player(b, 1), Enemy(b)));
        }

        [Test]
        public void Rubble_OneHitClearsIt()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.CaveIn), M(UnitStats.Warrior) }, new[] { P(2, 1), P(3, 1) }, new[] { Far });
            b.PlayerUseAction(Player(b, 0), ActionKind.CaveIn, P(3, 2));
            b.EndUnitTurn(Player(b, 0));
            Assert.IsTrue(b.CanAttackBarricade(Player(b, 1), P(3, 2)));
            Assert.IsNotNull(b.PlayerAttackBarricade(Player(b, 1), P(3, 2)));
            Assert.IsFalse(b.Board.IsObstacle(P(3, 2)));
        }

        [Test]
        public void Rubble_CrushesWhatIsPushedIntoIt_AndBreaks()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.CaveIn), M(UnitStats.Apprentice) }, new[] { P(1, 3), P(3, 1) },
                new[] { E(UnitStats.Crossbowman, 3, 2), Far });
            b.PlayerUseAction(Player(b, 0), ActionKind.CaveIn, P(3, 3));
            b.EndUnitTurn(Player(b, 0));
            var events = b.PlayerUseAction(Player(b, 1), ActionKind.FrostNova, P(3, 1));
            Assert.AreEqual(P(3, 2), Enemy(b).Pos);
            Assert.AreEqual(UnitStats.Crossbowman.MaxHp - Battle.CollisionDamage - Battle.CrushDamage, Enemy(b).Hp);
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Crush));
            Assert.IsFalse(b.IsRubble(P(3, 3)), "the rubble broke");
        }

        [Test]
        public void Hook_PullsAnEnemyNextToIt_ThroughAMine()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Hook) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 4), Far });
            var sapper = Player(b);
            Assert.IsNotNull(b.PlayerPlaceMine(sapper, P(3, 3)));
            sapper.ClearCooldowns();
            sapper.Ap = 1;
            Assert.IsNotNull(b.PlayerUseAction(sapper, ActionKind.Hook, P(3, 4)));
            Assert.AreEqual(P(3, 2), Enemy(b).Pos);
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.MineDamage, Enemy(b).Hp);
        }

        [Test]
        public void Hook_OverRubble_CrushesTheTarget()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Hook, Technique.CaveIn) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 4), Far });
            var sapper = Player(b);
            b.PlayerUseAction(sapper, ActionKind.CaveIn, P(3, 2));
            sapper.ClearCooldowns();
            sapper.Ap = 1;
            Assert.IsTrue(b.CanUseAction(sapper, ActionKind.Hook, P(3, 4)), "the hook flies over rubble");
            b.PlayerUseAction(sapper, ActionKind.Hook, P(3, 4));
            Assert.AreEqual(P(3, 3), Enemy(b).Pos);
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.CollisionDamage - Battle.CrushDamage, Enemy(b).Hp);
        }

        [Test]
        public void Hook_NeedsAStraightLine_AndCantMoveHugeUnits()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Hook) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Brute, 3, 3), E(UnitStats.Wolf, 4, 3), Far });
            var sapper = Player(b);
            Assert.IsFalse(b.CanUseAction(sapper, ActionKind.Hook, P(3, 3)), "huge");
            Assert.IsFalse(b.CanUseAction(sapper, ActionKind.Hook, P(4, 3)), "not a straight line");
        }

        [Test]
        public void Demolish_BlowsUpARock_LeavesRubble_AndStripsCover()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Demolish) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Crossbowman, 4, 3), E(UnitStats.Wolf, 3, 4), Far }, P(3, 3));
            Assert.IsTrue(b.InCover(Enemy(b, 0)));
            Assert.IsNotNull(b.PlayerUseAction(Player(b), ActionKind.Demolish, P(3, 3)));
            Assert.IsTrue(b.IsRubble(P(3, 3)));
            Assert.AreEqual(UnitStats.Crossbowman.MaxHp - Battle.DemolishDamage, Enemy(b, 0).Hp);
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.DemolishDamage, Enemy(b, 1).Hp);
            Assert.IsFalse(b.InCover(Enemy(b, 0)));
            Assert.AreEqual(Player(b).MaxHp, Player(b).Hp, "outside the 3x3");
        }

        [Test]
        public void QuickFuse_PlaceMineIsFree_AndReadyEveryTurn()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.QuickFuse) }, new[] { P(3, 1) }, new[] { E(UnitStats.Brute, 4, 1), Far });
            var sapper = Player(b);
            Assert.IsNotNull(b.PlayerPlaceMine(sapper, P(3, 3)));
            Assert.AreEqual(1, sapper.Ap);
            Assert.IsTrue(b.CanMove(sapper));
            Assert.IsNotNull(b.PlayerAttack(sapper, Enemy(b)));
            Assert.IsFalse(b.CanPlaceMine(sapper, P(2, 1)), "free only before acting");
            b.EndUnitTurn(sapper);
            b.EndPlayerTurn();
            Assert.AreEqual(0, sapper.CooldownLeft(ActionKind.PlaceMine));
        }

        [Test]
        public void Incendiary_BlastsLeaveTheGroundBurning()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Incendiary) }, new[] { P(3, 1) }, new[] { Far });
            var sapper = Player(b);
            b.PlayerPlaceMine(sapper, P(3, 3));
            var events = new List<BattleEvent>();
            b.Explode(P(3, 3), Battle.MineDamage, "mine", sapper, events);
            Assert.IsTrue(b.BurningTiles.ContainsKey(P(3, 3)));
        }

        [Test]
        public void SpikedBarricades_HurtEnemiesStartingNextToThem()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Barricade, Technique.SpikedBarricades) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Brute, 4, 2), Far });
            var sapper = Player(b);
            b.PlayerUseAction(sapper, ActionKind.Barricade, P(3, 2));
            b.EndUnitTurn(sapper);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<TickEvent>().Any(t => t.Cause == "spikes" && t.Hit.Target == Enemy(b) && t.Hit.Damage == Battle.SpikeDamage));
        }
    }
}
