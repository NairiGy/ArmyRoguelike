using System.Collections.Generic;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>Draws the isometric board: tiles, rocks and the statues of every placed unit.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        private static readonly Color TileColor = new Color(0.56f, 0.52f, 0.44f);

        private readonly Dictionary<GridPos, SpriteRenderer> _tiles = new Dictionary<GridPos, SpriteRenderer>();
        private readonly Dictionary<GridPos, SpriteRenderer> _highlights = new Dictionary<GridPos, SpriteRenderer>();
        private readonly Dictionary<Unit, UnitView> _units = new Dictionary<Unit, UnitView>();

        public Battle Battle { get; private set; }

        public static BoardView Create(Battle battle, Transform parent)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BoardView>();
            view.Build(battle);
            return view;
        }

        private void Build(Battle battle)
        {
            Battle = battle;
            foreach (var p in battle.Board.AllTiles())
            {
                var tile = new GameObject($"Tile {p}").AddComponent<SpriteRenderer>();
                tile.transform.SetParent(transform, false);
                tile.transform.position = IsoMath.GridToWorld(p);
                tile.sprite = PlaceholderArt.Tile;
                tile.color = TileColor;
                // Tiles sit underneath everything standing on the board.
                tile.sortingOrder = -1000 + IsoMath.SortingOrder(p) - 1000;
                _tiles[p] = tile;

                var highlight = new GameObject($"Highlight {p}").AddComponent<SpriteRenderer>();
                highlight.transform.SetParent(transform, false);
                highlight.transform.position = IsoMath.GridToWorld(p);
                highlight.transform.localScale = Vector3.one * 0.9f;
                highlight.sprite = PlaceholderArt.Tile;
                highlight.sortingOrder = tile.sortingOrder + 1;
                highlight.enabled = false;
                _highlights[p] = highlight;

                if (battle.Board.IsObstacle(p))
                    AddRock(p);
            }

            SyncUnits();
        }

        private readonly Dictionary<GridPos, SpriteRenderer> _hazards = new Dictionary<GridPos, SpriteRenderer>();

        /// <summary>Shows a mine or bomb on a tile (replacing whatever marker was there).</summary>
        public void AddHazard(GridPos p, bool isBomb)
        {
            RemoveHazard(p);
            var sr = new GameObject(isBomb ? $"Bomb {p}" : $"Mine {p}").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.position = IsoMath.GridToWorld(p);
            sr.sprite = isBomb ? PlaceholderArt.Bomb : PlaceholderArt.Mine;
            // Mines lie flat under statues; bombs sit on top so they're easy to spot.
            sr.sortingOrder = isBomb ? IsoMath.SortingOrder(p) + 9 : IsoMath.SortingOrder(p) + 1;
            _hazards[p] = sr;
        }

        public void RemoveHazard(GridPos p)
        {
            if (!_hazards.TryGetValue(p, out var sr))
                return;
            Destroy(sr.gameObject);
            _hazards.Remove(p);
        }

        /// <summary>Makes the markers match the rules exactly, e.g. after a turn's animations finish.</summary>
        public void SyncHazards()
        {
            var wanted = new Dictionary<GridPos, bool>();
            foreach (var m in Battle.Mines)
                wanted[m.Tile] = false;
            foreach (var b in Battle.Bombs)
                wanted[b.Tile] = true;
            foreach (var p in new List<GridPos>(_hazards.Keys))
                if (!wanted.ContainsKey(p))
                    RemoveHazard(p);
            foreach (var kv in wanted)
                if (!_hazards.TryGetValue(kv.Key, out var sr) || (sr.sprite == PlaceholderArt.Bomb) != kv.Value)
                    AddHazard(kv.Key, kv.Value);
        }

        /// <summary>A quick orange flash over a blast area.</summary>
        public System.Collections.IEnumerator Flash(IEnumerable<GridPos> area, float duration)
        {
            var flashes = new List<SpriteRenderer>();
            foreach (var p in area)
            {
                var sr = new GameObject("Blast").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.transform.position = IsoMath.GridToWorld(p);
                sr.sprite = PlaceholderArt.Tile;
                sr.sortingOrder = 14000;
                flashes.Add(sr);
            }

            for (var t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                var c = Color.Lerp(new Color(1f, 0.95f, 0.6f, 0.9f), new Color(1f, 0.35f, 0.1f, 0f), t);
                foreach (var sr in flashes)
                {
                    sr.color = c;
                    sr.transform.localScale = Vector3.one * (0.8f + 0.3f * t);
                }

                yield return null;
            }

            foreach (var sr in flashes)
                Destroy(sr.gameObject);
        }

        /// <summary>Draws a rock on a tile. Also used when a boulder lands during the battle.</summary>
        public void AddRock(GridPos p)
        {
            var rock = new GameObject($"Rock {p}").AddComponent<SpriteRenderer>();
            rock.transform.SetParent(transform, false);
            rock.transform.position = IsoMath.GridToWorld(p);
            rock.sprite = PlaceholderArt.Rock;
            rock.sortingOrder = IsoMath.SortingOrder(p) + 5;
        }

        /// <summary>Creates views for newly placed units and moves every view to its unit's tile.</summary>
        public void SyncUnits()
        {
            foreach (var unit in Battle.Units)
            {
                if (!unit.IsPlaced)
                    continue;
                if (!_units.TryGetValue(unit, out var view))
                    _units[unit] = view = UnitView.Create(unit, transform);
                view.SetGridPosition(unit.Pos);
                view.RefreshHp(unit.Hp);
            }
        }

        public UnitView ViewOf(Unit unit) => _units.TryGetValue(unit, out var v) ? v : null;

        public void SetHighlight(GridPos p, Color color)
        {
            if (!_highlights.TryGetValue(p, out var h))
                return;
            h.color = color;
            h.enabled = true;
        }

        public void ClearHighlights()
        {
            foreach (var h in _highlights.Values)
                h.enabled = false;
        }

        /// <summary>World-space centre of the board, for framing the camera.</summary>
        public Vector3 Centre => IsoMath.GridToWorld((Battle.Board.Width - 1) * 0.5f, (Battle.Board.Height - 1) * 0.5f);
    }
}
