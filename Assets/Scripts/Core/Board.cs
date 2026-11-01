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

        public Board(int width, int height, IEnumerable<GridPos> obstacles = null)
        {
            Width = width;
            Height = height;
            _obstacles = new bool[width, height];
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
        }

        /// <summary>A separate board with the same rocks.</summary>
        public Board Clone() => new Board(this);

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public bool IsObstacle(GridPos p) => InBounds(p) && _obstacles[p.X, p.Y];

        /// <summary>Adds a rock during a battle, e.g. where an ogre's boulder lands.</summary>
        public void AddObstacle(GridPos p)
        {
            if (InBounds(p))
                _obstacles[p.X, p.Y] = true;
        }

        /// <summary>Clears a tile, e.g. when an ice wall melts or a barricade blows up.</summary>
        public void RemoveObstacle(GridPos p)
        {
            if (InBounds(p))
                _obstacles[p.X, p.Y] = false;
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
                if (!IsObstacle(p))
                    return false;
                any = true;
            }

            return any;
        }
    }
}
