using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>The Moon Shrine fight (defend the objective) and the omens it hands out.</summary>
    public class RitualTests
    {
        private static readonly GridPos Shrine = new GridPos(3, 3);

        // A wolf boxed into the far corner: it can't move or attack, so turns pass quietly.
        private static readonly GridPos BoxedTile = new GridPos(7, 7);
        private static readonly GridPos[] BoxRocks = { new GridPos(6, 7), new GridPos(7, 6) };

        private static Battle Ritual(UnitStats[] army, (UnitStats, GridPos)[] enemies, int turns = 5,
            params (int, UnitStats)[] waves)
        {
            var b = new Battle(new Board(8, 8, BoxRocks), army, enemies);
            var zone = new Board(8, 8).AllTiles().Where(p => p != Shrine && p.StepsTo(Shrine) <= 2);
            b.SetupRitual(UnitStats.MoonShrine, Shrine, zone, turns, waves);
            return b;
        }

        private static Battle Plain(UnitStats[] army, (UnitStats, GridPos)[] enemies, params GridPos[] rocks) =>
            new Battle(new Board(8, 8, rocks), army, enemies);

        private static Unit Player(Battle b, int index = 0) =>
            b.Units.Where(u => u.Team == Team.Player && !u.IsObjective).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static void Start(Battle b, params GridPos[] placements)
        {
            for (var i = 0; i < placements.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), placements[i]), $"place at {placements[i]}");
            Assert.IsTrue(b.StartBattle());
        }

        // ---------- The ritual fight ----------

        [Test]
        public void Ritual_DeployAroundTheShrine_ShrineCantBeMovedOrAct()
        {
            var b = Ritual(new[] { UnitStats.Warrior }, new[] { (UnitStats.Wolf, BoxedTile) });
            var warrior = Player(b);
            Assert.IsTrue(b.CanPlace(warrior, new GridPos(3, 1)));
            Assert.IsTrue(b.CanPlace(warrior, new GridPos(4, 4)));
            Assert.IsFalse(b.CanPlace(warrior, new GridPos(0, 0)), "the back rows aren't the zone here");
            Assert.IsFalse(b.CanPlace(warrior, Shrine));
            Assert.IsFalse(b.CanPlace(b.Objective, new GridPos(3, 2)));

            Start(b, new GridPos(3, 2));
            Assert.IsFalse(b.CanAct(b.Objective));
            Assert.IsFalse(b.CanStillAct(b.Objective));
            Assert.AreEqual(1, b.LivingArmy.Count(), "the shrine doesn't count as an army unit");
        }

        [Test]
        public void Ritual_SurvivingTheTurnsWins()
        {
            var b = Ritual(new[] { UnitStats.Warrior }, new[] { (UnitStats.Wolf, BoxedTile) }, turns: 3);
            Start(b, new GridPos(3, 2));
            b.EndPlayerTurn();
            b.EndPlayerTurn();
            Assert.IsFalse(b.IsOver);
            Assert.AreEqual(1, b.RitualTurnsLeft);
            b.EndPlayerTurn();
            Assert.AreEqual(Phase.Victory, b.Phase);
            Assert.IsTrue(b.RitualComplete);
            Assert.IsTrue(Enemy(b).IsAlive, "the rest flee; nobody has to die");
        }

        [Test]
        public void Ritual_ShrineBreaking_LosesTheFight()
        {
            var b = Ritual(new[] { UnitStats.Warrior }, new[] { (UnitStats.Wolf, BoxedTile) });
            Start(b, new GridPos(3, 2));
            b.Objective.Hp = 0;
            b.EndPlayerTurn();
            Assert.AreEqual(Phase.Defeat, b.Phase);
            Assert.IsTrue(b.ObjectiveLost);
        }

        [Test]
        public void Ritual_KillingEveryoneDoesntWinWhileWavesAreComing()
        {
            var b = Ritual(new[] { UnitStats.Warrior }, new[] { (UnitStats.Wolf, new GridPos(3, 5)) }, 5, (1, UnitStats.Wolf));
            Start(b, new GridPos(3, 4));
            var warrior = Player(b);
            warrior.Ap = 99;
            Enemy(b).Hp = 1;
            b.PlayerAttack(warrior, Enemy(b));
            Assert.IsFalse(b.IsOver);

            b.EndPlayerTurn();
            var arrived = b.Living(Team.Enemy).Single();
            var p = arrived.Pos;
            Assert.IsTrue(p.X == 0 || p.Y == 0 || p.X == 7 || p.Y == 7, "arrives on an edge");
            Assert.GreaterOrEqual(p.StepsTo(Shrine), Battle.RitualSpawnDistance);
            Assert.AreEqual(0, b.PendingWaves.Count);

            arrived.Hp = 1;
            warrior.Ap = 99;
            warrior.Pos = new GridPos(p.X, p.Y == 0 ? 1 : p.Y - 1);
            b.PlayerAttack(warrior, arrived);
            Assert.AreEqual(Phase.Victory, b.Phase, "with no waves left, killing everyone wins early");
        }

        [Test]
        public void Ritual_EnemiesGoForTheShrine_UnlessADefenderIsNextToThem()
        {
            var b = Ritual(new[] { UnitStats.Warrior }, new[] { (UnitStats.Elemental, new GridPos(3, 7)) });
            Start(b, new GridPos(1, 3));
            var elemental = Enemy(b);
            Assert.AreEqual(b.Objective, EnemyAI.ChooseTarget(b, elemental));

            b.EndPlayerTurn();
            Assert.Less(elemental.Pos.StepsTo(Shrine), 4, "walked towards the shrine, not the warrior");

            var warrior = Player(b);
            warrior.Pos = new GridPos(elemental.Pos.X + 1, elemental.Pos.Y);
            Assert.AreEqual(warrior, EnemyAI.ChooseTarget(b, elemental));
        }

        [Test]
        public void Ritual_ShieldWallProtectsTheShrine()
        {
            var b = Ritual(new[] { UnitStats.Warrior }, new[] { (UnitStats.Elemental, new GridPos(3, 4)) });
            Start(b, new GridPos(2, 3));
            b.EndPlayerTurn();
            Assert.AreEqual(UnitStats.MoonShrine.MaxHp - (UnitStats.Elemental.Damage - 1), b.Objective.Hp);
        }

        [Test]
        public void MoonShrineEncounter_ShrineInTheMiddle_EnemiesOnTheEdgesAwayFromIt()
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var b = Encounter.MoonShrine.CreateBattle(ArmyMember.StartingArmy(), new System.Random(seed));
                Assert.AreEqual(Encounter.ShrineTile, b.Objective.Pos);
                Assert.AreEqual(Encounter.RitualTurns, b.RitualTurns);
                Assert.AreEqual(Encounter.RitualWaveTurns.Count * Encounter.RitualWaveSize, b.PendingWaves.Count);
                var enemies = b.Living(Team.Enemy).ToList();
                Assert.AreEqual(3, enemies.Count, $"seed {seed}");
                foreach (var e in enemies)
                {
                    Assert.AreEqual(UnitKind.Elemental, e.Stats.Kind);
                    Assert.GreaterOrEqual(e.Pos.StepsTo(Encounter.ShrineTile), Battle.RitualSpawnDistance);
                }

                Assert.IsTrue(b.Board.AllTiles().Count(b.IsPlayerZone) >= 12);
            }
        }

        // ---------- Break the Ritual ----------

        private static Battle Summoning((UnitStats, GridPos)[] enemies, GridPos[] totems, int summonTurn = 4)
        {
            var b = new Battle(new Board(8, 8, BoxRocks), new[] { UnitStats.Warrior }, enemies);
            b.SetupSummoning(UnitStats.Totem, totems, summonTurn);
            return b;
        }

        [Test]
        public void BreakRitual_SmashingEveryTotemWins_GuardsFlee()
        {
            var b = Summoning(new[] { (UnitStats.Wolf, BoxedTile) }, new[] { new GridPos(3, 2), new GridPos(5, 2) });
            Start(b, new GridPos(3, 1));
            var warrior = Player(b);
            warrior.Ap = 99;
            b.PlayerAttack(warrior, b.Totems[0]);
            b.PlayerAttack(warrior, b.Totems[0]);
            Assert.IsFalse(b.Totems[0].IsAlive);
            Assert.IsFalse(b.IsOver, "one totem still stands");

            warrior.Pos = new GridPos(5, 1);
            b.PlayerAttack(warrior, b.Totems[1]);
            b.PlayerAttack(warrior, b.Totems[1]);
            Assert.AreEqual(Phase.Victory, b.Phase);
            Assert.IsTrue(b.TotemsBroken);
            Assert.IsTrue(b.Units.Single(u => u.Stats == UnitStats.Wolf).IsAlive, "the guards flee; nobody has to die");
        }

        [Test]
        public void BreakRitual_SummoningCompletesAtTheStartOfTheEnemyTurn()
        {
            var b = Summoning(new[] { (UnitStats.Wolf, BoxedTile) }, new[] { new GridPos(3, 5) }, summonTurn: 2);
            Start(b, new GridPos(3, 0));
            Assert.AreEqual(2, b.SummonTurnsLeft);
            b.EndPlayerTurn();
            Assert.IsFalse(b.IsOver);
            Assert.AreEqual(1, b.SummonTurnsLeft);
            Assert.AreEqual(new GridPos(3, 5), b.Totems[0].Pos, "totems never move or act");

            var events = b.EndPlayerTurn();
            Assert.AreEqual(Phase.Defeat, b.Phase);
            Assert.IsTrue(b.SummoningComplete);
            Assert.IsTrue(b.ScenarioLost);
            Assert.AreEqual(0, events.Count, "the enemies don't get to act first");
        }

        [Test]
        public void BreakRitual_GoblinBombsDontHurtTheirTotems_YourMinesDo()
        {
            var b = Summoning(new[] { (UnitStats.GoblinBomber, BoxedTile) }, new[] { new GridPos(3, 5) });
            Start(b, new GridPos(3, 0));
            var bomber = Enemy(b, 0);
            var totem = b.Totems[0];
            b.Explode(new GridPos(3, 4), 3, "bomb", bomber, new System.Collections.Generic.List<BattleEvent>());
            Assert.AreEqual(UnitStats.Totem.MaxHp, totem.Hp);
            b.Explode(new GridPos(3, 4), 3, "mine", Player(b), new System.Collections.Generic.List<BattleEvent>());
            Assert.AreEqual(UnitStats.Totem.MaxHp - 3, totem.Hp);
        }

        [Test]
        public void Objectives_HaveNoZoneOfControl()
        {
            var b = Summoning(new[] { (UnitStats.Wolf, BoxedTile) }, new[] { new GridPos(3, 2) });
            Start(b, new GridPos(3, 1));
            Assert.IsFalse(b.InEnemyReach(Player(b), new GridPos(3, 1)));
            Assert.AreEqual(Battle.MoveCost, b.GetReachable(Player(b))[new GridPos(2, 1)]);
        }

        [Test]
        public void BreakRitual_GuardsGoForWhoeverThreatensATotem()
        {
            var b = new Battle(new Board(8, 8), new[] { UnitStats.Warrior, UnitStats.Archer },
                new[] { (UnitStats.Goblin, new GridPos(0, 4)) });
            b.SetupSummoning(UnitStats.Totem, new[] { new GridPos(6, 6) }, 4);
            Start(b, new GridPos(0, 1), new GridPos(7, 1));
            var goblin = Enemy(b);
            Assert.AreEqual(Player(b, 0), EnemyAI.ChooseTarget(b, goblin), "nobody near a totem: nearest as usual");

            Player(b, 1).Pos = new GridPos(6, 4);
            Assert.AreEqual(Player(b, 1), EnemyAI.ChooseTarget(b, goblin), "the archer next to the totem comes first");
            Assert.AreEqual(IntentKind.None, EnemyAI.PredictIntent(b, b.Totems[0]).Kind, "totems have no intent");
        }

        [Test]
        public void BreakRitualEncounter_ThreeTotemsOnTheFarHalf_GoblinGuards()
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var b = Encounter.BreakRitual.CreateBattle(ArmyMember.StartingArmy(), new System.Random(seed));
                Assert.AreEqual(Encounter.TotemCount, b.Totems.Count, $"seed {seed}");
                Assert.AreEqual(Encounter.SummonTurn, b.SummonTurn);
                foreach (var t in b.Totems)
                    Assert.IsTrue(t.Pos.Y >= 5 && t.Pos.Y <= 6);
                var guards = b.Living(Team.Enemy).Where(u => !u.IsObjective).ToList();
                Assert.AreEqual(3, guards.Count(g => g.Stats == UnitStats.GoblinBomber));
                Assert.AreEqual(2, guards.Count(g => g.Stats == UnitStats.Goblin));
                Assert.IsNull(b.Objective);
            }
        }

        // ---------- Omens ----------

        [Test]
        public void SunBlessing_MarksAnEnemyTile_StrikesItAtTheEnemyTurn()
        {
            var b = Plain(new[] { UnitStats.Warrior }, new[] { (UnitStats.Wolf, BoxedTile) }, BoxRocks);
            b.AddOmen(Omen.Sun, Team.Player);
            Start(b, new GridPos(0, 0));
            Assert.AreEqual(BoxedTile, b.SunMarks.Single());
            Assert.IsTrue(b.OpeningEvents.OfType<OmenEvent>().Any(e => e.Omen == Omen.Sun && e.Blessing));

            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<ExplosionEvent>().Any(e => e.Cause == "sun"));
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.SunDamage, Enemy(b).Hp);
        }

        [Test]
        public void SunCurse_StepOffTheMarkedTileToDodgeIt()
        {
            var b = Plain(new[] { UnitStats.Warrior }, new[] { (UnitStats.Wolf, BoxedTile) }, BoxRocks);
            b.AddOmen(Omen.Sun, Team.Enemy);
            Start(b, new GridPos(0, 0));
            Assert.AreEqual(new GridPos(0, 0), b.SunMarks.Single());
            b.PlayerMove(Player(b), new GridPos(1, 0));
            b.EndPlayerTurn();
            Assert.AreEqual(UnitStats.Warrior.MaxHp, Player(b).Hp);

            // Standing still on the next mark hurts.
            b.EndPlayerTurn();
            Assert.AreEqual(UnitStats.Warrior.MaxHp - Battle.SunDamage, Player(b).Hp);
        }

        [Test]
        public void NightBlessing_EnemiesCantShootIntoTheShroud()
        {
            var b = Plain(new[] { UnitStats.Archer }, new[] { (UnitStats.Crossbowman, new GridPos(3, 4)) });
            b.AddOmen(Omen.Night, Team.Player);
            Start(b, new GridPos(3, 0));
            var archer = Player(b);
            var crossbow = Enemy(b);
            Assert.IsTrue(b.IsShroudedFrom(archer.Pos, Team.Enemy));
            Assert.IsFalse(b.IsShroudedFrom(archer.Pos, Team.Player));
            Assert.IsFalse(b.CanHitFrom(crossbow, crossbow.Pos, archer), "no shot from range into the shroud");
            Assert.IsTrue(b.CanHitFrom(crossbow, new GridPos(3, 1), archer), "point blank still works");
            Assert.IsTrue(b.CanHitFrom(archer, archer.Pos, crossbow), "the shroud doesn't stop your own shots");
        }

        [Test]
        public void NightCurse_YourRangedCantHitTheShroudedEnemy()
        {
            var b = Plain(new[] { UnitStats.Archer }, new[] { (UnitStats.Wolf, new GridPos(3, 4)) });
            b.AddOmen(Omen.Night, Team.Enemy);
            Start(b, new GridPos(3, 0));
            Assert.IsFalse(b.CanAttack(Player(b), Enemy(b)));
        }

        [Test]
        public void MoonBlessing_ShieldAbsorbsTheEnemyTurn()
        {
            var b = Plain(new[] { UnitStats.Warrior }, new[] { (UnitStats.Wolf, new GridPos(3, 1)) });
            b.AddOmen(Omen.Moon, Team.Player);
            Start(b, new GridPos(3, 0));
            Assert.AreEqual(Battle.MoonShield, Player(b).MoonShield);
            b.EndPlayerTurn();
            Assert.AreEqual(UnitStats.Warrior.MaxHp, Player(b).Hp, "two wolf bites (2 each) fit in the shield");
            Assert.AreEqual(Battle.MoonShield, Player(b).MoonShield, "a fresh shield each turn, not stacked");
        }

        [Test]
        public void MoonCurse_ShieldsAnEnemy()
        {
            var b = Plain(new[] { UnitStats.Warrior }, new[] { (UnitStats.Wolf, new GridPos(3, 1)) });
            b.AddOmen(Omen.Moon, Team.Enemy);
            Start(b, new GridPos(3, 0));
            b.PlayerAttack(Player(b), Enemy(b));
            Assert.AreEqual(UnitStats.Wolf.MaxHp, Enemy(b).Hp);
        }

        [Test]
        public void Omens_NeverPickTheShrine()
        {
            var b = Ritual(new[] { UnitStats.Warrior }, new[] { (UnitStats.Wolf, BoxedTile) });
            b.AddOmen(Omen.Moon, Team.Player);
            b.AddOmen(Omen.Sun, Team.Enemy);
            Start(b, new GridPos(3, 2));
            for (var i = 0; i < 4 && !b.IsOver; i++)
            {
                Assert.AreEqual(0, b.Objective.MoonShield);
                Assert.IsFalse(b.SunMarks.Contains(Shrine));
                b.EndPlayerTurn();
            }
        }
    }
}
