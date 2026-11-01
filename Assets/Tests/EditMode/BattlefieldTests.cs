using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>Battlefields: one rule per fight, shown on the map node.</summary>
    public class BattlefieldTests
    {
        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        /// <summary>A started battle on this battlefield; the player units are moved where told after the start (null keeps them in place).</summary>
        private static Battle Setup(Battlefield? field, UnitStats[] army, GridPos[] at, params (UnitStats, GridPos)[] enemies)
        {
            var b = new Battle(new Board(8, 8), army.Select(s => new ArmyMember(s)), enemies);
            b.SetField(field);
            for (var i = 0; i < army.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), P(i, 0)));
            Assert.IsTrue(b.StartBattle());
            if (at != null)
                for (var i = 0; i < army.Length; i++)
                    Player(b, i).Pos = at[i];
            return b;
        }

        // ---------- Edge of the World ----------

        [Test]
        public void Edge_APushOffTheBoardKills()
        {
            var b = Setup(Battlefield.EdgeOfTheWorld, new[] { UnitStats.Warrior }, new[] { P(3, 6) },
                (UnitStats.Crossbowman, P(3, 7)), (UnitStats.Wolf, P(7, 3)));
            var crossbow = Enemy(b);
            Assert.IsTrue(b.WouldFall(crossbow, P(0, 1), Player(b)));
            var events = b.PlayerUseAction(Player(b), ActionKind.ShieldBash, crossbow.Pos);
            Assert.IsFalse(crossbow.IsAlive);
            Assert.IsTrue(crossbow.Fell);
            Assert.IsFalse(crossbow.IsRemains, "no bone pile");
            Assert.IsTrue(events.OfType<FallEvent>().Any());
        }

        [Test]
        public void Edge_WithoutItTheBoardEdgeIsAWall()
        {
            var b = Setup(null, new[] { UnitStats.Warrior }, new[] { P(3, 6) },
                (UnitStats.Crossbowman, P(3, 7)), (UnitStats.Wolf, P(7, 3)));
            var crossbow = Enemy(b);
            b.PlayerUseAction(Player(b), ActionKind.ShieldBash, crossbow.Pos);
            Assert.AreEqual(P(3, 7), crossbow.Pos);
            Assert.AreEqual(UnitStats.Crossbowman.MaxHp - Battle.CollisionDamage, crossbow.Hp);
        }

        [Test]
        public void Edge_HugeUnitsDontFall()
        {
            var b = Setup(Battlefield.EdgeOfTheWorld, new[] { UnitStats.Warrior }, new[] { P(3, 6) },
                (UnitStats.Brute, P(3, 7)), (UnitStats.Wolf, P(7, 3)));
            var brute = Enemy(b);
            b.PlayerUseAction(Player(b), ActionKind.ShieldBash, brute.Pos);
            Assert.IsTrue(brute.IsAlive);
            Assert.AreEqual(P(3, 7), brute.Pos);
        }

        [Test]
        public void Edge_TheBruteBullRushesYouOffTheTower()
        {
            var b = Setup(Battlefield.EdgeOfTheWorld, new[] { UnitStats.Warrior }, new[] { P(3, 0) }, (UnitStats.Brute, P(3, 1)));
            var warrior = Player(b);
            var intent = EnemyAI.PredictIntent(b, Enemy(b));
            Assert.AreEqual(IntentKind.BullRush, intent.Kind);
            Assert.AreEqual(warrior, intent.Target);
            b.EndPlayerTurn();
            Assert.IsTrue(warrior.Fell);
            Assert.AreEqual(Phase.Defeat, b.Phase);
        }

        // ---------- Storm ----------

        [Test]
        public void Storm_MarksOneUnitPerSideAndStrikesAfterTheEnemyTurn()
        {
            var b = Setup(Battlefield.Storm, new[] { UnitStats.Warrior }, null, (UnitStats.Wolf, P(7, 7)));
            var warrior = Player(b);
            var wolf = Enemy(b);
            CollectionAssert.AreEquivalent(new[] { warrior.Pos, wolf.Pos }, b.StormMarks);

            b.EndPlayerTurn();
            Assert.AreEqual(UnitStats.Warrior.MaxHp - Battlefields.StormDamage, warrior.Hp, "stayed on the mark");
            Assert.AreEqual(UnitStats.Wolf.MaxHp, wolf.Hp, "the wolf stepped off its mark");
            Assert.AreNotEqual(P(7, 7), wolf.Pos);
            Assert.AreEqual(2, b.StormMarks.Count, "new marks for the new round");
        }

        [Test]
        public void Storm_EnemiesNeverStopOnAMark()
        {
            var b = Setup(Battlefield.Storm, new[] { UnitStats.Warrior }, null, (UnitStats.Wolf, P(7, 7)));
            var wolf = Enemy(b);
            foreach (var mark in b.StormMarks)
                Assert.IsFalse(b.GetReachable(wolf).ContainsKey(mark));
        }

        // ---------- Arcane Overflow ----------

        [Test]
        public void ArcaneOverflow_CooldownsAreOneShorter()
        {
            var b = Setup(Battlefield.ArcaneOverflow, new[] { UnitStats.Warrior }, new[] { P(3, 3) },
                (UnitStats.Crossbowman, P(3, 4)), (UnitStats.Wolf, P(7, 7)));
            Assert.AreEqual(Battle.Cooldown(ActionKind.ShieldBash) - 1, b.CooldownIn(ActionKind.ShieldBash));
            Assert.AreEqual(Battle.Cooldown(ActionKind.Fireball) - 1, b.CooldownIn(ActionKind.Fireball));
            b.PlayerUseAction(Player(b), ActionKind.ShieldBash, P(3, 4));
            Assert.AreEqual(1, Player(b).CooldownLeft(ActionKind.ShieldBash), "ready again next turn");

            var plain = Setup(null, new[] { UnitStats.Warrior }, null, (UnitStats.Wolf, P(7, 7)));
            Assert.AreEqual(Battle.Cooldown(ActionKind.ShieldBash), plain.CooldownIn(ActionKind.ShieldBash));
        }

        // ---------- Fortune's Favored ----------

        [Test]
        public void Favored_OneUnitPerSideEachRound()
        {
            var b = Setup(Battlefield.FortunesFavored, new[] { UnitStats.Warrior, UnitStats.Archer }, null,
                (UnitStats.Wolf, P(7, 7)), (UnitStats.Wolf, P(6, 7)));
            Assert.AreEqual(1, b.Units.Count(u => u.Team == Team.Player && u.Favored));
            Assert.AreEqual(1, b.Units.Count(u => u.Team == Team.Enemy && u.Favored));
        }

        [Test]
        public void Favored_ABossAloneMeansNoFavoredEnemy()
        {
            var b = Setup(Battlefield.FortunesFavored, new[] { UnitStats.Warrior }, null, (UnitStats.Ogre, P(4, 6)));
            for (var round = 0; round < 5 && !b.IsOver; round++)
            {
                Assert.IsFalse(Enemy(b).Favored, "a boss is never favored");
                Assert.IsTrue(Player(b).Favored, "your side still gets one");
                Player(b).Hp = Player(b).MaxHp;
                b.EndPlayerTurn();
            }
        }

        [Test]
        public void Favored_TheNecromancersMinionsStayEligible()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var b = new Battle(new Board(8, 8), new[] { new ArmyMember(UnitStats.Warrior) },
                    new[] { (UnitStats.Necromancer, P(0, 7)), (UnitStats.Skeleton, P(7, 7)) }) { Rng = new SeededRng(seed) };
                b.SetField(Battlefield.FortunesFavored);
                b.Place(Player(b), P(0, 0));
                b.StartBattle();
                Assert.IsFalse(b.Units.First(u => u.Stats == UnitStats.Necromancer).Favored);
                Assert.IsTrue(b.Units.First(u => u.Stats == UnitStats.Skeleton).Favored);
            }
        }

        [Test]
        public void Favored_DealsDoubleDamage()
        {
            var b = Setup(Battlefield.FortunesFavored, new[] { UnitStats.Warrior }, new[] { P(3, 3) },
                (UnitStats.Brute, P(3, 4)), (UnitStats.Wolf, P(7, 7)));
            var warrior = Player(b);
            var brute = Enemy(b);
            warrior.Favored = true;
            var expected = UnitStats.Warrior.Damage * Battlefields.FavoredMultiplier;
            Assert.AreEqual(expected, b.PredictHpLoss(warrior, brute, out _));
            b.PlayerAttack(warrior, brute);
            Assert.AreEqual(UnitStats.Brute.MaxHp - expected, brute.Hp);
        }

        // ---------- What's On His Mind? ----------

        [Test]
        public void HiddenMinds_HidesIntents()
        {
            Assert.IsTrue(Setup(Battlefield.HiddenMinds, new[] { UnitStats.Warrior }, null, (UnitStats.Wolf, P(7, 7))).IntentsHidden);
            Assert.IsFalse(Setup(null, new[] { UnitStats.Warrior }, null, (UnitStats.Wolf, P(7, 7))).IntentsHidden);
        }

        // ---------- Map ----------

        [Test]
        public void Map_EveryFightButTheScenariosHasABattlefield()
        {
            for (var seed = 0; seed < 20; seed++)
            {
                var run = new Run(seed);
                Battlefield[] last = { };
                foreach (var step in run.Map)
                {
                    foreach (var node in step)
                    {
                        var fight = node.Kind == NodeKind.Battle || node.Kind == NodeKind.Elite || node.Kind == NodeKind.Boss;
                        Assert.AreEqual(fight, node.Field.HasValue, $"seed {seed}, {node.Kind}");
                    }

                    var fields = step.Where(n => n.Field.HasValue).Select(n => n.Field.Value).ToArray();
                    Assert.AreEqual(fields.Length, fields.Distinct().Count(), "a step's fights differ");
                    if (fields.Length > 0)
                    {
                        Assert.IsFalse(fields.Any(last.Contains), "no repeat from the step before");
                        last = fields;
                    }
                }
            }
        }
    }
}
