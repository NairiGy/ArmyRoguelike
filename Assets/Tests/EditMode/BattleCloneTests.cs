using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ArmyRoguelike.Core;
using NUnit.Framework;

namespace ArmyRoguelike.Tests
{
    /// <summary>
    /// <see cref="Battle.Clone"/>: a copy plays out exactly like the original, and neither touches the other. The state is
    /// compared by reflection over every field, so a field added to Battle or Unit but not copied fails here.
    /// </summary>
    public class BattleCloneTests
    {
        private const BindingFlags AllFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Moves played on the copy and then on the original between two comparisons.
        private const int StepsPerCopy = 5;
        private const int MaxSteps = 150;

        private static GridPos P(int x, int y) => new GridPos(x, y);

        /// <summary>All five lines, with the techniques that leave state on the board or on units.</summary>
        private static List<ArmyMember> FullArmy() => new List<ArmyMember>
        {
            new ArmyMember(UnitStats.Warrior, null, Technique.Sweep, Technique.Guard, Technique.ShieldUp, Technique.Battlecry, Technique.Taunt, Technique.ThrowShield, Technique.ZocStrike),
            new ArmyMember(UnitStats.Archer, null, Technique.OpeningShot, Technique.SnareTrap, Technique.CoveringFire, Technique.FireArrow, Technique.Kick, Technique.Vantage, Technique.MarkTarget, Technique.DeathMark),
            new ArmyMember(UnitStats.Rogue, null, Technique.Poison, Technique.Riposte, Technique.Dodge, Technique.SmokeBomb, Technique.Shadowstep, Technique.ThrowingKnife, Technique.Rupture, Technique.IntoShadows, Technique.Isolate),
            new ArmyMember(UnitStats.Apprentice, null, Technique.Fireball, Technique.Burning, Technique.Chill, Technique.Ward, Technique.IceWall,
                Technique.Firewall, Technique.SearingBolt, Technique.Blizzard, Technique.Transpose, Technique.Twincast),
            new ArmyMember(UnitStats.Sapper, null, Technique.Barricade, Technique.Turret, Technique.Detonate, Technique.ThrowBomb,
                Technique.CaveIn, Technique.Hook, Technique.Demolish, Technique.QuickFuse, Technique.Incendiary, Technique.SpikedBarricades),
        };

        private static Battle Placed(Encounter encounter, Battlefield? field, int seed)
        {
            var b = encounter.CreateBattle(FullArmy(), new Random(seed), Encounter.Regular);
            b.SetField(field);
            if (seed % 2 == 0)
                b.AddOmen(Omens.All[seed / 2 % Omens.All.Count], seed % 4 == 0 ? Team.Player : Team.Enemy);
            b.AddBanner(Banners.All[seed % Banners.All.Count]);
            var rng = new Random(seed);
            foreach (var u in b.Units.Where(u => u.Team == Team.Player && !u.IsObjective))
            {
                var tiles = b.Board.AllTiles().Where(t => b.CanPlace(u, t) && b.UnitAt(t) == null).ToList();
                Assert.IsTrue(b.Place(u, tiles[rng.Next(tiles.Count)]));
            }

            return b;
        }

        /// <summary>
        /// One move by a random player. Its options are built from the battle's own state, in a fixed order, so the same
        /// random numbers give the same move on a battle and its copy. Returns what happened.
        /// </summary>
        private static IEnumerable<BattleEvent> PlayOneStep(Battle b, Random rng)
        {
            var units = b.Living(Team.Player).Where(b.CanAct).ToList();
            if (units.Count == 0)
                return b.EndPlayerTurn();
            var unit = units[rng.Next(units.Count)];

            var targets = b.Living(Team.Enemy).Where(e => b.CanAttack(unit, e)).ToList();
            if (targets.Count > 0 && rng.Next(10) < 6)
                return b.PlayerAttack(unit, targets[rng.Next(targets.Count)]);

            var actions = unit.Actions.SelectMany(a => b.ActionTargets(unit, a).Select(t => (a, t))).ToList();
            if (actions.Count > 0 && rng.Next(10) < 5)
            {
                var (action, tile) = actions[rng.Next(actions.Count)];
                return b.PlayerUseAction(unit, action, tile);
            }

            var barricades = b.Barricades.Where(t => b.CanAttackBarricade(unit, t)).ToList();
            if (barricades.Count > 0 && rng.Next(10) < 2)
                return b.PlayerAttackBarricade(unit, barricades[rng.Next(barricades.Count)]);

            var moves = b.GetReachable(unit).Keys.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
            if (moves.Count > 0 && rng.Next(10) < 6)
                return b.PlayerMove(unit, moves[rng.Next(moves.Count)]);

            b.EndUnitTurn(unit);
            return new BattleEvent[0];
        }

