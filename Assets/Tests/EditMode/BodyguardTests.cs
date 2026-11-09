using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>The Bandit Brute's Bodyguard routine: it stands next to a crossbowman so Guard covers it.</summary>
    public class BodyguardTests
    {
        private static GridPos P(int x, int y) => new GridPos(x, y);

        /// <summary>A started 6x6 battle with one Warrior placed where told.</summary>
        private static Battle Setup(GridPos warriorAt, params (UnitStats, GridPos)[] enemies)
        {
            var b = new Battle(new Board(6, 6), new[] { new ArmyMember(UnitStats.Warrior) }, enemies);
            var warrior = b.Units.First(u => u.Team == Team.Player);
            Assert.IsTrue(b.Place(warrior, P(0, 0)));
            Assert.IsTrue(b.StartBattle());
            warrior.Pos = warriorAt;
            return b;
        }

        private static Unit Of(Battle b, UnitStats stats) => b.Units.First(u => u.Stats == stats);

        [Test]
        public void ThreatenedCrossbowman_WithNobodyToHit_ItWalksNextToIt()
        {
            // The warrior is 3 tiles from the crossbowman (threatened) but out of the brute's reach.
            var b = Setup(P(3, 2), (UnitStats.Crossbowman, P(3, 5)), (UnitStats.Brute, P(2, 4)));
            var brute = Of(b, UnitStats.Brute);
            Assert.AreEqual(EnemyBehaviour.Bodyguard, EnemyAI.BehaviourOf(brute.Stats));
            Assert.AreEqual(IntentKind.Guard, EnemyAI.PredictIntent(b, brute).Kind);

            b.EndPlayerTurn();
            Assert.AreEqual(1, Battle.Distance(brute, Of(b, UnitStats.Crossbowman)), "next to the crossbowman after its turn");
        }

        [Test]
        public void ACrossbowmanAnotherBruteGuards_IsLeftToIt()
        {
            // One brute already stands next to the threatened crossbowman; the second presses on towards the warrior.
            var b = Setup(P(3, 2), (UnitStats.Crossbowman, P(3, 5)), (UnitStats.Brute, P(2, 5)), (UnitStats.Brute, P(1, 4)));
            var second = b.Units.Last(u => u.Stats == UnitStats.Brute);
            var warrior = b.Units.First(u => u.Team == Team.Player);
            Assert.IsNull(EnemyAI.GuardPlan(b, second), "nothing left for it to guard");
            Assert.AreNotEqual(IntentKind.Guard, EnemyAI.PredictIntent(b, second).Kind);
            Assert.IsNotNull(EnemyAI.GuardPlan(b, b.Units.First(u => u.Stats == UnitStats.Brute)), "the first one keeps guarding");

            var before = Battle.Distance(second, warrior);
            b.EndPlayerTurn();
            Assert.Less(Battle.Distance(second, warrior), before);
        }

        [Test]
        public void UnthreatenedCrossbowman_ItWalksUpInstead()
        {
            // Nobody within 3 tiles of the crossbowman: the brute presses on like a plain brute.
            var b = Setup(P(0, 0), (UnitStats.Crossbowman, P(3, 5)), (UnitStats.Brute, P(2, 4)));
            var brute = Of(b, UnitStats.Brute);
            var warrior = b.Units.First(u => u.Team == Team.Player);
            Assert.AreNotEqual(IntentKind.Guard, EnemyAI.PredictIntent(b, brute).Kind);
            var before = Battle.Distance(brute, warrior);

            b.EndPlayerTurn();
            Assert.Less(Battle.Distance(brute, warrior), before);
        }

        [Test]
        public void ItPicksAGuardTileFromWhichItCanHit()
        {
            // The crossbowman shoots from where it stands; the brute steps to (2,2), next to both it and the warrior.
            var b = Setup(P(1, 2), (UnitStats.Crossbowman, P(2, 3)), (UnitStats.Brute, P(3, 2)));
            var brute = Of(b, UnitStats.Brute);
            var warrior = b.Units.First(u => u.Team == Team.Player);
            Assert.AreEqual(IntentKind.BullRush, EnemyAI.PredictIntent(b, brute).Kind);

            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<MoveEvent>().Any(m => m.Unit == brute && m.To == P(2, 2)), "it walked to the tile next to both");
            Assert.IsTrue(events.OfType<AttackEvent>().Any(a => a.Attacker == brute && a.Target == warrior), "and hit the warrior from there");
        }

        [Test]
        public void WithNoCrossbowmanLeft_ItWalksUpLikeAPlainBrute()
        {
            var b = Setup(P(2, 0), (UnitStats.Brute, P(2, 4)));
            var brute = Of(b, UnitStats.Brute);
            var warrior = b.Units.First(u => u.Team == Team.Player);
            var before = Battle.Distance(brute, warrior);

            b.EndPlayerTurn();
            Assert.Less(Battle.Distance(brute, warrior), before);
        }
    }
}
