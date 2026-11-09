using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    public class TechniqueTests
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

        private static ArmyMember M(UnitStats stats, params Technique[] techniques) => new ArmyMember(stats, null, techniques);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        // ---------- Learning ----------

        [Test]
        public void Learn_OnlyOwnLine()
        {
            var warrior = M(UnitStats.Warrior);
            Assert.IsFalse(warrior.Learn(Technique.Fireball), "a mage technique");
            Assert.IsTrue(warrior.Learn(Technique.Sweep));
            Assert.IsTrue(warrior.Learn(Technique.Guard));
            Assert.IsFalse(warrior.Learn(Technique.Sweep), "already known");
            Assert.AreEqual(2, warrior.Level);
        }

        [Test]
        public void Learn_NeedsTheRequirement()
        {
            var mage = M(UnitStats.Apprentice);
            Assert.IsFalse(mage.Learn(Technique.Mend), "needs Ward");
            Assert.IsFalse(Technique.LearnableBy(mage).Contains(Technique.Mend));
            Assert.IsTrue(mage.Learn(Technique.Ward));
            Assert.IsTrue(Technique.LearnableBy(mage).Contains(Technique.Mend));
            Assert.IsTrue(mage.Learn(Technique.Mend));
        }

        [Test]
        public void Forget_KeepsTheLevel_AndDropsDependents()
        {
            var mage = M(UnitStats.Apprentice, Technique.Fireball, Technique.Burning, Technique.Ward);
            Assert.AreEqual(3, mage.Level);
            Assert.IsTrue(mage.Forget(Technique.Fireball));
            Assert.IsFalse(mage.Knows(Technique.Burning), "Burning needs Fireball");
            Assert.IsTrue(mage.Knows(Technique.Ward));
            Assert.AreEqual(3, mage.Level);
        }

        [Test]
        public void Forget_RelearningGivesNoNewLevel()
        {
            var mage = M(UnitStats.Apprentice, Technique.Fireball, Technique.Burning);
            mage.Forget(Technique.Fireball);
            Assert.IsTrue(Technique.LearnableBy(mage).Contains(Technique.Fireball), "forgotten techniques stay learnable");
            Assert.IsFalse(mage.WouldLevelUp(Technique.Fireball));
            Assert.IsTrue(mage.Learn(Technique.Fireball));
            Assert.IsTrue(mage.Knows(Technique.Fireball));
            Assert.AreEqual(2, mage.Level);

            Assert.IsFalse(mage.WouldLevelUp(Technique.Burning), "dropped along with Fireball, but learned before");
            Assert.IsTrue(mage.Learn(Technique.Burning));
            Assert.AreEqual(2, mage.Level);

            Assert.IsTrue(mage.WouldLevelUp(Technique.Ward));
            Assert.IsTrue(mage.Learn(Technique.Ward));
            Assert.AreEqual(3, mage.Level, "a new technique still levels up");
        }

        [Test]
        public void Levels_ArcherGetsItsSecondDamageAtLevelSix()
        {
            var archer = M(UnitStats.Archer, Technique.SteadyAimPlus, Technique.Pierce, Technique.OpeningShot, Technique.Prey, Technique.LongShot);
            Assert.AreEqual(1, archer.LevelDamage, "level 5 still has one");
            archer.Learn(Technique.Vantage);
            Assert.AreEqual(6, archer.Level);
            Assert.AreEqual(2, archer.LevelDamage, "+1 at levels 3 and 6");
            CollectionAssert.AreEqual(new[] { 3, 6 }, Growth.DamageLevels(UnitStats.Apprentice).ToArray(), "the Mage keeps 3 and 6");
        }

        [Test]
        public void Catalog_EveryRequirementIsInTheSameLine()
        {
            Assert.AreEqual(62, Technique.All.Count);
            foreach (var t in Technique.All.Where(t => t.Requires != null))
                Assert.AreEqual(t.Line, t.Requires.Line, t.Name);
            Assert.AreEqual(Technique.All.Count, Technique.All.Select(t => t.Name).Distinct().Count(), "unique names");
        }

        // ---------- Levels ----------

        [Test]
        public void Levels_GrowByLine()
        {
            var warrior = M(UnitStats.Warrior, Technique.Sweep, Technique.Guard);
            Assert.AreEqual(4, warrior.LevelHp);
            Assert.AreEqual(0, warrior.LevelDamage);

            var rogue = M(UnitStats.Rogue, Technique.Pincer, Technique.Riposte);
            Assert.AreEqual(2, rogue.LevelHp);
            Assert.AreEqual(1, rogue.LevelDamage, "+1 at level 2");

            var archer = M(UnitStats.Archer, Technique.SteadyAimPlus, Technique.Pierce);
            Assert.AreEqual(0, archer.LevelDamage, "first damage at level 3");
            archer.Learn(Technique.Volley);
            Assert.AreEqual(1, archer.LevelDamage);
        }

        [Test]
        public void Levels_ApplyInBattle()
        {
            var b = Started(new[] { M(UnitStats.Rogue, Technique.Pincer, Technique.Riposte) }, new[] { P(3, 1) },
                new[] { (UnitStats.Wolf, P(7, 7)) });
            Assert.AreEqual(UnitStats.Rogue.MaxHp + 2, Player(b).MaxHp);
            Assert.AreEqual(UnitStats.Rogue.MaxHp + 2, Player(b).Hp, "starts at full HP");
            Assert.AreEqual(UnitStats.Rogue.Damage + 1, Player(b).BaseDamage);
        }

        [Test]
        public void Levels_SapperGetsAMineAtLevelThree()
        {
            var b = Started(new[] { M(UnitStats.Sapper, Technique.Barricade, Technique.ThrowBomb, Technique.BlastProof) },
                new[] { P(3, 1) }, new[] { (UnitStats.Wolf, P(7, 7)) });
            Assert.AreEqual(Battle.MaxMinesPerUnit + 1, Battle.MaxMinesFor(Player(b)));
        }

        // ---------- Techniques in battle ----------

        [Test]
        public void PassiveTechnique_Works()
        {
            var b = Started(new[] { M(UnitStats.Warrior, Technique.DoubleStrike) }, new[] { P(3, 1) },
                new[] { (UnitStats.Ogre, P(3, 2)) });
            var warrior = Player(b);
            b.PlayerAttack(warrior, Enemy(b));
            Assert.AreEqual(UnitStats.Ogre.MaxHp - 2 * warrior.BaseDamage, Enemy(b).Hp);
        }

        [Test]
        public void ActionTechnique_AddsTheAction()
        {
            var b = Started(new[] { M(UnitStats.Apprentice, Technique.Fireball), M(UnitStats.Apprentice) }, new[] { P(3, 0), P(0, 0) },
                new[] { (UnitStats.Wolf, P(3, 3)), (UnitStats.Wolf, P(7, 7)) });
            var mage = Player(b, 0);
            CollectionAssert.AreEqual(new[] { ActionKind.FrostNova, ActionKind.Fireball }, mage.Actions.ToArray());
            Assert.IsFalse(Player(b, 1).HasAction(ActionKind.Fireball));
            b.PlayerUseAction(mage, ActionKind.Fireball, P(3, 3));
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.FireballDamage, Enemy(b, 0).Hp);
            Assert.AreEqual(1, mage.TimesUsed(ActionKind.Fireball), "counted for the playtest log");
        }

        [Test]
        public void LongReach_HitsTwoTilesAway_AndStaysMelee()
        {
            var b = Started(new[] { M(UnitStats.Warrior, Technique.LongReach) }, new[] { P(3, 1) },
                new[] { (UnitStats.Wolf, P(3, 2)), (UnitStats.Wolf, P(3, 3)), (UnitStats.Wolf, P(7, 7)) });
            var warrior = Player(b);
            Assert.AreEqual(2, warrior.Range);
            Assert.IsFalse(warrior.IsRanged);
            Assert.IsTrue(b.CanAttack(warrior, Enemy(b, 1)));
        }
    }
}
