using System;
using System.Collections.Generic;
using System.Linq;

namespace ArmyRoguelike.Core
{
    /// <summary>
    /// A kind of battle: an enemy faction (or boss) plus a layout template. The template has fixed
    /// rules (for example "a rock ridge across the middle with three gaps") and randomises the details
    /// within them, so the same encounter looks a bit different each time but always plays the same way.
    /// The player's back two rows are always clear, and every enemy can be reached on foot.
    /// </summary>
    public sealed class Encounter
    {
        public const int Size = 6;

        public readonly string Name;

        /// <summary>Name of the layout template, shown in the HUD.</summary>
        public readonly string Layout;

        /// <summary>The layout rule in one sentence, for the HUD and the gamepedia.</summary>
        public readonly string LayoutRule;

        public readonly bool IsBoss;

        /// <summary>For a regular faction: the unit the Necromancer boss raises from it.</summary>
        public readonly UnitStats RaisedRepresentative;

        private readonly Func<Random, IReadOnlyList<Encounter>, Plan> _generate;

        /// <summary>One generated board: rocks and enemy start tiles.</summary>
        public sealed class Plan
        {
            public readonly List<GridPos> Rocks = new List<GridPos>();
            public readonly List<(UnitStats stats, GridPos pos)> Enemies = new List<(UnitStats, GridPos)>();

            // Ritual fights only: the objective's tile, where the player deploys, turns to survive and later waves.
            public GridPos? Objective;
            public List<GridPos> DeployZone;
            public int RitualTurns;
            public readonly List<(int afterEnemyTurn, UnitStats stats)> Waves = new List<(int, UnitStats)>();

            // Break the Ritual only: totem tiles and the enemy turn the summoning completes on.
            public readonly List<GridPos> Totems = new List<GridPos>();
            public int SummonTurn;

            /// <summary>The tiles the player deploys on: the ritual zone, or the back two rows.</summary>
            public IEnumerable<GridPos> PlayerTiles()
            {
                if (DeployZone != null)
                    return DeployZone;
                return Enumerable.Range(0, Battle.PlayerZoneRows).SelectMany(y => Enumerable.Range(0, Size).Select(x => P(x, y)));
            }
        }

        /// <summary>A ritual fight: defend an objective in the middle instead of killing everyone.</summary>
        public bool IsRitual => this == MoonShrine;

        /// <summary>A scenario fight (Moon Shrine or Break the Ritual): its own map node, rewards and curse on a loss.</summary>
        public bool IsScenario => this == MoonShrine || this == BreakRitual;

        private Encounter(string name, string layout, string layoutRule, bool isBoss, UnitStats raised,
            Func<Random, IReadOnlyList<Encounter>, Plan> generate)
        {
            Name = name;
            Layout = layout;
            LayoutRule = layoutRule;
            IsBoss = isBoss;
            RaisedRepresentative = raised;
            _generate = generate;
        }

        /// <summary>For an elite: the faction it's the harder version of. Null otherwise.</summary>
        public Encounter Base { get; private set; }

        /// <summary>A harder rematch of a faction: more of the same enemies. Pays an item pick on top of gold.</summary>
        public bool IsElite => Base != null;

        private static Encounter Elite(Encounter faction, string layoutRule, Func<Random, IReadOnlyList<Encounter>, Plan> generate) =>
            new Encounter("Elite " + faction.Name, faction.Layout, layoutRule, false, null, generate) { Base = faction };

        /// <summary>
        /// Generates a board and creates the battle. <paramref name="fought"/> is the factions beaten earlier
        /// in the act (the Necromancer builds his army from them). Retries until the rules hold.
        /// </summary>
        public Battle CreateBattle(IEnumerable<ArmyMember> army, Random rng = null, IReadOnlyList<Encounter> fought = null)
        {
            rng = rng ?? new Random(0);
            fought = fought ?? new Encounter[0];
            Plan plan = null;
            for (var attempt = 0; attempt < 50; attempt++)
            {
                plan = _generate(rng, fought);
                if (IsValid(plan))
                    break;
            }

            var battle = new Battle(new Board(Size, Size, plan.Rocks), army, plan.Enemies) { Rng = new Random(rng.Next()) };
            if (plan.Objective.HasValue)
                battle.SetupRitual(UnitStats.MoonShrine, plan.Objective.Value, plan.DeployZone, plan.RitualTurns, plan.Waves);
            if (plan.Totems.Count > 0)
                battle.SetupSummoning(UnitStats.Totem, plan.Totems, plan.SummonTurn);
            return battle;
        }

