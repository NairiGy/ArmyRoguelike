using System;
using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>A tile coordinate on the board. X runs along a row, Y goes from the player's side (0) to the enemy's side.</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>The four orthogonal directions. There is no diagonal movement.</summary>
        public static readonly GridPos[] Directions =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
        };

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Y + b.Y);

        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);

        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        /// <summary>Distance in orthogonal steps, ignoring obstacles.</summary>
        public int StepsTo(GridPos other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

        public IEnumerable<GridPos> Neighbours()
        {
            foreach (var d in Directions)
                yield return this + d;
        }

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => $"({X},{Y})";
    }
}
