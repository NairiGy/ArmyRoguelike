using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>Bombs as ammo: a bomber lobs its bomb next to a goblin, which flings it at one of your units the same turn.</summary>
    public class BombAmmoTests
    {
        private static GridPos P(int x, int y) => new GridPos(x, y);

        private static Battle Setup(GridPos warriorAt, params (UnitStats, GridPos)[] enemies)
        {
            var b = new Battle(new Board(6, 6), new[] { new ArmyMember(UnitStats.Warrior) }, enemies);
            var warrior = b.Units.First(u => u.Team == Team.Player);
            Assert.IsTrue(b.Place(warrior, P(0, 0)));
            Assert.IsTrue(b.StartBattle());
            warrior.Pos = warriorAt;
            return b;
        }

        [Test]
        public void Bomber_LobsAmmoNextToAGoblin_WhichFlingsItAtOnce()
        {
            // The warrior is out of the bomber's reach (4 tiles) but 2 from the goblin.
            var b = Setup(P(2, 1), (UnitStats.GoblinBomber, P(2, 5)), (UnitStats.Goblin, P(2, 3)));
            var warrior = b.Units.First(u => u.Team == Team.Player);
            var bomber = b.Units.First(u => u.Stats == UnitStats.GoblinBomber);
            var goblin = b.Units.First(u => u.Stats == UnitStats.Goblin);

            var intent = EnemyAI.PredictIntent(b, bomber);
            Assert.AreEqual(IntentKind.Bomb, intent.Kind);
            Assert.AreEqual(goblin, intent.Thrown, "the intent names the goblin");
            Assert.AreEqual(warrior, intent.Target, "and the unit it will hit");

            var hp = warrior.Hp;
            var events = b.EndPlayerTurn();
            var throwTile = events.OfType<BombThrowEvent>().Single().Tile;
            Assert.AreEqual(1, throwTile.StepsTo(P(2, 3)), "the bomb landed next to the goblin");
            Assert.IsTrue(events.OfType<FlingEvent>().Any(f => f.Thrower == goblin && f.Thrown == null && f.Target == warrior), "the goblin flung it");
            Assert.AreEqual(hp - UnitStats.GoblinBomber.BombDamage, warrior.Hp, "it went off on the warrior at once");
            Assert.IsNull(b.BombAt(throwTile), "nothing left lying there");

            var tally = new ComboTally();
            tally.Observe(events, 1);
            Assert.AreEqual(1, tally["goblins.flungBombs"]);
        }

        [Test]
        public void WithoutAGoblin_ItBombsTheUnitAsBefore()
        {
            var b = Setup(P(2, 1), (UnitStats.GoblinBomber, P(2, 4)));
            var warrior = b.Units.First(u => u.Team == Team.Player);
            var events = b.EndPlayerTurn();
            Assert.AreEqual(warrior.Pos, events.OfType<BombThrowEvent>().Single().Tile);
        }
    }
}