        private static List<string> Play(Battle b, int seed, int steps)
        {
            var rng = new Random(seed);
            var log = new List<string>();
            for (var i = 0; i < steps && !b.IsOver; i++)
            {
                var events = PlayOneStep(b, rng);
                Assert.IsNotNull(events, "the bot only plays legal moves");
                log.Add(string.Join(" ", events.Select(Text)));
                log.Add(Text(b));
            }

            return log;
        }

        /// <summary>Plays a whole battle: every few moves it copies the battle, plays on with the copy, then the same moves on the original.</summary>
        private static void PlayAndCompare(Battle b, int seed, string name, Action<Battle> seen = null)
        {
            for (var step = 0; step < MaxSteps && !b.IsOver; step += StepsPerCopy)
            {
                var before = Text(b);
                var copy = b.Clone();
                seen?.Invoke(copy);
                Assert.AreEqual(before, Text(copy), $"{name}: the copy matches at step {step}");
                AssertNothingShared(b, copy, name);

                var copyLog = Play(copy, seed + step, StepsPerCopy);
                Assert.AreEqual(before, Text(b), $"{name}: playing the copy left the original alone at step {step}");

                var log = Play(b, seed + step, StepsPerCopy);
                CollectionAssert.AreEqual(copyLog, log, $"{name}: the copy and the original parted ways after step {step}");
            }
        }

        [Test]
        public void Clone_PlaysOutLikeTheOriginal_InEveryEncounterAndBattlefield()
        {
            var encounters = Encounter.Regular.Concat(Encounter.Elites).Concat(Encounter.Bosses)
                .Concat(new[] { Encounter.MoonShrine, Encounter.BreakRitual }).ToList();
            var fields = Battlefields.All.Select(f => (Battlefield?)f).Concat(new Battlefield?[] { null }).ToList();

            // What the copies were made in the middle of, so the test can't quietly stop covering it.
            var mines = 0;
            var bombs = 0;
            var burning = 0;
            var ice = 0;
            var barricades = 0;
            var caltrops = 0;
            var overwatch = 0;
            var storm = 0;
            var sun = 0;
            var shroud = 0;
            var favored = 0;
            var poisoned = 0;
            var cooldowns = 0;
            var turrets = 0;
            var piles = 0;
            var waves = 0;
            var snares = 0;
            var hidden = 0;
            Action<Battle> seen = c =>
            {
                mines += c.Mines.Count;
                bombs += c.Bombs.Count;
                burning += c.BurningTiles.Count;
                ice += c.Ice.Count;
                barricades += c.Barricades.Count();
                caltrops += c.Caltrops.Count;
                overwatch += c.OverwatchTiles.Count();
                storm += c.StormMarks.Count;
                sun += c.SunMarks.Count;
                shroud += c.Shroud.Count;
                favored += c.Units.Count(u => u.Favored);
                poisoned += c.Units.Count(u => u.PoisonTurns > 0);
                cooldowns += c.Units.Count(u => u.Actions.Any(a => u.CooldownLeft(a) > 0));
                turrets += c.Units.Count(u => u.IsSummon);
                piles += c.Units.Count(u => u.IsRemains);
                waves += c.PendingWaves.Count;
                snares += c.Snares.Count;
                hidden += c.Units.Count(u => u.IsHidden);
            };

            foreach (var encounter in encounters)
            foreach (var field in fields)
            for (var seed = 0; seed < 3; seed++)
                PlayAndCompare(StartedBattle(encounter, field, seed), seed, $"{encounter.Name} on {field}, seed {seed}", seen);

            foreach (var (name, count) in new[]
                     {
                         ("mines", mines), ("bombs", bombs), ("burning", burning), ("ice", ice), ("barricades", barricades), ("caltrops", caltrops),
                         ("overwatch", overwatch), ("storm", storm), ("sun", sun), ("shroud", shroud), ("favored", favored), ("poisoned", poisoned),
                         ("cooldowns", cooldowns), ("turrets", turrets), ("piles", piles), ("waves", waves), ("snares", snares), ("hidden", hidden),
                     })
                Assert.Greater(count, 0, $"no copy was made with {name} in play");
        }

