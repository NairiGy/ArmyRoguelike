using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>Battle rules from the overnight pass: champions and the bosses' Stone Heart phases.</summary>
    public class NightBattleTests
    {
        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static Battle Setup(UnitStats[] army, GridPos[] placements, (UnitStats, GridPos)[] enemies)
        {
            var b = new Battle(new Board(6, 6), army.Select(s => new ArmyMember(s)), enemies);
            for (var i = 0; i < placements.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), placements[i]));
            return b;
        }

        // ---------- Champions ----------

        [Test]
        public void Champion_IsTheToughestEnemy_WithExtraHp()
        {
            var b = Setup(new[] { UnitStats.Warrior }, new[] { new GridPos(2, 0) },
                new[] { (UnitStats.Wolf, new GridPos(1, 4)), (UnitStats.PackLeader, new GridPos(3, 4)) });
            var champ = b.MakeChampion(Affix.Armored);
            Assert.AreEqual(UnitStats.PackLeader, champ.Stats);
            Assert.AreEqual(champ, b.Champion);
            Assert.IsTrue(b.StartBattle());
            Assert.AreEqual(champ.MaxHp, champ.Hp);
            Assert.AreEqual(UnitStats.PackLeader.MaxHp + Champions.ExtraHp, champ.MaxHp);
        }

        [Test]
        public void Champion_Armored_TakesLessDamage()
        {
            var b = Setup(new[] { UnitStats.Warrior }, new[] { new GridPos(2, 0) }, new[] { (UnitStats.Brute, new GridPos(2, 4)) });
            var brute = b.MakeChampion(Affix.Armored);
            b.StartBattle();
            var warrior = Player(b);
            warrior.Pos = new GridPos(2, 3);
            var hp = brute.Hp;
            b.PlayerAttack(warrior, brute);
            Assert.AreEqual(hp - (UnitStats.Warrior.Damage - Champions.ArmoredReduction), brute.Hp);
        }

        [Test]
        public void Champion_Vengeful_HurtsAdjacentFoesWhenItDies()
        {
            var b = Setup(new[] { UnitStats.Warrior, UnitStats.Archer }, new[] { new GridPos(2, 0), new GridPos(3, 0) },
                new[] { (UnitStats.Wolf, new GridPos(2, 4)) });
            var wolf = b.MakeChampion(Affix.Vengeful);
            b.StartBattle();
            var warrior = Player(b);
            warrior.Pos = new GridPos(2, 3);
            wolf.Hp = 1;
            var hp = warrior.Hp;
            var events = b.PlayerAttack(warrior, wolf);
            Assert.IsFalse(wolf.IsAlive);
            Assert.AreEqual(hp - Champions.VengefulDamage, warrior.Hp);
            Assert.IsTrue(events.OfType<ExplosionEvent>().Any(e => e.Cause == "vengeful"));
            Assert.AreEqual(UnitStats.Archer.MaxHp, Player(b, 1).Hp, "only units next to it");
        }

        [Test]
        public void Champion_Regenerating_HealsAtItsTurn()
        {
            var b = Setup(new[] { UnitStats.Warrior }, new[] { new GridPos(0, 0) }, new[] { (UnitStats.Brute, new GridPos(5, 5)) });
            var brute = b.MakeChampion(Affix.Regenerating);
            b.StartBattle();
            brute.Hp -= 5;
            var hp = brute.Hp;
            b.EndPlayerTurn();
            Assert.AreEqual(hp + Champions.RegenHeal, brute.Hp);
        }

        [Test]
        public void Champion_MightyAndFleet_ChangeItsNumbers()
        {
            var b = Setup(new[] { UnitStats.Warrior }, new[] { new GridPos(0, 0) },
                new[] { (UnitStats.Brute, new GridPos(5, 5)), (UnitStats.Crossbowman, new GridPos(4, 5)) });
            var brute = b.MakeChampion(Affix.Mighty);
            Assert.AreEqual(UnitStats.Brute.Damage + Champions.MightyDamage, brute.BaseDamage);
            var b2 = Setup(new[] { UnitStats.Warrior }, new[] { new GridPos(0, 0) }, new[] { (UnitStats.Brute, new GridPos(5, 5)) });
            var fleet = b2.MakeChampion(Affix.Fleet);
            Assert.AreEqual(UnitStats.Brute.Moves + Champions.FleetMoves, fleet.MaxMoves);
        }

        [Test]
        public void Champion_SurvivesCloning()
        {
            var b = Setup(new[] { UnitStats.Warrior }, new[] { new GridPos(0, 0) }, new[] { (UnitStats.Brute, new GridPos(5, 5)) });
            b.MakeChampion(Affix.Vengeful);
            b.StartBattle();
            var copy = b.Clone();
            Assert.AreEqual(Affix.Vengeful, Enemy(copy).Affix);
            Assert.AreEqual(Enemy(b).MaxHp, Enemy(copy).MaxHp);
        }

        [Test]
        public void Run_LaterBattlesHaveChampions_EarlyOnesDont()
        {
            var run = new Run(7);
            for (var step = 0; step < Run.Steps; step++)
                foreach (var node in run.Map[step])
                {
                    var fight = node.Kind == NodeKind.Battle || node.Kind == NodeKind.Elite;
                    Assert.AreEqual(fight && step >= Champions.FirstStep, node.Champion != Affix.None, $"step {step + 1} {node.Kind}");
                }
        }

        // ---------- Stone Heart ----------

        [Test]
        public void StoneHeart_BossLosesAtMostOnePhaseARound()
        {
            var b = Setup(new[] { UnitStats.Warrior }, new[] { new GridPos(2, 1) }, new[] { (UnitStats.Ogre, new GridPos(2, 3)) });
            b.StartBattle();
            var giant = Enemy(b);
            var warrior = Player(b);
            warrior.Pos = new GridPos(2, 2);
            var floor = Battle.NextPhaseHp(giant);
            giant.Hp = floor + 1;
            warrior.Ap = 5;
            var events = b.PlayerAttack(warrior, giant);
            Assert.AreEqual(floor, giant.Hp, "the damage past the phase is lost");
            Assert.AreEqual(2, giant.BossPhase);
            Assert.IsTrue(events.OfType<StatusEvent>().Any(s => s.Kind == StatusKind.Phase && s.Amount == 2));
            b.PlayerAttack(warrior, giant);
            Assert.AreEqual(floor, giant.Hp, "no more damage this round");
        }

        [Test]
        public void StoneHeart_GiantLastPhase_MarksRockfall()
        {
            var b = Setup(new[] { UnitStats.Warrior }, new[] { new GridPos(0, 0) }, new[] { (UnitStats.Ogre, new GridPos(3, 3)) });
            b.StartBattle();
            var giant = Enemy(b);
            giant.BossPhase = Battle.BossPhases;
            b.EndPlayerTurn();
            var warrior = Player(b);
            Assert.IsTrue(b.StormMarks.Contains(warrior.Pos) && b.IsRockfallMark(warrior.Pos) || !warrior.IsAlive);
        }

        [Test]
        public void StoneHeart_NecromancerRaisesSkeletonsOnANewPhase()
        {
            var b = Setup(new[] { UnitStats.Warrior }, new[] { new GridPos(2, 1) }, new[] { (UnitStats.Necromancer, new GridPos(2, 3)) });
            b.StartBattle();
            var necro = Enemy(b);
            var warrior = Player(b);
            warrior.Pos = new GridPos(2, 2);
            necro.Hp = Battle.NextPhaseHp(necro) + 1;
            var events = b.PlayerAttack(warrior, necro);
            Assert.AreEqual(Battle.PhaseSkeletons, events.OfType<ReinforceEvent>().Sum(r => r.Units.Count));
            Assert.AreEqual(1 + Battle.PhaseSkeletons, b.Living(Team.Enemy).Count());
        }

        // ---------- Crumbling Roof ----------

        [Test]
        public void CrumblingRoof_CollisionAndDeathOpenAHole_PushedUnitFallsIn()
        {
            var b = new Battle(new Board(6, 6, new[] { new GridPos(2, 5) }), new[] { new ArmyMember(UnitStats.Warrior), new ArmyMember(UnitStats.Warrior) },
                new[] { (UnitStats.Crossbowman, new GridPos(2, 4)), (UnitStats.Wolf, new GridPos(3, 4)) });
            b.SetField(Battlefield.CrumblingRoof);
            Assert.IsTrue(b.Place(Player(b, 0), new GridPos(0, 0)));
            Assert.IsTrue(b.Place(Player(b, 1), new GridPos(1, 0)));
            b.StartBattle();
            var w1 = Player(b, 0);
            var w2 = Player(b, 1);
            var bow = Enemy(b, 0);
            var wolf = Enemy(b, 1);
            w1.Pos = new GridPos(2, 3);
            bow.Hp = 10;
            b.PlayerUseAction(w1, ActionKind.ShieldBash, bow.Pos);
            Assert.AreEqual(1, b.FloorCracks[new GridPos(2, 4)], "the slam into the rock cracks the floor");
            bow.Hp = 1;
            w1.Ap = 5;
            w1.IsDone = false;
            b.PlayerAttack(w1, bow);
            Assert.IsFalse(bow.IsAlive, $"bow hp {bow.Hp} at {bow.Pos}, w1 {w1}");
            Assert.IsTrue(b.Board.IsHole(new GridPos(2, 4)), "a death on the cracked tile opens a hole");
            b.EndUnitTurn(w1);
            w2.Pos = new GridPos(4, 4);
            Assert.IsTrue(b.WouldFall(wolf, new GridPos(-1, 0), w2));
            var events = b.PlayerUseAction(w2, ActionKind.ShieldBash, wolf.Pos);
            Assert.IsFalse(wolf.IsAlive, $"wolf {wolf}, w2 {w2}, events {string.Join(",", events.Select(e => e.GetType().Name))}");
            Assert.IsTrue(wolf.Fell);
            Assert.IsTrue(events.OfType<FallEvent>().Any());
        }

        [Test]
        public void CrumblingRoof_HolesDontBlockSight()
        {
            var board = new Board(6, 6);
            board.AddHole(new GridPos(2, 2));
            Assert.IsTrue(board.IsObstacle(new GridPos(2, 2)));
            Assert.IsFalse(board.IsRock(new GridPos(2, 2)));
            Assert.IsTrue(board.HasLineOfSight(new GridPos(2, 0), new GridPos(2, 4)));
        }

        [Test]
        public void CrumblingRoof_OffOtherFields_NothingCracks()
        {
            var b = new Battle(new Board(6, 6, new[] { new GridPos(2, 5) }), new[] { new ArmyMember(UnitStats.Warrior) },
                new[] { (UnitStats.Crossbowman, new GridPos(2, 4)) });
            Assert.IsTrue(b.Place(Player(b), new GridPos(0, 0)));
            b.StartBattle();
            Player(b).Pos = new GridPos(2, 3);
            b.PlayerUseAction(Player(b), ActionKind.ShieldBash, Enemy(b).Pos);
            Assert.AreEqual(0, b.FloorCracks.Count);
        }

        [Test]
        public void Boss_StrengthenedByArmyLevels()
        {
            var b = Setup(new[] { UnitStats.Warrior }, new[] { new GridPos(0, 0) }, new[] { (UnitStats.Ogre, new GridPos(3, 3)) });
            b.StrengthenBosses(6);
            Assert.AreEqual(UnitStats.Ogre.MaxHp + 6, Enemy(b).MaxHp);
            Assert.AreEqual(Enemy(b).MaxHp, Enemy(b).Hp);
        }
    }
}
