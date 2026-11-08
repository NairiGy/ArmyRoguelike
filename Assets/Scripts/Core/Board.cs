using System;
using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>The grid and its obstacles. Units are tracked by <see cref="Battle"/>.</summary>
    public sealed class Board
    {
        public readonly int Width;
        public readonly int Height;

        private readonly bool[,] _obstacles;

        // Holes in the roof (Crumbling Roof): nothing stands on them, but shots fly over them.
        private readonly bool[,] _holes;
        // Low obstacles (Rubble): block walking and standing like a rock, but not line of sight.
        private readonly bool[,] _low;

        public Board(int width, int height, IEnumerable<GridPos> obstacles = null)
        {
            Width = width;
            Height = height;
            _obstacles = new bool[width, height];
            _holes = new bool[width, height];
            _low = new bool[width, height];
            if (obstacles == null)
                return;
            foreach (var p in obstacles)
                _obstacles[p.X, p.Y] = true;
        }

        private Board(Board other)
        {
            Width = other.Width;
            Height = other.Height;
            _obstacles = (bool[,])other._obstacles.Clone();
            _holes = (bool[,])other._holes.Clone();
            _low = (bool[,])other._low.Clone();
        }

        /// <summary>A separate board with the same rocks.</summary>
        public Board Clone() => new Board(this);

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        /// <summary>Nothing can stand on or walk through it: a rock, rubble, or a hole in the roof.</summary>
        public bool IsObstacle(GridPos p) => InBounds(p) && (_obstacles[p.X, p.Y] || _holes[p.X, p.Y]);

        /// <summary>A rock (or ice, a barricade): blocks sight and gives cover. Holes and rubble don't.</summary>
        public bool IsRock(GridPos p) => InBounds(p) && _obstacles[p.X, p.Y] && !_low[p.X, p.Y];

        /// <summary>A hole in the roof (Crumbling Roof): a unit pushed into it falls.</summary>
        public bool IsHole(GridPos p) => InBounds(p) && _holes[p.X, p.Y];

        public void AddHole(GridPos p)
        {
            if (InBounds(p))
                _holes[p.X, p.Y] = true;
        }

        /// <summary>Adds a rock during a battle, e.g. where an ogre's boulder lands.</summary>
        public void AddObstacle(GridPos p)
        {
            if (!InBounds(p))
                return;
            _obstacles[p.X, p.Y] = true;
            _low[p.X, p.Y] = false;
        }

        /// <summary>Adds a low obstacle (Rubble): it blocks walking and standing, but shots and line of sight pass over it.</summary>
        public void AddLowObstacle(GridPos p)
        {
            if (!InBounds(p))
                return;
            _obstacles[p.X, p.Y] = true;
            _low[p.X, p.Y] = true;
        }

        /// <summary>A low obstacle (Rubble) stands here.</summary>
        public bool IsLowObstacle(GridPos p) => InBounds(p) && _low[p.X, p.Y];

        /// <summary>Clears a tile, e.g. when an ice wall melts or a barricade blows up.</summary>
        public void RemoveObstacle(GridPos p)
        {
            if (!InBounds(p))
                return;
            _obstacles[p.X, p.Y] = false;
            _low[p.X, p.Y] = false;
        }

        public IEnumerable<GridPos> AllTiles()
        {
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                yield return new GridPos(x, y);
        }

        /// <summary>
        /// True if no obstacle lies on the straight line between the centres of two tiles.
        /// The end tiles themselves are not checked. A line that passes exactly through a
        /// corner is blocked only if both tiles touching that corner are obstacles.
        /// </summary>
        public bool HasLineOfSight(GridPos from, GridPos to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var steps = Math.Max(Math.Abs(dx), Math.Abs(dy)) * 8;
            if (steps == 0)
                return true;

            for (var i = 1; i < steps; i++)
            {
                var t = (double)i / steps;
                var x = from.X + dx * t;
                var y = from.Y + dy * t;
                if (BlocksAt(x, y, from, to))
                    return false;
            }

            return true;
        }

        private bool BlocksAt(double x, double y, GridPos from, GridPos to)
        {
            // Tile centres sit on integer coordinates, so a tile covers [n - 0.5, n + 0.5).
            const double eps = 1e-9;
            var fx = x + 0.5;
            var fy = y + 0.5;
            var onXEdge = Math.Abs(fx - Math.Round(fx)) < eps;
            var onYEdge = Math.Abs(fy - Math.Round(fy)) < eps;

            var xs = onXEdge ? new[] { (int)Math.Round(fx) - 1, (int)Math.Round(fx) } : new[] { (int)Math.Floor(fx) };
            var ys = onYEdge ? new[] { (int)Math.Round(fy) - 1, (int)Math.Round(fy) } : new[] { (int)Math.Floor(fy) };

            // On an edge or corner the line squeezes between tiles; it is blocked only if every
            // touching tile (other than the shooter's and the target's) is an obstacle.
            var any = false;
            foreach (var cx in xs)
            foreach (var cy in ys)
            {
                var p = new GridPos(cx, cy);
                if (p == from || p == to)
                    continue;
                if (!IsRock(p))
                    return false;
                any = true;
            }

            return any;
        }
    }
}
