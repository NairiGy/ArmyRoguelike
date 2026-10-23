using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>A statue on the board: sprite, type letter and HP bar.</summary>
    public sealed class UnitView : MonoBehaviour
    {
        private const float BarWidth = 0.4f;
        private const float BarHeight = 0.05f;

        public Unit Unit { get; private set; }

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
            _body.color = unit.Team == Team.Player ? PlaceholderArt.PlayerStone : PlaceholderArt.EnemyStone;
            if (unit.IsPackLeader)
                _body.transform.localScale = Vector3.one * 1.25f;

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
            sr.transform.localScale = new Vector3(BarWidth, BarHeight, 1f);
            return sr;
        }

        public void SetGridPosition(GridPos p) => SetWorldPosition(IsoMath.GridToWorld(p));

        public void SetWorldPosition(Vector3 world)
        {
            transform.position = world;
            var order = IsoMath.SortingOrder(world) + 5;
            _body.sortingOrder = order;
            if (_labelRenderer != null)
                _labelRenderer.sortingOrder = order + 1;
            _barBack.sortingOrder = order + 2;
            _barFill.sortingOrder = order + 3;
        }

        public void RefreshHp(int hp)
        {
            _shownHp = Mathf.Max(0, hp);
            var frac = (float)_shownHp / Unit.Stats.MaxHp;
            var t = _barFill.transform;
            t.localScale = new Vector3(BarWidth * frac, BarHeight, 1f);
            t.localPosition = new Vector3(-BarWidth * (1f - frac) * 0.5f, _barBack.transform.localPosition.y, 0f);
        }
    }
}
