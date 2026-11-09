using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary><see cref="ComboTally"/>: counting the enemy factions' combos from battle events.</summary>
    public class ComboTallyTests
    {
        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        private static (UnitStats, GridPos) E(UnitStats s, int x, int y) => (s, P(x, y));

        private static ArmyMember M(UnitStats stats) => new ArmyMember(stats);

        /// <summary>A started 6x6 battle where players stand where told (anywhere, not only the back rows).</summary>
        private static Battle Setup(ArmyMember[] army, GridPos[] at, params (UnitStats, GridPos)[] enemies)
        {
            var b = new Battle(new Board(6, 6), army, enemies);
            for (var i = 0; i < army.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), P(i, 0)));
            Assert.IsTrue(b.StartBattle());
            for (var i = 0; i < army.Length; i++)
                Player(b, i).Pos = at[i];
            return b;
        }

        [Test]
        public void ThreeWolvesOnOneUnit_CountAChainAndASurround()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 2) },
                E(UnitStats.Wolf, 1, 2), E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 2, 3));
            var tally = new ComboTally();
            tally.Observe(b.EndPlayerTurn(), 1);

            Assert.AreEqual(3, tally["wolves.bites"]);
            Assert.AreEqual(3, tally["wolves.chainMax"]);
            Assert.AreEqual(1, tally["wolves.surrounds"]);
            Assert.AreEqual(0 + 1 + 2, tally["wolves.chainBonus"], "+1 for the second bite, +2 for the third");
        }

        [Test]
        public void HellswapThenDemonHit_CountsAFollowUp()
        {
            var b = Setup(new[] { M(UnitStats.Warrior), M(UnitStats.Archer) }, new[] { P(4, 0), P(2, 1) },
                E(UnitStats.Demon, 2, 4), E(UnitStats.Imp, 2, 3));
            var tally = new ComboTally();
            tally.Observe(b.EndPlayerTurn(), 1);

            Assert.AreEqual(1, tally["demons.hellswaps"]);
            Assert.AreEqual(1, tally["demons.swapFollowUps"]);
            Assert.Greater(tally["demons.swapFollowUpDamage"], 0);
        }

        [Test]
        public void BruteGuardingACrossbowman_CountsAsGuardForShooter()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 2) },
                E(UnitStats.Crossbowman, 2, 3), E(UnitStats.Brute, 3, 3));
            var tally = new ComboTally();
            tally.Observe(b.PlayerAttack(Player(b), Enemy(b, 0)), 1);

            Assert.AreEqual(1, tally["bandits.guards"]);
            Assert.AreEqual(1, tally["bandits.guardsForShooter"]);
        }

        [Test]
        public void Merge_AddsCountsAndKeepsTheHighestMax()
        {
            var a = new ComboTally();
            var b = new ComboTally();
            var battle = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 2) },
                E(UnitStats.Wolf, 1, 2), E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 2, 3));
            b.Observe(battle.EndPlayerTurn(), 1);
            a.Merge(b);
            a.Merge(b);

            Assert.AreEqual(6, a["wolves.bites"]);
            Assert.AreEqual(3, a["wolves.chainMax"], "a Max key keeps the highest, not the sum");
        }
    }
}