        private static Battle StartedBattle(Encounter encounter, Battlefield? field, int seed)
        {
            var b = Placed(encounter, field, seed);
            Assert.IsTrue(b.StartBattle());
            return b;
        }

        [Test]
        public void Clone_DuringPlacement_ThenBothStartAlike()
        {
            var b = Placed(Encounter.Wolves, Battlefield.Storm, 3);
            var copy = b.Clone();
            Assert.AreEqual(Text(b), Text(copy));
            Assert.IsTrue(copy.StartBattle());
            Assert.AreEqual(Phase.Placement, b.Phase, "starting the copy doesn't start the original");
            Assert.IsTrue(b.StartBattle());
            Assert.AreEqual(Text(b), Text(copy));
            CollectionAssert.AreEqual(b.OpeningEvents.Select(Text), copy.OpeningEvents.Select(Text));
        }

        /// <summary>Catches plain state that play can't leave set at a moment a copy is made (Wolf Bite's chain only counts during the enemy turn).</summary>
        [Test]
        public void Clone_CopiesEveryPlainField()
        {
            var b = StartedBattle(Encounter.Wolves, null, 1);
            var value = 2;
            foreach (var o in b.Units.Cast<object>().Concat(new object[] { b }))
            foreach (var field in FieldsOf(o.GetType()).Where(f => !f.IsInitOnly))
            {
                if (field.FieldType == typeof(int))
                    field.SetValue(o, value++);
                else if (field.FieldType == typeof(bool))
                    field.SetValue(o, !(bool)field.GetValue(o));
            }

            Assert.AreEqual(Text(b), Text(b.Clone()));
        }

        [Test]
        public void Clone_SharesOnlyWhatABattleNeverChanges()
        {
            var b = StartedBattle(Encounter.Goblins, null, 1);
            var copy = b.Clone();
            var warrior = copy.Units.First(u => u.Stats == UnitStats.Warrior);
            var original = b.Units.First(u => u.Stats == UnitStats.Warrior);
            Assert.AreNotSame(original, warrior);
            Assert.AreSame(original.Member, warrior.Member, "army members are shared");
            Assert.AreSame(original.Stats, warrior.Stats);

            warrior.Hp = 1;
            var free = b.Board.AllTiles().First(t => !b.Board.IsObstacle(t));
            copy.Board.AddObstacle(free);
            Assert.AreEqual(original.MaxHp, original.Hp);
            Assert.IsFalse(b.Board.IsObstacle(free));
        }

        [Test]
        public void SeededRng_CopyRollsTheSameNumbers_AndStaysInRange()
        {
            var rng = new SeededRng(7);
            for (var i = 0; i < 100; i++)
                rng.Next(6);
            var copy = rng.Clone();
            var seen = new HashSet<int>();
            for (var i = 0; i < 1000; i++)
            {
                var roll = rng.Next(6);
                Assert.AreEqual(roll, copy.Next(6));
                Assert.That(roll, Is.InRange(0, 5));
                seen.Add(roll);
            }

            Assert.AreEqual(6, seen.Count, "every result comes up");
            Assert.AreEqual(0, rng.Next(1));
            Assert.AreNotEqual(new SeededRng(1).Next(1000), new SeededRng(2).Next(1000), "seeds differ");
        }

        // ---------- Tick credit ----------

        [Test]
        public void TickEvents_NameWhoCausedThem()
        {
            var b = new Battle(new Board(8, 8), new[] { new ArmyMember(UnitStats.Apprentice, null, Technique.Fireball, Technique.Burning) },
                new[] { (UnitStats.Ogre, P(3, 4)) });
            var mage = b.Units.First(u => u.Team == Team.Player);
            var ogre = b.Units.First(u => u.Team == Team.Enemy);
            b.Place(mage, P(3, 1));
            b.StartBattle();
            b.PlayerUseAction(mage, ActionKind.Fireball, P(3, 3));
            ogre.Hp = ogre.MaxHp;
            var events = b.EndPlayerTurn();
            var burn = events.OfType<TickEvent>().First(t => t.Cause == "burn");
            Assert.AreSame(mage, burn.Source);
            Assert.AreSame(ogre, burn.Hit.Target);

            var poisoned = new Battle(new Board(8, 8), new[] { new ArmyMember(UnitStats.Rogue, null, Technique.Poison) },
                new[] { (UnitStats.Ogre, P(3, 2)) });
            var rogue = poisoned.Units.First(u => u.Team == Team.Player);
            poisoned.Place(rogue, P(3, 1));
            poisoned.StartBattle();
            poisoned.PlayerAttack(rogue, poisoned.Units.First(u => u.Team == Team.Enemy));
            var tick = poisoned.EndPlayerTurn().OfType<TickEvent>().First(t => t.Cause == "poison");
            Assert.AreSame(rogue, tick.Source);
        }

