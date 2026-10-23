using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArmyRoguelike.View
{
    /// <summary>Turns mouse input into battle actions, plays the animations and draws the HUD.</summary>
    public sealed class BattleController : MonoBehaviour
    {
        private static readonly Color ZoneColor = new Color(0.45f, 0.75f, 1f, 0.35f);
        private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color MoveColor = new Color(0.45f, 0.75f, 1f, 0.4f);
        private static readonly Color AttackColor = new Color(1f, 0.3f, 0.25f, 0.55f);
        private static readonly Color SelectedColor = new Color(1f, 0.9f, 0.4f, 0.5f);
        private static readonly Color IntentTargetColor = new Color(1f, 0.55f, 0.2f, 0.6f);
        private static readonly Color AuraColor = new Color(0.9f, 0.35f, 0.3f, 0.25f);

        // Wolf tints: warm while the leader's aura reaches them, pale and cold once the leader is dead.
        private static readonly Color NearLeaderTint = new Color(1.25f, 0.8f, 0.75f);
        private static readonly Color LeaderlessTint = new Color(0.8f, 0.9f, 1.15f);

        private const float ReferenceHeight = 720f;
        private const float PauseBetweenEnemies = 0.25f;

        private BoardView _board;
        private IntentView _intent;
        private Camera _camera;
        private Unit _toPlace;
        private Unit _selected;
        private Unit _hoveredUnit;
        private GridPos? _hovered;
        private bool _busy;
        private string _hint = "";
        private Rect _hudRect;

        private Battle Battle => _board.Battle;

        public void Init(BoardView board, Camera cam)
        {
            _board = board;
            _camera = cam;
            _intent = IntentView.Create(transform);
            _toPlace = NextUnplaced();
            RefreshUnitStates();
        }

        private void Update()
        {
            if (_board == null || _camera == null || Mouse.current == null)
                return;

            _hovered = HoveredTile();
            _hoveredUnit = _hovered.HasValue ? Battle.UnitAt(_hovered.Value) : null;
            var clicked = !_busy && Mouse.current.leftButton.wasPressedThisFrame && _hovered.HasValue;

            _intent.Hide();
            switch (Battle.Phase)
            {
                case Phase.Placement:
                    UpdatePlacement(clicked);
                    break;
                case Phase.PlayerTurn when !_busy:
                    UpdatePlayerTurn(clicked);
                    break;
                default:
                    _board.ClearHighlights();
                    break;
            }
        }

        // ---------- Placement ----------

        private void UpdatePlacement(bool clicked)
        {
            if (clicked)
            {
                var tile = _hovered.Value;
                if (_hoveredUnit != null && _hoveredUnit.Team == Team.Player && _hoveredUnit != _toPlace)
                {
                    // Clicking a placed statue picks it up again.
                    _toPlace = _hoveredUnit;
                }
                else if (_toPlace != null && Battle.Place(_toPlace, tile))
                {
                    _board.SyncUnits();
                    _toPlace = NextUnplaced();
                }
            }

            _board.ClearHighlights();
            foreach (var p in Battle.Board.AllTiles())
                if (Battle.IsPlayerZone(p) && !Battle.Board.IsObstacle(p))
                    _board.SetHighlight(p, ZoneColor);
            if (_hovered.HasValue && _toPlace != null && Battle.CanPlace(_toPlace, _hovered.Value))
                _board.SetHighlight(_hovered.Value, HoverColor);
        }

        private Unit NextUnplaced() => Battle.Units.FirstOrDefault(u => u.Team == Team.Player && !u.IsPlaced);

        private void StartBattle()
        {
            if (!Battle.StartBattle())
                return;
            _toPlace = null;
            _hint = "Click a statue to select it.";
            RefreshUnitStates();
        }

        // ---------- Player turn ----------

        private void UpdatePlayerTurn(bool clicked)
        {
            if (clicked)
                HandlePlayerClick(_hovered.Value);
            if (_busy || Battle.Phase != Phase.PlayerTurn)
                return;
            DrawPlayerHighlights();
        }

        private void HandlePlayerClick(GridPos tile)
        {
            var target = _hoveredUnit;

            if (target != null && target.Team == Team.Player)
            {
                if (Battle.CanAct(target))
                    Select(target);
                else if (target.IsDone)
                    _hint = $"{target.Stats.Name} has finished its turn.";
                else if (Battle.ActiveUnit != null)
                    _hint = $"Finish the {Battle.ActiveUnit.Stats.Name}'s turn first (Done).";
                return;
            }

            if (_selected == null)
                return;

            if (target != null && Battle.CanAttack(_selected, target))
            {
                var attack = Battle.PlayerAttack(_selected, target);
                if (attack != null)
                    StartCoroutine(PlayPlayerAction(attack));
                return;
            }

            if (Battle.GetReachable(_selected).ContainsKey(tile))
            {
                var move = Battle.PlayerMove(_selected, tile);
                if (move != null)
                    StartCoroutine(PlayPlayerAction(move));
                return;
            }

            if (target != null && target.Team == Team.Enemy)
            {
                _hint = _selected.Ap < Battle.AttackCost
                    ? $"Attacking needs {Battle.AttackCost} AP."
                    : "Out of range.";
                return;
            }

            // Clicking elsewhere drops the selection, unless that unit is mid-turn.
            if (Battle.ActiveUnit != _selected)
                Select(null);
        }

        private void Select(Unit unit)
        {
            if (_selected != null)
                _board.ViewOf(_selected)?.SetSelected(false);
            _selected = unit;
            if (_selected != null)
                _board.ViewOf(_selected)?.SetSelected(true);
            _hint = unit == null ? "Click a statue to select it." : "Click a blue tile to move or a red one to attack.";
        }

        private IEnumerator PlayPlayerAction(BattleEvent ev)
        {
            _busy = true;
            _board.ClearHighlights();
            yield return PlayEvent(ev);

            // A unit with no AP left has nothing more to do.
            if (_selected != null && _selected.IsAlive && _selected.Ap == 0)
                FinishSelectedUnit();
            RefreshUnitStates();
            _busy = false;
        }

        private void FinishSelectedUnit()
        {
            if (_selected == null)
                return;
            Battle.EndUnitTurn(_selected);
            Select(null);
            RefreshUnitStates();
        }

        private void EndTurn()
        {
            Select(null);
            StartCoroutine(PlayEnemyTurn());
        }

        private IEnumerator PlayEnemyTurn()
        {
            _busy = true;
            _board.ClearHighlights();
            foreach (var u in Battle.Living(Team.Player))
                _board.ViewOf(u)?.SetDimmed(true);

            var events = Battle.EndPlayerTurn();
            Unit lastActor = null;
            foreach (var ev in events)
            {
                var actor = ev is MoveEvent m ? m.Unit : ((AttackEvent)ev).Attacker;
                if (lastActor != null && actor != lastActor)
                    yield return new WaitForSeconds(PauseBetweenEnemies);
                lastActor = actor;
                yield return PlayEvent(ev);
            }

            _hint = "Your turn. Click a statue to select it.";
            RefreshUnitStates();
            _busy = false;
        }

        private void DrawPlayerHighlights()
        {
            _board.ClearHighlights();
            DrawEnemyHover();
            if (_selected == null)
                return;

            _board.SetHighlight(_selected.Pos, SelectedColor);
            var reachable = Battle.GetReachable(_selected);
            foreach (var p in reachable.Keys)
                _board.SetHighlight(p, MoveColor);
            foreach (var enemy in Battle.Living(Team.Enemy))
                if (Battle.CanAttack(_selected, enemy))
                    _board.SetHighlight(enemy.Pos, AttackColor);

            if (_hovered.HasValue && reachable.ContainsKey(_hovered.Value))
                _board.SetHighlight(_hovered.Value, HoverColor);
        }

        /// <summary>While hovering an enemy: its intended target, and for the pack leader its aura.</summary>
        private void DrawEnemyHover()
        {
            var enemy = _hoveredUnit;
            if (enemy == null || enemy.Team != Team.Enemy)
                return;

            if (enemy.IsPackLeader)
                foreach (var p in Battle.Board.AllTiles())
                    if (p.StepsTo(enemy.Pos) <= Battle.LeaderAuraRange && !Battle.Board.IsObstacle(p))
                        _board.SetHighlight(p, AuraColor);

            var target = EnemyAI.ChooseTarget(Battle, enemy);
            if (target == null)
                return;
            _board.SetHighlight(target.Pos, IntentTargetColor);
            var lift = Vector3.up * 0.15f;
            _intent.Show(IsoMath.GridToWorld(enemy.Pos) + lift, IsoMath.GridToWorld(target.Pos) + lift);
        }

        // ---------- Animation ----------

        private IEnumerator PlayEvent(BattleEvent ev)
        {
            switch (ev)
            {
                case MoveEvent move:
                {
                    var view = _board.ViewOf(move.Unit);
                    yield return view.MoveAlong(move.Path);
                    break;
                }
                case AttackEvent attack:
                {
                    var attacker = _board.ViewOf(attack.Attacker);
                    var target = _board.ViewOf(attack.Target);
                    yield return attacker.Lunge(target.transform.position, () =>
                    {
                        DamagePopup.Spawn(target.TopWorld + Vector3.up * 0.1f, attack.Damage);
                        target.RefreshHp(attack.TargetHpAfter);
                    });
                    attacker.SetGridPosition(attack.Attacker.Pos);
                    yield return attack.Killed ? target.Die() : target.Hit();
                    break;
                }
            }
        }

        /// <summary>Dims statues that have finished their turn and tints wolves by their pack state.</summary>
        private void RefreshUnitStates()
        {
            foreach (var u in Battle.Units.Where(u => u.IsPlaced && u.IsAlive))
            {
                var view = _board.ViewOf(u);
                if (view == null)
                    continue;
                view.SetDimmed(u.Team == Team.Player && Battle.Phase == Phase.PlayerTurn && u.IsDone);
                switch (Battle.GetPackState(u))
                {
                    case PackState.NearLeader:
                        view.SetTint(NearLeaderTint);
                        break;
                    case PackState.Leaderless:
                        view.SetTint(LeaderlessTint);
                        break;
                    default:
                        view.SetTint(Color.white);
                        break;
                }
            }
        }

        // ---------- Input helpers ----------

        private GridPos? HoveredTile()
        {
            var screen = Mouse.current.position.ReadValue();
            if (IsOverHud(screen))
                return null;
            var world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            var p = IsoMath.WorldToGrid(world);
            return Battle.Board.InBounds(p) ? p : (GridPos?)null;
        }

        private bool IsOverHud(Vector2 screen)
        {
            var scale = Screen.height / ReferenceHeight;
            var gui = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
            return _hudRect.Contains(gui);
        }

        // ---------- HUD ----------

        private void OnGUI()
        {
            if (_board == null)
                return;
            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            switch (Battle.Phase)
            {
                case Phase.Placement:
                    DrawPlacementHud();
                    break;
                case Phase.PlayerTurn:
                case Phase.EnemyTurn:
                    DrawTurnHud();
                    break;
                default:
                    DrawEndHud();
                    break;
            }
        }

        private void DrawPlacementHud()
        {
            var army = Battle.Units.Where(u => u.Team == Team.Player).ToList();
            _hudRect = new Rect(10, 10, 230, 70 + army.Count * 30 + 40);
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            GUILayout.Label("Place your army in the blue tiles.");
            GUILayout.Label("Click a placed statue to move it.");
            foreach (var u in army)
            {
                var label = $"{u.Stats.Name}{(u.IsPlaced ? "  (placed)" : "")}{(u == _toPlace ? "  <" : "")}";
                if (GUILayout.Button(label))
                    _toPlace = u;
            }

            GUILayout.Space(6);
            GUI.enabled = Battle.AllPlaced;
            if (GUILayout.Button("Start battle"))
                StartBattle();
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawTurnHud()
        {
            var playerTurn = Battle.Phase == Phase.PlayerTurn && !_busy;
            _hudRect = new Rect(10, 10, 280, 300);
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            GUILayout.Label($"Turn {Battle.TurnNumber}: {(playerTurn ? "your turn" : "enemy turn")}");

            if (_selected != null)
            {
                GUILayout.Label(UnitLine(_selected));
                GUILayout.Label($"AP {_selected.Ap}   (move {Battle.MoveCost} per tile, attack {Battle.AttackCost})");
            }

            var hoverText = HoverText();
            if (hoverText != null)
                GUILayout.Label(hoverText);

            if (playerTurn)
            {
                GUILayout.Label(_hint);
                GUI.enabled = _selected != null;
                if (GUILayout.Button(_selected != null ? $"Done with {_selected.Stats.Name}" : "Done"))
                    FinishSelectedUnit();
                GUI.enabled = true;
                if (GUILayout.Button("End turn"))
                    EndTurn();
            }

            GUILayout.EndArea();
        }

        private void DrawEndHud()
        {
            const float w = 220, h = 90;
            _hudRect = new Rect((Screen.width / (Screen.height / ReferenceHeight) - w) * 0.5f, 120, w, h);
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUILayout.Label(Battle.Phase == Phase.Victory ? "Victory!" : "Defeat", style);
            if (GUILayout.Button("Restart"))
                GameBootstrap.Restart();
            GUILayout.EndArea();
        }

        private string HoverText()
        {
            if (_hoveredUnit != null && _hoveredUnit != _selected)
            {
                var text = "Hover: " + UnitLine(_hoveredUnit);
                switch (Battle.GetPackState(_hoveredUnit))
                {
                    case PackState.NearLeader:
                        text += $"\n+1 damage: near the Pack Leader";
                        break;
                    case PackState.Leaderless:
                        text += "\n-1 damage: the Pack Leader is dead";
                        break;
                }

                if (_hoveredUnit.IsPackLeader)
                    text += $"\nWolves within {Battle.LeaderAuraRange} tiles get +1 damage";
                if (_hoveredUnit.Team == Team.Enemy && EnemyAI.ChooseTarget(Battle, _hoveredUnit) is Unit target)
                    text += $"\nWants to attack: {target.Stats.Name}";
                return text;
            }
            if (_selected != null && _hovered.HasValue && Battle.GetReachable(_selected).TryGetValue(_hovered.Value, out var cost))
                return $"Move here: {cost} AP";
            return null;
        }

        private string UnitLine(Unit u) =>
            $"{u.Stats.Name}  HP {u.Hp}/{u.Stats.MaxHp}  DMG {Battle.EffectiveDamage(u)}  RNG {u.Stats.Range}";
    }
}
