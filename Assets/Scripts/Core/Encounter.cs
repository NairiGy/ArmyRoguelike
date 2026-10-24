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
        public const int Size = 8;

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
        }

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

            return new Battle(new Board(Size, Size, plan.Rocks), army, plan.Enemies);
        }

        /// <summary>The enemy types this encounter can field (with nothing fought yet), for the gamepedia.</summary>
        public IEnumerable<string> EnemySummary(IReadOnlyList<Encounter> fought = null)
        {
            var plan = _generate(new Random(0), fought ?? new Encounter[0]);
            return plan.Enemies.GroupBy(e => e.stats.Name).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key);
        }

        /// <summary>Rocks off the player rows and enemy tiles, enemies on distinct tiles, and all reachable on foot.</summary>
        private static bool IsValid(Plan plan)
        {
            var rocks = new HashSet<GridPos>(plan.Rocks);
            if (rocks.Any(r => r.Y < Battle.PlayerZoneRows || !In(r)))
                return false;
            var occupied = new HashSet<GridPos>();
            foreach (var (stats, pos) in plan.Enemies)
            foreach (var c in Battle.Footprint(pos, stats.Size))
                if (!In(c) || rocks.Contains(c) || !occupied.Add(c))
                    return false;

            // Flood fill from the player's rows through anything that isn't a rock.
            var seen = new HashSet<GridPos>();
            var queue = new Queue<GridPos>();
            for (var x = 0; x < Size; x++)
            {
                seen.Add(new GridPos(x, 0));
                queue.Enqueue(new GridPos(x, 0));
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

        /// <summary>Adds a rock and its mirror image across the board's vertical middle.</summary>
        private static void MirroredPair(Plan plan, int x, int y)
        {
            plan.Rocks.Add(P(x, y));
            plan.Rocks.Add(P(Size - 1 - x, y));
        }

        /// <summary>Random free tiles in the given rows, avoiding rocks and units already placed.</summary>
        private static List<GridPos> FreeTiles(Plan plan, Random rng, int minY, int maxY)
        {
            var taken = new HashSet<GridPos>(plan.Rocks.Concat(plan.Enemies.SelectMany(e => Battle.Footprint(e.pos, e.stats.Size))));
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
            "Open ground with a few mirrored rocks; the wolves start bunched around their leader.", false, UnitStats.Wolf,
            (rng, _) =>
            {
                var plan = new Plan();
                for (var i = 0; i < 1 + rng.Next(2); i++)
                    MirroredPair(plan, rng.Next(0, 4), 3 + rng.Next(2));
                var leader = P(2 + rng.Next(4), 7);
                plan.Enemies.Add((UnitStats.PackLeader, leader));
                var near = FreeTiles(plan, rng, 5, 7).Where(t => t.StepsTo(leader) <= 2).Take(3);
                foreach (var t in near)
                    plan.Enemies.Add((UnitStats.Wolf, t));
                return plan;
            });

        public static readonly Encounter Goblins = new Encounter("Goblin Bombers", "Ridge",
            "A rock ridge across the middle with three gaps; bombers lob over it from the back row.", false, UnitStats.GoblinBomber,
            (rng, _) =>
            {
                var plan = new Plan();
                var gaps = Enumerable.Range(0, Size).OrderBy(_ => rng.Next()).Take(3).ToList();
                for (var x = 0; x < Size; x++)
                    if (!gaps.Contains(x))
                        plan.Rocks.Add(P(x, 4));
                PlaceRandom(plan, rng, UnitStats.GoblinBomber, 3, 7, 7);
                PlaceRandom(plan, rng, UnitStats.Goblin, 2, 5, 5);
                return plan;
            });

        public static readonly Encounter Bandits = new Encounter("Bandit Camp", "Camp",
            "Three rocks of cover in the middle; crossbowmen hide behind them, cutpurses start on the flanks.", false, UnitStats.Crossbowman,
            (rng, _) =>
            {
                var plan = new Plan();
                var cover = Enumerable.Range(0, Size).OrderBy(_ => rng.Next()).Take(3).Select(x => P(x, 4 + rng.Next(2))).ToList();
                plan.Rocks.AddRange(cover);
                foreach (var rock in cover.Take(2))
                {
                    var behind = P(rock.X, rock.Y + 1);
                    if (!plan.Rocks.Contains(behind))
                        plan.Enemies.Add((UnitStats.Crossbowman, behind));
                }

                var flanks = FreeTiles(plan, rng, 5, 6).Where(t => t.X <= 1 || t.X >= Size - 2).ToList();
                var left = flanks.FirstOrDefault(t => t.X <= 1);
                var right = flanks.FirstOrDefault(t => t.X >= Size - 2);
                plan.Enemies.Add((UnitStats.Cutpurse, left));
                plan.Enemies.Add((UnitStats.Cutpurse, right));
                var centre = FreeTiles(plan, rng, 5, 7).Where(t => t.X >= 2 && t.X <= 5).Take(1);
                foreach (var t in centre)
                    plan.Enemies.Add((UnitStats.Brute, t));
                return plan;
            });

        public static readonly Encounter Elementals = new Encounter("Elementals", "Crystal Cave",
            "Two mirrored pairs of crystals; three elementals spread across the back.", false, UnitStats.Shard,
            (rng, _) =>
            {
                var plan = new Plan();
                MirroredPair(plan, rng.Next(0, 4), 2 + rng.Next(2));
                MirroredPair(plan, rng.Next(0, 4), 4 + rng.Next(2));
                var spots = FreeTiles(plan, rng, 5, 7);
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

        // ---------- Bosses ----------

        public static readonly Encounter Necromancer = new Encounter("Necromancer", "Graveyard",
            "Tombstones scattered across the middle. His army is raised from the factions you beat this act.", true, null,
            (rng, fought) =>
            {
                var plan = new Plan();
                var stones = 4 + rng.Next(3);
                foreach (var t in FreeTiles(plan, rng, 3, 6).Take(stones))
                    plan.Rocks.Add(t);
                plan.Enemies.Add((UnitStats.Necromancer, FreeTiles(plan, rng, 7, 7).First(t => t.X >= 2 && t.X <= 5)));
                var minions = fought.Where(f => f.RaisedRepresentative != null)
                    .SelectMany(f => Enumerable.Repeat(UnitStats.Raised(f.RaisedRepresentative), 2)).ToList();
                while (minions.Count < 4)
                    minions.Add(UnitStats.Skeleton);
                var tiles = FreeTiles(plan, rng, 5, 7);
                for (var i = 0; i < minions.Count && i < tiles.Count; i++)
                    plan.Enemies.Add((minions[i], tiles[i]));
                return plan;
            });

        public static readonly Encounter Giant = new Encounter("Giant", "Quarry",
            "Open quarry with a pair of boulders; the Giant reshapes it with slams and thrown rocks.", true, null,
            (rng, _) =>
            {
                var plan = new Plan();
                MirroredPair(plan, rng.Next(0, 3), 2 + rng.Next(2));
                plan.Enemies.Add((UnitStats.Ogre, P(1 + rng.Next(5), 5)));
                return plan;
            });

        /// <summary>The factions Act 1 battles are drawn from; each appears at most once per run.</summary>
        public static readonly IReadOnlyList<Encounter> Regular = new[] { Wolves, Goblins, Bandits, Elementals };

        /// <summary>Act 1 ends with one of these, picked per run.</summary>
        public static readonly IReadOnlyList<Encounter> Bosses = new[] { Necromancer, Giant };
    }
}
