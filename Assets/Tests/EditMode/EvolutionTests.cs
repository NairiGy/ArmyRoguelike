using System.Linq;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>The rules of the evolved unit types.</summary>
    public class EvolutionTests
    {
        private static Unit Player(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Player).ElementAt(index);

        private static Unit Enemy(Battle b, int index = 0) => b.Units.Where(u => u.Team == Team.Enemy).ElementAt(index);

        private static GridPos P(int x, int y) => new GridPos(x, y);

        /// <summary>A started battle where players stand where told (anywhere, not only the back rows).</summary>
        private static Battle Setup(UnitStats[] army, GridPos[] at, (UnitStats, GridPos)[] enemies, params GridPos[] rocks)
        {
            var b = new Battle(new Board(8, 8, rocks), army.Select(s => new ArmyMember(s)), enemies);
            for (var i = 0; i < army.Length; i++)
                Assert.IsTrue(b.Place(Player(b, i), P(i, 0)));
            Assert.IsTrue(b.StartBattle());
            for (var i = 0; i < army.Length; i++)
                Player(b, i).Pos = at[i];
            return b;
        }

        private static (UnitStats, GridPos) E(UnitStats s, int x, int y) => (s, P(x, y));

        private static (UnitStats, GridPos) Far => (UnitStats.Wolf, P(7, 7));

        // ---------- Warrior tree ----------

        [Test]
        public void Swordsman_SweepHitsBothSides()
        {
            var b = Setup(new[] { UnitStats.Swordsman }, new[] { P(3, 2) },
                new[] { E(UnitStats.Wolf, 2, 3), E(UnitStats.Wolf, 3, 3), E(UnitStats.Wolf, 4, 3), Far });
            b.PlayerAttack(Player(b), Enemy(b, 1));
            Assert.IsTrue(Enumerable.Range(0, 3).All(i => Enemy(b, i).Hp == UnitStats.Wolf.MaxHp - 4));
        }

        [Test]
        public void Longswordsman_HitsTwoTilesInALine()
        {
            var b = Setup(new[] { UnitStats.Longswordsman }, new[] { P(3, 2) },
                new[] { E(UnitStats.Wolf, 3, 3), E(UnitStats.Wolf, 3, 4), Far });
            Assert.IsTrue(b.CanAttack(Player(b), Enemy(b, 1)), "reach 2");
            b.PlayerAttack(Player(b), Enemy(b, 1));
            Assert.Less(Enemy(b, 0).Hp, UnitStats.Wolf.MaxHp);
            Assert.Less(Enemy(b, 1).Hp, UnitStats.Wolf.MaxHp);
        }

        [Test]
        public void DualBlades_StrikesTwice()
        {
            var b = Setup(new[] { UnitStats.DualBlades }, new[] { P(3, 2) }, new[] { E(UnitStats.Ogre, 3, 3) });
            b.PlayerAttack(Player(b), Enemy(b));
            Assert.AreEqual(UnitStats.Ogre.MaxHp - 2 * UnitStats.DualBlades.Damage, Enemy(b).Hp);
        }

        [Test]
        public void Shielder_GuardsAdjacentAlly()
        {
            var b = Setup(new[] { UnitStats.Shielder, UnitStats.Archer }, new[] { P(3, 1), P(4, 1) },
                new[] { E(UnitStats.Wolf, 4, 2) });
            var shielder = Player(b, 0);
            var archer = Player(b, 1);
            var events = b.EndPlayerTurn();
            Assert.AreEqual(archer.MaxHp, archer.Hp, "the archer is never hit");
            Assert.Less(shielder.Hp, shielder.MaxHp);
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Guard));
        }

        [Test]
        public void Bastion_TauntsAndIsTough()
        {
            var b = Setup(new[] { UnitStats.Bastion, UnitStats.Archer }, new[] { P(4, 3), P(5, 3) },
                new[] { E(UnitStats.Wolf, 5, 4) });
            Assert.AreEqual(Player(b, 0), EnemyAI.ChooseTarget(b, Enemy(b)), "taunt beats the nearer archer");
            var lone = Setup(new[] { UnitStats.Bastion }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2) });
            lone.EndPlayerTurn();
            Assert.AreEqual(UnitStats.Bastion.MaxHp - 1, Player(lone).Hp, "one bite of 2, reduced to 1");
        }

        [Test]
        public void Juggernaut_ChargesAndPushes()
        {
            var b = Setup(new[] { UnitStats.Juggernaut }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 4) });
            var jug = Player(b);
            var wolf = Enemy(b);
            Assert.IsTrue(b.CanUseAction(jug, ActionKind.Charge, wolf.Pos));
            b.PlayerUseAction(jug, ActionKind.Charge, wolf.Pos);
            Assert.AreEqual(P(3, 3), jug.Pos);
            Assert.AreEqual(P(3, 5), wolf.Pos, "knocked back one tile");
        }

        [Test]
        public void Push_IntoRock_DealsCollisionDamage()
        {
            var b = Setup(new[] { UnitStats.Juggernaut }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 4) }, P(3, 5));
            var wolf = Enemy(b);
            b.PlayerUseAction(Player(b), ActionKind.Charge, wolf.Pos);
            Assert.AreEqual(P(3, 4), wolf.Pos);
            Assert.AreEqual(UnitStats.Wolf.MaxHp - UnitStats.Juggernaut.Damage - Battle.CollisionDamage, wolf.Hp);
        }

        // ---------- Archer tree ----------

        [Test]
        public void Sniper_PiercesTheLine()
        {
            var b = Setup(new[] { UnitStats.Sniper }, new[] { P(3, 0) },
                new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 3, 4), Far });
            b.PlayerAttack(Player(b), Enemy(b, 0));
            Assert.Less(Enemy(b, 1).Hp, UnitStats.Wolf.MaxHp, "the wolf behind is hit too");
        }

        [Test]
        public void Deadeye_FirstShotOnEachTargetDouble()
        {
            var b = Setup(new[] { UnitStats.Deadeye }, new[] { P(3, 0) },
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
        public void Snare_RootsNextTurn_AndHunterPunishesIt()
        {
            var b = Setup(new[] { UnitStats.Hunter }, new[] { P(3, 0) }, new[] { E(UnitStats.Wolf, 3, 3) });
            var hunter = Player(b);
            var wolf = Enemy(b);
            b.PlayerUseAction(hunter, ActionKind.Snare, wolf.Pos);
            Assert.IsTrue(wolf.IsSnared);
            Assert.AreEqual(b.EffectiveDamage(hunter) + Battle.PreyBonus, b.DamageAgainst(hunter, wolf));
            b.EndPlayerTurn();
            Assert.AreEqual(P(3, 3), wolf.Pos, "rooted: it couldn't move");
        }

        [Test]
        public void VolleyMaster_HitsPlus_FriendlyFireIncluded()
        {
            var b = Setup(new[] { UnitStats.VolleyMaster, UnitStats.Warrior }, new[] { P(3, 1), P(4, 4) },
                new[] { E(UnitStats.Wolf, 3, 4), E(UnitStats.Wolf, 2, 4), E(UnitStats.Wolf, 2, 5), Far });
            b.PlayerAttack(Player(b, 0), Enemy(b, 0));
            Assert.Less(Enemy(b, 1).Hp, UnitStats.Wolf.MaxHp);
            Assert.AreEqual(UnitStats.Wolf.MaxHp, Enemy(b, 2).Hp, "diagonal tiles are outside the plus");
            Assert.Less(Player(b, 1).Hp, Player(b, 1).MaxHp, "the warrior was in the area");
        }

        // ---------- Rogue tree ----------

        [Test]
        public void Assassin_PincerBonus()
        {
            var b = Setup(new[] { UnitStats.Assassin, UnitStats.Warrior }, new[] { P(3, 2), P(3, 4) },
                new[] { E(UnitStats.Wolf, 3, 3) });
            Assert.AreEqual(b.EffectiveDamage(Player(b)) + Battle.PincerBonus, b.DamageAgainst(Player(b), Enemy(b)));
        }

        [Test]
        public void Shadow_KillRefundsAp()
        {
            var b = Setup(new[] { UnitStats.Shadow }, new[] { P(3, 2) }, new[] { E(UnitStats.Wolf, 3, 3), Far });
            var shadow = Player(b);
            Enemy(b).Hp = 1;
            var before = shadow.Ap;
            b.PlayerAttack(shadow, Enemy(b));
            Assert.AreEqual(before - Battle.AttackCost + Battle.KillRefundAp, shadow.Ap);
        }

        [Test]
        public void Poisoner_PoisonTicksOnEnemyTurns()
        {
            var b = Setup(new[] { UnitStats.Poisoner }, new[] { P(0, 0) }, new[] { E(UnitStats.Ogre, 5, 5) });
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
        public void Duelist_CountersOncePerTurn_BlademasterEveryTime()
        {
            var b = Setup(new[] { UnitStats.Duelist }, new[] { P(3, 1) }, new[] { E(UnitStats.Ogre, 3, 2) });
            var events = b.EndPlayerTurn();
            Assert.AreEqual(1, events.OfType<AttackEvent>().Count(a => a.Attacker.Team == Team.Player));

            var b2 = Setup(new[] { UnitStats.Blademaster }, new[] { P(3, 1) }, new[] { E(UnitStats.Ogre, 3, 2) });
            Player(b2).Hp = 50; // survive both smashes so every one gets countered
            var events2 = b2.EndPlayerTurn();
            var smashes = events2.OfType<SmashEvent>().Count();
            Assert.AreEqual(smashes, events2.OfType<AttackEvent>().Count(a => a.Attacker.Team == Team.Player));
        }

        [Test]
        public void Swashbuckler_DodgesFirstHit()
        {
            var b = Setup(new[] { UnitStats.Swashbuckler }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 2, 1) });
            var swash = Player(b);
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<EffectEvent>().Any(e => e.Kind == EffectKind.Dodge));
            Assert.AreEqual(swash.MaxHp - 3, swash.Hp, "only the second bite landed: 2, +1 from the bite chain");
        }

        // ---------- Mage tree ----------

        [Test]
        public void Apprentice_BoltIgnoresLineOfSight()
        {
            var b = Setup(new[] { UnitStats.Apprentice }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 4) }, P(3, 2), P(3, 3));
            Assert.IsTrue(b.CanAttack(Player(b), Enemy(b)));
        }

        [Test]
        public void Fireball_Hits3x3_FriendlyFire_AndPyroBurns()
        {
            var b = Setup(new[] { UnitStats.Pyromancer, UnitStats.Warrior }, new[] { P(3, 0), P(4, 3) },
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
        public void BlastMage_PushesOutward_AndSetsOffMines()
        {
            var b = Setup(new[] { UnitStats.BlastMage, UnitStats.Sapper }, new[] { P(4, 1), P(6, 2) },
                new[] { E(UnitStats.Ogre, 0, 6), E(UnitStats.Wolf, 4, 3) });
            var sapper = Player(b, 1);
            b.PlayerPlaceMine(sapper, P(6, 3));
            b.EndUnitTurn(sapper);
            var wolf = Enemy(b, 1);
            wolf.Hp = 20;
            var events = b.PlayerUseAction(Player(b, 0), ActionKind.Fireball, P(5, 3));
            Assert.AreEqual(P(3, 3), wolf.Pos, "pushed one tile away from the centre");
            Assert.IsTrue(events.OfType<ExplosionEvent>().Any(), "the mine inside the area went off");
        }

        [Test]
        public void FrostMage_SlowsNextTurn_GlacierShatters()
        {
            var b = Setup(new[] { UnitStats.Glacier }, new[] { P(3, 0) }, new[] { E(UnitStats.Ogre, 3, 2) });
            var glacier = Player(b);
            var ogre = Enemy(b);
            var normal = b.DamageAgainst(glacier, ogre);
            b.PlayerAttack(glacier, ogre);
            Assert.IsTrue(ogre.IsSlowed);
            Assert.AreEqual(normal * 2, b.DamageAgainst(glacier, ogre));
            b.EndUnitTurn(glacier);
            b.EndPlayerTurn();
            Assert.IsTrue(ogre.SlowedThisTurn);
            Assert.AreEqual(P(3, 2), ogre.Pos, "chilled: it couldn't move");
        }

        [Test]
        public void Cryomancer_IceWallBlocksThenMelts()
        {
            var b = Setup(new[] { UnitStats.Cryomancer }, new[] { P(3, 0) }, new[] { Far });
            b.PlayerUseAction(Player(b), ActionKind.IceWall, P(3, 2));
            Assert.IsTrue(b.Board.IsObstacle(P(3, 2)));
            for (var i = 0; i < Battle.IceRounds; i++)
                b.EndPlayerTurn();
            Assert.IsFalse(b.Board.IsObstacle(P(3, 2)));
        }

        [Test]
        public void Ward_Absorbs_ArchwardenWardsNeighbours_MenderHeals()
        {
            var b = Setup(new[] { UnitStats.Archwarden, UnitStats.Warrior, UnitStats.Rogue }, new[] { P(3, 0), P(3, 2), P(4, 2) },
                new[] { Far });
            b.PlayerUseAction(Player(b, 0), ActionKind.Ward, P(3, 2));
            Assert.AreEqual(Battle.WardShield, Player(b, 1).Shield);
            Assert.AreEqual(Battle.WardShield, Player(b, 2).Shield, "wide ward");

            var m = Setup(new[] { UnitStats.Mender, UnitStats.Warrior }, new[] { P(3, 0), P(3, 2) }, new[] { Far });
            Player(m, 1).Hp = 5;
            m.PlayerUseAction(Player(m, 0), ActionKind.Mend, P(3, 2));
            Assert.AreEqual(5 + Battle.MendHeal, Player(m, 1).Hp);
        }

        [Test]
        public void WardShield_SoaksDamageFirst()
        {
            var b = Setup(new[] { UnitStats.WardMage, UnitStats.Rogue }, new[] { P(1, 0), P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 2), E(UnitStats.Wolf, 4, 1) });
            var rogue = Player(b, 1);
            b.PlayerUseAction(Player(b, 0), ActionKind.Ward, P(3, 1));
            b.EndPlayerTurn();
            Assert.AreEqual(rogue.MaxHp - (2 + 3 - Battle.WardShield), rogue.Hp, "bites of 2 and 3 (bite chain), minus the shield");
        }

        // ---------- Sapper tree ----------

        [Test]
        public void Demolitionist_BiggerMines()
        {
            var b = Setup(new[] { UnitStats.Demolitionist }, new[] { P(3, 1) }, new[] { Far });
            b.PlayerPlaceMine(Player(b), P(3, 2));
            Assert.AreEqual(Battle.BigMineDamage, b.MineAt(P(3, 2)).Damage);
        }

        [Test]
        public void Safecracker_BlastsSpareYourSide()
        {
            var b = Setup(new[] { UnitStats.Safecracker }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 4) });
            var safe = Player(b);
            b.PlayerPlaceMine(safe, P(3, 2));
            var events = b.EndPlayerTurn();
            var blast = events.OfType<ExplosionEvent>().First();
            Assert.IsFalse(blast.Hits.Any(h => h.Target == safe));
        }

        [Test]
        public void Bombardier_ThrowsDelayedBomb()
        {
            var b = Setup(new[] { UnitStats.Bombardier }, new[] { P(3, 0) }, new[] { E(UnitStats.Ogre, 3, 4) });
            b.PlayerUseAction(Player(b), ActionKind.ThrowBomb, P(3, 3));
            Assert.IsNotNull(b.BombAt(P(3, 3)));
            var events = b.EndPlayerTurn();
            Assert.IsTrue(events.OfType<ExplosionEvent>().Any(e => e.Cause == "bomb" && e.Owner == Player(b)));
        }

        [Test]
        public void Engineer_Barricades_CaptainDetonates()
        {
            var b = Setup(new[] { UnitStats.SapperCaptain }, new[] { P(3, 1) }, new[] { E(UnitStats.Wolf, 3, 3), Far });
            var captain = Player(b);
            b.PlayerUseAction(captain, ActionKind.Barricade, P(3, 2));
            Assert.IsTrue(b.Board.IsObstacle(P(3, 2)));
            captain.Ap = 4;
            var events = b.PlayerUseAction(captain, ActionKind.Detonate, captain.Pos);
            Assert.IsFalse(b.Board.IsObstacle(P(3, 2)));
            Assert.AreEqual(UnitStats.Wolf.MaxHp - Battle.BarricadeBlastDamage, Enemy(b).Hp);
            Assert.IsTrue(events.OfType<ExplosionEvent>().Any(e => e.Cause == "barricade"));
        }

        [Test]
        public void TurretBuilder_TurretShoots_AndDoesNotCountForDefeat()
        {
            var b = Setup(new[] { UnitStats.TurretBuilder }, new[] { P(3, 1) }, new[] { E(UnitStats.Ogre, 3, 5) });
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
