using System.Collections.Generic;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>Draws the isometric board: tiles, rocks and the statues of every placed unit.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        private static readonly Color TileColor = new Color(0.56f, 0.52f, 0.44f);
        private Color _rockTint = Color.white;
        private Sprite[] _rockArt;
        private Material _rockArtMaterial;
        private Color _rockArtColor;
        private Material _spriteMaterial;

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
                highlight.transform.localScale = Vector3.one * 0.96f;
                highlight.sprite = PlaceholderArt.GlowTile;
                highlight.sortingOrder = tile.sortingOrder + 1;
                if (_spriteMaterial == null)
                    _spriteMaterial = highlight.sharedMaterial;
                highlight.enabled = false;
                _highlights[p] = highlight;

                if (battle.Board.IsRock(p))
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
            if (_glowMaterial == null)
                _glowMaterial = new Material(Resources.Load<Shader>("Shaders/Glow"));
            var centre = Vector3.zero;
            foreach (var p in area)
            {
                var sr = new GameObject("Blast").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.transform.position = IsoMath.GridToWorld(p);
                sr.sprite = PlaceholderArt.GlowTile;
                sr.sharedMaterial = _glowMaterial;
                sr.sortingOrder = 14000;
                flashes.Add(sr);
                centre += sr.transform.position;
            }

            // The blast itself: flash, shockwave, debris and a shake at the middle of the area.
            if (flashes.Count > 0)
                Fx.Explosion(centre / flashes.Count);

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

        private static readonly Color IceTint = new Color(0.7f, 0.9f, 1f, 0.9f);
        private static readonly Color BarricadeTint = new Color(0.8f, 0.58f, 0.36f);

        // Placeholder for the art chat: rubble is a low, grey-brown block (see docs/night/mage-sapper.md).
        private static readonly Color RubbleTint = new Color(0.55f, 0.5f, 0.45f);
        private static readonly Color BurnTint = new Color(1f, 0.35f, 0.1f, 0.45f);

        private readonly Dictionary<GridPos, SpriteRenderer> _rocks = new Dictionary<GridPos, SpriteRenderer>();
        private readonly Dictionary<GridPos, SpriteRenderer> _burns = new Dictionary<GridPos, SpriteRenderer>();
        private readonly Dictionary<GridPos, SpriteRenderer> _floors = new Dictionary<GridPos, SpriteRenderer>();
        private static readonly Color CrackTint = new Color(0.25f, 0.2f, 0.15f, 0.45f);
        private static readonly Color HoleTint = new Color(0.02f, 0.02f, 0.04f, 0.95f);

        /// <summary>Draws a rock on a tile (once). Also used when a boulder lands during the battle.</summary>
        public void AddRock(GridPos p)
        {
            if (_rocks.ContainsKey(p))
                return;
            var rock = new GameObject($"Rock {p}").AddComponent<SpriteRenderer>();
            rock.transform.SetParent(transform, false);
            rock.transform.position = IsoMath.GridToWorld(p);
            if (_spriteMaterial == null)
                _spriteMaterial = rock.sharedMaterial;
            rock.sortingOrder = IsoMath.SortingOrder(p) + 5;
            _rocks[p] = rock;
            StyleRock(p, rock);
        }

        /// <summary>
        /// Plain rocks use the scenery's painted rocks when it has them; ice walls and barricades keep the
        /// tinted block so they stay recognisable.
        /// </summary>
        private void StyleRock(GridPos p, SpriteRenderer rock)
        {
            var ice = Battle.Ice.ContainsKey(p);
            var barricade = Battle.IsBarricade(p);
            var rubble = Battle.IsRubble(p);
            var painted = !ice && !barricade && !rubble && _rockArt != null && _rockArt.Length > 0;
            // Painted rocks get a soft shadow so they sit on the floor instead of blending into it.
            var shadow = rock.transform.Find("Shadow")?.GetComponent<SpriteRenderer>();
            if (painted && shadow == null)
            {
                shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
                shadow.transform.SetParent(rock.transform, false);
                shadow.transform.localScale = Vector3.one * 0.95f;
                shadow.sprite = PlaceholderArt.SoftShadow;
                shadow.color = new Color(0f, 0f, 0f, 0.55f);
                shadow.sortingOrder = rock.sortingOrder - 1;
            }

            if (shadow != null)
                shadow.enabled = painted;
            if (painted)
            {
                rock.sprite = _rockArt[Variant(p, _rockArt.Length)];
                rock.sharedMaterial = BattleScenery.PerTexture(_rockArtMaterial, rock.sprite.texture);
                rock.color = _rockArtColor;
                rock.transform.localScale = Vector3.one;
                return;
            }

            rock.sprite = PlaceholderArt.Rock;
            rock.sharedMaterial = _spriteMaterial;
            rock.color = ice ? IceTint : barricade ? BarricadeTint : rubble ? RubbleTint : _rockTint;
            // Rubble sits low: a squashed block, so it reads as a heap rather than a wall.
            rock.transform.localScale = rubble ? new Vector3(1f, 0.55f, 1f) : Vector3.one;
        }

        /// <summary>A stable pseudo-random pick per tile, so the board looks the same every frame.</summary>
        private static int Variant(GridPos p, int count) => Mathf.Abs((p.X * 73856093) ^ (p.Y * 19349663)) % count;

        /// <summary>
        /// Makes rocks, ice walls, barricades and burning ground match the rules: ice is pale blue,
        /// barricades are wood-brown, burning tiles glow orange.
        /// </summary>
        public void SyncTiles()
        {
            foreach (var p in Battle.Board.AllTiles())
            {
                if (Battle.Board.IsRock(p))
                {
                    AddRock(p);
                    StyleRock(p, _rocks[p]);
                }
                else if (_rocks.TryGetValue(p, out var rock))
                {
                    Destroy(rock.gameObject);
                    _rocks.Remove(p);
                }

                // Crumbling Roof (placeholder look): cracked tiles darken, holes go nearly black.
                Battle.FloorCracks.TryGetValue(p, out var cracks);
                var floor = Battle.Board.IsHole(p) ? HoleTint : cracks > 0 ? CrackTint : (Color?)null;
                if (floor.HasValue)
                {
                    if (!_floors.TryGetValue(p, out var sr))
                    {
                        sr = new GameObject($"Floor {p}").AddComponent<SpriteRenderer>();
                        sr.transform.SetParent(transform, false);
                        sr.transform.position = IsoMath.GridToWorld(p);
                        sr.sprite = PlaceholderArt.Tile;
                        sr.sortingOrder = _tiles[p].sortingOrder + 2;
                        _floors[p] = sr;
                    }

                    sr.color = floor.Value;
                }
                else if (_floors.TryGetValue(p, out var old))
                {
                    Destroy(old.gameObject);
                    _floors.Remove(p);
                }

                var burning = Battle.BurningTiles.ContainsKey(p);
                if (burning && !_burns.ContainsKey(p))
                {
                    var sr = new GameObject($"Burn {p}").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(transform, false);
                    sr.transform.position = IsoMath.GridToWorld(p);
                    sr.sprite = PlaceholderArt.Tile;
                    sr.color = BurnTint;
                    sr.sortingOrder = _tiles[p].sortingOrder + 2;
                    _burns[p] = sr;
                }
                else if (!burning && _burns.TryGetValue(p, out var burn))
                {
                    Destroy(burn.gameObject);
                    _burns.Remove(p);
                }
            }
        }

        private void Update()
        {
            // Burning ground flickers.
            var a = 0.35f + 0.15f * Mathf.Sin(Time.time * 9f);
            foreach (var burn in _burns.Values)
                burn.color = new Color(BurnTint.r, BurnTint.g, BurnTint.b, a);

            // Highlights breathe gently, so they read as magic light rather than paint.
            var pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 3f);
            foreach (var kv in _highlights)
                if (kv.Value.enabled && _highlightColors.TryGetValue(kv.Key, out var c))
                    kv.Value.color = new Color(c.r, c.g, c.b, c.a * pulse);
        }

        /// <summary>Creates views for newly placed units and moves every view to its unit's tile.</summary>
        public void SyncUnits()
        {
            foreach (var unit in Battle.Units)
            {
                if (!unit.IsPlaced)
                    continue;
                if (!_units.TryGetValue(unit, out var view))
                    _units[unit] = view = NewView(unit);
                if (!unit.IsAlive && !unit.IsRemains)
                    continue;
                view.SetGridPosition(unit.Pos);
                if (unit.IsAlive)
                {
                    view.RefreshHp(unit.Hp);
                    view.RefreshStatus();
                }
            }
        }

        public UnitView ViewOf(Unit unit) => _units.TryGetValue(unit, out var v) ? v : null;

        /// <summary>Creates the view for a unit that appeared mid-battle (a shard), without touching the others.</summary>
        public UnitView AddView(Unit unit)
        {
            if (!_units.TryGetValue(unit, out var view))
                _units[unit] = view = NewView(unit);
            view.SetGridPosition(unit.Pos);
            return view;
        }

        private Material _playerStatueMaterial;
        private Material _enemyStatueMaterial;
        private IReadOnlyDictionary<UnitKind, Material> _enemyStatueMaterialsByKind;

        private UnitView NewView(Unit unit)
        {
            var view = UnitView.Create(unit, transform);
            view.SetStatueMaterial(StatueMaterialFor(unit));
            return view;
        }

        /// <summary>
        /// Draws painted statues with these materials (per team, and for enemies of some kinds their faction's
        /// own), now and for units that appear later.
        /// </summary>
        public void SetStatueMaterials(Material player, Material enemy, IReadOnlyDictionary<UnitKind, Material> enemyByKind = null)
        {
            _playerStatueMaterial = player;
            _enemyStatueMaterial = enemy;
            _enemyStatueMaterialsByKind = enemyByKind;
            foreach (var kv in _units)
                kv.Value.SetStatueMaterial(StatueMaterialFor(kv.Key));
        }

        private Material StatueMaterialFor(Unit unit)
        {
            if (unit.Team == Team.Player)
                return _playerStatueMaterial;
            return _enemyStatueMaterialsByKind != null && _enemyStatueMaterialsByKind.TryGetValue(unit.Stats.Kind, out var m) ? m : _enemyStatueMaterial;
        }

        /// <summary>Recolours the ground to suit the battle's scenery palette.</summary>
        public void SetGroundTint(Color color)
        {
            foreach (var tile in _tiles.Values)
                tile.color = color;
        }

        /// <summary>Replaces the plain tiles with painted slabs (picked per tile) drawn with the scenery's material.</summary>
        public void SetTileArt(Sprite[] slabs, Material material, Color color)
        {
            foreach (var kv in _tiles)
            {
                kv.Value.sprite = slabs[Variant(kv.Key, slabs.Length)];
                kv.Value.flipX = Variant(new GridPos(kv.Key.Y, kv.Key.X), 2) == 1;
                kv.Value.sharedMaterial = BattleScenery.PerTexture(material, kv.Value.sprite.texture);
                kv.Value.color = color;
            }
        }

        /// <summary>Draws a thin outline round every tile, so the grid reads on a painted floor.</summary>
        public void SetGridLines(Color color)
        {
            foreach (var kv in _tiles)
            {
                var line = new GameObject($"Grid {kv.Key}").AddComponent<SpriteRenderer>();
                line.transform.SetParent(transform, false);
                line.transform.position = kv.Value.transform.position;
                line.sprite = PlaceholderArt.GridLine;
                line.color = color;
                // Over the tile, burning ground and highlights' edges, under everything standing on the board.
                line.sortingOrder = kv.Value.sortingOrder + 3;
            }
        }

        /// <summary>Plain rocks become the scenery's painted rocks (picked per tile), drawn with its material.</summary>
        public void SetRockArt(Sprite[] rocks, Material material, Color color)
        {
            _rockArt = rocks;
            _rockArtMaterial = material;
            _rockArtColor = color;
            SyncTiles();
        }

        /// <summary>Recolours plain rocks (not ice or barricades) to suit the scenery palette.</summary>
        public void SetRockTint(Color color)
        {
            _rockTint = color;
            SyncTiles();
        }

        private readonly Dictionary<GridPos, Color> _highlightColors = new Dictionary<GridPos, Color>();
        private static Material _glowMaterial;

        /// <summary>
        /// Lights a tile in a colour: a soft glowing edge and faint fill, added as light so it glows on the
        /// dark floor. Dark colours (like where a boulder will land) are painted on normally instead.
        /// </summary>
        public void SetHighlight(GridPos p, Color color)
        {
            if (!_highlights.TryGetValue(p, out var h))
                return;
            if (_glowMaterial == null)
                _glowMaterial = new Material(Resources.Load<Shader>("Shaders/Glow"));
            h.sharedMaterial = Mathf.Max(color.r, color.g, color.b) < 0.35f ? _spriteMaterial : _glowMaterial;
            _highlightColors[p] = color;
            h.color = color;
            h.enabled = true;
        }

        public void ClearHighlights()
        {
            foreach (var h in _highlights.Values)
                h.enabled = false;
        }

        private readonly Dictionary<GridPos, SunMark> _sunMarks = new Dictionary<GridPos, SunMark>();

        /// <summary>Shows a sun mark on a tile, unless one is already there.</summary>
        public void AddSunMark(GridPos p)
        {
            if (!_sunMarks.ContainsKey(p))
                _sunMarks[p] = SunMark.Create(transform, p);
        }

        /// <summary>
        /// A sun beam striking a tile: the mark charges up, then onImpact runs as the beam lands
        /// (the hits show then). The afterglow carries on after this returns.
        /// </summary>
        public System.Collections.IEnumerator SunStrike(GridPos p, System.Action onImpact)
        {
            AddSunMark(p);
            var mark = _sunMarks[p];
            _sunMarks.Remove(p);
            yield return mark.Charge();
            mark.Impact();
            onImpact?.Invoke();
        }

        private readonly Dictionary<Unit, MoonShieldView> _moonShields = new Dictionary<Unit, MoonShieldView>();

        /// <summary>
        /// The moon shields a unit this turn: last turn's shields fade first (shields cast earlier this turn,
        /// by a second moon omen, stay).
        /// </summary>
        public void ShowMoonShield(Unit unit, int points, int turn)
        {
            foreach (var kv in new List<KeyValuePair<Unit, MoonShieldView>>(_moonShields))
            {
                if (kv.Value != null && kv.Value.CastTurn == turn && kv.Key != unit)
                    continue;
                if (kv.Value != null)
                    kv.Value.Fade();
                _moonShields.Remove(kv.Key);
            }

            var view = ViewOf(unit);
            if (view != null)
                _moonShields[unit] = MoonShieldView.Create(view, points, turn);
        }

        /// <summary>
        /// A shield absorbed damage on a unit. Moon shields soak damage before ward shields, so the moon
        /// shield (if any) takes this first. Returns whether a moon shield took it.
        /// </summary>
        public bool MoonShieldAbsorb(Unit unit, int amount)
        {
            if (!_moonShields.TryGetValue(unit, out var shield) || shield == null || shield.Points <= 0)
                return false;
            shield.Absorb(amount);
            if (shield.Points <= 0)
                _moonShields.Remove(unit);
            return true;
        }

        /// <summary>World-space centre of the board, for framing the camera.</summary>
        public Vector3 Centre => IsoMath.GridToWorld((Battle.Board.Width - 1) * 0.5f, (Battle.Board.Height - 1) * 0.5f);
    }
}
