using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>The Warrior's active techniques and passives from the 2026-11-05 redesign.</summary>
    public class WarriorKitTests
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

        [Test]
        public void BaseKit_HasNoPassive_AndOnlyTheNewTechniquesAreOffered()
        {
            Assert.AreEqual(0, UnitStats.Warrior.Abilities.Count);
            var offered = Technique.All.Where(t => t.Line == UnitStats.Warrior).ToList();
            Assert.AreEqual(8, offered.Count);
            Assert.IsFalse(offered.Contains(Technique.Guard));
            Assert.IsFalse(offered.Contains(Technique.ShieldWall));
        }

        [Test]
        public void ShieldBash_HugeTakesTheDamageButDoesNotMove()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { E(UnitStats.Brute, 3, 2), Far });
            var brute = Enemy(b);
            b.PlayerUseAction(Player(b), ActionKind.ShieldBash, brute.Pos);
            Assert.AreEqual(P(3, 2), brute.Pos);
            Assert.AreEqual(UnitStats.Brute.MaxHp - Battle.BashDamage, brute.Hp, "no collision either");
        }

        [Test]
        public void Sweep_HitsTheTargetAndBothSides_FullDamage_NotAllies()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.Sweep), M(UnitStats.Archer) }, new[] { P(3, 2), P(2, 3) },
                new[] { E(UnitStats.Wolf, 3, 3), E(UnitStats.Wolf, 4, 3), Far });
            var warrior = Player(b);
            Assert.IsNotNull(b.PlayerUseAction(warrior, ActionKind.Sweep, Enemy(b, 0).Pos));
            Assert.AreEqual(UnitStats.Wolf.MaxHp - warrior.BaseDamage, Enemy(b, 0).Hp);
            Assert.AreEqual(UnitStats.Wolf.MaxHp - warrior.BaseDamage, Enemy(b, 1).Hp);
            Assert.AreEqual(Player(b, 1).MaxHp, Player(b, 1).Hp);
            Assert.Greater(warrior.CooldownLeft(ActionKind.Sweep), 0);
            Assert.IsFalse(b.CanMove(warrior), "an action: no moving afterwards");
        }

        [Test]
        public void Sweep_APlainAttackHitsOnlyTheTarget()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.Sweep) }, new[] { P(3, 2) },
                new[] { E(UnitStats.Wolf, 3, 3), E(UnitStats.Wolf, 4, 3), Far });
            b.PlayerAttack(Player(b), Enemy(b, 0));
            Assert.AreEqual(UnitStats.Wolf.MaxHp, Enemy(b, 1).Hp);
        }

        [Test]
        public void Charge_HitsEveryEnemyOnTheWay_PushesThemAside_AndArrives()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.Charge) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Crossbowman, 3, 2), E(UnitStats.Crossbowman, 3, 3), Far });
            var warrior = Player(b);
            Assert.IsNotNull(b.PlayerUseAction(warrior, ActionKind.Charge, P(3, 4)));
            Assert.AreEqual(P(3, 4), warrior.Pos);
            foreach (var i in new[] { 0, 1 })
            {
                var c = Enemy(b, i);
                Assert.AreEqual(UnitStats.Crossbowman.MaxHp - Battle.ChargeDamage, c.Hp);
                Assert.AreEqual(1, System.Math.Abs(c.Pos.X - 3), "pushed one tile to a side");
                Assert.AreEqual(2 + i, c.Pos.Y, "not along the charge");
            }
        }

        [Test]
        public void Charge_BlockedSide_GoesToTheOther_BothBlocked_CollisionAndStops()
        {
            for (var seed = 0; seed < 8; seed++)
            {
                var oneSide = Setup(new[] { M(UnitStats.Warrior, Technique.Charge) }, new[] { P(3, 1) }, new[] { E(UnitStats.Crossbowman, 3, 2), Far }, P(2, 2));
                oneSide.PlayerUseAction(Player(oneSide), ActionKind.Charge, P(3, 2));
                Assert.AreEqual(P(4, 2), Enemy(oneSide).Pos);
            }

            var both = Setup(new[] { M(UnitStats.Warrior, Technique.Charge) }, new[] { P(3, 1) }, new[] { E(UnitStats.Crossbowman, 3, 2), Far }, P(2, 2), P(4, 2));
            both.PlayerUseAction(Player(both), ActionKind.Charge, P(3, 3));
            Assert.AreEqual(P(3, 2), Enemy(both).Pos);
            Assert.AreEqual(UnitStats.Crossbowman.MaxHp - Battle.ChargeDamage - Battle.CollisionDamage, Enemy(both).Hp);
            Assert.AreEqual(P(3, 1), Player(both).Pos, "it stops in front of the enemy");
        }

        [Test]
        public void Charge_HugeStopsIt_AlliesAreNotPushed_AndItCantEndOnAnAlly()
        {
            var huge = Setup(new[] { M(UnitStats.Warrior, Technique.Charge) }, new[] { P(3, 1) }, new[] { E(UnitStats.Brute, 3, 3), Far });
            huge.PlayerUseAction(Player(huge), ActionKind.Charge, P(3, 4));
            Assert.AreEqual(P(3, 2), Player(huge).Pos);
            Assert.AreEqual(P(3, 3), Enemy(huge).Pos);

            var allies = Setup(new[] { M(UnitStats.Warrior, Technique.Charge), M(UnitStats.Archer) }, new[] { P(3, 1), P(3, 2) }, new[] { E(UnitStats.Crossbowman, 3, 3), Far });
            Assert.IsFalse(allies.CanUseAction(Player(allies), ActionKind.Charge, P(3, 2)), "can't end on an ally");
            allies.PlayerUseAction(Player(allies), ActionKind.Charge, P(3, 3));
            Assert.AreEqual(P(3, 3), Player(allies).Pos, "passed through the archer");
            Assert.AreEqual(P(3, 2), Player(allies, 1).Pos);
            Assert.AreEqual(Player(allies, 1).MaxHp, Player(allies, 1).Hp);
        }

        [Test]
        public void ShieldUp_TakesTwoLess_AtLeastOne_ForTwoEnemyTurns()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.ShieldUp) }, new[] { P(3, 2) }, new[] { E(UnitStats.Brute, 3, 3) });
            var warrior = Player(b);
            Assert.IsNotNull(b.PlayerUseAction(warrior, ActionKind.ShieldUp, warrior.Pos));
            Assert.AreEqual(Battle.ShieldUpTurns, warrior.ShieldUpTurns);
            b.EndPlayerTurn();
            Assert.AreEqual(warrior.MaxHp - 1, warrior.Hp, "the Brute's 3, reduced to 1");
            Assert.AreEqual(1, warrior.ShieldUpTurns);
            b.EndPlayerTurn();
            Assert.AreEqual(0, warrior.ShieldUpTurns, "gone after two enemy turns");
        }

        [Test]
        public void Battlecry_GivesNearbyAlliesAMoveAndTemporaryHp_ThatFade()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.Battlecry), M(UnitStats.Archer), M(UnitStats.Sapper) },
                new[] { P(3, 1), P(4, 2), P(7, 0) }, new[] { E(UnitStats.Wolf, 7, 7) });
            var warrior = Player(b);
            var archer = Player(b, 1);
            var far = Player(b, 2);
            var moves = archer.Moves;
            Assert.IsNotNull(b.PlayerUseAction(warrior, ActionKind.Battlecry, warrior.Pos));
            Assert.AreEqual(moves + Battle.BattlecryMoves, archer.Moves);
            Assert.AreEqual(Battle.BattlecryHp, archer.TempHp);
            Assert.AreEqual(Battle.BattlecryHp, warrior.TempHp, "itself too");
            Assert.AreEqual(0, far.TempHp, "out of range");
            b.EndPlayerTurn();
            Assert.AreEqual(0, archer.TempHp);
            Assert.AreEqual(1, archer.Moves, "back to one move next turn");
        }

        [Test]
        public void TemporaryHp_IsLostBeforeRealHp()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.Battlecry) }, new[] { P(3, 2) }, new[] { E(UnitStats.Brute, 3, 3) });
            var warrior = Player(b);
            b.PlayerUseAction(warrior, ActionKind.Battlecry, warrior.Pos);
            b.EndPlayerTurn();
            Assert.AreEqual(warrior.MaxHp - (UnitStats.Brute.Damage - Battle.BattlecryHp), warrior.Hp);
        }

        [Test]
        public void Taunt_ForcesEnemiesInRange_ForOneEnemyTurn()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.Taunt), M(UnitStats.Archer) }, new[] { P(4, 3), P(5, 3) }, new[] { E(UnitStats.Wolf, 5, 4) });
            var warrior = Player(b);
            Assert.AreEqual(Player(b, 1), EnemyAI.ChooseTarget(b, Enemy(b)), "not taunted yet");
            Assert.IsNotNull(b.PlayerUseAction(warrior, ActionKind.Taunt, warrior.Pos));
            Assert.AreEqual(warrior, EnemyAI.ChooseTarget(b, Enemy(b)), "beats the nearer archer");
            b.EndPlayerTurn();
            Assert.Less(warrior.Hp, warrior.MaxHp);
            Assert.AreEqual(Player(b, 1).MaxHp, Player(b, 1).Hp);
            Assert.IsFalse(warrior.IsTaunting, "over at your next turn");
        }

        [Test]
        public void ThrowShield_IsFree_BuffsForTheBattle_OncePerBattle()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.ThrowShield) }, new[] { P(3, 1) }, new[] { E(UnitStats.Ogre, 3, 3) });
            var warrior = Player(b);
            var hp = warrior.Hp;
            var moves = warrior.Moves;
            Assert.IsNotNull(b.PlayerUseAction(warrior, ActionKind.ThrowShield, warrior.Pos));
            Assert.AreEqual(hp - Battle.ThrowShieldHpCost, warrior.Hp);
            Assert.AreEqual(moves + Battle.ThrowShieldMoves, warrior.Moves, "usable this turn");
            Assert.AreEqual(1, warrior.Ap, "free");
            Assert.AreEqual(UnitStats.Warrior.Damage + Battle.ThrowShieldDamage, warrior.BaseDamage);
            Assert.IsTrue(b.CanMove(warrior));
            Assert.IsFalse(b.CanUseAction(warrior, ActionKind.ThrowShield, warrior.Pos), "once per battle");
            warrior.Pos = P(0, 0);
            b.EndPlayerTurn();
            Assert.AreEqual(UnitStats.Warrior.Moves + Battle.ThrowShieldMoves, warrior.Moves, "lasts");
        }

        [Test]
        public void ThrowShield_CantKillItsUser()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.ThrowShield) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 7, 7) });
            var warrior = Player(b);
            warrior.Hp = Battle.ThrowShieldHpCost;
            Assert.IsFalse(b.CanUseAction(warrior, ActionKind.ThrowShield, warrior.Pos));
        }

        [Test]
        public void Vanguard_DeploysOneRowFurther()
        {
            var b = new Battle(new Board(6, 6), new[] { M(UnitStats.Warrior, Technique.Vanguard), M(UnitStats.Warrior) }, new[] { E(UnitStats.Wolf, 5, 5) });
            Assert.IsTrue(b.CanPlace(Player(b, 0), P(2, Battle.PlayerZoneRows)));
            Assert.IsFalse(b.CanPlace(Player(b, 0), P(2, Battle.PlayerZoneRows + 1)));
            Assert.IsFalse(b.CanPlace(Player(b, 1), P(2, Battle.PlayerZoneRows)), "only with Vanguard");
        }

        [Test]
        public void ZocStrike_HitsAUnitLeavingItsReach_OnceARound_ButNotSlippery()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.ZocStrike) }, new[] { P(3, 3) },
                new[] { E(UnitStats.Crossbowman, 3, 4), E(UnitStats.Crossbowman, 4, 3) });
            var warrior = Player(b);
            var first = Enemy(b, 0);
            var second = Enemy(b, 1);
            first.Moves = second.Moves = 1;
            Assert.IsNotNull(b.DoMove(first, P(3, 5)));
            Assert.AreEqual(UnitStats.Crossbowman.MaxHp - warrior.BaseDamage, first.Hp);
            Assert.IsNotNull(b.DoMove(second, P(5, 3)));
            Assert.AreEqual(UnitStats.Crossbowman.MaxHp, second.Hp, "at most one attack a round");

            var slip = Setup(new[] { M(UnitStats.Warrior, Technique.ZocStrike) }, new[] { P(3, 3) }, new[] { E(UnitStats.Goblin, 3, 4) });
            Enemy(slip).Moves = 1;
            slip.DoMove(Enemy(slip), P(3, 5));
            Assert.AreEqual(UnitStats.Goblin.MaxHp, Enemy(slip).Hp, "Slippery ignores it");
        }
    }
}
