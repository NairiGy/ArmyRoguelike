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

        private SpriteRenderer _body;
        private TextMesh _label;
        private MeshRenderer _labelRenderer;
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
            _baseColor = unit.Team == Team.Player ? PlaceholderArt.PlayerStone : PlaceholderArt.EnemyStone;
            _body.color = _baseColor;
            if (unit.IsPackLeader)
                _body.transform.localScale = Vector3.one * 1.25f;
            else if (unit.Size > 1)
                _body.transform.localScale = Vector3.one * 1.3f;
            _bodyRest = _body.transform.localPosition;

            var top = _body.sprite.bounds.max.y * _body.transform.localScale.y;

            if (unit.Team == Team.Player)
            {
                _label = new GameObject("Label").AddComponent<TextMesh>();
                _label.transform.SetParent(transform, false);
                _label.transform.localPosition = new Vector3(0f, 0.27f, 0f);
                _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _label.text = unit.Stats.Name.Substring(0, 1);
                _label.fontSize = 48;
                _label.characterSize = 0.05f;
                _label.anchor = TextAnchor.MiddleCenter;
                _label.alignment = TextAlignment.Center;
                _label.color = new Color(0.08f, 0.08f, 0.1f);
                _labelRenderer = _label.GetComponent<MeshRenderer>();
                _labelRenderer.sharedMaterial = _label.font.material;
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
            // Bones lie flat on the tile, under anything standing there.
            _body.sortingOrder = _showingBones ? order - 4 : order;
            if (_labelRenderer != null)
                _labelRenderer.sortingOrder = order + 1;
            _barBack.sortingOrder = order + 2;
            _barFill.sortingOrder = order + 3;
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
            if (!_selected)
                return;
            _bobTime += Time.deltaTime;
            _body.transform.localPosition = _bodyRest + Vector3.up * (Mathf.Abs(Mathf.Sin(_bobTime * 5f)) * 0.06f);
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
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
            {
                _body.transform.localPosition = _bodyRest + Vector3.down * (t * 0.15f);
                _body.color = new Color(start.r, start.g, start.b, 1f - t);
                yield return null;
            }

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
            if (_labelRenderer != null)
                _labelRenderer.enabled = visible;
        }
    }
}