        /// <summary>The enemy types this encounter can field (with nothing fought yet), for the gamepedia.</summary>
        public IEnumerable<string> EnemySummary(IReadOnlyList<Encounter> fought = null)
        {
            var plan = _generate(new Random(0), fought ?? new Encounter[0]);
            return plan.Enemies.GroupBy(e => e.stats.Name).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key);
        }

        /// <summary>
        /// Rocks off the player's tiles, the objective and enemy tiles, no two rocks touching (not even
        /// diagonally), enemies on distinct tiles, and all reachable on foot from where the player deploys.
        /// </summary>
        private static bool IsValid(Plan plan)
        {
            var rocks = new HashSet<GridPos>(plan.Rocks);
            var playerTiles = plan.PlayerTiles().ToList();
            if (rocks.Any(r => playerTiles.Contains(r) || !In(r) || r == plan.Objective))
                return false;
            if (rocks.Any(a => rocks.Any(b => a != b && Touching(a, b))))
                return false;
            var occupied = new HashSet<GridPos>();
            if (plan.Objective.HasValue)
                occupied.Add(plan.Objective.Value);
            foreach (var t in plan.Totems)
                if (!In(t) || rocks.Contains(t) || playerTiles.Contains(t) || !occupied.Add(t))
                    return false;
            foreach (var (stats, pos) in plan.Enemies)
            foreach (var c in Battle.Footprint(pos, stats.Size))
                if (!In(c) || rocks.Contains(c) || playerTiles.Contains(c) || !occupied.Add(c))
                    return false;

            // Flood fill from the player's tiles through anything that isn't a rock.
            var seen = new HashSet<GridPos>();
            var queue = new Queue<GridPos>();
            foreach (var p in playerTiles)
            {
                seen.Add(p);
                queue.Enqueue(p);
            }

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var n in p.Neighbours())
                    if (In(n) && !rocks.Contains(n) && seen.Add(n))
                        queue.Enqueue(n);
            }

            return occupied.All(seen.Contains);
        }

        private static bool In(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Size && p.Y < Size;

        private static GridPos P(int x, int y) => new GridPos(x, y);

        /// <summary>Two tiles side by side or corner to corner.</summary>
        private static bool Touching(GridPos a, GridPos b) => Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;

        /// <summary>
        /// Adds a rock in one of the two outer columns on each side (x 0 or 1) and its mirror image across the
        /// board's vertical middle, so the two never touch.
        /// </summary>
        private static void MirroredPair(Plan plan, Random rng, int y)
        {
            var x = rng.Next(0, 2);
            plan.Rocks.Add(P(x, y));
            plan.Rocks.Add(P(Size - 1 - x, y));
        }

        /// <summary>Every other tile of a row, starting at column 0 or 1: rocks with a gap between each.</summary>
        private static List<int> AlternateColumns(Random rng)
        {
            var start = rng.Next(0, 2);
            return Enumerable.Range(0, Size).Where(x => x % 2 == start).ToList();
        }

        /// <summary>Random free tiles in the given rows, avoiding rocks and units already placed.</summary>
        private static List<GridPos> FreeTiles(Plan plan, Random rng, int minY, int maxY)
        {
            var taken = new HashSet<GridPos>(plan.Rocks.Concat(plan.Totems).Concat(plan.Enemies.SelectMany(e => Battle.Footprint(e.pos, e.stats.Size))));
            var tiles = new List<GridPos>();
            for (var y = minY; y <= maxY; y++)
            for (var x = 0; x < Size; x++)
                if (!taken.Contains(P(x, y)))
                    tiles.Add(P(x, y));
            return tiles.OrderBy(_ => rng.Next()).ToList();
        }

        private static void PlaceRandom(Plan plan, Random rng, UnitStats stats, int count, int minY, int maxY)
        {
            foreach (var tile in FreeTiles(plan, rng, minY, maxY).Take(count))
                plan.Enemies.Add((stats, tile));
        }

        // ---------- Regular factions ----------

        public static readonly Encounter Wolves = new Encounter("Wolf Pack", "Open Field",
            "Open ground with a mirrored pair of rocks; the wolves start bunched around their leader.", false, UnitStats.Wolf,
            (rng, _) =>
            {
                var plan = new Plan();
                MirroredPair(plan, rng, 2 + rng.Next(2));
                var leader = P(1 + rng.Next(4), 5);
                plan.Enemies.Add((UnitStats.PackLeader, leader));
                var near = FreeTiles(plan, rng, 4, 5).Where(t => t.StepsTo(leader) <= 2).Take(3);
                foreach (var t in near)
                    plan.Enemies.Add((UnitStats.Wolf, t));
                return plan;
            });

        public static readonly Encounter Goblins = new Encounter("Goblin Bombers", "Ridge",
            "A broken ridge across the middle: three rocks with a gap between each; bombers lob over it from the back row.", false, UnitStats.GoblinBomber,
            (rng, _) =>
            {
                var plan = new Plan();
                foreach (var x in AlternateColumns(rng))
                    plan.Rocks.Add(P(x, 3));
                PlaceRandom(plan, rng, UnitStats.GoblinBomber, 3, 5, 5);
                PlaceRandom(plan, rng, UnitStats.Goblin, 2, 4, 4);
                return plan;
            });

        public static readonly Encounter Bandits = new Encounter("Bandit Camp", "Camp",
            "Two rocks of cover in the middle with a crossbowman behind each; cutpurses start on the flanks.", false, UnitStats.Crossbowman,
            (rng, _) =>
            {
                var plan = new Plan();
                var columns = AlternateColumns(rng).OrderBy(_ => rng.Next()).Take(2).ToList();
                foreach (var x in columns)
                {
                    var rock = P(x, 3 + rng.Next(2));
                    plan.Rocks.Add(rock);
                    plan.Enemies.Add((UnitStats.Crossbowman, P(rock.X, rock.Y + 1)));
                }

                var flanks = FreeTiles(plan, rng, 4, 5).Where(t => t.X <= 1 || t.X >= Size - 2).ToList();
                var left = flanks.FirstOrDefault(t => t.X <= 1);
                var right = flanks.FirstOrDefault(t => t.X >= Size - 2);
                plan.Enemies.Add((UnitStats.Cutpurse, left));
                plan.Enemies.Add((UnitStats.Cutpurse, right));
                var centre = FreeTiles(plan, rng, 4, 5).Where(t => t.X >= 1 && t.X <= 4).Take(1);
                foreach (var t in centre)
                    plan.Enemies.Add((UnitStats.Brute, t));
                return plan;
            });

        public static readonly Encounter Elementals = new Encounter("Elementals", "Crystal Cave",
            "A mirrored pair of crystals; three elementals spread across the back.", false, UnitStats.Shard,
            (rng, _) =>
            {
                var plan = new Plan();
                MirroredPair(plan, rng, 2 + rng.Next(2));
                var spots = FreeTiles(plan, rng, 4, 5);
                var picked = new List<GridPos>();
                foreach (var t in spots)
                {
                    if (picked.Count == 3)
                        break;
                    if (picked.All(p => p.StepsTo(t) >= 3))
                        picked.Add(t);
                }

                foreach (var t in picked)
                    plan.Enemies.Add((UnitStats.Elemental, t));
                return plan;
            });

        // ---------- Ritual ----------

        /// <summary>Moon Shrine: enemy turns to survive, and when waves arrive (after these enemy turns, 2 each).</summary>
        public const int RitualTurns = 5;

        public static readonly IReadOnlyList<int> RitualWaveTurns = new[] { 1, 3 };
        public const int RitualWaveSize = 2;
        public const int RitualDeployRange = 2;

        public static readonly GridPos ShrineTile = new GridPos(2, 2);

        public static readonly Encounter MoonShrine = new Encounter("Moon Shrine", "Moon Shrine",
            "Defend the shrine in the middle for 5 enemy turns. Elementals come from every edge, and more arrive on turns 2 and 4.",
            false, null,
            (rng, _) =>
            {
                var plan = new Plan
                {
                    Objective = ShrineTile,
                    DeployZone = Enumerable.Range(0, Size * Size).Select(i => P(i % Size, i / Size))
                        .Where(p => p != ShrineTile && p.StepsTo(ShrineTile) <= RitualDeployRange).ToList(),
                    RitualTurns = RitualTurns,
                };
                MirroredPair(plan, rng, 4 + rng.Next(2));
                bool Edge(GridPos p) => p.X == 0 || p.Y == 0 || p.X == Size - 1 || p.Y == Size - 1;
                var picked = new List<GridPos>();
                foreach (var t in FreeTiles(plan, rng, 0, Size - 1).Where(t => Edge(t) && t.StepsTo(ShrineTile) >= Battle.RitualSpawnDistance))
                {
                    if (picked.Count == 3)
                        break;
                    if (picked.All(p => p.StepsTo(t) >= 3))
                        picked.Add(t);
                }

                foreach (var t in picked)
                    plan.Enemies.Add((UnitStats.Elemental, t));
                foreach (var turn in RitualWaveTurns)
                    for (var i = 0; i < RitualWaveSize; i++)
                        plan.Waves.Add((turn, UnitStats.Elemental));
                return plan;
            });

        // ---------- Break the Ritual ----------

        /// <summary>Break the Ritual: how many totems, and the enemy turn the summoning completes on.</summary>
        public const int TotemCount = 3;

        public const int SummonTurn = 4;

        public static readonly Encounter BreakRitual = new Encounter("Break the Ritual", "Totem Grounds",
            "Three goblin totems stand on the far half. Smash them all before the summoning completes at the start of enemy turn 4.",
            false, null,
            (rng, _) =>
            {
                var plan = new Plan { SummonTurn = SummonTurn };
                MirroredPair(plan, rng, 2);
                // Spread out where the rows allow it; any that don't fit 3 apart go 2 apart.
                foreach (var spacing in new[] { 3, 2 })
                foreach (var t in FreeTiles(plan, rng, 4, 5))
                    if (plan.Totems.Count < TotemCount && plan.Totems.All(p => p.StepsTo(t) >= spacing))
                        plan.Totems.Add(t);

                PlaceRandom(plan, rng, UnitStats.GoblinBomber, 3, 5, 5);
                PlaceRandom(plan, rng, UnitStats.Goblin, 2, 3, 4);
                return plan;
            });

        // ---------- Bosses ----------

        public static readonly Encounter Necromancer = new Encounter("Necromancer", "Graveyard",
            "Two tombstones in the middle. His army is raised from the factions you beat this act.", true, null,
            (rng, fought) =>
            {
                var plan = new Plan();
                // Two tombstones that don't touch.
                foreach (var t in FreeTiles(plan, rng, 2, 3))
                    if (plan.Rocks.Count < 2 && plan.Rocks.All(r => !Touching(r, t)))
                        plan.Rocks.Add(t);
                plan.Enemies.Add((UnitStats.Necromancer, FreeTiles(plan, rng, 5, 5).First(t => t.X >= 1 && t.X <= 4)));
                var minions = fought.Where(f => f.RaisedRepresentative != null)
                    .SelectMany(f => Enumerable.Repeat(UnitStats.Raised(f.RaisedRepresentative), 1)).ToList();
                while (minions.Count < 4)
                    minions.Add(UnitStats.Skeleton);
                var tiles = FreeTiles(plan, rng, 4, 5);
                for (var i = 0; i < minions.Count && i < tiles.Count; i++)
                    plan.Enemies.Add((minions[i], tiles[i]));
                return plan;
            });

        public static readonly Encounter Giant = new Encounter("Giant", "Quarry",
            "Open quarry with a pair of boulders; the Giant reshapes it with slams and thrown rocks.", true, null,
            (rng, _) =>
            {
                var plan = new Plan();
                MirroredPair(plan, rng, 2 + rng.Next(2));
                plan.Enemies.Add((UnitStats.Ogre, P(1 + rng.Next(3), 4)));
                return plan;
            });

        /// <summary>The factions Act 1 battles are drawn from; each appears at most once per run.</summary>
        public static readonly IReadOnlyList<Encounter> Regular = new[] { Wolves, Goblins, Bandits, Elementals };

        // ---------- Elites: harder rematches of the factions (more of the same enemies) ----------

        public static readonly Encounter EliteWolves = Elite(Wolves,
            "Open ground with a mirrored pair of rocks; two packs, each bunched around its own leader.",
            (rng, _) =>
            {
                var plan = new Plan();
                MirroredPair(plan, rng, 2 + rng.Next(2));
                var leaders = new[] { P(rng.Next(2), 5), P(4 + rng.Next(2), 5) };
                foreach (var leader in leaders)
                {
                    plan.Enemies.Add((UnitStats.PackLeader, leader));
                    foreach (var t in FreeTiles(plan, rng, 4, 5).Where(t => t.StepsTo(leader) <= 2).Take(2))
                        plan.Enemies.Add((UnitStats.Wolf, t));
                }

                return plan;
            });

        public static readonly Encounter EliteGoblins = Elite(Goblins,
            "A broken ridge across the middle (three rocks with gaps); four bombers lob over it from the back row.",
            (rng, _) =>
            {
                var plan = new Plan();
                foreach (var x in AlternateColumns(rng))
                    plan.Rocks.Add(P(x, 3));
                PlaceRandom(plan, rng, UnitStats.GoblinBomber, 4, 5, 5);
                PlaceRandom(plan, rng, UnitStats.Goblin, 3, 4, 4);
                return plan;
            });

        public static readonly Encounter EliteBandits = Elite(Bandits,
            "Three separate rocks of cover in the middle with a crossbowman behind each, cutpurses on the flanks, two brutes in the centre.",
            (rng, _) =>
            {
                var plan = new Plan();
                var columns = AlternateColumns(rng);
                foreach (var x in columns)
                {
                    var rock = P(x, 3 + rng.Next(2));
                    plan.Rocks.Add(rock);
                    plan.Enemies.Add((UnitStats.Crossbowman, P(rock.X, rock.Y + 1)));
                }

                // Cutpurses on the flanks; a crossbowman may already stand there, so fall back to the back rows.
                foreach (var onFlank in new Func<GridPos, bool>[] { t => t.X <= 1, t => t.X >= Size - 2 })
                {
                    var free = FreeTiles(plan, rng, 4, 5);
                    plan.Enemies.Add((UnitStats.Cutpurse, free.Where(onFlank).DefaultIfEmpty(free[0]).First()));
                }
                foreach (var t in FreeTiles(plan, rng, 4, 5).Where(t => t.X >= 1 && t.X <= 4).Take(2))
                    plan.Enemies.Add((UnitStats.Brute, t));
                return plan;
            });

        public static readonly Encounter EliteElementals = Elite(Elementals,
            "A mirrored pair of crystals; four elementals spread across the back.",
            (rng, _) =>
            {
                var plan = new Plan();
                MirroredPair(plan, rng, 2 + rng.Next(2));
                // Spread out where the crystals allow it; any fourth that doesn't fit 3 apart goes 2 apart.
                var picked = new List<GridPos>();
                foreach (var spacing in new[] { 3, 2 })
                foreach (var t in FreeTiles(plan, rng, 4, 5))
                    if (picked.Count < 4 && picked.All(p => p.StepsTo(t) >= spacing))
                        picked.Add(t);

                foreach (var t in picked)
                    plan.Enemies.Add((UnitStats.Elemental, t));
                return plan;
            });

        /// <summary>The elite version of each faction, in the same order as <see cref="Regular"/>.</summary>
        public static readonly IReadOnlyList<Encounter> Elites = new[] { EliteWolves, EliteGoblins, EliteBandits, EliteElementals };

        /// <summary>The elite version of a faction.</summary>
        public static Encounter EliteOf(Encounter faction) => Elites.First(e => e.Base == faction);

        /// <summary>Act 1 ends with one of these, picked per run.</summary>
        public static readonly IReadOnlyList<Encounter> Bosses = new[] { Necromancer, Giant };
    }
}
