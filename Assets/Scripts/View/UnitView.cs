using System.Collections;
using System.Collections.Generic;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// A statue on the board: sprite, type letter and HP bar. All statues share the same simple
    /// animations: rise when selected, hop between tiles, lunge to attack, shake when hit, sink when killed.
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        private const float BarWidth = 0.4f;
        private const float BarHeight = 0.05f;
        private const float StepTime = 0.14f;
        private const float HopHeight = 0.08f;

        public Unit Unit { get; private set; }

        private Color _baseColor;
        private Color _tint = Color.white;
        private bool _selected;
        private bool _dimmed;
        private bool _showingBones;
        private Vector3 _bodyRest;

        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color BareShadowColor = new Color(0f, 0f, 0f, 0.7f);
        // For figures standing on nothing: a wide soft halo around the feet and a firm dark patch where they touch.
        private static readonly Color FeetHaloColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color FeetContactColor = new Color(0f, 0f, 0f, 0.75f);

        // How far above the floor a lifted statue's shadow has shrunk and faded the most, and by how much.
        private const float ShadowLiftRange = 0.1f;
        private const float ShadowLiftShrink = 0.3f;
        private const float ShadowLiftFade = 0.45f;
        private Color _shadowColor;

        private SpriteRenderer _body;
        private SpriteRenderer _shadow;
        // A figure standing straight on the floor also gets a small dark patch right under its feet.
        private SpriteRenderer _contact;
        private Vector3 _shadowScale;
        private Vector3 _contactScale;
        // 1 normally; the death animation fades it out.
        private float _shadowFade = 1f;
        private SpriteRenderer _base;
        private TextMesh _label;
        private MeshRenderer _labelRenderer;
        private SpriteRenderer _labelPlate;
        private SpriteRenderer _barBack;
        private readonly List<SpriteRenderer> _hpPieces = new List<SpriteRenderer>();
        private int _shownHp;
        private int _previewLoss;
        private bool _barsVisible = true;

        public static UnitView Create(Unit unit, Transform parent)
        {
            var go = new GameObject(unit.Stats.Name);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<UnitView>();
            view.Build(unit);
            return view;
        }

        private void Build(Unit unit)
        {
            Unit = unit;
            _shownHp = unit.Hp;

            _body = new GameObject("Body").AddComponent<SpriteRenderer>();
            _body.transform.SetParent(transform, false);
            _body.sprite = PlaceholderArt.Statue(unit.Stats.Kind);
            // Raised (undead) enemies are pale bone instead of grey granite.
            _baseColor = unit.Team == Team.Player ? TierStone(unit.Stats.Tier) : unit.Stats.IsUndead ? new Color(0.78f, 0.76f, 0.66f) : PlaceholderArt.EnemyStone;
            if (PlaceholderArt.IsPainted(unit.Stats.Kind))
            {
                _baseColor = Color.white;
                // Shown as drawn: the player's statues face down-right, towards the enemy rows, and enemy
                // statues face down-left, towards the camera, so their faces show.
                // The big Elemental was drawn turned slightly the other way, so it is mirrored.
                _body.flipX = unit.Stats.Kind == UnitKind.Elemental;
            }
            _body.color = _baseColor;
            if (unit.IsPackLeader)
                _body.transform.localScale = Vector3.one * 1.25f;
            else if (unit.Size > 1)
                _body.transform.localScale = Vector3.one * 1.3f;
            else if (unit.Stats.Tier > 1)
                // Evolved statues stand a little taller.
                _body.transform.localScale = Vector3.one * (1f + 0.06f * (unit.Stats.Tier - 1));

            // A soft shadow on the floor grounds the statue; it stays put while the body bobs and lunges.
            _shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            _shadow.transform.SetParent(transform, false);
            _shadow.sprite = PlaceholderArt.SoftShadow;
            ApplyGrounding();

            var top = _body.transform.localPosition.y + _body.sprite.bounds.max.y * _body.transform.localScale.y;

            if (unit.Team == Team.Player)
            {
                _label = new GameObject("Label").AddComponent<TextMesh>();
                _label.transform.SetParent(transform, false);
                _label.transform.localPosition = new Vector3(0f, 0.27f, 0f);
                _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _label.text = unit.Stats.Label;
                _label.fontSize = unit.Stats.Label.Length > 1 ? 36 : 48;
                _label.characterSize = 0.05f;
                _label.anchor = TextAnchor.MiddleCenter;
                _label.alignment = TextAlignment.Center;
                // Light text on a dark plate, so it reads on every stone colour.
                _label.color = new Color(0.97f, 0.95f, 0.9f);
                _labelRenderer = _label.GetComponent<MeshRenderer>();
                _labelRenderer.sharedMaterial = _label.font.material;

                _labelPlate = new GameObject("LabelPlate").AddComponent<SpriteRenderer>();
                _labelPlate.transform.SetParent(transform, false);
                _labelPlate.transform.localPosition = _label.transform.localPosition;
                _labelPlate.transform.localScale = new Vector3(unit.Stats.Label.Length > 1 ? 0.3f : 0.2f, 0.19f, 1f);
                _labelPlate.sprite = PlaceholderArt.Pixel;
                _labelPlate.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            }

            _barBack = MakeBar("HpBack", new Color(0.1f, 0.1f, 0.1f, 0.85f), top + 0.08f);

            transform.localScale = Vector3.one * BattleScenery.UnitScale;
            SetGridPosition(unit.Pos);
            RefreshHp(unit.Hp);
        }

        // How wide a player statue's pedestal is, in tiles before BattleScenery.UnitScale (big units scale it up).
        private const float PedestalWidth = 0.8f;

        private Material _statueMaterial;

        /// <summary>
        /// What the statue stands on and the shadow under it. A player figure stands on the chosen painted pedestal
        /// (BattleScenery.Pedestal), its feet on the pedestal's top face; otherwise it stands straight on the floor.
        /// Run again when the pedestal choice changes (F2 in a test battle).
        /// </summary>
        public void ApplyGrounding()
        {
            if (_base != null)
                Destroy(_base.gameObject);
            _base = null;
            if (_contact != null)
                Destroy(_contact.gameObject);
            _contact = null;

            var kind = Unit.Stats.Kind;
            var bodyHeight = 0f;
            if (BattleScenery.DrawnBases && PlaceholderArt.IsFigureOnly(kind))
            {
                // A stone base drawn in code (the art experiments' renders), square to the tile.
                _base = new GameObject("Base").AddComponent<SpriteRenderer>();
                _base.transform.SetParent(transform, false);
                _base.transform.localScale = Vector3.one * Unit.Size;
                _base.sprite = PlaceholderArt.StatueBase;
                bodyHeight = PlaceholderArt.StatueBaseHeight * Unit.Size;
            }
            else if (Unit.Team == Team.Player && PlaceholderArt.IsFigureOnly(kind) && PlaceholderArt.Pedestal(BattleScenery.Pedestal) is Sprite pedestal)
            {
                var width = PedestalWidth * Unit.Size;
                _base = new GameObject("Pedestal").AddComponent<SpriteRenderer>();
                _base.transform.SetParent(transform, false);
                _base.transform.localScale = Vector3.one * width;
                _base.sprite = pedestal;
                if (_statueMaterial != null)
                    _base.sharedMaterial = BattleScenery.PerTexture(_statueMaterial, pedestal.texture);
                bodyHeight = PlaceholderArt.PedestalTop(pedestal) * width;
            }

            _body.transform.localPosition = Vector3.up * bodyHeight;
            _bodyRest = _body.transform.localPosition;
            _shadow.transform.localPosition = Vector3.zero;

            if (_base != null && _base.sprite != PlaceholderArt.StatueBase)
            {
                // On a pedestal, the shadow is the pedestal's: as wide as its foot, flat on the floor.
                FitShadow(0f, PedestalWidth * Unit.Size * 0.95f, 0f);
            }
            else if (_base == null && PlaceholderArt.IsFigureOnly(kind))
            {
                // A figure standing straight on the floor gets its shadow from its own feet: a crouch, a wide
                // stance or four paws each sit on a shadow of their own shape instead of the same round blot.
                var feet = PlaceholderArt.Footprint(_body.sprite);
                var scale = _body.transform.localScale.x;
                // Wings, tails and weapons low down can widen the measure; no shadow spreads far past its tile.
                var span = Mathf.Min(feet.y * scale, 0.8f * Unit.Size);
                // The back foot stands higher on the picture than the front one, so the shadow sits a little up.
                FitShadow((_body.flipX ? -feet.x : feet.x) * scale, span, span * 0.08f);
            }
            else
            {
                _shadowColor = ShadowColor;
                _shadow.transform.localScale = Vector3.one * (0.75f * Unit.Size);
            }

            _shadow.color = _shadowColor;
            _shadowScale = _shadow.transform.localScale;
        }

        /// <summary>A wide soft halo and a firm dark patch, both as wide as <paramref name="span"/>, centred at <paramref name="centre"/>.</summary>
        private void FitShadow(float centre, float span, float up)
        {
            var at = new Vector3(centre, up, 0f);
            _shadowColor = FeetHaloColor;
            _shadow.transform.localPosition = at;
            // The halo's soft edge shows only about two thirds of its width.
            _shadow.transform.localScale = Vector3.one * Mathf.Max(0.35f, span * 1.6f + 0.15f);

            _contact = new GameObject("Contact shadow").AddComponent<SpriteRenderer>();
            _contact.transform.SetParent(transform, false);
            _contact.transform.localPosition = at;
            _contact.transform.localScale = _contactScale = Vector3.one * Mathf.Max(0.18f, span * 1.05f);
            _contact.sprite = PlaceholderArt.ContactShadow;
            _contact.color = FeetContactColor;
        }

        /// <summary>F2 in a test battle: every statue on the board moves to the next pedestal shape.</summary>
        public static void CyclePedestal()
        {
            var shapes = (BattleScenery.PedestalShape[])System.Enum.GetValues(typeof(BattleScenery.PedestalShape));
            BattleScenery.Pedestal = shapes[((int)BattleScenery.Pedestal + 1) % shapes.Length];
            foreach (var view in FindObjectsByType<UnitView>(FindObjectsSortMode.None))
                if (view.Unit.IsAlive && !view._showingBones)
                {
                    view.ApplyGrounding();
                    view.SetWorldPosition(view.transform.position);
                }
        }

        /// <summary>
        /// The shadow follows the statue off the floor: lifted (selected, or mid-step), it shrinks and fades, and the
        /// dark patch under the feet all but goes, so the statue reads as leaving the ground and landing again.
        /// </summary>
        private void LateUpdate()
        {
            // A pedestal is part of the statue: it rises, hops, lunges and shakes with the figure.
            if (_base != null)
                _base.transform.localPosition = _body.transform.localPosition - _bodyRest;
            if (!_shadow.enabled)
                return;
            var lift =Mathf.Clamp01((_body.transform.localPosition.y - _bodyRest.y) / ShadowLiftRange);
            _shadow.transform.localScale = _shadowScale * (1f - ShadowLiftShrink * lift);
            _shadow.color = new Color(0f, 0f, 0f, _shadowColor.a * _shadowFade * (1f - ShadowLiftFade * lift));
            if (_contact != null)
            {
                _contact.transform.localScale = _contactScale * (1f - ShadowLiftShrink * lift);
                _contact.color = new Color(0f, 0f, 0f, FeetContactColor.a * _shadowFade * (1f - 0.85f * lift));
            }
        }

        private void SetShadowsEnabled(bool enabled)
        {
            _shadow.enabled = enabled;
            if (_contact != null)
                _contact.enabled = enabled;
        }

        private SpriteRenderer MakeBar(string name, Color color, float y)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = PlaceholderArt.Pixel;
            sr.color = color;
            sr.transform.localPosition = new Vector3(0f, y, 0f);
            sr.transform.localScale = new Vector3(BarWidth * BarScale, BarHeight, 1f);
            return sr;
        }

        public void SetGridPosition(GridPos p) => SetWorldPosition(WorldOf(p));

        /// <summary>Where the statue stands for an anchor tile: the tile centre, or the centre of a 2x2 footprint.</summary>
        public Vector3 WorldOf(GridPos anchor)
        {
            var offset = (Unit.Size - 1) * 0.5f;
            return IsoMath.GridToWorld(anchor.X + offset, anchor.Y + offset);
        }

        public void SetWorldPosition(Vector3 world)
        {
            transform.position = world;
            var order = IsoMath.SortingOrder(world) + 5;
            _shadow.sortingOrder = order - 5;
            if (_contact != null)
                _contact.sortingOrder = order - 5;
            if (_base != null)
                _base.sortingOrder = order - 1;
            // Bones lie flat on the tile, under anything standing there.
            _body.sortingOrder = _showingBones ? order - 4 : order;
            if (_labelPlate != null)
                _labelPlate.sortingOrder = order + 1;
            if (_labelRenderer != null)
                _labelRenderer.sortingOrder = order + 2;
            _barBack.sortingOrder = order + 3;
            foreach (var piece in _hpPieces)
                piece.sortingOrder = order + 4;
        }

        /// <summary>The gap between two HP pieces (world units), shrunk for units with a lot of HP.</summary>
        private const float PieceGap = 0.008f;

        private static readonly Color LossPreviewColor = new Color(1f, 0.85f, 0.75f, 0.8f);
        private static readonly Color EmptyPieceColor = new Color(0.24f, 0.24f, 0.26f, 0.9f);
        private Color FillColor => Unit.Team == Team.Player ? new Color(0.35f, 0.8f, 0.4f) : new Color(0.85f, 0.3f, 0.25f);

        /// <summary>
        /// The HP bar in pieces, one per point of max HP: filled for HP left, pale and pulsing for HP the hovered
        /// attack would take off, dark for HP lost.
        /// </summary>
        public void RefreshHp(int hp)
        {
            _shownHp = Mathf.Max(0, hp);
            var max = Mathf.Max(1, Unit.MaxHp);
            var keep = _shownHp - Mathf.Min(_previewLoss, _shownHp);
            var width = BarWidth * BarScale;
            var gap = Mathf.Min(PieceGap, width / max * 0.3f);
            var size = (width - gap * (max - 1)) / max;
            var y = _barBack.transform.localPosition.y;
            while (_hpPieces.Count < max)
            {
                var piece = MakeBar("HpPiece", Color.white, y);
                piece.sortingOrder = _barBack.sortingOrder + 1;
                _hpPieces.Add(piece);
            }

            for (var i = 0; i < _hpPieces.Count; i++)
            {
                var piece = _hpPieces[i];
                piece.enabled = _barsVisible && i < max;
                if (i >= max)
                    continue;
                piece.transform.localScale = new Vector3(size, BarHeight, 1f);
                piece.transform.localPosition = new Vector3(-width * 0.5f + size * 0.5f + i * (size + gap), y, 0f);
                piece.color = i < keep ? FillColor : i < _shownHp ? LossPreviewColor : EmptyPieceColor;
            }

            ShowCracks(1f - (float)Mathf.Min(_shownHp, max) / max);
        }

        /// <summary>
        /// Shows the HP the selected unit's attack would take off as a pale, pulsing chunk at the end of the
        /// HP bar (0 clears it).
        /// </summary>
        public void ShowLossPreview(int loss)
        {
            loss = Mathf.Max(0, loss);
            if (loss == _previewLoss)
                return;
            _previewLoss = loss;
            RefreshHp(_shownHp);
        }

        private MaterialPropertyBlock _crackBlock;

        /// <summary>Painted statues crack as they lose HP (0 = whole, 1 = about to break).</summary>
        private void ShowCracks(float damage) => SetStatueFloat("_Crack", Mathf.Clamp01(damage));

        /// <summary>Sets a per-statue value of the statue shader (cracks, hit flash) on painted statues.</summary>
        private void SetStatueFloat(string property, float value)
        {
            if (!PlaceholderArt.IsPainted(Unit.Stats.Kind))
                return;
            if (_crackBlock == null)
                _crackBlock = new MaterialPropertyBlock();
            _body.GetPropertyBlock(_crackBlock);
            _crackBlock.SetFloat(property, value);
            _body.SetPropertyBlock(_crackBlock);
        }

        /// <summary>The colour of chips and rubble knocked off this statue.</summary>
        private Color StoneColor => Unit.Team == Team.Player ? new Color(0.85f, 0.88f, 0.95f) : new Color(0.42f, 0.44f, 0.52f);

        /// <summary>The middle of the statue's body, where blows land.</summary>
        private Vector3 BodyCentre => transform.position + Vector3.up * (_bodyRest.y + _body.sprite.bounds.max.y * _body.transform.lossyScale.y * 0.45f);

        /// <summary>Where the statue's feet rest (at rest, not bobbing), for effects wrapped around it.</summary>
        public Vector3 BodyBase => transform.position + _bodyRest;

        /// <summary>The statue's height in world units.</summary>
        public float BodyHeight => _body.sprite.bounds.max.y * _body.transform.lossyScale.y;

        /// <summary>The statue's sorting order, for effects drawn just in front of it.</summary>
        public int BodyOrder => _body.sortingOrder;

        /// <summary>Big units get a wider HP bar.</summary>
        private float BarScale => Unit.Size;

        /// <summary>Player statues get richer stone as they evolve: marble, bronze, gold.</summary>
        public static Color TierStone(int tier)
        {
            switch (tier)
            {
                case 2: return new Color(0.95f, 0.78f, 0.55f);
                case 3: return new Color(0.96f, 0.88f, 0.58f);
                default: return PlaceholderArt.PlayerStone;
            }
        }

        // ---------- Status markers ----------

        private static readonly Color ShieldColor = new Color(0.4f, 0.9f, 1f);
        private static readonly Color MoonShieldColor = new Color(0.85f, 0.88f, 0.95f);
        private static readonly Color SnareColor = new Color(0.45f, 0.85f, 0.3f);
        private static readonly Color SlowColor = new Color(0.65f, 0.8f, 1f);
        private static readonly Color PoisonColor = new Color(0.75f, 0.35f, 0.95f);
        private static readonly Color LootColor = new Color(1f, 0.82f, 0.1f);
        private static readonly Color FavoredColor = new Color(1f, 0.45f, 0.15f);

        // Demons (placeholders): dark red = Fury, black-red = under a Soul Contract.
        private static readonly Color FuryColor = new Color(0.75f, 0.05f, 0.05f);
        private static readonly Color ContractColor = new Color(0.3f, 0f, 0.05f);

        private readonly List<SpriteRenderer> _statusIcons = new List<SpriteRenderer>();

        /// <summary>
        /// Small coloured squares to the right of the HP bar: cyan = ward shield, silver = moon shield, green = snared,
        /// light blue = slowed, gold = carrying stolen gold, purple = poisoned, orange = favored by fortune (double
        /// damage). The HUD hover text spells them out.
        /// </summary>
        public void RefreshStatus()
        {
            var colors = new List<Color>();
            if (Unit.IsAlive)
            {
                if (Unit.Shield > 0)
                    colors.Add(ShieldColor);
                if (Unit.MoonShield > 0)
                    colors.Add(MoonShieldColor);
                if (Unit.IsSnared)
                    colors.Add(SnareColor);
                if (Unit.IsSlowed)
                    colors.Add(SlowColor);
                if (Unit.CarryingLoot > 0)
                    colors.Add(LootColor);
                if (Unit.PoisonTurns > 0)
                    colors.Add(PoisonColor);
                if (Unit.Favored)
                    colors.Add(FavoredColor);
                if (Unit.Fury > 0)
                    colors.Add(FuryColor);
                if (Unit.ContractBy != null)
                    colors.Add(ContractColor);
            }

            while (_statusIcons.Count < colors.Count)
            {
                var sr = new GameObject("Status").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = PlaceholderArt.Pixel;
                sr.transform.localScale = new Vector3(0.07f, 0.07f, 1f);
                _statusIcons.Add(sr);
            }

            var y = _barBack.transform.localPosition.y;
            var x = BarWidth * BarScale * 0.5f + 0.07f;
            for (var i = 0; i < _statusIcons.Count; i++)
            {
                var icon = _statusIcons[i];
                icon.enabled = i < colors.Count;
                if (!icon.enabled)
                    continue;
                icon.color = colors[i];
                icon.transform.localPosition = new Vector3(x + i * 0.09f, y, 0f);
                icon.sortingOrder = _barBack.sortingOrder + 2;
            }
        }

        // ---------- AP pips ----------

        private const float PipSize = 0.05f;
        private const float PipSpacing = 0.068f;
        private static readonly Color PipFull = new Color(1f, 0.85f, 0.25f);
        private static readonly Color PipSpend = new Color(1f, 0.4f, 0.15f);
        private static readonly Color PipEmpty = new Color(0.15f, 0.15f, 0.17f, 0.85f);

        private readonly List<SpriteRenderer> _pips = new List<SpriteRenderer>();

        /// <summary>
        /// A row of dots under the HP bar: yellow for AP the unit has, orange for AP the hovered action
        /// would spend, dark for AP already used this turn (up to its normal maximum).
        /// </summary>
        public void ShowAp(int ap, int maxAp, int cost)
        {
            var count = Mathf.Max(ap, maxAp);
            while (_pips.Count < count)
            {
                var sr = new GameObject("ApPip").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = PlaceholderArt.Pixel;
                sr.transform.localScale = new Vector3(PipSize, PipSize, 1f);
                _pips.Add(sr);
            }

            var y = _barBack.transform.localPosition.y - 0.07f;
            var left = -(count - 1) * PipSpacing * 0.5f;
            var keep = ap - Mathf.Clamp(cost, 0, ap);
            for (var i = 0; i < _pips.Count; i++)
            {
                var pip = _pips[i];
                pip.enabled = i < count;
                if (!pip.enabled)
                    continue;
                pip.transform.localPosition = new Vector3(left + i * PipSpacing, y, 0f);
                pip.color = i < keep ? PipFull : i < ap ? PipSpend : PipEmpty;
                pip.sortingOrder = _barBack.sortingOrder + 2;
            }
        }

        public void HideAp()
        {
            foreach (var pip in _pips)
                pip.enabled = false;
        }

        /// <summary>Top of the statue in world space, for pop-ups.</summary>
        public Vector3 TopWorld => _barBack.transform.position;

        /// <summary>The left end of the HP bar in world space, for labels beside it.</summary>
        public Vector3 BarLeftWorld => new Vector3(_barBack.bounds.min.x, _barBack.bounds.center.y, _barBack.transform.position.z);

        public void SetSelected(bool selected)
        {
            if (selected && !_selected && _wake <= 0f && IsLivingStatue)
            {
                // Waking: stone dust falls off as it lifts.
                StartCoroutine(Crumble());
                Fx.Dust(transform.position, 2, 0.12f);
            }

            _selected = selected;
        }

        /// <summary>Darkens a statue that has finished its turn.</summary>
        public void SetDimmed(bool dimmed)
        {
            _dimmed = dimmed;
            ApplyColor();
        }

        /// <summary>Extra colour multiplied over the stone, e.g. for pack-leader effects.</summary>
        public void SetTint(Color tint)
        {
            _tint = tint;
            ApplyColor();
        }

        /// <summary>Draws a painted statue with a scenery material (e.g. its team's palette ramp). Placeholders are left alone.</summary>
        public void SetStatueMaterial(Material material)
        {
            if (material == null || !PlaceholderArt.IsPainted(Unit.Stats.Kind))
                return;
            _statueMaterial = material;
            _body.sharedMaterial = BattleScenery.PerTexture(material, _body.sprite.texture);
            if (_base != null)
                _base.sharedMaterial = BattleScenery.PerTexture(material, _base.sprite.texture);
        }

        private bool _hidden;

        /// <summary>Vanished (rogue): placeholder see-through statue.</summary>
        public void SetHidden(bool hidden)
        {
            _hidden = hidden;
            ApplyColor();
        }

        private void ApplyColor()
        {
            var c = _baseColor * _tint;
            if (_dimmed)
                c *= 0.6f;
            c.a = _hidden ? 0.4f : 1f;
            _body.color = c;
        }

        private void Update()
        {
            // The whole unit (statue, base, shadow, bars) scales around its tile; tunable live via ArtTuning.
            transform.localScale = Vector3.one * BattleScenery.UnitScale;
            ShedDust();
            UpdateWake();
            if (_previewLoss > 0 && _barsVisible)
            {
                // The pieces the hovered attack would take off pulse.
                var pulse = new Color(LossPreviewColor.r, LossPreviewColor.g, LossPreviewColor.b, 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 6f));
                var max = Mathf.Min(Unit.MaxHp, _hpPieces.Count);
                for (var i = Mathf.Max(0, _shownHp - _previewLoss); i < Mathf.Min(_shownHp, max); i++)
                    _hpPieces[i].color = pulse;
            }
        }

        // ---------- Idle life ----------

        private bool IsLivingStatue => Unit != null && PlaceholderArt.IsPainted(Unit.Stats.Kind) && !_showingBones && Unit.IsAlive;

        // How far a selected statue lifts off the floor, and how fast it rises and drops back.
        private const float WakeLift = 0.07f;
        private const float RiseTime = 0.18f;
        private const float DropTime = 0.08f;
        private float _wake;

        /// <summary>
        /// The selected statue wakes: it lifts off the floor and holds there, stone-still, with its rim
        /// glowing brighter. Deselected, it drops back with a thud and a puff of dust. Rigid moves only
        /// (a bob or squash makes stone look rubbery).
        /// </summary>
        private void UpdateWake()
        {
            var target = _selected && IsLivingStatue ? 1f : 0f;
            var was = _wake;
            if (was <= 0f && target <= 0f)
                return;
            _wake = Mathf.MoveTowards(_wake, target, Time.deltaTime / (target > _wake ? RiseTime : DropTime));
            // Fast off the floor and easing to a hold; on the way down, speeding up into the landing. Set every
            // frame while lifted, so it lifts again after a step (the step animation runs after this).
            var height = 1f - (1f - _wake) * (1f - _wake);
            _body.transform.localPosition = _bodyRest + Vector3.up * (WakeLift * height);
            if (!Mathf.Approximately(was, _wake))
                SetStatueFloat("_Wake", _wake);
            if (_wake <= 0f && was > 0f && IsLivingStatue)
            {
                Fx.Dust(transform.position, 4, 0.2f);
                Sfx.Play("unit_step", transform.position, pitch: 0.08f);
            }
        }

        private float _nextDust = -1f;

        /// <summary>
        /// Living statues are never quite still: now and then a few specks of stone dust crumble off.
        /// (A slow breathing rise and fall was tried; squashing stone looked rubbery.)
        /// </summary>
        private void ShedDust()
        {
            if (!PlaceholderArt.IsPainted(Unit.Stats.Kind) || _showingBones || !Unit.IsAlive)
                return;
            if (_nextDust < 0f)
                _nextDust = Time.time + Random.Range(2f, 8f);
            if (Time.time < _nextDust)
                return;
            _nextDust = Time.time + Random.Range(5f, 12f);
            StartCoroutine(Crumble());
        }

        private IEnumerator Crumble()
        {
            var height = _body.sprite.bounds.max.y * _body.transform.localScale.y;
            var specks = new SpriteRenderer[Random.Range(2, 5)];
            var starts = new Vector3[specks.Length];
            for (var i = 0; i < specks.Length; i++)
            {
                specks[i] = new GameObject("Dust").AddComponent<SpriteRenderer>();
                specks[i].transform.SetParent(transform, false);
                specks[i].sprite = PlaceholderArt.Pixel;
                specks[i].sortingOrder = _body.sortingOrder + 1;
                starts[i] = new Vector3(Random.Range(-0.15f, 0.15f), height * Random.Range(0.35f, 0.85f), 0f);
                specks[i].transform.localScale = Vector3.one * Random.Range(0.012f, 0.022f);
            }

            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.9f)
            {
                for (var i = 0; i < specks.Length; i++)
                {
                    specks[i].transform.localPosition = starts[i] + new Vector3(i % 2 == 0 ? 0.03f * t : -0.03f * t, -0.3f * t * t, 0f);
                    specks[i].color = new Color(0.62f, 0.6f, 0.58f, 0.8f * (1f - t));
                }

                yield return null;
            }

            foreach (var s in specks)
                Destroy(s.gameObject);
        }

        // ---------- Animations ----------

        public IEnumerator MoveAlong(IReadOnlyList<GridPos> path)
        {
            var from = transform.position;
            foreach (var tile in path)
            {
                var to = WorldOf(tile);
                // A heavy stone step: eased across, lifted in the middle, then it lands with a little settle
                // and a puff of dust.
                for (var t = 0f; t < 1f; t += Time.deltaTime / StepTime)
                {
                    var e = t * t * (3f - 2f * t);
                    SetWorldPosition(Vector3.Lerp(from, to, e));
                    _body.transform.localPosition = _bodyRest + Vector3.up * (Mathf.Sin(t * Mathf.PI) * HopHeight);
                    yield return null;
                }

                SetWorldPosition(to);
                Fx.Dust(to, 3, 0.18f);
                Sfx.Play("unit_step", to, pitch: 0.08f);
                for (var t = 0f; t < 1f; t += Time.deltaTime / 0.06f)
                {
                    _body.transform.localPosition = _bodyRest + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.02f);
                    yield return null;
                }

                from = to;
            }

            _body.transform.localPosition = _bodyRest;
        }

        /// <summary>Shoved: a fast slide with no hop. If it was blocked, a short bump towards the obstacle and back.</summary>
        public IEnumerator Slide(GridPos from, GridPos to, GridPos? blockedDir)
        {
            if (from == to)
            {
                if (blockedDir.HasValue)
                    yield return Lunge(WorldOf(from + blockedDir.Value), null);
                yield break;
            }

            var a = WorldOf(from);
            var b = WorldOf(to);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.12f)
            {
                SetWorldPosition(Vector3.Lerp(a, b, t));
                yield return null;
            }

            SetWorldPosition(b);
        }

        /// <summary>
        /// An attack towards the target: a wind-up (leaning back), a fast strike, a brief freeze on impact
        /// (hit-stop) and the way back. A ranged attacker only recoils and fires a projectile instead.
        /// <paramref name="onImpact"/> runs when the blow or the projectile lands; without it, this is just
        /// a gesture (placing a mine, raising the dead...), so nothing flies and nothing freezes.
        /// </summary>
        public IEnumerator Lunge(Vector3 target, System.Action onImpact)
        {
            var home = transform.position;
            var toward = (target - home).normalized;
            var ranged = Unit.IsRanged;

            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.09f)
            {
                transform.position = home - toward * (0.06f * Mathf.Sin(t * Mathf.PI * 0.5f));
                yield return null;
            }

            var windUp = transform.position;
            var reach = Vector3.Lerp(home, target, ranged ? 0.1f : 0.42f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.06f)
            {
                transform.position = Vector3.Lerp(windUp, reach, 1f - (1f - t) * (1f - t));
                yield return null;
            }

            transform.position = reach;
            if (onImpact != null)
            {
                if (ranged)
                    yield return Fx.Projectile(BodyCentre, target + Vector3.up * 0.25f, new Color(1f, 0.95f, 0.8f), onImpact);
                else
                {
                    Fx.Impact(Vector3.Lerp(home, target, 0.75f) + Vector3.up * 0.25f, new Color(1f, 0.9f, 0.7f));
                    Sfx.Play("unit_attack", target);
                    onImpact();
                    yield return new WaitForSeconds(0.06f);
                }
            }

            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.16f)
            {
                transform.position = Vector3.Lerp(reach, home, t * t * (3f - 2f * t));
                yield return null;
            }

            transform.position = home;
        }

        /// <summary>Struck: a white flash fading to red, stone chips flying off and a hard shake.</summary>
        public IEnumerator Hit()
        {
            var home = transform.position;
            Fx.Chips(BodyCentre, StoneColor);
            Sfx.Play("unit_hurt", home);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.24f)
            {
                // White for the first instant, then a red tint that fades back.
                SetStatueFloat("_Flash", Mathf.Clamp01(1f - t * 4f));
                _body.color = Color.Lerp(Color.white, new Color(1f, 0.45f, 0.4f), Mathf.Clamp01(t * 3f) * (1f - t));
                transform.position = home + Vector3.right * (Mathf.Sin(t * Mathf.PI * 7f) * 0.05f * (1f - t));
                yield return null;
            }

            SetStatueFloat("_Flash", 0f);
            transform.position = home;
            ApplyColor();
        }

        /// <summary>
        /// Crumbles: chunks of stone break off and fall around it in a cloud of dust while the statue sinks
        /// and fades. Units that leave remains turn into a bone pile instead of vanishing.
        /// </summary>
        public IEnumerator Die()
        {
            SetSelected(false);
            SetBarsVisible(false);
            Fx.Rubble(transform.position, StoneColor, (BodyCentre - transform.position).y * 2f);
            Fx.Shake(0.04f, 0.18f);
            Sfx.Play("unit_die", transform.position);
            var start = _body.color;
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
            {
                _body.transform.localPosition = _bodyRest + Vector3.down * (t * 0.15f);
                _body.color = new Color(start.r, start.g, start.b, 1f - t);
                _shadowFade = 1f - t;
                if (_base != null)
                    _base.color = new Color(1f, 1f, 1f, 1f - t);
                yield return null;
            }

            SetShadowsEnabled(false);
            if (_base != null)
                _base.enabled = false;

            if (!Unit.Stats.LeavesRemains && !Unit.LeftPile)
            {
                gameObject.SetActive(false);
                yield break;
            }

            _body.sprite = PlaceholderArt.Bones;
            _body.transform.localPosition = _bodyRest;
            _showingBones = true;
            ApplyColor();
            SetGridPosition(Unit.Pos);
        }

        /// <summary>
        /// Pushed off the roof over the void: it drops past the edge and starts to tumble, then, behind the
        /// tower (<paramref name="behindOrder"/>), is pulled across to the black hole, spinning faster and
        /// shrinking until it is gone. <paramref name="onSwallowed"/> runs as it vanishes.
        /// </summary>
        public IEnumerator FallIntoVoid(Vector3 hole, int behindOrder, System.Action onSwallowed)
        {
            SetSelected(false);
            SetBarsVisible(false);
            SetShadowsEnabled(false);
            if (_base != null)
                _base.enabled = false;
            Fx.Dust(transform.position);
            Sfx.Play("unit_die", transform.position);
            var body = _body.transform;
            var scale = transform.localScale;
            var spin = transform.position.x < hole.x ? -1f : 1f;

            var velocity = Vector3.zero;
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
            {
                velocity += Vector3.down * 5f * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                body.Rotate(0f, 0f, spin * 150f * Time.deltaTime);
                yield return null;
            }

            // Swept sideways first, then up into the hole, faster and faster.
            _body.sortingOrder = behindOrder;
            var from = transform.position;
            var bend = new Vector3(Mathf.Lerp(from.x, hole.x, 0.35f), from.y - 0.4f, from.z);
            var start = _body.color;
            for (var t = 0f; t < 1f; t += Time.deltaTime / 1.1f)
            {
                var e = t * t;
                transform.position = Vector3.Lerp(Vector3.Lerp(from, bend, e), Vector3.Lerp(bend, hole, e), e);
                body.Rotate(0f, 0f, spin * (150f + 650f * t) * Time.deltaTime);
                transform.localScale = scale * Mathf.Lerp(1f, 0.06f, e);
                _body.color = new Color(start.r, start.g, start.b, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t)));
                yield return null;
            }

            onSwallowed?.Invoke();
            body.localRotation = Quaternion.identity;
            transform.localScale = scale;
            _body.color = start;
        }

        /// <summary>A bone pile pulls itself back up into a statue.</summary>
        public IEnumerator Raise()
        {
            UndoCrumble();
            _body.color = new Color(0.75f, 0.55f, 1f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
            {
                _body.transform.localPosition = _bodyRest + Vector3.down * ((1f - t) * 0.25f);
                yield return null;
            }

            _body.transform.localPosition = _bodyRest;
            ApplyColor();
            RefreshHp(Unit.Hp);
            SetBarsVisible(true);
        }

        /// <summary>Stands a crumbled statue straight back up, with no animation (the effects playground's death demo).</summary>
        public void Restore()
        {
            gameObject.SetActive(true);
            UndoCrumble();
            _body.transform.localPosition = _bodyRest;
            ApplyColor();
            RefreshHp(Unit.Hp);
            SetBarsVisible(true);
            RefreshStatus();
        }

        /// <summary>Puts back what Die took away: the statue sprite, its shadow and base, and its place on the board.</summary>
        private void UndoCrumble()
        {
            _body.sprite = PlaceholderArt.Statue(Unit.Stats.Kind);
            _showingBones = false;
            _shadowFade = 1f;
            SetShadowsEnabled(true);
            if (_base != null)
            {
                _base.color = Color.white;
                _base.enabled = true;
            }
            SetGridPosition(Unit.Pos);
        }

        private void SetBarsVisible(bool visible)
        {
            _barsVisible = visible;
            _barBack.enabled = visible;
            RefreshHp(_shownHp);
            if (!visible)
            {
                HideAp();
                foreach (var icon in _statusIcons)
                    icon.enabled = false;
            }
            if (_labelRenderer != null)
                _labelRenderer.enabled = visible;
            if (_labelPlate != null)
                _labelPlate.enabled = visible;
        }
    }
}
