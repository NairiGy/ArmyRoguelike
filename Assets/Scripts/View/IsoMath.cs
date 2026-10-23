using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>Converts between grid tiles and isometric world positions.</summary>
    public static class IsoMath
    {
        public const float TileWidth = 1f;
        public const float TileHeight = 0.5f;

        /// <summary>World position of a tile's centre. Grid (0,0) is the bottom corner of the board.</summary>
        public static Vector3 GridToWorld(GridPos p) => GridToWorld(p.X, p.Y);

        public static Vector3 GridToWorld(float x, float y) =>
            new Vector3((x - y) * TileWidth * 0.5f, (x + y) * TileHeight * 0.5f, 0f);

        public static GridPos WorldToGrid(Vector2 world)
        {
            var a = world.x / (TileWidth * 0.5f);
            var b = world.y / (TileHeight * 0.5f);
            return new GridPos(Mathf.RoundToInt((a + b) * 0.5f), Mathf.RoundToInt((b - a) * 0.5f));
        }

        /// <summary>Sorting order so tiles further back are drawn first. Leaves room for layers within a tile.</summary>
        public static int SortingOrder(GridPos p) => 1000 - (p.X + p.Y) * 10;

        public static int SortingOrder(Vector3 world) => 1000 - Mathf.RoundToInt(world.y / (TileHeight * 0.5f) * 10f);
    }
}
