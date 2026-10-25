using System.Collections;
using System.Collections.Generic;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// A statue on the board: sprite, type letter and HP bar. All statues share the same simple
    /// animations: bob when selected, hop between tiles, lunge to attack, shake when hit, sink when killed.
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
        private float _bobTime;
        private Vector3 _bodyRest;

        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.5f);

        private SpriteRenderer _body;
        private SpriteRenderer _shadow;
        private TextMesh _label;
        private MeshRenderer _labelRenderer;
        private SpriteRenderer _labelPlate;
        private SpriteRenderer _barBack;
        private SpriteRenderer _barFill;
        private int _shownHp;

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
                _body.flipX = false;
            }
            _body.color = _baseColor;
            if (unit.IsPackLeader)
                _body.transform.localScale = Vector3.one * 1.25f;
            else if (unit.Size > 1)
                _body.transform.localScale = Vector3.one * 1.3f;
            else if (unit.Stats.Tier > 1)
                // Evolved statues stand a little taller.
                _body.transform.localScale = Vector3.one * (1f + 0.06f * (unit.Stats.Tier - 1));
            _bodyRest = _body.transform.localPosition;

            // A soft shadow on the floor grounds the statue; it stays put while the body bobs and lunges.
            _shadow = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            _shadow.transform.SetParent(transform, false);
            _shadow.transform.localScale = Vector3.one * (0.75f * Unit.Size);
            _shadow.sprite = PlaceholderArt.SoftShadow;
            _shadow.color = ShadowColor;

            var top = _body.sprite.bounds.max.y * _body.transform.localScale.y;

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
            _barFill = MakeBar("HpFill", unit.Team == Team.Player ? new Color(0.35f, 0.8f, 0.4f) : new Color(0.85f, 0.3f, 0.25f), top + 0.08f);

            SetGridPosition(unit.Pos);
            RefreshHp(unit.Hp);
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
            // Bones lie flat on the tile, under anything standing there.
            _body.sortingOrder = _showingBones ? order - 4 : order;
            if (_labelPlate != null)
                _labelPlate.sortingOrder = order + 1;
            if (_labelRenderer != null)
                _labelRenderer.sortingOrder = order + 2;
            _barBack.sortingOrder = order + 3;
            _barFill.sortingOrder = order + 4;
        }

        public void RefreshHp(int hp)
        {
            _shownHp = Mathf.Max(0, hp);
            var frac = (float)_shownHp / Unit.MaxHp;
            var t = _barFill.transform;
            t.localScale = new Vector3(BarWidth * frac * BarScale, BarHeight, 1f);
            t.localPosition = new Vector3(-BarWidth * BarScale * (1f - frac) * 0.5f, _barBack.transform.localPosition.y, 0f);
        }

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
        private static readonly Color SnareColor = new Color(0.45f, 0.85f, 0.3f);
        private static readonly Color SlowColor = new Color(0.65f, 0.8f, 1f);
        private static readonly Color PoisonColor = new Color(0.75f, 0.35f, 0.95f);
        private static readonly Color LootColor = new Color(1f, 0.82f, 0.1f);

        private readonly List<SpriteRenderer> _statusIcons = new List<SpriteRenderer>();

        /// <summary>
        /// Small coloured squares to the right of the HP bar: cyan = ward shield, green = snared,
        /// light blue = slowed, gold = carrying stolen gold, purple = poisoned. The HUD hover text spells them out.
        /// </summary>
        public void RefreshStatus()
        {
            var colors = new List<Color>();
            if (Unit.IsAlive)
            {
                if (Unit.Shield > 0)
                    colors.Add(ShieldColor);
                if (Unit.IsSnared)
                    colors.Add(SnareColor);
                if (Unit.IsSlowed)
                    colors.Add(SlowColor);
                if (Unit.CarryingLoot > 0)
                    colors.Add(LootColor);
                if (Unit.PoisonTurns > 0)
                    colors.Add(PoisonColor);
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
                icon.sortingOrder = _barFill.sortingOrder + 1;
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
                pip.sortingOrder = _barFill.sortingOrder + 1;
            }
        }

        public void HideAp()
        {
            foreach (var pip in _pips)
                pip.enabled = false;
        }

        /// <summary>Top of the statue in world space, for pop-ups.</summary>
        public Vector3 TopWorld => _barBack.transform.position;

        public void SetSelected(bool selected)
        {
            _selected = selected;
            _bobTime = 0f;
            if (!selected)
                _body.transform.localPosition = _bodyRest;
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
            if (material != null && PlaceholderArt.IsPainted(Unit.Stats.Kind))
                _body.sharedMaterial = material;
        }

        private void ApplyColor()
        {
            var c = _baseColor * _tint;
            if (_dimmed)
                c *= 0.6f;
            c.a = 1f;
            _body.color = c;
        }

        private void Update()
        {
            Breathe();
            if (!_selected)
                return;
            _bobTime += Time.deltaTime;
            _body.transform.localPosition = _bodyRest + Vector3.up * (Mathf.Abs(Mathf.Sin(_bobTime * 5f)) * 0.06f);
        }

        // ---------- Idle life ----------

        private Vector3 _bodyScale;
        private float _breathPhase = -1f;
        private float _nextDust;

        /// <summary>
        /// Living statues are never quite still: a slow, tiny rise and fall (out of step between units),
        /// and now and then a few specks of stone dust crumble off.
        /// </summary>
        private void Breathe()
        {
            if (!PlaceholderArt.IsPainted(Unit.Stats.Kind) || _showingBones || !Unit.IsAlive)
                return;
            if (_breathPhase < 0f)
            {
                _breathPhase = Random.value * 10f;
                _bodyScale = _body.transform.localScale;
                _nextDust = Time.time + Random.Range(2f, 8f);
            }

            var breath = Mathf.Sin(Time.time * 1.3f + _breathPhase);
            _body.transform.localScale = new Vector3(_bodyScale.x * (1f - 0.006f * breath), _bodyScale.y * (1f + 0.014f * breath), _bodyScale.z);

            if (Time.time < _nextDust)
                return;
            _nextDust = Time.time + Random.Range(5f, 12f);
            StartCoroutine(Crumble());
        }

        private IEnumerator Crumble()
        {
            var height = _body.sprite.bounds.max.y * _bodyScale.y;
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
                for (var t = 0f; t < 1f; t += Time.deltaTime / StepTime)
                {
                    var p = Vector3.Lerp(from, to, t);
                    SetWorldPosition(p);
                    _body.transform.localPosition = _bodyRest + Vector3.up * (Mathf.Sin(t * Mathf.PI) * HopHeight);
                    yield return null;
                }

                SetWorldPosition(to);
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

        /// <summary>Quick lunge towards the target and back. <paramref name="onImpact"/> runs at the furthest point.</summary>
        public IEnumerator Lunge(Vector3 target, System.Action onImpact)
        {
            var home = transform.position;
            var reach = Vector3.Lerp(home, target, Unit.Stats.IsRanged ? 0.15f : 0.4f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.08f)
            {
                transform.position = Vector3.Lerp(home, reach, t);
                yield return null;
            }

            onImpact?.Invoke();
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.14f)
            {
                transform.position = Vector3.Lerp(reach, home, t);
                yield return null;
            }

            transform.position = home;
        }

        public IEnumerator Hit()
        {
            var home = transform.position;
            _body.color = new Color(1f, 0.45f, 0.4f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.18f)
            {
                transform.position = home + Vector3.right * (Mathf.Sin(t * Mathf.PI * 6f) * 0.04f * (1f - t));
                yield return null;
            }

            transform.position = home;
            ApplyColor();
        }

        /// <summary>Sinks and fades out. Units that leave remains turn into a bone pile instead of vanishing.</summary>
        public IEnumerator Die()
        {
            SetSelected(false);
            SetBarsVisible(false);
            var start = _body.color;
            var shadowStart = _shadow.color;
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
            {
                _body.transform.localPosition = _bodyRest + Vector3.down * (t * 0.15f);
                _body.color = new Color(start.r, start.g, start.b, 1f - t);
                _shadow.color = new Color(0f, 0f, 0f, shadowStart.a * (1f - t));
                yield return null;
            }

            _shadow.enabled = false;

            if (!Unit.Stats.LeavesRemains)
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

        /// <summary>A bone pile pulls itself back up into a statue.</summary>
        public IEnumerator Raise()
        {
            _body.sprite = PlaceholderArt.Statue(Unit.Stats.Kind);
            _showingBones = false;
            _shadow.color = ShadowColor;
            _shadow.enabled = true;
            SetGridPosition(Unit.Pos);
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

        private void SetBarsVisible(bool visible)
        {
            _barBack.enabled = visible;
            _barFill.enabled = visible;
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
