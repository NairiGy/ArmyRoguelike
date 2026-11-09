using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>The Archer and Rogue technique pools from the 2026-11-08 night redesign, and the Rogue's new base kit.</summary>
    public class ArcherRogueKitTests
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

        // ---------- Pools ----------

        [Test]
        public void Pools_FourteenEach_AboutOneThirdRare_WithParkedOnesLeftOut()
        {
            foreach (var (line, rares) in new[] { (UnitStats.Archer, 6), (UnitStats.Rogue, 5) })
            {
                var offered = Technique.All.Where(t => t.Line == line).ToList();
                Assert.AreEqual(14, offered.Count, line.Name);
                Assert.AreEqual(rares, offered.Count(t => t.Rarity == TechniqueRarity.Rare), line.Name);
                Assert.Greater(offered.Count(t => t.Action.HasValue), 3, line.Name + " has actions");
                Assert.Greater(offered.Count(t => !t.Action.HasValue), 3, line.Name + " has passives");
            }

            Assert.IsFalse(Technique.All.Contains(Technique.Volley));
            Assert.IsFalse(Technique.All.Contains(Technique.SpikedSnares));
            Assert.IsFalse(Technique.All.Contains(Technique.RipostePlus));
        }

        [Test]
        public void Chains_DeathMarkSuppressingFireAndRuptureNeedTheirRoot()
        {
            var archer = M(UnitStats.Archer);
            Assert.IsFalse(Technique.DeathMark.CanLearn(archer));
            Assert.IsFalse(Technique.SuppressingFire.CanLearn(archer));
            archer.Learn(Technique.MarkTarget);
            archer.Learn(Technique.Pierce);
            Assert.IsTrue(Technique.DeathMark.CanLearn(archer));
            Assert.IsTrue(Technique.SuppressingFire.CanLearn(archer));

            var rogue = M(UnitStats.Rogue);
            Assert.IsFalse(Technique.Rupture.CanLearn(rogue));
            rogue.Learn(Technique.Poison);
            Assert.IsTrue(Technique.Rupture.CanLearn(rogue));
        }

        // ---------- Archer ----------

        [Test]
        public void LongShot_AddsOneFromThreeTilesAway()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.LongShot) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 3, 3) });
            var archer = Player(b);
            Assert.AreEqual(b.DamageAgainst(archer, Enemy(b, 0)) + Battle.LongShotBonus, b.DamageAgainst(archer, Enemy(b, 1)));
        }

        [Test]
        public void Vantage_NextToARock_AddsRange_AndShootsPastCover()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.Vantage), M(UnitStats.Archer) }, new[] { P(3, 0), P(6, 0) },
                new[] { E(UnitStats.Wolf, 3, 4), E(UnitStats.Crossbowman, 6, 2) }, P(2, 0), P(7, 2));
            var lookout = Player(b, 0);
            var plain = Player(b, 1);
            Assert.IsTrue(b.CanAttack(lookout, Enemy(b, 0)), "4 tiles away, next to the rock");
            Assert.IsTrue(b.InCover(Enemy(b, 1)));
            Assert.IsFalse(b.CanAttack(plain, Enemy(b, 1)), "cover stops a plain shot");

            lookout.Pos = P(6, 0);
            plain.Pos = P(0, 7);
            Assert.IsFalse(b.HasVantage(lookout, lookout.Pos), "no rock next to it there");
            Assert.IsFalse(b.CanAttack(lookout, Enemy(b, 1)));
            lookout.Pos = P(5, 0);
            b.Board.AddObstacle(P(4, 0));
            Assert.IsTrue(b.CanAttack(lookout, Enemy(b, 1)), "from a vantage point the cover doesn't help");
        }

        [Test]
        public void Kick_HurtsAndPushes_SmallUnitsTwoTiles()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.Kick) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Crossbowman, 3, 2), E(UnitStats.Wolf, 2, 1), Far });
            var archer = Player(b);
            var xbow = Enemy(b, 0);
            var expected = xbow.Hp - (b.EffectiveDamage(archer) - archer.Stats.Damage + Battle.KickDamage);
            Assert.IsNotNull(b.PlayerUseAction(archer, ActionKind.Kick, xbow.Pos));
            Assert.AreEqual(P(3, 3), xbow.Pos, "medium: 1 tile");
            Assert.AreEqual(expected, xbow.Hp);

            archer.ClearCooldowns();
            b.EndPlayerTurn();
            archer.Pos = P(3, 1);
            var wolf = Enemy(b, 1);
            wolf.Pos = P(2, 1);
            Assert.IsNotNull(b.PlayerUseAction(archer, ActionKind.Kick, wolf.Pos));
            Assert.AreEqual(P(0, 1), wolf.Pos, "small: 2 tiles");
        }

        [Test]
        public void SnareTrap_StopsHurtsAndPinsTheFirstEnemy_AtMostTwo()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.SnareTrap) }, new[] { P(3, 0) }, new[] { E(UnitStats.Wolf, 3, 5), Far });
            var archer = Player(b);
            Assert.IsFalse(b.CanUseAction(archer, ActionKind.SnareTrap, P(3, 4)), "4 tiles is too far");
            Assert.IsNotNull(b.PlayerUseAction(archer, ActionKind.SnareTrap, P(3, 3)));
            CollectionAssert.Contains(b.Snares, P(3, 3));

            var wolf = Enemy(b);
            wolf.Moves = 3;
            b.DoMove(wolf, P(3, 2));
            Assert.AreEqual(P(3, 3), wolf.Pos, "it stops on the snare");
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.SnareDamage, wolf.Hp);
            Assert.IsTrue(wolf.IsSnared, "pinned for its next turn");
            Assert.AreEqual(0, wolf.Moves);
            Assert.AreEqual(0, b.Snares.Count, "used up");

            var limit = Setup(new[] { M(UnitStats.Archer, Technique.SnareTrap) }, new[] { P(3, 0) }, new[] { Far });
            var setter = Player(limit);
            for (var i = 0; i < Battle.MaxSnares; i++)
            {
                setter.ClearCooldowns();
                setter.Ap = setter.MaxAp;
                Assert.IsNotNull(limit.PlayerUseAction(setter, ActionKind.SnareTrap, P(2 + i, 2)));
            }

            setter.ClearCooldowns();
            setter.Ap = setter.MaxAp;
            Assert.IsFalse(limit.CanUseAction(setter, ActionKind.SnareTrap, P(4, 2)), "two at a time");
        }

        [Test]
        public void SpikedSnares_DealThree()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.SnareTrap, Technique.SpikedSnares) }, new[] { P(3, 0) }, new[] { E(UnitStats.Wolf, 3, 5), Far });
            b.PlayerUseAction(Player(b), ActionKind.SnareTrap, P(3, 3));
            var wolf = Enemy(b);
            wolf.Moves = 2;
            b.DoMove(wolf, P(3, 3));
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.SpikedSnareDamage, wolf.Hp);
        }

        [Test]
        public void FireArrow_HitsAndSetsTheGroundBurning()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.FireArrow) }, new[] { P(3, 0) }, new[] { E(UnitStats.Brute, 3, 3), Far });
            var archer = Player(b);
            var brute = Enemy(b);
            var expected = brute.Hp - b.DamageAgainst(archer, brute);
            Assert.IsNotNull(b.PlayerUseAction(archer, ActionKind.FireArrow, brute.Pos));
            Assert.AreEqual(expected, brute.Hp);
            Assert.IsTrue(b.BurningTiles.ContainsKey(P(3, 3)));
            Assert.AreEqual(Battle.Cooldown(ActionKind.FireArrow), archer.CooldownLeft(ActionKind.FireArrow));

            brute.SnareTurns = 1;
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<TickEvent>().Any(t => t.Cause == "burn" && t.Hit.Target == brute && t.Source == archer));
        }

        [Test]
        public void CoveringFire_ShootsTheFirstEnemyWalkingIntoSight_AndStopsIt()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.CoveringFire) }, new[] { P(3, 0) }, new[] { E(UnitStats.Wolf, 3, 5), Far });
            var archer = Player(b);
            var wolf = Enemy(b);
            Assert.IsNotNull(b.PlayerUseAction(archer, ActionKind.CoveringFire, archer.Pos));
            CollectionAssert.Contains(b.CoverTiles.ToList(), P(3, 3));
            Assert.IsEmpty(b.OverwatchTiles, "not shown as a crossbowman's");

            var expected = wolf.Hp - b.DamageAgainst(archer, wolf);
            wolf.Moves = 3;
            var events = b.DoMove(wolf, P(3, 2));
            Assert.AreEqual(P(3, 3), wolf.Pos, "stopped on the first covered tile");
            Assert.AreEqual(expected, wolf.Hp);
            Assert.AreEqual(0, wolf.Moves);
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.CoveringFire));
            Assert.IsEmpty(b.CoverTiles, "one shot");
        }

        [Test]
        public void CoveringFire_EndsAtYourNextTurn()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.CoveringFire) }, new[] { P(0, 0) }, new[] { Far });
            b.PlayerUseAction(Player(b), ActionKind.CoveringFire, P(0, 0));
            Assert.IsNotEmpty(b.CoverTiles);
            b.EndPlayerTurn();
            Assert.IsEmpty(b.CoverTiles);
        }

        [Test]
        public void SuppressingFire_PinsEveryEnemyOnTheLine_UpToARock()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.Pierce, Technique.SuppressingFire) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Wolf, 3, 1), E(UnitStats.Wolf, 3, 3), E(UnitStats.Wolf, 4, 2), Far });
            var archer = Player(b);
            Assert.IsNotNull(b.PlayerUseAction(archer, ActionKind.SuppressingFire, P(3, 2)));
            Assert.IsTrue(Enemy(b, 0).IsSnared);
            Assert.IsTrue(Enemy(b, 1).IsSnared);
            Assert.IsFalse(Enemy(b, 2).IsSnared, "off the line");
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.PinDamage, Enemy(b, 1).Hp);

            var rocky = Setup(new[] { M(UnitStats.Archer, Technique.Pierce, Technique.SuppressingFire) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Wolf, 3, 1), E(UnitStats.Wolf, 3, 3), Far }, P(3, 2));
            Assert.IsNotNull(rocky.PlayerUseAction(Player(rocky), ActionKind.SuppressingFire, P(3, 1)));
            Assert.IsTrue(Enemy(rocky, 0).IsSnared);
            Assert.IsFalse(Enemy(rocky, 1).IsSnared, "behind the rock");
        }

        [Test]
        public void MarkTarget_IsFree_AndTheNextHitByAnyoneDealsMore_Once()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.MarkTarget), M(UnitStats.Warrior) }, new[] { P(3, 0), P(4, 2) },
                new[] { E(UnitStats.Brute, 3, 2) });
            var archer = Player(b, 0);
            var warrior = Player(b, 1);
            var brute = Enemy(b);
            Assert.IsNotNull(b.PlayerUseAction(archer, ActionKind.MarkTarget, brute.Pos));
            Assert.IsTrue(brute.Marked);
            Assert.AreEqual(archer.MaxAp, archer.Ap, "free");
            Assert.IsFalse(b.CanUseAction(archer, ActionKind.MarkTarget, brute.Pos), "once on cooldown");
            b.EndUnitTurn(archer);

            var plain = b.DamageAgainst(warrior, brute);
            Assert.AreEqual(plain + Battle.MarkBonus, b.PredictHpLoss(warrior, brute, out _), "the preview counts it");
            var events = b.PlayerAttack(warrior, brute);
            Assert.AreEqual(UnitStats.Brute.MaxHp - plain - Battle.MarkBonus, brute.Hp);
            Assert.IsFalse(brute.Marked, "used up");
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Mark));
        }

        [Test]
        public void DeathMark_AMarkedKillMarksItsNeighbours()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.MarkTarget, Technique.DeathMark) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 4, 2), E(UnitStats.Wolf, 3, 3), E(UnitStats.Wolf, 6, 6) });
            var archer = Player(b);
            var target = Enemy(b, 0);
            b.PlayerUseAction(archer, ActionKind.MarkTarget, target.Pos);
            target.Hp = 1;
            b.PlayerAttack(archer, target);
            Assert.IsFalse(target.IsAlive);
            Assert.IsTrue(Enemy(b, 1).Marked);
            Assert.IsTrue(Enemy(b, 2).Marked);
            Assert.IsFalse(Enemy(b, 3).Marked, "not next to it");
            Assert.IsTrue(Enemy(b, 1).MarkSpreads, "and spreads again");
        }

        // ---------- Rogue ----------

        [Test]
        public void Isolate_HitsEnemiesWithNoAllyNextToThem()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Isolate) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Brute, 3, 2), E(UnitStats.Wolf, 6, 6) });
            var rogue = Player(b);
            var brute = Enemy(b);
            Assert.IsTrue(b.IsExposed(brute));
            var alone = b.DamageAgainst(rogue, brute);
            Enemy(b, 1).Pos = P(4, 2);
            Assert.IsFalse(b.IsExposed(brute));
            Assert.AreEqual(alone - Battle.IsolateBonus, b.DamageAgainst(rogue, brute));
        }

        [Test]
        public void Shadowstep_NotAfterWalking()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Shadowstep) }, new[] { P(0, 0) }, new[] { E(UnitStats.Wolf, 2, 2), Far });
            var rogue = Player(b);
            Assert.IsTrue(b.CanUseAction(rogue, ActionKind.Shadowstep, P(2, 1)), "in reach before walking");
            b.PlayerMove(rogue, P(1, 0));
            Assert.IsFalse(b.CanUseAction(rogue, ActionKind.Shadowstep, P(2, 1)));
        }

        [Test]
        public void RogueBase_SixHp()
        {
            Assert.AreEqual(6, UnitStats.Rogue.MaxHp);
            Assert.AreEqual(1, Battle.VanishTurns);
        }

        [Test]
        public void CheapShot_HitsPinnedOrSlowedHarder()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.CheapShot) }, new[] { P(3, 1) }, new[] { E(UnitStats.Brute, 3, 2) });
            var rogue = Player(b);
            var brute = Enemy(b);
            var plain = b.DamageAgainst(rogue, brute);
            brute.SnareTurns = 1;
            Assert.AreEqual(plain + Battle.CheapShotBonus, b.DamageAgainst(rogue, brute), "pinned");
            brute.SnareTurns = 0;
            brute.SlowNext = 1;
            Assert.AreEqual(plain + Battle.CheapShotBonus, b.DamageAgainst(rogue, brute), "slowed");
        }

        [Test]
        public void Elusive_LeavesReachWithoutLosingMoves()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Elusive) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), Far });
            var rogue = Player(b);
            Assert.IsEmpty(b.CostlyMoveTiles(rogue));
            b.PlayerMove(rogue, P(2, 1));
            Assert.AreEqual(1, rogue.Moves, "Swift: one move left");
        }

        [Test]
        public void ThrowingKnife_TwoToThreeTiles_CarriesPoison()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.ThrowingKnife, Technique.Poison) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Wolf, 3, 1), E(UnitStats.Brute, 3, 3), E(UnitStats.Wolf, 3, 4) });
            var rogue = Player(b);
            Assert.IsFalse(b.CanUseAction(rogue, ActionKind.ThrowingKnife, P(3, 1)), "too close");
            Assert.IsFalse(b.CanUseAction(rogue, ActionKind.ThrowingKnife, P(3, 4)), "too far");
            var brute = Enemy(b, 1);
            var expected = brute.Hp - (Battle.KnifeDamage + rogue.Member.LevelDamage);
            Assert.IsNotNull(b.PlayerUseAction(rogue, ActionKind.ThrowingKnife, brute.Pos));
            Assert.AreEqual(expected, brute.Hp);
            Assert.Greater(brute.PoisonTurns, 0);
        }

        [Test]
        public void Shadowstep_IsFree_LandsNextToAnEnemy_ThenNoWalking()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Shadowstep) }, new[] { P(0, 0) }, new[] { E(UnitStats.Wolf, 2, 2), Far }, P(1, 0));
            var rogue = Player(b);
            Assert.IsFalse(b.CanUseAction(rogue, ActionKind.Shadowstep, P(0, 2)), "not next to an enemy");
            Assert.IsTrue(b.CanUseAction(rogue, ActionKind.Shadowstep, P(2, 1)), "past the rock");
            Assert.IsNotNull(b.PlayerUseAction(rogue, ActionKind.Shadowstep, P(2, 1)));
            Assert.AreEqual(P(2, 1), rogue.Pos);
            Assert.AreEqual(rogue.MaxAp, rogue.Ap, "free");
            Assert.AreEqual(0, rogue.Moves, "no walking after it");
            Assert.IsTrue(b.CanAttack(rogue, Enemy(b)));
        }

        [Test]
        public void SmokeBomb_HidesItselfAndOneAlly_WithoutTheAmbushBonus()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.SmokeBomb), M(UnitStats.Archer), M(UnitStats.Warrior) },
                new[] { P(3, 1), P(4, 1), P(2, 1) }, new[] { E(UnitStats.Brute, 4, 4) });
            var rogue = Player(b, 0);
            var archer = Player(b, 1);
            var warrior = Player(b, 2);
            var plain = b.DamageAgainst(archer, Enemy(b));
            Assert.IsFalse(b.CanUseAction(rogue, ActionKind.SmokeBomb, P(3, 2)), "aimed at an ally, not a tile");
            Assert.IsNotNull(b.PlayerUseAction(rogue, ActionKind.SmokeBomb, archer.Pos));
            b.EndUnitTurn(rogue);
            Assert.IsTrue(rogue.IsHidden);
            Assert.IsTrue(archer.IsHidden);
            Assert.IsFalse(warrior.IsHidden, "next to it, but only one ally");
            Assert.AreEqual(plain, b.DamageAgainst(archer, Enemy(b)), "no ambush from smoke");
            Assert.AreEqual(4, Battle.Cooldown(ActionKind.SmokeBomb));
        }

        [Test]
        public void IntoTheShadows_HidesAtMostOnceEveryThreeTurns()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.IntoShadows) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 4, 1), Far });
            var rogue = Player(b);
            Enemy(b, 0).Hp = 1;
            b.PlayerAttack(rogue, Enemy(b, 0));
            Assert.IsTrue(rogue.IsHidden);
            b.EndPlayerTurn();
            Assert.IsFalse(rogue.IsHidden);
            rogue.Pos = P(3, 1);
            Enemy(b, 1).Pos = P(4, 1);
            Enemy(b, 1).Hp = 1;
            b.PlayerAttack(rogue, Enemy(b, 1));
            Assert.IsFalse(rogue.IsHidden, "the next turn's kill doesn't hide it");
        }

        [Test]
        public void Pins_CountOnlyUntilTheirTurnIsOver()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.CheapShot) }, new[] { P(0, 0) }, new[] { E(UnitStats.Brute, 3, 3) });
            var rogue = Player(b);
            var brute = Enemy(b);
            brute.SnareTurns = 1;
            Assert.IsTrue(brute.IsSnared, "pinned for its coming turn");
            b.EndPlayerTurn();
            Assert.IsFalse(brute.IsSnared, "its pinned turn is over at yours");
            rogue.Pos = P(3, 2);
            Assert.AreEqual(b.EffectiveDamage(rogue), b.DamageAgainst(rogue, brute), "no Cheap Shot now");
        }

        [Test]
        public void ConditionalBonuses_AreCappedAtFour()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.CheapShot, Technique.Isolate, Technique.Pincer), M(UnitStats.Warrior) },
                new[] { P(3, 1), P(3, 3) }, new[] { E(UnitStats.Brute, 3, 2) });
            var rogue = Player(b);
            var brute = Enemy(b);
            brute.SnareTurns = 1;
            b.PlayerUseAction(rogue, ActionKind.Vanish, rogue.Pos);
            Assert.AreEqual(Battle.MaxConditionalBonus, b.ConditionalBonus(rogue, brute), "3 + 2 + 2 + 2, capped");
            Assert.AreEqual(b.EffectiveDamage(rogue) + Battle.MaxConditionalBonus, b.DamageAgainst(rogue, brute));
        }

        [Test]
        public void Kick_WithOpeningShot_DoublesTheKickNotTheShot()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.Kick, Technique.OpeningShot) }, new[] { P(3, 1) },
                new[] { E(UnitStats.Brute, 3, 2) });
            var archer = Player(b);
            var brute = Enemy(b);
            var kick = b.EffectiveDamage(archer) - archer.Stats.Damage + Battle.KickDamage;
            b.PlayerUseAction(archer, ActionKind.Kick, brute.Pos);
            Assert.AreEqual(UnitStats.Brute.MaxHp - 2 * kick, brute.Hp);
        }

        [Test]
        public void DeathMark_SpreadsOnAnyDeath_TicksToo()
        {
            var b = Setup(new[] { M(UnitStats.Archer, Technique.MarkTarget, Technique.DeathMark) }, new[] { P(3, 0) },
                new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 4, 2), Far });
            var target = Enemy(b, 0);
            b.PlayerUseAction(Player(b), ActionKind.MarkTarget, target.Pos);
            target.Hp = 1;
            target.PoisonTurns = 1;
            b.EndPlayerTurn();
            Assert.IsFalse(target.IsAlive);
            Assert.IsTrue(Enemy(b, 1).Marked);
        }

        [Test]
        public void BlinkAndShadowstep_BlockedWhilePinnedOrSlowed()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Shadowstep), M(UnitStats.Apprentice, Technique.Blink) }, new[] { P(0, 0), P(5, 0) },
                new[] { E(UnitStats.Wolf, 2, 2), Far });
            var rogue = Player(b, 0);
            var mage = Player(b, 1);
            Assert.IsTrue(b.CanUseAction(rogue, ActionKind.Shadowstep, P(2, 1)));
            Assert.IsTrue(b.CanUseAction(mage, ActionKind.Blink, P(5, 2)));
            rogue.RootedThisTurn = true;
            mage.SlowedThisTurn = true;
            Assert.IsFalse(b.CanUseAction(rogue, ActionKind.Shadowstep, P(2, 1)));
            Assert.IsFalse(b.CanUseAction(mage, ActionKind.Blink, P(5, 2)));
        }

        [Test]
        public void Rupture_PreviewsTheKill()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Poison, Technique.Rupture) }, new[] { P(3, 1) }, new[] { E(UnitStats.Brute, 3, 2) });
            var brute = Enemy(b);
            brute.PoisonTurns = 2;
            brute.Hp = 2 * Battle.PoisonDamage + 1;
            CollectionAssert.Contains(b.ActionKills(Player(b), ActionKind.Rupture, brute.Pos), brute);
        }

        [Test]
        public void Rupture_BurstsThePoison_ThenStrikesAndPoisonsAgain()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.Poison, Technique.Rupture) }, new[] { P(3, 1) }, new[] { E(UnitStats.Brute, 3, 2) });
            var rogue = Player(b);
            var brute = Enemy(b);
            Assert.IsFalse(b.CanUseAction(rogue, ActionKind.Rupture, brute.Pos), "needs a poisoned target");
            brute.PoisonTurns = 2;
            var expected = brute.Hp - 2 * Battle.PoisonDamage - b.DamageAgainst(rogue, brute);
            var events = b.PlayerUseAction(rogue, ActionKind.Rupture, brute.Pos);
            Assert.IsNotNull(events);
            Assert.AreEqual(expected, brute.Hp);
            Assert.IsTrue(events.OfType<TickEvent>().Any(t => t.Cause == "rupture" && t.Source == rogue));
            Assert.AreEqual(Battle.PoisonTurns, brute.PoisonTurns, "the strike poisons again");
        }

        [Test]
        public void IntoTheShadows_AKillHidesIt_WithoutABonus()
        {
            var b = Setup(new[] { M(UnitStats.Rogue, Technique.IntoShadows) }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), Far });
            var rogue = Player(b);
            Enemy(b).Hp = 1;
            b.PlayerAttack(rogue, Enemy(b));
            Assert.IsTrue(rogue.IsHidden);
            Assert.IsTrue(rogue.AmbushUsed, "no +2 from it");
            Assert.IsFalse(b.CanAttack(Enemy(b, 1), rogue));
        }
    }
}
