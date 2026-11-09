using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    public class BattleTests
    {
        private static Battle Make(UnitStats[] army, (UnitStats, GridPos)[] enemies, params GridPos[] obstacles) =>
            new Battle(new Board(8, 8, obstacles), army, enemies);

        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static Battle Started(UnitStats[] army, GridPos[] placements, (UnitStats, GridPos)[] enemies, params GridPos[] obstacles)
        {
            var b = Make(army, enemies, obstacles);
            for (var i = 0; i < placements.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), placements[i]));
            Assert.IsTrue(b.StartBattle());
            return b;
        }

        [Test]
        public void Placement_OnlyInBackTwoRows()
        {
            var b = Battle.CreatePrototype();
            var warrior = Player(b);
            Assert.IsTrue(b.CanPlace(warrior, new GridPos(0, 0)));
            Assert.IsTrue(b.CanPlace(warrior, new GridPos(5, 1)));
            Assert.IsFalse(b.CanPlace(warrior, new GridPos(0, 2)));
        }

        [Test]
        public void Placement_TileCanHoldOnlyOneUnit()
        {
            var b = Battle.CreatePrototype();
            Assert.IsTrue(b.Place(Player(b, 0), new GridPos(3, 0)));
            Assert.IsFalse(b.Place(Player(b, 1), new GridPos(3, 0)));
        }

        [Test]
        public void StartBattle_RequiresWholeArmyPlaced()
        {
            var b = Battle.CreatePrototype();
            b.Place(Player(b, 0), new GridPos(0, 0));
            Assert.IsFalse(b.StartBattle());
            b.Place(Player(b, 1), new GridPos(1, 0));
            b.Place(Player(b, 2), new GridPos(2, 0));
            Assert.IsTrue(b.StartBattle());
            Assert.AreEqual(Phase.PlayerTurn, b.Phase);
            Assert.AreEqual(1, Player(b, 0).Ap);
        }

        [Test]
        public void Move_CostsOneMovePerTileAroundObstacles()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(0, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) }, new GridPos(0, 1));
            var rogue = Player(b);
            Assert.AreEqual(2, rogue.Moves, "Swift");
            rogue.Moves = 4;
            var reach = b.GetReachable(rogue);
            // (0,2) is straight ahead but (0,1) is a rock: right, up, up, left = 4.
            Assert.AreEqual(4, reach[new GridPos(0, 2)]);
            Assert.IsFalse(reach.ContainsKey(new GridPos(0, 1)));

            var move = b.PlayerMove(rogue, new GridPos(0, 2))?.OfType<MoveEvent>().Single();
            Assert.IsNotNull(move);
            Assert.AreEqual(4, move.Path.Count);
            Assert.AreEqual(0, rogue.Moves);
            Assert.AreEqual(1, rogue.Ap, "moving costs no AP");
        }

        [Test]
        public void Move_NotAfterSpendingAp_ForBothSides()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Brute, new GridPos(3, 2)) });
            var rogue = Player(b);
            Assert.IsNotNull(b.PlayerAttack(rogue, Enemy(b)));
            Assert.AreEqual(2, rogue.Moves, "its moves are still there");
            Assert.AreEqual(0, b.GetReachable(rogue).Count, "but it can't walk after attacking");
            Assert.IsFalse(b.CanStillAct(rogue));

            var brute = Enemy(b);
            rogue.Hp = 20; // small: the Bull Rush throws it into the edge, which would kill it
            b.EndPlayerTurn();
            Assert.AreEqual(new GridPos(3, 2), brute.Pos, "the brute hit back and stayed");
            Assert.IsTrue(brute.ActedThisTurn);
            Assert.IsTrue(b.GetReachable(rogue).Count > 0, "a new turn: moving is allowed again");
        }

        [Test]
        public void Move_OneTilePerTurnByDefault()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            var warrior = Player(b);
            Assert.AreEqual(1, warrior.Moves);
            CollectionAssert.AreEquivalent(new[] { new GridPos(2, 0), new GridPos(4, 0), new GridPos(3, 1) }, b.GetReachable(warrior).Keys);
            b.PlayerMove(warrior, new GridPos(3, 1));
            Assert.AreEqual(0, b.GetReachable(warrior).Count);
            b.EndPlayerTurn();
            Assert.AreEqual(1, warrior.Moves, "moves refill and don't carry over");
        }

        [Test]
        public void Move_NoDiagonals()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            Player(b).Moves = 2;
            Assert.AreEqual(2, b.GetReachable(Player(b))[new GridPos(4, 1)]);
        }

        [Test]
        public void Attack_UsesTheOneApAndDealsDamage()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 2)) });
            var rogue = Player(b);
            var wolf = Enemy(b);
            var hit = b.PlayerAttack(rogue, wolf)?.OfType<AttackEvent>().Single();
            Assert.AreEqual(4, hit.Damage);
            Assert.AreEqual(2, wolf.Hp);
            Assert.AreEqual(0, rogue.Ap);
            Assert.IsNull(b.PlayerAttack(rogue, wolf), "one attack a turn");
            rogue.Ap = 1;
            b.PlayerAttack(rogue, wolf);
            Assert.IsFalse(wolf.IsAlive);
            Assert.AreEqual(Phase.Victory, b.Phase);
        }

        [Test]
        public void Archer_RangeCountsStepsAndNeedsLineOfSight()
        {
            var b = Started(new[] { UnitStats.Archer }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)), (UnitStats.Wolf, new GridPos(4, 4)) },
                new GridPos(0, 0));
            var archer = Player(b);
            Assert.IsTrue(b.CanAttack(archer, Enemy(b, 0)), "3 steps straight ahead");
            Assert.IsFalse(b.CanAttack(archer, Enemy(b, 1)), "4 steps away");

            var blocked = Started(new[] { UnitStats.Archer }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)) }, new GridPos(3, 3));
            Assert.IsFalse(blocked.CanAttack(Player(blocked), Enemy(blocked)), "rock in the way");
        }

        [Test]
        public void LineOfSight_SqueezesPastSingleCorner()
        {
            var board = new Board(8, 8, new[] { new GridPos(1, 0) });
            Assert.IsTrue(board.HasLineOfSight(new GridPos(0, 0), new GridPos(2, 2)));
            var walled = new Board(8, 8, new[] { new GridPos(1, 0), new GridPos(0, 1) });
            Assert.IsFalse(walled.HasLineOfSight(new GridPos(0, 0), new GridPos(1, 1)));
        }

        [Test]
        public void UnusedAp_DoesntCarryOver()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(0, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            var rogue = Player(b);
            b.EndPlayerTurn();
            Assert.AreEqual(1, rogue.Ap);
        }

        [Test]
        public void OnlyOneUnitActsAtATime()
        {
            var b = Started(new[] { UnitStats.Warrior, UnitStats.Rogue }, new[] { new GridPos(0, 0), new GridPos(5, 0) },
                new[] { (UnitStats.Wolf, new GridPos(7, 7)) });
            var warrior = Player(b, 0);
            var rogue = Player(b, 1);
            Assert.IsNotNull(b.PlayerMove(warrior, new GridPos(0, 1)));
            Assert.IsFalse(b.CanAct(rogue));
            b.EndUnitTurn(warrior);
            Assert.IsFalse(b.CanAct(warrior));
            Assert.IsTrue(b.CanAct(rogue));
        }

        [Test]
        public void PackBond_WolvesHaveExtraHp_AndLoseItWhenTheLeaderDies()
        {
            var b = Started(new[] { UnitStats.Rogue, UnitStats.Rogue }, new[] { new GridPos(0, 1), new GridPos(1, 1) },
                new[]
                {
                    (UnitStats.PackLeader, new GridPos(0, 2)),
                    (UnitStats.Wolf, new GridPos(1, 3)),
                    (UnitStats.Wolf, new GridPos(6, 6)),
                });
            var leader = Enemy(b, 0);
            var healthy = Enemy(b, 1);
            var wounded = Enemy(b, 2);
            Assert.AreEqual(UnitStats.Wolf.MaxHp + 1, healthy.MaxHp, "anywhere on the board, no range");
            Assert.AreEqual(UnitStats.Wolf.MaxHp + 1, healthy.Hp);
            Assert.AreEqual(PackState.Bonded, b.GetPackState(wounded));
            Assert.AreEqual(UnitStats.Wolf.Damage, b.EffectiveDamage(healthy), "no damage aura any more");
            wounded.Hp = 2;

            leader.Hp = UnitStats.Rogue.Damage;
            var events = b.PlayerAttack(Player(b, 0), leader);
            Assert.IsFalse(leader.IsAlive);
            Assert.AreEqual(2, events.OfType<TickEvent>().Count(t => t.Cause == "leader lost"));
            Assert.AreEqual(UnitStats.Wolf.MaxHp - 1, healthy.MaxHp);
            Assert.AreEqual(UnitStats.Wolf.MaxHp - 1, healthy.Hp, "lost 2");
            Assert.IsFalse(wounded.IsAlive, "a wolf on 2 HP dies with its leader");
            Assert.AreEqual(PackState.Leaderless, b.GetPackState(healthy));
        }

        [Test]
        public void WolfBite_EachBiteThisTurnHitsHarder_ResetsNextTurn()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 2)), (UnitStats.Wolf, new GridPos(2, 1)), (UnitStats.Wolf, new GridPos(4, 1)) });
            Player(b).Member.Learn(Technique.Tough);
            Player(b).Hp = 50;
            var events = b.EndPlayerTurn();
            var bites = events.OfType<AttackEvent>().Where(a => a.Attacker.Team == Team.Enemy).Select(a => a.Damage).ToList();
            // 2, 3, 4 before Tough (-1); Shield Wall doesn't cover the warrior itself.
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, bites);
            Assert.AreEqual(0, b.BiteChain, "the chain resets at the start of the next turn");
        }

        [Test]
        public void BloodScent_GoesForTheWeakestItCanReach()
        {
            var b = Started(new[] { UnitStats.Warrior, UnitStats.Archer, UnitStats.Rogue },
                new[] { new GridPos(3, 1), new GridPos(4, 1), new GridPos(0, 0) },
                new[] { (UnitStats.Wolf, new GridPos(3, 3)) });
            var wolf = Enemy(b);
            Player(b, 1).Hp = 3;
            Player(b, 2).Hp = 1;
            Assert.AreEqual(Player(b, 1), EnemyAI.ChooseTarget(b, wolf), "the wounded archer, not the nearer warrior; the rogue is out of reach");
        }

        [Test]
        public void Elusive_GoblinsIgnoreZoneOfControl()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) }, new[] { (UnitStats.Goblin, new GridPos(3, 2)) });
            var goblin = Enemy(b);
            goblin.Moves = goblin.MaxMoves;
            Assert.AreEqual(2, b.GetReachable(goblin)[new GridPos(3, 4)], "two steps away, though it started engaged");
            Assert.AreEqual(0, b.EngagedBy(goblin, goblin.Pos).Count());
        }

        [Test]
        public void Fling_ThrowsAGoblinThatLandsAndStillBites()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 0) },
                new[] { (UnitStats.Goblin, new GridPos(3, 4)), (UnitStats.Goblin, new GridPos(4, 4)) });
            var thrower = Enemy(b, 0);
            var thrown = Enemy(b, 1);
            var warrior = Player(b);
            var intent = EnemyAI.PredictIntent(b, thrower);
            Assert.AreEqual(IntentKind.Fling, intent.Kind, "too far to walk up and strike");
            Assert.AreEqual(thrown, intent.Thrown);

            var events = b.EndPlayerTurn();
            var fling = events.OfType<FlingEvent>().Single();
            Assert.AreEqual(thrown, fling.Thrown);
            Assert.AreEqual(new GridPos(3, 1), thrown.Pos, "landed next to the warrior, on the side facing the thrower");
            Assert.AreEqual(UnitStats.Goblin.MaxHp, thrown.Hp, "no landing damage");
            Assert.AreEqual(warrior.MaxHp - Battle.FlingDamage - UnitStats.Goblin.Damage, warrior.Hp, "the landing, then its strike");
        }

        [Test]
        public void Fling_ThrowsYourMineBackAtYou()
        {
            var b = Started(new[] { UnitStats.Sapper }, new[] { new GridPos(3, 0) }, new[] { (UnitStats.Goblin, new GridPos(4, 1)) });
            var sapper = Player(b);
            Assert.IsNotNull(b.PlayerPlaceMine(sapper, new GridPos(3, 1)));
            var events = b.EndPlayerTurn();
            var blast = events.OfType<ExplosionEvent>().First();
            Assert.AreEqual("mine", blast.Cause);
            Assert.AreEqual(sapper.Pos, blast.Center, "it went off on the sapper");
            Assert.AreEqual(0, b.Mines.Count);
            Assert.AreEqual(sapper.MaxHp - Battle.MineDamage, sapper.Hp);
        }

        [Test]
        public void PowderKeg_BomberExplodesWhenItDies()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.GoblinBomber, new GridPos(3, 2)), (UnitStats.Wolf, new GridPos(2, 2)) });
            var rogue = Player(b);
            var wolf = Enemy(b, 1);
            Enemy(b, 0).Hp = 1;
            var events = b.PlayerAttack(rogue, Enemy(b, 0));
            Assert.IsTrue(events.OfType<ExplosionEvent>().Any(e => e.Cause == "powder keg"));
            Assert.AreEqual(rogue.MaxHp - Battle.PowderKegDamage, rogue.Hp, "the killer is caught in it");
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.PowderKegDamage, wolf.Hp, "so is its own side");
        }

        [Test]
        public void Cooldown_ActionReadyAgainTwoTurnsLater_AttackHasNone()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Brute, new GridPos(3, 2)), (UnitStats.Wolf, new GridPos(7, 7)) }, new GridPos(3, 4));
            var warrior = Player(b);
            Assert.IsNotNull(b.PlayerUseAction(warrior, ActionKind.ShieldBash, new GridPos(3, 2)));
            Assert.AreEqual(Battle.Cooldown(ActionKind.ShieldBash), warrior.CooldownLeft(ActionKind.ShieldBash));
            b.EndPlayerTurn();
            Assert.AreEqual(1, warrior.CooldownLeft(ActionKind.ShieldBash));
            b.EndPlayerTurn();
            Assert.AreEqual(0, warrior.CooldownLeft(ActionKind.ShieldBash), "ready two of its turns later");
        }

        [Test]
        public void LightFingers_CutpurseStealsAndRunsInTheSameTurn()
        {
            var b = Started(new[] { UnitStats.Archer }, new[] { new GridPos(3, 1) }, new[] { (UnitStats.Cutpurse, new GridPos(3, 2)) });
            var thief = Enemy(b);
            b.EndPlayerTurn();
            Assert.AreEqual(UnitStats.Cutpurse.StealsGold, thief.CarryingLoot);
            Assert.AreEqual(new GridPos(3, 3), thief.Pos, "stabbed, then ran (stepping out of the archer's reach uses up its moves)");
        }

        [Test]
        public void Caltrops_StopYourUnitAndHurt()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 0) }, new[] { (UnitStats.Cutpurse, new GridPos(7, 7)) });
            var warrior = Player(b);
            var thief = Enemy(b);
            thief.Ap = 1;
            thief.Pos = new GridPos(4, 2);
            b.DoCaltrops(thief, new GridPos(3, 2));
            thief.Pos = new GridPos(7, 7);
            warrior.Moves = 3;
            b.PlayerMove(warrior, new GridPos(3, 3));
            Assert.AreEqual(new GridPos(3, 2), warrior.Pos, "stopped on the caltrops");
            Assert.AreEqual(0, warrior.Moves);
            Assert.AreEqual(warrior.MaxHp - Battle.CaltropsDamage, warrior.Hp);
            Assert.AreEqual(0, b.Caltrops.Count, "used up");
        }

        [Test]
        public void BehindCover_NoRangedHitsNextToARock()
        {
            var b = Started(new[] { UnitStats.Archer }, new[] { new GridPos(3, 1) }, new[] { (UnitStats.Crossbowman, new GridPos(3, 4)) }, new GridPos(4, 4));
            var archer = Player(b);
            var crossbow = Enemy(b);
            Assert.IsFalse(b.CanAttack(archer, crossbow), "next to the rock");
            crossbow.Pos = new GridPos(2, 3);
            Assert.IsTrue(b.CanAttack(archer, crossbow), "out of cover");
        }

        [Test]
        public void Overwatch_ShootsTheFirstUnitWalkingIntoSight()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 0) }, new[] { (UnitStats.Crossbowman, new GridPos(3, 5)) });
            var warrior = Player(b);
            var crossbow = Enemy(b);
            Assert.AreEqual(IntentKind.Overwatch, EnemyAI.PredictIntent(b, crossbow).Kind);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<OverwatchEvent>().Any());
            Assert.IsTrue(b.OverwatchTiles.Contains(new GridPos(3, 1)));

            var move = b.PlayerMove(warrior, new GridPos(3, 1));
            var shot = move.OfType<AttackEvent>().Single();
            Assert.AreEqual(crossbow, shot.Attacker);
            Assert.AreEqual(warrior.MaxHp - UnitStats.Crossbowman.Damage, warrior.Hp);
            Assert.IsFalse(b.OverwatchTiles.Any(), "one shot, then it stops watching");
        }

        [Test]
        public void BullRush_HitsAndPushes()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) }, new[] { (UnitStats.Brute, new GridPos(3, 2)) });
            var warrior = Player(b);
            Assert.AreEqual(IntentKind.BullRush, EnemyAI.PredictIntent(b, Enemy(b)).Kind);
            b.EndPlayerTurn();
            Assert.AreEqual(new GridPos(3, 0), warrior.Pos, "pushed away");
            Assert.AreEqual(warrior.MaxHp - UnitStats.Brute.Damage, warrior.Hp);
        }

        [Test]
        public void Tremor_OutOfReach_WalksCloserAndSlows()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) }, new[] { (UnitStats.Elemental, new GridPos(3, 4)) });
            var warrior = Player(b);
            var elemental = Enemy(b);
            Assert.AreEqual(IntentKind.Tremor, EnemyAI.PredictIntent(b, elemental).Kind);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<TremorEvent>().Any());
            Assert.AreEqual(new GridPos(3, 3), elemental.Pos, "walked closer first");
            Assert.AreEqual(warrior.MaxHp, warrior.Hp, "no damage");
            Assert.IsTrue(warrior.SlowedThisTurn, "slowed on its next turn");
            Assert.IsFalse(warrior.IsSnared, "one tremor only slows");
        }

        [Test]
        public void Tremor_TwoOverlapping_Pin()
        {
            // Neither elemental can reach the warrior to strike; the first slows it, the second pins it.
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Elemental, new GridPos(3, 4)), (UnitStats.Elemental, new GridPos(1, 2)) });
            var warrior = Player(b);
            var events = b.EndPlayerTurn();
            var tremors = events.OfType<TremorEvent>().ToList();
            Assert.AreEqual(2, tremors.Count);
            Assert.Contains(warrior, tremors[0].Slowed.ToList());
            Assert.Contains(warrior, tremors[1].Pinned.ToList());
            Assert.IsTrue(warrior.IsSnared, "pinned: it can't move this turn");
            Assert.IsFalse(warrior.SlowedThisTurn, "pinned instead of slowed");

            var tally = new ComboTally();
            tally.Observe(events, 1);
            Assert.AreEqual(1, tally["elementals.tremorPins"]);
        }

        [Test]
        public void Aftershock_KillingAShard_SlowsWhoIsNextToIt()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) }, new[] { (UnitStats.Shard, new GridPos(3, 2)) });
            var warrior = Player(b);
            var shard = Enemy(b);
            shard.Hp = 1;
            var events = b.PlayerAttack(warrior, shard);
            Assert.IsFalse(shard.IsAlive);
            Assert.IsTrue(events.OfType<StatusEvent>().Any(s => s.Kind == StatusKind.Slow && s.Target == warrior && s.Source == shard));
            Assert.AreEqual(1, warrior.SlowNext, "slowed on its next turn, so a tremor before then pins it");
        }

        [Test]
        public void Grasp_SkeletonHoldsARangedUnitButStrikesAWarrior()
        {
            var b = Started(new[] { UnitStats.Archer }, new[] { new GridPos(3, 1) }, new[] { (UnitStats.Skeleton, new GridPos(3, 2)) });
            var archer = Player(b);
            Assert.AreEqual(IntentKind.Grasp, EnemyAI.PredictIntent(b, Enemy(b)).Kind);
            b.EndPlayerTurn();
            Assert.AreEqual(archer.MaxHp, archer.Hp, "no damage");
            Assert.IsTrue(archer.RootedThisTurn, "can't move this turn");

            var w = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) }, new[] { (UnitStats.Skeleton, new GridPos(3, 2)) });
            Assert.AreEqual(IntentKind.Attack, EnemyAI.PredictIntent(w, Enemy(w)).Kind);
            w.EndPlayerTurn();
            Assert.AreEqual(Player(w).MaxHp - UnitStats.Skeleton.Damage, Player(w).Hp);
        }

        [Test]
        public void Howl_GivesWolvesAnExtraMoveWhenItHelps()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 0) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)), (UnitStats.PackLeader, new GridPos(0, 5)) });
            var wolf = Enemy(b, 0);
            var leader = Enemy(b, 1);
            // The wolf needs 3 moves to reach (3,1); it has 2, so the howl lets it bite.
            Assert.AreEqual(IntentKind.Howl, EnemyAI.PredictIntent(b, leader).Kind);
            var events = b.EndPlayerTurn();
            var howl = events.OfType<HowlEvent>().Single();
            CollectionAssert.AreEqual(new[] { wolf }, howl.Wolves.ToList());
            Assert.IsTrue(events.IndexOf(howl) < events.FindIndex(e => e is AttackEvent), "the leader goes first");
            Assert.AreEqual(new GridPos(3, 1), wolf.Pos);
            Assert.Less(Player(b).Hp, Player(b).MaxHp);
        }

        [Test]
        public void Enemy_TargetsNearestUnit()
        {
            var b = Started(new[] { UnitStats.Warrior, UnitStats.Archer }, new[] { new GridPos(0, 0), new GridPos(6, 1) },
                new[] { (UnitStats.Wolf, new GridPos(6, 5)) });
            Assert.AreEqual(Player(b, 1), EnemyAI.ChooseTarget(b, Enemy(b)));
        }

        [Test]
        public void Enemy_WalksUpAndAttacks()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)) });
            var events = b.EndPlayerTurn();
            var wolf = Enemy(b);
            Assert.AreEqual(new GridPos(3, 2), wolf.Pos, "a Swift wolf walks 2 tiles");
            Assert.AreEqual(1, events.OfType<AttackEvent>().Count(), "walking costs no AP, then one bite");
            Assert.AreEqual(10, Player(b).Hp);
            Assert.AreEqual(Phase.PlayerTurn, b.Phase);
        }

        [Test]
        public void Enemy_AdjacentAttacksOnce()
        {
            var b = Started(new[] { UnitStats.Warrior }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.Wolf, new GridPos(3, 2)) });
            var events = b.EndPlayerTurn();
            Assert.AreEqual(1, events.OfType<AttackEvent>().Count());
            Assert.AreEqual(10, Player(b).Hp);
        }

        [Test]
        public void Enemy_KillingLastUnitIsDefeat()
        {
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(3, 1) },
                new[] { (UnitStats.PackLeader, new GridPos(3, 2)), (UnitStats.Wolf, new GridPos(4, 1)) });
            b.EndPlayerTurn();
            Assert.AreEqual(Phase.Defeat, b.Phase);
        }

        [Test]
        public void Prototype_FullBattleTerminates()
        {
            var b = Battle.CreatePrototype();
            b.Place(Player(b, 0), new GridPos(3, 1));
            b.Place(Player(b, 1), new GridPos(3, 0));
            b.Place(Player(b, 2), new GridPos(4, 1));
            b.StartBattle();
            for (var turn = 0; turn < 50 && !b.IsOver; turn++)
                b.EndPlayerTurn();
            Assert.AreEqual(Phase.Defeat, b.Phase, "a passive player should lose");
        }

        [Test]
        public void ZoneOfControlDisplay_ReachTiles_CostlyMoves_EngagedBy()
        {
            // Rogue at (3,3) next to a wolf at (3,4); a rock at (2,4) next to the wolf.
            var b = Started(new[] { UnitStats.Rogue }, new[] { new GridPos(3, 0) },
                new[] { (UnitStats.Wolf, new GridPos(3, 4)) }, new GridPos(2, 4));
            var rogue = Player(b);
            var wolf = Enemy(b);
            rogue.Pos = new GridPos(3, 3);

            var reach = b.EnemyReachTiles(rogue);
            CollectionAssert.AreEquivalent(new[] { new GridPos(3, 3), new GridPos(4, 4), new GridPos(3, 5) }, reach,
                "free tiles next to the wolf (the rock is left out), plus the rogue's own");
            CollectionAssert.AreEqual(new[] { wolf }, b.EngagedBy(rogue, rogue.Pos).ToList());

            var costly = b.CostlyMoveTiles(rogue);
            Assert.IsTrue(costly.Contains(new GridPos(3, 2)), "stepping away pays zone of control");
            Assert.IsTrue(b.GetReachable(rogue).Keys.All(costly.Contains), "every move starts by leaving the wolf's reach");

            rogue.Pos = new GridPos(0, 0);
            Assert.AreEqual(0, b.EngagedBy(rogue, rogue.Pos).Count());
            Assert.AreEqual(0, b.CostlyMoveTiles(rogue).Count, "nothing costs extra away from enemies");
        }
    }
}
