using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>The rules of the base kits and of the techniques units learn.</summary>
    public class KitTests
    {
        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        /// <summary>A started battle where players stand where told (anywhere, not only the back rows).</summary>
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

        /// <summary>A unit of the army that knows these techniques.</summary>
        private static ArmyMember M(UnitStats stats, params Technique[] techniques) => new ArmyMember(stats, null, techniques);

        private static (UnitStats, GridPos) E(UnitStats s, int x, int y) => (s, P(x, y));

        private static (UnitStats, GridPos) Far => (UnitStats.Wolf, P(7, 7));

        // ---------- Warrior techniques ----------

        [Test]
        public void LongReach_HitsTwoTilesInALine()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.LongReach) }, new[] { P(3, 2) },
                new[] { E(UnitStats.Wolf, 3, 3), E(UnitStats.Wolf, 3, 4), Far });
            Assert.IsTrue(b.CanAttack(Player(b), Enemy(b, 1)), "reach 2");
            b.PlayerAttack(Player(b), Enemy(b, 1));
            Assert.Less(Enemy(b, 0).Hp, UnitStats.Wolf.MaxHp);
            Assert.Less(Enemy(b, 1).Hp, UnitStats.Wolf.MaxHp);
        }

        [Test]
        public void DoubleStrike_StrikesTwice()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.Sweep, Technique.DoubleStrike) }, new[] { P(3, 2) }, new[] { E(UnitStats.Ogre, 3, 3) });
            b.PlayerAttack(Player(b), Enemy(b));
            Assert.AreEqual(UnitStats.Ogre.MaxHp - 2 * Player(b).BaseDamage, Enemy(b).Hp);
        }

        [Test]
        public void Guard_ProtectsAdjacentAlly()
        {
            var b = Setup(new[] { M(UnitStats.Warrior, Technique.Guard), M(UnitStats.Archer) }, new[] { P(3, 1), P(4, 1) },
                new[] { E(UnitStats.Wolf, 4, 2) });
            var shielder = Player(b, 0);
            var archer = Player(b, 1);
            var events = b.EndPlayerTurn();
            Assert.AreEqual(archer.MaxHp, archer.Hp, "the archer is never hit");
            Assert.Less(shielder.Hp, shielder.MaxHp);
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Guard));
        }

        [Test]
        public void Tough_TakesOneLess()
        {
            var lone = Setup(new[] { M(UnitStats.Warrior, Technique.Tough) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2) });
            lone.EndPlayerTurn();
            Assert.AreEqual(Player(lone).MaxHp - 1, Player(lone).Hp, "one bite of 2, reduced to 1");
        }

        // ---------- Archer techniques ----------

        [Test]
        public void Pierce_HitsTheLine()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.SteadyAimPlus, Technique.Pierce) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 3, 3), Far });
            b.PlayerAttack(Player(b), Enemy(b, 0));
            Assert.Less(Enemy(b, 1).Hp, UnitStats.Wolf.MaxHp, "the wolf behind is hit too");
        }

        [Test]
        public void OpeningShot_FirstShotOnEachTargetDouble()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.SteadyAimPlus, Technique.OpeningShot) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Ogre, 3, 4), E(UnitStats.Brute, 1, 3) });
            var deadeye = Player(b);
            var ogre = Enemy(b, 0);
            var brute = Enemy(b, 1);
            var normal = b.EffectiveDamage(deadeye);
            b.PlayerAttack(deadeye, ogre);
            Assert.AreEqual(UnitStats.Ogre.MaxHp - 2 * normal, ogre.Hp);
            deadeye.Ap = 1;
            b.PlayerAttack(deadeye, ogre);
            Assert.AreEqual(UnitStats.Ogre.MaxHp - 3 * normal, ogre.Hp, "only the first hit on a target is doubled");
            Assert.AreEqual(2 * normal, b.DamageAgainst(deadeye, brute), "a new target gets the opening shot");
        }

        [Test]
        public void PinningShot_HitsForOne_RootsNextTurn_AndPreyPunishesIt()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.Prey) }, new[] { P(3, 0) }, new[] { E(UnitStats.Wolf, 3, 3) });
            var hunter = Player(b);
            var wolf = Enemy(b);
            b.PlayerUseAction(hunter, ActionKind.PinningShot, wolf.Pos);
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.PinDamage, wolf.Hp);
            Assert.IsTrue(wolf.IsSnared);
            Assert.AreEqual(b.EffectiveDamage(hunter) + Battle.PreyBonus, b.DamageAgainst(hunter, wolf));
            b.EndPlayerTurn();
            Assert.AreEqual(P(3, 3), wolf.Pos, "pinned: it couldn't move");
        }

        [Test]
        public void PinningShot_NeedsLineOfSight()
        {
            var b = Setup(new[] { M(UnitStats.Archer) }, new[] { P(3, 0) }, new[] { E(UnitStats.Wolf, 3, 3) }, P(3, 2));
            Assert.IsFalse(b.CanUseAction(Player(b), ActionKind.PinningShot, P(3, 3)));
        }

        // ---------- Base kits ----------

        [Test]
        public void ShieldBash_DamagesAndPushes_CollisionHurtsMore()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), Far });
            var wolf = Enemy(b);
            b.PlayerUseAction(Player(b), ActionKind.ShieldBash, wolf.Pos);
            Assert.AreEqual(P(3, 4), wolf.Pos, "a small unit goes two tiles");
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.BashDamage, wolf.Hp);

            var walled = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), Far }, P(3, 3));
            var stuck = Enemy(walled);
            walled.PlayerUseAction(Player(walled), ActionKind.ShieldBash, stuck.Pos);
            Assert.AreEqual(P(3, 2), stuck.Pos);
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.BashDamage - Battle.CollisionDamage, stuck.Hp, "slammed into the rock");

            var crowd = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Brute, 3, 3), Far });
            var front = Enemy(crowd, 0);
            var behind = Enemy(crowd, 1);
            crowd.PlayerUseAction(Player(crowd), ActionKind.ShieldBash, front.Pos);
            // The Brute's Guard takes the bash itself, then the collision hurts both.
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.CollisionDamage, front.Hp);
            Assert.AreEqual(UnitStats.Brute.MaxHp - Battle.BashDamage - Battle.CollisionDamage, behind.Hp, "the unit it slams into is hurt too");
        }

        [Test]
        public void Vanish_AttackGetsTheBonus_AndStaysHiddenThroughTheEnemyTurn()
        {
            var b = Setup(new[] { M(UnitStats.Rogue), M(UnitStats.Warrior) }, new[] { P(3, 1), P(0, 0) }, new[] { E(UnitStats.Brute, 3, 2) });
            var rogue = Player(b, 0);
            var warrior = Player(b, 1);
            var brute = Enemy(b);
            b.PlayerUseAction(rogue, ActionKind.Vanish, rogue.Pos);
            Assert.IsTrue(rogue.IsHidden);

            var normal = b.EffectiveDamage(rogue);
            var hit = b.PlayerAttack(rogue, brute).OfType<AttackEvent>().First();
            Assert.AreEqual(normal + Battle.VanishBonus, hit.Damage, "the ambush bonus");
            Assert.IsTrue(rogue.IsHidden, "attacking doesn't reveal it");
            Assert.AreEqual(normal, b.DamageAgainst(rogue, brute), "the bonus is spent");

            Assert.AreEqual(warrior, EnemyAI.ChooseTarget(b, brute), "the hidden rogue next to it is ignored");
            Assert.IsFalse(b.CanAttack(brute, rogue));
            b.EndUnitTurn(rogue);
            brute.SnareTurns = 1; // keeps it next to the rogue
            b.EndPlayerTurn();
            Assert.AreEqual(rogue.MaxHp, rogue.Hp, "the enemy turn passed it by");
            Assert.IsFalse(rogue.IsHidden, "it shows again at your next turn");
        }

        [Test]
        public void Vanish_WearsOffAtYourNextTurn()
        {
            var b = Setup(new[] { M(UnitStats.Rogue) }, new[] { P(0, 0) }, new[] { Far });
            var rogue = Player(b);
            b.PlayerUseAction(rogue, ActionKind.Vanish, rogue.Pos);
            var events = b.EndPlayerTurn();
            Assert.IsFalse(rogue.IsHidden);
            Assert.IsTrue(events.OfType<StatusEvent>().Any(s => s.Kind == StatusKind.Reveal));
        }

        [Test]
        public void Blink_TeleportsPastRocksAndReach()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Blink) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), Far }, P(2, 1));
            var mage = Player(b);
            Assert.IsTrue(b.CanUseAction(mage, ActionKind.Blink, P(1, 1)), "over the rock");
            Assert.IsTrue(b.CanUseAction(mage, ActionKind.Blink, P(3, 4)), "3 steps is in range");
            Assert.IsFalse(b.CanUseAction(mage, ActionKind.Blink, P(3, 5)), "4 steps is too far");
            Assert.IsFalse(b.CanUseAction(mage, ActionKind.Blink, P(3, 2)), "occupied");
            b.PlayerUseAction(mage, ActionKind.Blink, P(1, 1));
            Assert.AreEqual(P(1, 1), mage.Pos);
            Assert.AreEqual(1, mage.Moves, "its move is still there");
        }

        [Test]
        public void Blink_IsFree_ButOnlyBeforeActing()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Blink) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), Far });
            var mage = Player(b);
            b.PlayerUseAction(mage, ActionKind.Blink, P(3, 3));
            Assert.AreEqual(mage.MaxAp, mage.Ap, "no AP spent");
            Assert.IsTrue(b.CanMove(mage), "it can still move");
            Assert.IsTrue(b.CanAttack(mage, Enemy(b)), "and attack");

            var other = Setup(new[] { M(UnitStats.Apprentice, Technique.Blink) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), Far });
            var attacker = Player(other);
            other.PlayerAttack(attacker, Enemy(other));
            Assert.IsFalse(other.CanUseAction(attacker, ActionKind.Blink, P(1, 1)), "no blinking away after attacking");
        }

        [Test]
        public void Vanish_IsFree_ButOnlyBeforeActing()
        {
            var b = Setup(new[] { M(UnitStats.Rogue) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), Far });
            var rogue = Player(b);
            b.PlayerUseAction(rogue, ActionKind.Vanish, rogue.Pos);
            Assert.AreEqual(rogue.MaxAp, rogue.Ap, "no AP spent");
            Assert.IsTrue(b.CanMove(rogue), "it can still move");
            Assert.IsTrue(b.CanAttack(rogue, Enemy(b)), "and attack");

            var other = Setup(new[] { M(UnitStats.Rogue) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), Far });
            var attacker = Player(other);
            other.PlayerAttack(attacker, Enemy(other));
            Assert.IsFalse(other.CanUseAction(attacker, ActionKind.Vanish, attacker.Pos), "no vanishing after attacking");
        }

        [Test]
        public void Walking_PassesThroughAllies_ButNotEnemies()
        {
            var b = Setup(new[] { M(UnitStats.Rogue), M(UnitStats.Warrior) }, new[] { P(3, 1), P(3, 2) }, new[] { Far });
            var rogue = Player(b, 0);
            Assert.IsFalse(b.GetReachable(rogue).ContainsKey(P(3, 2)), "can't stop on the ally");
            Assert.IsNull(b.PlayerMove(rogue, P(3, 2)));
            Assert.IsNotNull(b.PlayerMove(rogue, P(3, 3)));
            Assert.AreEqual(P(3, 3), rogue.Pos);
            Assert.AreEqual(0, rogue.Moves);

            var blocked = Setup(new[] { M(UnitStats.Rogue) }, new[] { P(3, 1) }, new[] { E(UnitStats.Brute, 3, 2), Far });
            Assert.IsFalse(blocked.GetReachable(Player(blocked)).ContainsKey(P(3, 3)), "enemies still block");
        }

        [Test]
        public void Walking_ThroughAnAlly_NeverStopsOnItsTile()
        {
            var b = Setup(new[] { M(UnitStats.Rogue), M(UnitStats.Warrior) }, new[] { P(3, 1), P(3, 2) }, new[] { E(UnitStats.Cutpurse, 4, 2) });
            var rogue = Player(b, 0);
            var thief = Enemy(b);
            thief.Ap = 1;
            b.DoCaltrops(thief, P(3, 2));
            thief.Pos = P(5, 5);
            b.PlayerMove(rogue, P(3, 3));
            Assert.AreEqual(P(3, 3), rogue.Pos, "the caltrops under the ally don't stop it there");
            Assert.AreEqual(rogue.MaxHp, rogue.Hp);
        }

        [Test]
        public void Volley_HitsPlus_FriendlyFireIncluded()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.Volley), M(UnitStats.Warrior) }, new[] { P(3, 1), P(4, 4) },
                new[] { E(UnitStats.Wolf, 3, 4), E(UnitStats.Wolf, 2, 4), E(UnitStats.Wolf, 2, 5), Far });
            b.PlayerAttack(Player(b, 0), Enemy(b, 0));
            Assert.Less(Enemy(b, 1).Hp, UnitStats.Wolf.MaxHp);
            Assert.AreEqual(UnitStats.Wolf.MaxHp, Enemy(b, 2).Hp, "diagonal tiles are outside the plus");
            Assert.Less(Player(b, 1).Hp, Player(b, 1).MaxHp, "the warrior was in the area");
        }

        // ---------- Rogue techniques ----------

        [Test]
        public void Pincer_Bonus()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Pincer), M(UnitStats.Warrior) }, new[] { P(3, 2), P(3, 4) },
                new[] { E(UnitStats.Wolf, 3, 3) });
            Assert.AreEqual(b.EffectiveDamage(Player(b)) + Battle.PincerBonus, b.DamageAgainst(Player(b), Enemy(b)));
        }

        [Test]
        public void Momentum_KillRefundsAp()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Pincer, Technique.Momentum) }, new[] { P(3, 2) }, new[] { E(UnitStats.Wolf, 3, 3), Far });
            var shadow = Player(b);
            Enemy(b).Hp = 1;
            var before = shadow.Ap;
            b.PlayerAttack(shadow, Enemy(b));
            Assert.AreEqual(before - Battle.AttackCost + Battle.KillRefundAp, shadow.Ap);
        }

        [Test]
        public void Momentum_LetsItWalkOnAfterAKill()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Pincer, Technique.Momentum) }, new[] { P(3, 2) }, new[] { E(UnitStats.Wolf, 3, 3), Far });
            var shadow = Player(b);
            shadow.Moves = 0;
            Enemy(b).Hp = 1;
            b.PlayerAttack(shadow, Enemy(b));
            Assert.AreEqual(Battle.KillRefundMoves, shadow.Moves);
            Assert.IsTrue(b.CanMove(shadow));
            Assert.IsTrue(b.CanStillAct(shadow), "nothing else in reach, but it isn't finished");
        }

        [Test]
        public void BonePile_ANewOneReplacesTheOldOne_SoOneUnitRises()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(3, 2) },
                new[] { E(UnitStats.Skeleton, 3, 3), E(UnitStats.Skeleton, 3, 4), E(UnitStats.Necromancer, 5, 5) });
            var warrior = Player(b);
            var (first, second, necro) = (Enemy(b, 0), Enemy(b, 1), Enemy(b, 2));
            first.Hp = 1;
            b.PlayerAttack(warrior, first);
            second.Pos = first.Pos;
            second.Hp = 1;
            warrior.Ap = warrior.MaxAp;
            b.PlayerAttack(warrior, second);
            Assert.AreEqual(second, b.RemainsAt(P(3, 3)));
            Assert.IsFalse(first.IsRemains, "the older pile is gone");
            var raises = b.DoRaise(necro, second).OfType<RaiseEvent>().ToList();
            Assert.AreEqual(1, raises.Count);
            Assert.AreEqual(second, b.UnitAt(P(3, 3)));
            Assert.IsFalse(first.IsAlive);
        }

        [Test]
        public void EnemiesPreferAnUnshieldedTarget_IfTheyCanReachOne()
        {
            var b = Setup(new[] { M(UnitStats.Warrior), M(UnitStats.Archer) }, new[] { P(3, 2), P(4, 2) }, new[] { E(UnitStats.Brute, 3, 3) });
            var (warrior, archer) = (Player(b, 0), Player(b, 1));
            var brute = Enemy(b);
            Assert.AreEqual(warrior, EnemyAI.ChooseTarget(b, brute), "the nearest, with no shields around");
            warrior.Shield = 2;
            Assert.AreEqual(archer, EnemyAI.ChooseTarget(b, brute));
            archer.Pos = P(0, 0);
            Assert.AreEqual(warrior, EnemyAI.ChooseTarget(b, brute), "nobody else in reach: it hits the shield");
        }

        [Test]
        public void KillPrediction_CoversAttacksAndAimedActions()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Fireball) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 3, 3), Far });
            var mage = Player(b);
            var (near, behind) = (Enemy(b, 0), Enemy(b, 1));
            near.Hp = b.EffectiveDamage(mage);
            CollectionAssert.AreEqual(new[] { near }, b.AttackKills(mage));
            near.Hp = 20;
            Assert.AreEqual(0, b.AttackKills(mage).Count);

            behind.Hp = Battle.FireballDamage;
            CollectionAssert.AreEqual(new[] { behind }, b.ActionKills(mage, ActionKind.Fireball, P(3, 3)));
            behind.Shield = 1;
            Assert.AreEqual(0, b.ActionKills(mage, ActionKind.Fireball, P(3, 3)).Count, "the shield saves it");
        }

        [Test]
        public void Poison_TicksOnEnemyTurns()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Pincer, Technique.Poison) }, new[] { P(0, 0) }, new[] { E(UnitStats.Ogre, 5, 5) });
            var ogre = Enemy(b);
            var poisoner = Player(b);
            poisoner.Pos = P(5, 4);
            b.PlayerAttack(poisoner, ogre);
            var afterHit = ogre.Hp;
            poisoner.Pos = P(0, 0);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<TickEvent>().Any(t => t.Cause == "poison"));
            Assert.AreEqual(afterHit - Battle.PoisonDamage, ogre.Hp);
        }

        [Test]
        public void Riposte_CountersOncePerTurn_RipostePlusEveryTime()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Riposte) }, new[] { P(3, 1) }, new[] { E(UnitStats.Ogre, 3, 2) });
            var events = b.EndPlayerTurn();
            Assert.AreEqual(1, events.OfType<AttackEvent>().Count(a => a.Attacker.Team == Team.Player));

            var b2 = Setup(new[] { M(UnitStats.Rogue, Technique.Riposte, Technique.RipostePlus) }, new[] { P(3, 1) }, new[] { E(UnitStats.Ogre, 3, 2) });
            Player(b2).Hp = 50; // survive both smashes so every one gets countered
            var events2 = b2.EndPlayerTurn();
            var smashes = events2.OfType<SmashEvent>().Count();
            Assert.AreEqual(smashes, events2.OfType<AttackEvent>().Count(a => a.Attacker.Team == Team.Player));
        }

        [Test]
        public void Dodge_DodgesFirstHit()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Riposte, Technique.Dodge) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 2, 1) });
            var swash = Player(b);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Dodge));
            Assert.AreEqual(swash.MaxHp - 3, swash.Hp, "only the second bite landed: 2, +1 from the bite chain");
        }

        // ---------- Mage techniques ----------

        [Test]
        public void Apprentice_BoltIgnoresLineOfSight()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 4) }, P(3, 2), P(3, 3));
            Assert.IsTrue(b.CanAttack(Player(b), Enemy(b)));
        }

        [Test]
        public void Fireball_FriendlyFire_AndBurningGround()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Fireball, Technique.Burning), M(UnitStats.Warrior) }, new[] { P(3, 0), P(4, 3) },
                new[] { E(UnitStats.Wolf, 3, 3), Far });
            var pyro = Player(b, 0);
            b.PlayerUseAction(pyro, ActionKind.Fireball, P(3, 3));
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.FireballDamage, Enemy(b, 0).Hp);
            Assert.Less(Player(b, 1).Hp, Player(b, 1).MaxHp);
            Assert.IsTrue(b.BurningTiles.ContainsKey(P(3, 3)));
            var wolf = Enemy(b, 0);
            wolf.Hp = 10;
            Enemy(b, 1).Hp = 0;
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<TickEvent>().Any(t => t.Cause == "burn" && t.Hit.Target == wolf));
        }

        [Test]
        public void Concussive_PushesOutward_AndSetsOffMines()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Fireball, Technique.Concussive), M(UnitStats.Sapper) }, new[] { P(4, 1), P(6, 2) },
                new[] { E(UnitStats.Ogre, 0, 6), E(UnitStats.Wolf, 4, 3) });
            var sapper = Player(b, 1);
            b.PlayerPlaceMine(sapper, P(6, 3));
            b.EndUnitTurn(sapper);
            var wolf = Enemy(b, 1);
            wolf.Hp = 20;
            var events = b.PlayerUseAction(Player(b, 0), ActionKind.Fireball, P(5, 3));
            Assert.AreEqual(P(2, 3), wolf.Pos, "a small unit: pushed two tiles away from the centre");
            Assert.IsTrue(events.OfType<ExplosionEvent>().Any(), "the mine inside the area went off");
        }

        [Test]
        public void Chill_SlowsNextTurn_ShatterAddsDamage()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Chill, Technique.Shatter) }, new[] { P(3, 0) }, new[] { E(UnitStats.Brute, 3, 2) });
            var glacier = Player(b);
            var brute = Enemy(b);
            var normal = b.DamageAgainst(glacier, brute);
            b.PlayerAttack(glacier, brute);
            Assert.IsTrue(brute.IsSlowed);
            Assert.AreEqual(normal + Battle.ShatterBonus, b.DamageAgainst(glacier, brute));
            b.EndUnitTurn(glacier);
            b.EndPlayerTurn();
            Assert.IsFalse(brute.IsSlowed, "the slow held it for its own turn only, not into yours");
            Assert.AreEqual(P(3, 1), brute.Pos, "chilled: it walked up");
            Assert.AreEqual(glacier.MaxHp, glacier.Hp, "and so couldn't strike");
        }

        [Test]
        public void IceWall_BlocksThenMelts()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.IceWall) }, new[] { P(3, 0) }, new[] { Far });
            b.PlayerUseAction(Player(b), ActionKind.IceWall, P(3, 2));
            Assert.IsTrue(b.Board.IsObstacle(P(3, 2)));
            for (var i = 0; i < Battle.IceRounds; i++)
                b.EndPlayerTurn();
            Assert.IsFalse(b.Board.IsObstacle(P(3, 2)));
        }

        [Test]
        public void Ward_Absorbs_WideWardCoversNeighbours_MendHeals()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Ward, Technique.WideWard), M(UnitStats.Warrior), M(UnitStats.Rogue) }, new[] { P(3, 0), P(3, 2), P(4, 2) },
                new[] { Far });
            b.PlayerUseAction(Player(b, 0), ActionKind.Ward, P(3, 2));
            Assert.AreEqual(Battle.WardShield, Player(b, 1).Shield);
            Assert.AreEqual(Battle.WardShield, Player(b, 2).Shield, "wide ward");

            var m = Setup(new[] { M(UnitStats.Apprentice, Technique.Ward, Technique.Mend), M(UnitStats.Warrior) }, new[] { P(3, 0), P(3, 2) }, new[] { Far });
            Player(m, 1).Hp = 5;
            m.PlayerUseAction(Player(m, 0), ActionKind.Mend, P(3, 2));
            Assert.AreEqual(5 + Battle.MendHeal, Player(m, 1).Hp);
        }

        [Test]
        public void WardShield_SoaksDamageFirst()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Ward), M(UnitStats.Rogue) }, new[] { P(1, 0), P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 4, 1) });
            var rogue = Player(b, 1);
            b.PlayerUseAction(Player(b, 0), ActionKind.Ward, P(3, 1));
            b.EndPlayerTurn();
            Assert.AreEqual(rogue.MaxHp - (2 + 3 - Battle.WardShield), rogue.Hp, "bites of 2 and 3 (bite chain), minus the shield");
        }

        // ---------- Sapper techniques ----------

        [Test]
        public void BigMines_BiggerMines()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.BigMines) }, new[] { P(3, 1) }, new[] { Far });
            b.PlayerPlaceMine(Player(b), P(3, 2));
            Assert.AreEqual(Battle.BigMineDamage, b.MineAt(P(3, 2)).Damage);
        }

        [Test]
        public void BlastProof_BlastsSpareYourSide()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.BigMines, Technique.BlastProof) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 4) });
            var safe = Player(b);
            b.PlayerPlaceMine(safe, P(3, 2));
            var events = b.EndPlayerTurn();
            var blast = events.OfType<ExplosionEvent>().First();
            Assert.IsFalse(blast.Hits.Any(h => h.Target == safe));
        }

        [Test]
        public void ThrowBomb_DelayedBomb()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.ThrowBomb) }, new[] { P(3, 0) }, new[] { E(UnitStats.Ogre, 3, 4) });
            b.PlayerUseAction(Player(b), ActionKind.ThrowBomb, P(3, 3));
            Assert.IsNotNull(b.BombAt(P(3, 3)));
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<ExplosionEvent>().Any(e => e.Cause == "bomb" && e.Owner == Player(b)));
        }

        [Test]
        public void Barricade_AndDetonate()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Barricade, Technique.Detonate) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 3), Far });
            var captain = Player(b);
            b.PlayerUseAction(captain, ActionKind.Barricade, P(3, 2));
            Assert.IsTrue(b.Board.IsObstacle(P(3, 2)));
            captain.Ap = 4;
            var events = b.PlayerUseAction(captain, ActionKind.Detonate, captain.Pos);
            Assert.IsFalse(b.IsBarricade(P(3, 2)));
            Assert.IsTrue(b.IsRubble(P(3, 2)), "a blown barricade leaves rubble");
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.BarricadeBlastDamage, Enemy(b).Hp);
            Assert.IsTrue(events.OfType<ExplosionEvent>().Any(e => e.Cause == "barricade"));
        }

        [Test]
        public void Barricade_HasHp_AndBreaks()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Barricade), M(UnitStats.Warrior) }, new[] { P(3, 1), P(4, 2) }, new[] { Far });
            var sapper = Player(b, 0);
            var warrior = Player(b, 1);
            b.PlayerUseAction(sapper, ActionKind.Barricade, P(3, 2));
            Assert.AreEqual(Battle.BarricadeHp, b.BarricadeHpAt(P(3, 2)));
            b.EndUnitTurn(sapper);
            Assert.IsTrue(b.CanAttackBarricade(warrior, P(3, 2)), "your own side can break it too");
            var events = b.PlayerAttackBarricade(warrior, P(3, 2));
            Assert.AreEqual(Battle.BarricadeHp - warrior.BaseDamage, b.BarricadeHpAt(P(3, 2)));
            Assert.IsTrue(events.OfType<BarricadeHitEvent>().Any());
            Assert.IsTrue(b.Board.IsObstacle(P(3, 2)), "still standing");

            warrior.Ap = 1;
            events = b.PlayerAttackBarricade(warrior, P(3, 2));
            Assert.IsFalse(b.IsBarricade(P(3, 2)));
            Assert.IsTrue(b.IsRubble(P(3, 2)), "broken: it collapses into rubble");
            Assert.IsTrue(events.OfType<TileEvent>().Any(t => t.Kind == "barricade" && !t.Added));
        }

        [Test]
        public void Barricade_EnemyWithNobodyToHitBreaksIt()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Barricade) }, new[] { P(0, 0) }, new[] { E(UnitStats.Brute, 3, 4) });
            var sapper = Player(b);
            sapper.Pos = P(3, 2);
            b.PlayerUseAction(sapper, ActionKind.Barricade, P(3, 3));
            // Fence the brute in so the barricade is the only thing it can reach.
            sapper.Pos = P(0, 0);
            b.EndUnitTurn(sapper);
            Enemy(b).SnareTurns = 1;
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<BarricadeHitEvent>().Any(e => e.Attacker == Enemy(b)));
            Assert.Less(b.BarricadeHpAt(P(3, 3)), Battle.BarricadeHp);
        }

        // ---------- Attack preview ----------

        [Test]
        public void PredictHpLoss_MatchesTheHit_WithToughAndGuard()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(3, 2) }, new[] { E(UnitStats.Brute, 3, 3), E(UnitStats.Wolf, 7, 7) });
            var warrior = Player(b);
            var brute = Enemy(b, 0);
            var predicted = b.PredictHpLoss(warrior, brute, out var guard);
            Assert.IsNull(guard);
            var hpBefore = brute.Hp;
            b.PlayerAttack(warrior, brute);
            Assert.AreEqual(hpBefore - brute.Hp, predicted, "the preview matches the real hit");

            var guarded = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(3, 2) }, new[] { E(UnitStats.Wolf, 3, 3), E(UnitStats.Brute, 4, 3) });
            Assert.AreEqual(0, guarded.PredictHpLoss(Player(guarded), Enemy(guarded, 0), out var brute2));
            Assert.AreEqual(Enemy(guarded, 1), brute2, "the brute guards the wolf");
        }

        [Test]
        public void Turret_Shoots_AndDoesNotCountForDefeat()
        {
            var b = Setup(new[] { M(UnitStats.Sapper, Technique.Barricade, Technique.Turret) }, new[] { P(3, 1) }, new[] { E(UnitStats.Ogre, 3, 5) });
            var builder = Player(b);
            b.PlayerUseAction(builder, ActionKind.Turret, P(4, 1));
            var turret = b.Units.Single(u => u.IsSummon);
            Assert.AreEqual(0, b.GetReachable(turret).Count, "immobile");
            builder.Hp = 0;
            b.EndUnitTurn(builder);
            b.EndPlayerTurn();
            Assert.AreEqual(Phase.Defeat, b.Phase, "the turret alone doesn't keep the battle going");
        }
    }
}
