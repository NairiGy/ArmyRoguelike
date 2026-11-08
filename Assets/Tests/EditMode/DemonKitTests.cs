using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>The demon faction: Imp, Demon, Devil and Fiend, their encounter and their AI.</summary>
    public class DemonKitTests
    {
        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        private static (UnitStats, GridPos) E(UnitStats s, int x, int y) => (s, P(x, y));

        /// <summary>A started 6x6 battle where players stand where told (anywhere, not only the back rows).</summary>
        private static Battle Setup(ArmyMember[] army, GridPos[] at, (UnitStats, GridPos)[] enemies, params GridPos[] rocks)
        {
            var b = new Battle(new Board(6, 6, rocks), army, enemies);
            for (var i = 0; i < army.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), P(i, 0)));
            Assert.IsTrue(b.StartBattle());
            for (var i = 0; i < army.Length; i++)
                Player(b, i).Pos = at[i];
            return b;
        }

        private static ArmyMember M(UnitStats stats, params Technique[] techniques) => new ArmyMember(stats, null, techniques);

        // ---------- Imp ----------

        [Test]
        public void Hellswap_DropsARangedUnitNextToTheDemon_WhichThenHitsIt()
        {
            var b = Setup(new[] { M(UnitStats.Warrior), M(UnitStats.Archer) }, new[] { P(4, 0), P(2, 1) },
                new[] { E(UnitStats.Demon, 2, 4), E(UnitStats.Imp, 2, 3) });
            var archer = Player(b, 1);
            var demon = Enemy(b, 0);
            var imp = Enemy(b, 1);
            var intent = EnemyAI.PredictIntent(b, imp);
            Assert.AreEqual(IntentKind.Hellswap, intent.Kind);
            Assert.AreEqual(archer, intent.Target, "the ranged unit first");

            var events = b.EndPlayerTurn();
            var swap = events.OfType<SwapEvent>().Single();
            Assert.AreEqual(archer, swap.Target);
            Assert.AreEqual(P(2, 1), imp.Pos, "the imp took the archer's tile");
            Assert.AreEqual(P(2, 3), archer.Pos, "the archer landed next to the demon");
            Assert.IsTrue(events.OfType<AttackEvent>().Any(a => a.Attacker == demon && a.Target == archer), "the demon acted after the imp and hit the archer");
            Assert.AreEqual(Battle.Cooldown(ActionKind.Hellswap), imp.CooldownLeft(ActionKind.Hellswap));
        }

        [Test]
        public void Hellswap_NeedsAnAllyNextToTheLandingTile()
        {
            var b = Setup(new[] { M(UnitStats.Archer) }, new[] { P(2, 0) }, new[] { E(UnitStats.Imp, 2, 3) });
            Assert.IsNull(EnemyAI.ChooseHellswap(b, Enemy(b)), "alone, a swap only hands your unit a better spot");
            Assert.AreNotEqual(IntentKind.Hellswap, EnemyAI.PredictIntent(b, Enemy(b)).Kind);
        }

        [Test]
        public void Hellswap_NeverTargetsAUnitAlreadyInReach_OrAnObjective()
        {
            var b = Setup(new[] { M(UnitStats.Archer) }, new[] { P(2, 1) }, new[] { E(UnitStats.Demon, 2, 4), E(UnitStats.Imp, 2, 3), E(UnitStats.Fiend, 1, 1) });
            Assert.IsNull(EnemyAI.ChooseHellswap(b, Enemy(b, 1)), "the archer is already next to the fiend");
        }

        [Test]
        public void BloodOffering_DyingImpGivesTheNearestDemonFury()
        {
            var b = Setup(new[] { M(UnitStats.Rogue) }, new[] { P(2, 2) }, new[] { E(UnitStats.Imp, 2, 3), E(UnitStats.Demon, 4, 4) });
            var imp = Enemy(b, 0);
            var demon = Enemy(b, 1);
            imp.Hp = 1;
            var events = b.PlayerAttack(Player(b), imp);
            Assert.IsFalse(imp.IsAlive);
            Assert.AreEqual(Battle.OfferingFury, demon.Fury);
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Fury && e.Unit == demon));
            Assert.AreEqual(UnitStats.Demon.Damage + Battle.OfferingFury, b.EffectiveDamage(demon), "Fury is +1 damage per point");
        }

        [Test]
        public void BloodOffering_DemonTooFarAway_GetsNothing()
        {
            var b = Setup(new[] { M(UnitStats.Rogue) }, new[] { P(0, 2) }, new[] { E(UnitStats.Imp, 0, 3), E(UnitStats.Demon, 5, 5) });
            Enemy(b, 0).Hp = 1;
            b.PlayerAttack(Player(b), Enemy(b, 0));
            Assert.AreEqual(0, Enemy(b, 1).Fury);
        }

        // ---------- Demon ----------

        [Test]
        public void Bloodlust_ADeathNextToTheDemonFeedsIt_StackingWithTheOffering()
        {
            var b = Setup(new[] { M(UnitStats.Rogue) }, new[] { P(2, 2) }, new[] { E(UnitStats.Imp, 2, 3), E(UnitStats.Demon, 3, 3) });
            Enemy(b, 0).Hp = 1;
            b.PlayerAttack(Player(b), Enemy(b, 0));
            Assert.AreEqual(Battle.OfferingFury + Battle.BloodlustFury, Enemy(b, 1).Fury);
        }

        [Test]
        public void RendingBlow_PaysHpForAHarderHit_ThenCoolsDown()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 1) }, new[] { E(UnitStats.Demon, 2, 2) });
            var warrior = Player(b);
            var demon = Enemy(b);
            Assert.AreEqual(IntentKind.RendingBlow, EnemyAI.PredictIntent(b, demon).Kind);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<TickEvent>().Any(t => t.Cause == "blood price" && t.Hit.Target == demon));
            Assert.AreEqual(UnitStats.Demon.MaxHp - Battle.RendingBlowCost, demon.Hp);
            Assert.AreEqual(warrior.MaxHp - UnitStats.Demon.Damage - Battle.RendingBlowBonus, warrior.Hp);
            Assert.AreEqual(IntentKind.Attack, EnemyAI.PredictIntent(b, demon).Kind, "on cooldown: a plain strike next");
        }

        [Test]
        public void RendingBlow_NotWithTooLittleHp()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 1) }, new[] { E(UnitStats.Demon, 2, 2) });
            var demon = Enemy(b);
            demon.Hp = Battle.RendingBlowCost;
            Assert.IsFalse(b.CanRendingBlow(demon, Player(b)));
            Assert.AreEqual(IntentKind.Attack, EnemyAI.PredictIntent(b, demon).Kind);
        }

        // ---------- Devil ----------

        [Test]
        public void SoulContract_WalksIntoRange_Signs_AndComesDueNextEnemyTurn()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 0) }, new[] { E(UnitStats.Devil, 2, 4) });
            var warrior = Player(b);
            var devil = Enemy(b);
            var first = b.EndPlayerTurn();
            Assert.IsTrue(first.OfType<StatusEvent>().Any(s => s.Kind == StatusKind.Contract && s.Target == warrior));
            Assert.AreEqual(devil, warrior.ContractBy);
            Assert.AreEqual(warrior.MaxHp, warrior.Hp, "nothing lost yet");

            var second = b.EndPlayerTurn();
            var due = second.OfType<TickEvent>().Single(t => t.Cause == "contract");
            Assert.AreEqual(warrior, due.Hit.Target);
            Assert.AreEqual(Battle.ContractDamage, due.Hit.Damage);
            Assert.IsNull(warrior.ContractBy, "a contract comes due once");
        }

        [Test]
        public void SoulContract_HurtingTheDevilBreaksIt()
        {
            var b = Setup(new[] { M(UnitStats.Archer) }, new[] { P(2, 0) }, new[] { E(UnitStats.Devil, 2, 4) });
            var archer = Player(b);
            var devil = Enemy(b);
            b.EndPlayerTurn();
            Assert.AreEqual(devil, archer.ContractBy);
            Assert.AreEqual(3, Battle.Distance(archer, devil));

            var shot = b.PlayerAttack(archer, devil);
            Assert.IsTrue(shot.OfType<StatusEvent>().Any(s => s.Kind == StatusKind.ContractBroken && s.Target == archer));
            Assert.IsNull(archer.ContractBy);
            var next = b.EndPlayerTurn();
            Assert.IsFalse(next.OfType<TickEvent>().Any(t => t.Cause == "contract"));
        }

        [Test]
        public void SoulContract_VoidIfTheDevilDies()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 0) }, new[] { E(UnitStats.Devil, 2, 4) });
            var warrior = Player(b);
            b.EndPlayerTurn();
            Enemy(b).Hp = 0;
            var next = b.EndPlayerTurn();
            Assert.IsFalse(next.OfType<TickEvent>().Any(t => t.Cause == "contract"));
            Assert.AreEqual(warrior.MaxHp, warrior.Hp);
        }

        [Test]
        public void SoulContract_IntentSeesTheWalkUp()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 0) }, new[] { E(UnitStats.Devil, 2, 4) });
            Assert.AreEqual(4, Battle.Distance(Player(b), Enemy(b)), "out of contract range before it walks");
            var intent = EnemyAI.PredictIntent(b, Enemy(b));
            Assert.AreEqual(IntentKind.SoulContract, intent.Kind);
            Assert.AreEqual(Player(b), intent.Target);
            Assert.AreEqual(P(2, 4), Enemy(b).Pos, "predicting doesn't move the real devil");
        }

        [Test]
        public void SoulContract_ADevilThatFallsVoidsIt()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 4) }, new[] { E(UnitStats.Devil, 2, 5) });
            b.SetField(Battlefield.EdgeOfTheWorld);
            var warrior = Player(b);
            var devil = Enemy(b);
            warrior.ContractBy = devil;
            b.PlayerUseAction(warrior, ActionKind.ShieldBash, devil.Pos);
            Assert.IsTrue(devil.Fell);
            Assert.IsNull(warrior.ContractBy);
        }

        [Test]
        public void SoulContract_NoBrokenPopupForADeadUnit()
        {
            var b = Setup(new[] { M(UnitStats.Rogue), M(UnitStats.Archer) }, new[] { P(2, 3), P(5, 0) }, new[] { E(UnitStats.Devil, 2, 4) });
            var archer = Player(b, 1);
            var devil = Enemy(b);
            archer.ContractBy = devil;
            archer.Hp = 0;
            devil.Hp = 1;
            var events = b.PlayerAttack(Player(b, 0), devil);
            Assert.IsFalse(events.OfType<StatusEvent>().Any(s => s.Kind == StatusKind.ContractBroken));
            Assert.IsNull(archer.ContractBy);
        }

        [Test]
        public void Fury_IsCapped()
        {
            var b = Setup(new[] { M(UnitStats.Rogue) }, new[] { P(2, 2) }, new[] { E(UnitStats.Imp, 2, 3), E(UnitStats.Demon, 3, 3) });
            var demon = Enemy(b, 1);
            demon.Fury = Battle.MaxFury - 1;
            Enemy(b, 0).Hp = 1;
            b.PlayerAttack(Player(b), Enemy(b, 0));
            Assert.AreEqual(Battle.MaxFury, demon.Fury, "offering and bloodlust would give 2, the cap stops at 3");
        }

        [Test]
        public void RendingBlow_IntentShowsTheTotalWithFuryAndFavored()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 1) }, new[] { E(UnitStats.Demon, 2, 2) });
            var demon = Enemy(b);
            demon.Fury = 2;
            Assert.AreEqual(UnitStats.Demon.Damage + 2 + Battle.RendingBlowBonus, EnemyAI.PredictIntent(b, demon).Amount);
            demon.Favored = true;
            Assert.AreEqual((UnitStats.Demon.Damage + 2 + Battle.RendingBlowBonus) * Battlefields.FavoredMultiplier, EnemyAI.PredictIntent(b, demon).Amount);
        }

        [Test]
        public void Devil_TridentStrikesTwoTilesAway()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 2) }, new[] { E(UnitStats.Devil, 2, 4) });
            var devil = Enemy(b);
            Assert.IsTrue(b.CanHitFrom(devil, devil.Pos, Player(b)), "Long Reach: 2 tiles in a straight line");
            Assert.IsFalse(b.CanHitFrom(devil, P(3, 4), Player(b)), "but only in a straight line");
        }

        // ---------- Fiend ----------

        [Test]
        public void FireBreath_HitsTheLineOfTwo_AndSetsItBurning()
        {
            var b = Setup(new[] { M(UnitStats.Warrior), M(UnitStats.Archer) }, new[] { P(2, 2), P(2, 1) }, new[] { E(UnitStats.Fiend, 2, 3) });
            var fiend = Enemy(b);
            Assert.AreEqual(IntentKind.FireBreath, EnemyAI.PredictIntent(b, fiend).Kind);
            var events = b.EndPlayerTurn();
            var breath = events.OfType<AreaAttackEvent>().Single(a => a.Kind == "fire breath");
            CollectionAssert.AreEquivalent(new[] { Player(b, 0), Player(b, 1) }, breath.Hits.Select(h => h.Target));
            Assert.IsTrue(breath.Hits.All(h => h.Damage == Battle.FireBreathDamage));
            Assert.IsTrue(b.BurningTiles.ContainsKey(P(2, 2)) && b.BurningTiles.ContainsKey(P(2, 1)));
        }

        [Test]
        public void FireBreath_ARockStopsTheLine()
        {
            var b = Setup(new[] { M(UnitStats.Archer) }, new[] { P(2, 1) }, new[] { E(UnitStats.Fiend, 2, 3) }, P(2, 2));
            var fiend = Enemy(b);
            Assert.IsNull(b.FireBreathLine(fiend, Player(b)));
        }

        [Test]
        public void Spines_HurtAMeleeAttacker()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 2) }, new[] { E(UnitStats.Fiend, 2, 3) });
            var warrior = Player(b);
            var events = b.PlayerAttack(warrior, Enemy(b));
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Thorns && e.Unit == warrior));
            Assert.AreEqual(warrior.MaxHp - Battle.ThornsDamage, warrior.Hp);
        }

        [Test]
        public void Hellborn_BurningGroundDoesntHurtDemons()
        {
            var b = Setup(new[] { M(UnitStats.Apprentice, Technique.Fireball, Technique.Burning) }, new[] { P(2, 0) },
                new[] { E(UnitStats.Demon, 2, 3) });
            var demon = Enemy(b);
            b.PlayerUseAction(Player(b), ActionKind.Fireball, demon.Pos);
            Assert.IsTrue(b.BurningTiles.ContainsKey(demon.Pos));
            var hp = demon.Hp;
            var events = b.EndPlayerTurn();
            Assert.IsFalse(events.OfType<TickEvent>().Any(t => t.Cause == "burn" && t.Hit.Target == demon));
            Assert.LessOrEqual(hp - demon.Hp, Battle.RendingBlowCost, "only its own blood price, no burn");
        }

        // ---------- Encounter, raised version, copies ----------

        [Test]
        public void Encounter_NormalAndElite_FieldTheirArmies()
        {
            for (var seed = 0; seed < 40; seed++)
            {
                int Count(Encounter e, UnitStats s) =>
                    e.CreateBattle(ArmyMember.StartingArmy(), new System.Random(seed)).Living(Team.Enemy).Count(u => u.Stats == s);
                Assert.AreEqual(1, Count(Encounter.Demons, UnitStats.Demon), $"seed {seed}");
                Assert.AreEqual(2, Count(Encounter.Demons, UnitStats.Imp), $"seed {seed}");
                Assert.AreEqual(1, Count(Encounter.Demons, UnitStats.Devil), $"seed {seed}");
                Assert.AreEqual(1, Count(Encounter.Demons, UnitStats.Fiend), $"seed {seed}");
                Assert.AreEqual(2, Count(Encounter.EliteDemons, UnitStats.Demon), $"seed {seed}");
                Assert.AreEqual(2, Count(Encounter.EliteDemons, UnitStats.Imp), $"seed {seed}");
                Assert.AreEqual(1, Count(Encounter.EliteDemons, UnitStats.Devil), $"seed {seed}");
                Assert.AreEqual(1, Count(Encounter.EliteDemons, UnitStats.Fiend), $"seed {seed}");
            }

            Assert.AreEqual(2, Encounter.Demons.Pool);
            Assert.AreEqual(Encounter.Demons, Encounter.EliteOf(Encounter.Demons).Base);
            CollectionAssert.Contains(Encounter.Regular, Encounter.Demons);
        }

        [Test]
        public void Necromancer_RaisesABoneImp_AfterTheDemonsAreBeaten()
        {
            var b = Encounter.Necromancer.CreateBattle(ArmyMember.StartingArmy(), new System.Random(3), new[] { Encounter.Demons });
            var bone = b.Living(Team.Enemy).Single(u => u.Stats.IsUndead);
            Assert.AreEqual(UnitKind.Imp, bone.Stats.Kind);
            Assert.IsTrue(bone.Has(Ability.Hellswap));
        }

        [Test]
        public void Clone_CopiesFuryAndContracts()
        {
            var b = Setup(new[] { M(UnitStats.Warrior) }, new[] { P(2, 0) }, new[] { E(UnitStats.Devil, 2, 3), E(UnitStats.Demon, 4, 4) });
            Enemy(b, 1).Fury = 2;
            Player(b).ContractBy = Enemy(b, 0);
            var c = b.Clone();
            Assert.AreEqual(2, Enemy(c, 1).Fury);
            Assert.AreSame(Enemy(c, 0), Player(c).ContractBy, "points at the copied devil");
        }
    }
}
