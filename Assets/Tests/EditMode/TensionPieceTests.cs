using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>The tension pieces decided 2026-11-09: strong options with a real cost (design note, "Synergy map").</summary>
    public class TensionPieceTests
    {
        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        private static ArmyMember M(UnitStats stats, params Technique[] techniques) => new ArmyMember(stats, null, techniques);

        private static (UnitStats, GridPos) E(UnitStats s, int x, int y) => (s, P(x, y));

        /// <summary>A slow enemy far away, so the battle goes on while the test runs turns.</summary>
        private static (UnitStats, GridPos) Far => (UnitStats.Brute, P(7, 7));

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

        private static void NextTurn(Battle b)
        {
            foreach (var u in b.Units.Where(u => u.Team == Team.Player && u.IsAlive).ToList())
                b.EndUnitTurn(u);
            b.EndPlayerTurn();
        }

        // ---------- Archer: Planted ----------

        [Test]
        public void Planted_IsFree_PinsItselfThisTurnAndTheNext()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.Planted) }, new[] { P(0, 1) }, new[] { Far });
            var archer = Player(b);
            Assert.IsNotNull(b.PlayerUseAction(archer, ActionKind.Planted, archer.Pos));
            Assert.AreEqual(1, archer.Ap, "free");
            Assert.IsTrue(archer.IsPlanted);
            Assert.IsFalse(b.CanMove(archer), "can't move after planting");
            Assert.IsFalse(b.CanUseAction(archer, ActionKind.Planted, archer.Pos), "not twice");

            NextTurn(b);
            Assert.IsTrue(archer.IsPlanted, "still planted on its next turn");
            Assert.IsFalse(b.CanMove(archer), "pinned on its next turn");

            NextTurn(b);
            Assert.IsFalse(archer.IsPlanted);
            Assert.IsTrue(b.CanMove(archer));
        }

        [Test]
        public void Planted_ShotsDealMoreAndPin_KickDoesNot()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.Planted, Technique.Kick) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Brute, 3, 4), E(UnitStats.Brute, 4, 1) });
            var archer = Player(b);
            var far = Enemy(b, 0);
            var near = Enemy(b, 1);
            var before = b.DamageAgainst(archer, far);
            b.PlayerUseAction(archer, ActionKind.Planted, archer.Pos);
            Assert.AreEqual(before + Battle.PlantedBonus, b.DamageAgainst(archer, far));

            b.PlayerAttack(archer, far);
            Assert.AreEqual(UnitStats.Brute.MaxHp - before - Battle.PlantedBonus, far.Hp);
            Assert.IsTrue(far.IsSnared, "its shot pins");

            archer.Ap = 1;
            b.PlayerUseAction(archer, ActionKind.Kick, near.Pos);
            Assert.AreEqual(UnitStats.Brute.MaxHp - b.EffectiveDamage(archer) + UnitStats.Archer.Damage - Battle.KickDamage, near.Hp,
                "Kick isn't a shot: no Planted bonus");
        }

        [Test]
        public void Planted_AddsToPinningShot()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.Planted) }, new[] { P(3, 1) }, new[] { E(UnitStats.Brute, 3, 4) });
            var archer = Player(b);
            b.PlayerUseAction(archer, ActionKind.Planted, archer.Pos);
            b.PlayerUseAction(archer, ActionKind.PinningShot, Enemy(b).Pos);
            Assert.AreEqual(UnitStats.Brute.MaxHp - Battle.PinDamage - Battle.PlantedBonus, Enemy(b).Hp);
        }

        [Test]
        public void Planted_SkeletonsDontGraspAPlantedArcher()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.Planted) }, new[] { P(3, 1) }, new[] { E(UnitStats.Skeleton, 3, 2) });
            var archer = Player(b);
            var skeleton = Enemy(b);
            Assert.IsTrue(EnemyAI.WantsToGrasp(b, skeleton, archer), "a ranged unit is worth grasping");
            b.PlayerUseAction(archer, ActionKind.Planted, archer.Pos);
            Assert.IsFalse(EnemyAI.WantsToGrasp(b, skeleton, archer), "it can't move anyway");
            Assert.IsFalse(b.CanGrasp(skeleton, archer));
        }

        // ---------- Archer: Steady Aim+ ----------

        [Test]
        public void SteadyAimPlus_MoreStandingStill_LessAfterMoving()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.SteadyAimPlus) }, new[] { P(0, 1) }, new[] { Far });
            var archer = Player(b);
            Assert.AreEqual(archer.BaseDamage + Battle.SteadyAimPlusBonus, b.EffectiveDamage(archer));
            b.PlayerMove(archer, P(1, 1));
            Assert.AreEqual(archer.BaseDamage - Battle.SteadyAimPlusPenalty, b.EffectiveDamage(archer));
        }

        // ---------- Rogue: Lone Wolf ----------

        [Test]
        public void LoneWolf_MoreWhileTheRogueItselfIsExposed()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.LoneWolf), M(UnitStats.Warrior) }, new[] { P(3, 1), P(6, 0) },
                new[] { E(UnitStats.Brute, 3, 2) });
            var rogue = Player(b);
            var brute = Enemy(b);
            Assert.IsTrue(b.IsExposed(rogue));
            var alone = b.DamageAgainst(rogue, brute);
            Player(b, 1).Pos = P(2, 1);
            Assert.IsFalse(b.IsExposed(rogue));
            Assert.AreEqual(alone - Battle.LoneWolfBonus, b.DamageAgainst(rogue, brute));
        }

        // ---------- Mage: Overchannel ----------

        [Test]
        public void Overchannel_DoublesTheNextSpellOnEveryTarget_ThenTheMageIsSlowed()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Fireball, Technique.Overchannel) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Brute, 3, 3), E(UnitStats.Brute, 4, 3), Far });
            var mage = Player(b);
            Assert.IsNotNull(b.PlayerUseAction(mage, ActionKind.Overchannel, mage.Pos));
            Assert.AreEqual(1, mage.Ap, "free");
            b.PlayerUseAction(mage, ActionKind.Fireball, P(3, 3));
            Assert.AreEqual(UnitStats.Brute.MaxHp - 2 * Battle.FireballDamage, Enemy(b, 0).Hp);
            Assert.AreEqual(UnitStats.Brute.MaxHp - 2 * Battle.FireballDamage, Enemy(b, 1).Hp, "every target in the area");
            Assert.IsFalse(mage.Overchanneled, "only the next spell");

            NextTurn(b);
            Assert.IsTrue(mage.SlowedThisTurn, "slowed on its next turn");
        }

        [Test]
        public void Overchannel_DoublesTheBolt_AndSlowsEvenWithNoSpell()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Overchannel) }, new[] { P(3, 0) }, new[] { E(UnitStats.Brute, 3, 3) });
            var mage = Player(b);
            var normal = b.DamageAgainst(mage, Enemy(b));
            b.PlayerUseAction(mage, ActionKind.Overchannel, mage.Pos);
            Assert.AreEqual(2 * normal, b.DamageAgainst(mage, Enemy(b)));

            var c = Setup(new[] { M(UnitStats.Apprentice, Technique.Overchannel) }, new[] { P(0, 0) }, new[] { Far });
            var idle = Player(c);
            c.PlayerUseAction(idle, ActionKind.Overchannel, idle.Pos);
            NextTurn(c);
            Assert.IsTrue(idle.SlowedThisTurn, "the cost is paid on use");
            Assert.IsFalse(idle.Overchanneled, "the double doesn't carry over");
        }

        // ---------- Sapper: Rigged Barricades ----------

        /// <summary>Hits the barricade until it breaks (at most 10 hits); returns the events of the breaking hit.</summary>
        private static System.Collections.Generic.List<BattleEvent> Break(Battle b, Unit attacker, GridPos tile)
        {
            System.Collections.Generic.List<BattleEvent> events = null;
            for (var i = 0; i < 10 && b.IsBarricade(tile); i++)
            {
                attacker.Ap = 1;
                attacker.ActedThisTurn = false;
                attacker.IsDone = false;
                events = b.PlayerAttackBarricade(attacker, tile);
                Assert.IsNotNull(events, "the hit is allowed");
            }

            Assert.IsFalse(b.IsBarricade(tile), "broken");
            return events;
        }

        [Test]
        public void RiggedBarricades_ABrokenBarricadeExplodesInTheThreeByThree()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Barricade, Technique.RiggedBarricades), M(UnitStats.Warrior) },
                new[] { P(3, 1), P(2, 2) }, new[] { E(UnitStats.Brute, 4, 3), Far });
            var sapper = Player(b);
            b.PlayerUseAction(sapper, ActionKind.Barricade, P(3, 2));
            Assert.IsTrue(b.IsRigged(P(3, 2)));
            b.EndUnitTurn(sapper);
            var warrior = Player(b, 1);
            var events = Break(b, warrior, P(3, 2));

            var blast = events.OfType<ExplosionEvent>().Single();
            Assert.AreEqual(Battle.RiggedCause, blast.Cause);
            Assert.AreEqual(sapper, blast.Owner, "the sapper's blast");
            Assert.AreEqual(UnitStats.Brute.MaxHp - Battle.RiggedBlastDamage, Enemy(b).Hp);
            Assert.AreEqual(sapper.MaxHp - Battle.RiggedBlastDamage, sapper.Hp, "it hurts your side too");
            Assert.IsTrue(b.IsRubble(P(3, 2)), "and still leaves rubble");
        }

        [Test]
        public void RiggedBarricades_BlastProofSparesYourSide_PlainBarricadesDontExplode()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Barricade, Technique.RiggedBarricades, Technique.BlastProof) },
                new[] { P(3, 1) }, new[] { Far });
            var sapper = Player(b);
            b.PlayerUseAction(sapper, ActionKind.Barricade, P(3, 2));
            Break(b, sapper, P(3, 2));
            Assert.AreEqual(sapper.MaxHp, sapper.Hp);

            var c = Setup(new[] { M(UnitStats.Sapper, Technique.Barricade) }, new[] { P(3, 1) }, new[] { Far });
            var plain = Player(c);
            c.PlayerUseAction(plain, ActionKind.Barricade, P(3, 2));
            Break(c, plain, P(3, 2));
            Assert.AreEqual(plain.MaxHp, plain.Hp);
        }

        [Test]
        public void RiggedBarricades_EnemiesDontBreakOneThatWouldHurtOnlyThem()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Barricade, Technique.RiggedBarricades, Technique.BlastProof) },
                new[] { P(3, 0) }, new[] { E(UnitStats.Brute, 3, 3) });
            var sapper = Player(b);
            b.PlayerUseAction(sapper, ActionKind.Barricade, P(3, 1));
            var brute = Enemy(b);
            // Wear it down so one hit breaks it.
            sapper.Ap = 1;
            b.PlayerAttackBarricade(sapper, P(3, 1));
            Assert.IsFalse(EnemyAI.RiggedBlastHurtsUs(b, brute, P(3, 1)), "out of the blast: fine to break");
            brute.Pos = P(3, 2);
            Assert.IsTrue(EnemyAI.RiggedBlastHurtsUs(b, brute, P(3, 1)), "standing in it, with Blast Proof sparing the sapper");
        }

        // ---------- Sapper: Big Mines ----------

        [Test]
        public void BigMines_BiggerButOnlyTwoOut()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.BigMines, Technique.QuickFuse) }, new[] { P(3, 1) }, new[] { Far });
            var sapper = Player(b);
            Assert.AreEqual(2, Battle.MaxMinesFor(sapper));
            b.PlayerPlaceMine(sapper, P(3, 2));
            Assert.AreEqual(Battle.BigMineDamage, b.MineAt(P(3, 2)).Damage);
            NextTurn(b);
            b.PlayerPlaceMine(sapper, P(2, 1));
            NextTurn(b);
            Assert.IsFalse(b.CanPlaceMine(sapper, P(4, 1)), "a third is one too many");
        }
    }
}
