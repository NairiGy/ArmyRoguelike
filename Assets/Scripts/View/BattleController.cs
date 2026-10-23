using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArmyRoguelike.View
{
    /// <summary>Turns mouse input into battle actions and draws the HUD.</summary>
    public sealed class BattleController : MonoBehaviour
    {
        private static readonly Color ZoneColor = new Color(0.45f, 0.75f, 1f, 0.35f);
        private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.55f);

        private const float ReferenceHeight = 720f;

        private BoardView _board;
        private Camera _camera;
        private Unit _toPlace;
        private Rect _hudRect;

        private Battle Battle => _board.Battle;

        public void Init(BoardView board, Camera cam)
        {
            _board = board;
            _camera = cam;
            _toPlace = NextUnplaced();
        }

        private void Update()
        {
            if (_board == null || _camera == null || Mouse.current == null)
                return;

            var hovered = HoveredTile();
            var clicked = Mouse.current.leftButton.wasPressedThisFrame && hovered.HasValue;

            if (Battle.Phase == Phase.Placement)
                UpdatePlacement(hovered, clicked);
            else
                _board.ClearHighlights();
        }

        // ---------- Placement ----------

        private void UpdatePlacement(GridPos? hovered, bool clicked)
        {
            if (clicked)
            {
                var tile = hovered.Value;
                var occupant = Battle.UnitAt(tile);
                if (occupant != null && occupant.Team == Team.Player && occupant != _toPlace)
                {
                    // Clicking a placed statue picks it up again.
                    _toPlace = occupant;
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
            if (hovered.HasValue && _toPlace != null && Battle.CanPlace(_toPlace, hovered.Value))
                _board.SetHighlight(hovered.Value, HoverColor);
        }

        private Unit NextUnplaced() => Battle.Units.FirstOrDefault(u => u.Team == Team.Player && !u.IsPlaced);

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

            _hudRect = new Rect(10, 10, 230, 0);
            switch (Battle.Phase)
            {
                case Phase.Placement:
                    DrawPlacementHud();
                    break;
                default:
                    _hudRect.height = 40;
                    GUI.Box(_hudRect, $"Turn {Battle.TurnNumber}: {Battle.Phase}");
                    break;
            }
        }

        private void DrawPlacementHud()
        {
            var army = Battle.Units.Where(u => u.Team == Team.Player).ToList();
            _hudRect.height = 70 + army.Count * 30 + 40;
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
            {
                Battle.StartBattle();
                _toPlace = null;
            }

            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