        // ---------- Comparing by reflection ----------

        /// <summary>The whole state of a battle, or the content of an event, as text. Units elsewhere than the battle's list are named by id.</summary>
        private static string Text(object o)
        {
            var text = new StringBuilder();
            Write(o, text, true);
            return text.ToString();
        }

        private static void Write(object o, StringBuilder text, bool top = false)
        {
            switch (o)
            {
                case null:
                    text.Append("null");
                    return;
                case string _:
                case Enum _:
                case GridPos _:
                case ValueType _ when o.GetType().IsPrimitive:
                    text.Append(o);
                    return;
                case Unit unit when !top:
                    text.Append('#').Append(unit.Id);
                    return;
                case UnitStats stats:
                    text.Append(stats.Name);
                    return;
                case ArmyMember member:
                    text.Append(member.Stats.Name).Append(" L").Append(member.Level);
                    return;
                case IDictionary dictionary:
                {
                    var entries = new List<string>();
                    foreach (DictionaryEntry e in dictionary)
                        entries.Add(Text(new[] { e.Key, e.Value }));
                    entries.Sort(StringComparer.Ordinal);
                    text.Append('{').Append(string.Join(",", entries)).Append('}');
                    return;
                }
                case IEnumerable sequence:
                {
                    var items = new List<string>();
                    foreach (var item in sequence)
                    {
                        var part = new StringBuilder();
                        Write(item, part);
                        items.Add(part.ToString());
                    }

                    if (IsSet(o))
                        items.Sort(StringComparer.Ordinal);
                    text.Append('[').Append(string.Join(",", items)).Append(']');
                    return;
                }
            }

            var type = o.GetType();
            text.Append(type.Name).Append('(');
            foreach (var field in FieldsOf(type))
            {
                text.Append(field.Name).Append('=');
                if (o is Battle && field.Name == "_units")
                    foreach (var unit in (IEnumerable<Unit>)field.GetValue(o))
                    {
                        Write(unit, text, true);
                        text.Append('\n');
                    }
                else
                    Write(field.GetValue(o), text);
                text.Append(';');
            }

            text.Append(')');
        }

        private static bool IsSet(object o) => o.GetType().IsGenericType && o.GetType().GetGenericTypeDefinition() == typeof(HashSet<>);

        /// <summary>No object that changes during a battle may be reachable from both the original and the copy.</summary>
        private static void AssertNothingShared(Battle a, Battle b, string name)
        {
            var reachable = new HashSet<object>(ReferenceComparer.Instance);
            Collect(a, reachable);
            var inCopy = new HashSet<object>(ReferenceComparer.Instance);
            Collect(b, inCopy);
            foreach (var o in inCopy)
                Assert.IsFalse(reachable.Contains(o), $"{name}: the copy shares a {o.GetType().Name} with the original");
        }

        private static void Collect(object o, HashSet<object> found)
        {
            // Unit types, army members and text never change during a battle.
            if (o == null || o is string || o is UnitStats || o is ArmyMember || o is Equipment || o is Technique || o is Pointer || o.GetType().IsPrimitive || o.GetType().IsEnum)
                return;
            if (!o.GetType().IsValueType && !found.Add(o))
                return;
            if (o is Array array && array.GetType().GetElementType().IsPrimitive)
                return;
            if (o is IEnumerable sequence)
            {
                foreach (var item in sequence)
                    Collect(item, found);
                return;
            }

            foreach (var field in FieldsOf(o.GetType()))
                Collect(field.GetValue(o), found);
        }

        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();

        /// <summary>A type's fields in a fixed order. Not OpeningEvents, which holds the original's first events only; the copy starts with none.</summary>
        private static FieldInfo[] FieldsOf(Type type)
        {
            if (!FieldCache.TryGetValue(type, out var fields))
                FieldCache[type] = fields = type.GetFields(AllFields).Where(f => f.Name != "<OpeningEvents>k__BackingField")
                    .OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
            return fields;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }
    }
}
