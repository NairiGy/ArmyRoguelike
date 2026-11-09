using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArmyRoguelike.View
{
    /// <summary>Where a battle sits in the run, for the HUD and the playtest log.</summary>
    public readonly struct BattleContext
    {
        public readonly int Step;
        public readonly int Steps;
        public readonly string Encounter;
        public readonly int Gold;
        public readonly int RunSeed;
        public readonly bool Ambush;
        public readonly bool Boss;
        public readonly string Layout;
        public readonly string LayoutRule;

        /// <summary>A test battle from the test setup screen, outside any run.</summary>
        public readonly bool Test;

        /// <summary>An elite fight: a harder rematch that pays an item pick.</summary>
        public readonly bool Elite;

        public BattleContext(int step, int steps, string encounter, int gold, int runSeed, bool ambush, bool boss,
            string layout = "", string layoutRule = "", bool test = false, bool elite = false)
        {
            Test = test;
            Elite = elite;
            Step = step;
            Steps = steps;
            Encounter = encounter;
            Gold = gold;
            RunSeed = runSeed;
            Ambush = ambush;
            Boss = boss;
            Layout = layout;
            LayoutRule = layoutRule;
        }

        public int GoldIfWon(int survivors) => Run.GoldFor(survivors) + (Ambush ? Run.AmbushBonusGold : 0);
    }

    /// <summary>Turns mouse input into battle actions, plays the animations and draws the HUD.</summary>
    public sealed class BattleController : MonoBehaviour
    {
        private static readonly Color ZoneColor = new Color(0.45f, 0.75f, 1f, 0.35f);
        private static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color MoveColor = new Color(0.45f, 0.75f, 1f, 0.4f);
        private static readonly Color AttackColor = new Color(1f, 0.3f, 0.25f, 0.55f);

        // Enemies the selected unit can walk up to and still attack this turn.
        private static readonly Color ReachAttackColor = new Color(1f, 0.55f, 0.4f, 0.3f);
        private static readonly Color SelectedColor = new Color(1f, 0.9f, 0.4f, 0.5f);
        private static readonly Color IntentTargetColor = new Color(1f, 0.55f, 0.2f, 0.6f);
        private static readonly Color AuraColor = new Color(0.9f, 0.35f, 0.3f, 0.25f);
        private static readonly Color SmashZoneColor = new Color(1f, 0.3f, 0.25f, 0.3f);
        private static readonly Color RockLandingColor = new Color(0.15f, 0.12f, 0.1f, 0.6f);
        private static readonly Color RaiseColor = new Color(0.7f, 0.4f, 1f, 0.6f);
        private static readonly Color BombZoneColor = new Color(1f, 0.45f, 0.1f, 0.4f);
        private static readonly Color MineSpotColor = new Color(1f, 0.9f, 0.3f, 0.5f);
        private static readonly Color OverwatchColor = new Color(0.95f, 0.2f, 0.45f, 0.22f);
        private static readonly Color CaltropsColor = new Color(0.55f, 0.55f, 0.6f, 0.6f);
        private static readonly Color CoverColor = new Color(0.3f, 0.75f, 0.95f, 0.2f);
        private static readonly Color SnareColor = new Color(0.45f, 0.85f, 0.35f, 0.55f);
        // Zone of control, while one of your units is selected: tiles next to enemies (their reach), move tiles
        // that end the unit's movement early, and a dotted link to each enemy holding the selected unit.
        private static readonly Color EnemyReachColor = new Color(0.9f, 0.25f, 0.2f, 0.18f);
        private static readonly Color CostlyMoveColor = new Color(1f, 0.6f, 0.2f, 0.45f);
        private static readonly Color EngagedLinkColor = new Color(0.95f, 0.3f, 0.25f, 0.9f);
        private static readonly Color ShroudColor = new Color(0.25f, 0.2f, 0.55f, 0.45f);
        // Placeholder (see the "Art needed" table): a tile lightning will strike at the end of the enemy turn.
        private static readonly Color StormMarkColor = new Color(0.55f, 0.8f, 1f, 0.75f);

        // Wolf tints: warm while the leader's aura reaches them, pale and cold once the leader is dead.
        private static readonly Color BondedTint = new Color(1.25f, 0.8f, 0.75f);
        private static readonly Color LeaderlessTint = new Color(0.8f, 0.9f, 1.15f);

        private const float ReferenceHeight = 720f;
        private const float PauseBetweenEnemies = 0.25f;
        private const float BoulderFlightTime = 0.45f;
        private const float BlastTime = 0.35f;

        private BoardView _board;
        private IntentView _intent;
        private Camera _camera;
        private Vector3 _cameraHome;
        private BattleContext _context;
        private Action _onContinue;
        private Unit _toPlace;
        private Unit _selected;
        private Unit _hoveredUnit;
        private GridPos? _hovered;
        private bool _busy;
        private bool _enemyTurnPlaying;
        private bool _confirmEndTurn;
        /// <summary>The special action being aimed (mine, fireball, snare...), or null for move/attack.</summary>
        private ActionKind? _action;
        private string _hint = "";
        private PlaytestLogger _logger;
        private string _markerNote;
        private float _markerNoteUntil;

        private Battle Battle => _board.Battle;

        public void Init(BoardView board, Camera cam, BattleContext context, Action onContinue)
        {
            _board = board;
            _camera = cam;
            _cameraHome = cam.transform.position;
            _context = context;
            _onContinue = onContinue;
            _intent = IntentView.Create(transform);
            _logger = new PlaytestLogger(board.Battle, context);
            _toPlace = NextUnplaced();
            RefreshUnitStates();
            if (context.Test)
                FxPlayground.Create(transform, board, () => _hovered);
        }

        /// <summary>File name of this battle's playtest log once saved, for the run log.</summary>
        /// <remarks>Saves first: "Continue" can be clicked while the last turn is still animating, before it saved.</remarks>
        public string LogFile => _logger?.Save() ?? _logger?.SavedFile;

        private void Update()
        {
            if (_board == null || _camera == null || Mouse.current == null)
                return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                _markerNote = $"Marker {_logger.AddMarker()} saved";
                _markerNoteUntil = Time.unscaledTime + 2f;
            }

            // Number keys pick the selected unit's special actions in the order the HUD lists them.
            if (keyboard != null && !_busy && _selected != null && Battle.CanAct(_selected))
            {
                var keys = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key };
                var actions = _selected.Actions.ToList();
                for (var i = 0; i < actions.Count && i < keys.Length; i++)
                    if (keys[i].wasPressedThisFrame)
                        ToggleAction(actions[i]);
            }

            _hovered = HoveredTile();
            _hoveredUnit = _hovered.HasValue ? Battle.UnitAt(_hovered.Value) : null;
            UpdateCardInput(keyboard);

            // Test battles only: L strikes the hovered tile with lightning, to try the effect (visual only).
            if (_context.Test && keyboard != null && keyboard.lKey.wasPressedThisFrame && _hovered.HasValue)
                StartCoroutine(LightningStrike.Strike(_board.transform, _hovered.Value));
            // Left-click chooses (selects a unit, opens a card); right-click acts (move, attack, use the aimed action).
            var clicked = !_busy && !_confirmEndTurn && Mouse.current.leftButton.wasPressedThisFrame && _hovered.HasValue;
            var ordered = !_busy && !_confirmEndTurn && Mouse.current.rightButton.wasPressedThisFrame && _hovered.HasValue;
            if (clicked)
                OpenCardFor(_hoveredUnit);

            _intent.Hide();
            HideAllIntents();
            _intentBadges.Clear();
            HideEngagedLinks();
            switch (Battle.Phase)
            {
                case Phase.Placement:
                    UpdatePlacement(clicked);
                    break;
                case Phase.PlayerTurn when !_busy:
                    UpdatePlayerTurn(clicked, ordered);
                    break;
                default:
                    _board.ClearHighlights();
                    break;
            }

            RefreshApPips();
            UpdateLossPreview();
        }

        /// <summary>
        /// AP pips on every player statue during the player's turn; the selected one also previews
        /// what the hovered action would cost. Hidden while the enemies play out their turn.
        /// </summary>
        private void RefreshApPips()
        {
            var show = Battle.Phase == Phase.PlayerTurn && !_enemyTurnPlaying;
            var cost = show && !_busy ? PreviewCost() : 0;
            foreach (var u in Battle.Units.Where(u => u.Team == Team.Player && u.IsPlaced && u.IsAlive && !u.IsObjective))
            {
                var view = _board.ViewOf(u);
                if (view == null)
                    continue;
                if (show)
                    view.ShowAp(u.Ap, u.MaxAp, u == _selected ? cost : 0);
                else
                    view.HideAp();
            }
        }

        /// <summary>What the action costs this unit right now: Quick Fuse and an unused Twincast make it free.</summary>
        private static int CardCost(Unit unit, ActionKind action) => Battle.TwincastFree(unit, action) ? 0 : Battle.CostOf(unit, action);

        /// <summary>AP the selected unit would spend if the player clicked the hovered tile now (0 if nothing).</summary>
        private int PreviewCost()
        {
            if (_selected == null || !_hovered.HasValue || !Battle.CanAct(_selected))
                return 0;
            var tile = _hovered.Value;
            if (_action.HasValue)
                return Battle.CanUseAction(_selected, _action.Value, tile) ? CardCost(_selected, _action.Value) : 0;
            if (_hoveredUnit != null && _hoveredUnit.Team == Team.Enemy)
            {
                if (Battle.CanAttack(_selected, _hoveredUnit))
                    return Battle.AttackCost;
                return Battle.FindAttackSpot(_selected, _hoveredUnit).HasValue ? Battle.AttackCost : 0;
            }

            // Moving costs moves, not AP.
            return 0;
        }

        /// <summary>Moves the selected unit would spend walking to the hovered tile (0 if it can't).</summary>
        private int PreviewMoves()
        {
            if (_selected == null || !_hovered.HasValue || !Battle.CanAct(_selected) || _action.HasValue)
                return 0;
            if (_hoveredUnit != null && _hoveredUnit.Team == Team.Enemy)
                return Battle.FindAttackSpot(_selected, _hoveredUnit) is var spot && spot.HasValue ? spot.Value.cost : 0;
            return Battle.GetReachable(_selected).TryGetValue(_hovered.Value, out var move) ? move : 0;
        }

        // ---------- Placement ----------

        private void UpdatePlacement(bool clicked)
        {
            if (clicked)
            {
                var tile = _hovered.Value;
                if (_hoveredUnit != null && _hoveredUnit.Team == Team.Player && !_hoveredUnit.IsObjective && _hoveredUnit != _toPlace)
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
                if ((Battle.IsPlayerZone(p) || (_toPlace != null && Battle.IsDeployTile(_toPlace, p))) && !Battle.Board.IsObstacle(p))
                    _board.SetHighlight(p, ZoneColor);
            if (_hovered.HasValue && _toPlace != null && Battle.CanPlace(_toPlace, _hovered.Value))
                _board.SetHighlight(_hovered.Value, HoverColor);
            DrawEnemyHover();
        }

        private Unit NextUnplaced() => Battle.Units.FirstOrDefault(u => u.Team == Team.Player && !u.IsPlaced);

        private void StartBattle()
        {
            if (!Battle.StartBattle())
                return;
            _logger.BattleStarted();
            _toPlace = null;
            _hint = "Click a statue to select it.";
            RefreshUnitStates();
            // Omens take effect at the start of the first turn too.
            if (Battle.OpeningEvents.Count > 0)
            {
                Record(Battle.OpeningEvents);
                StartCoroutine(PlayPlayerAction(Battle.OpeningEvents));
            }
        }

        // ---------- Player turn ----------

        /// <summary>
        /// Picks a special action to aim, or drops it if it was already picked. Detonate needs no target,
        /// so it fires straight away.
        /// </summary>
        private void ToggleAction(ActionKind action)
        {
            if (_selected == null || !Battle.CanAct(_selected))
                return;
            if (_action == action)
            {
                _action = null;
                return;
            }

            // Actions whose only target is the unit itself (Detonate, Vanish) go off at once: there is
            // nothing to pick, and clicking the statue often lands on the tile behind it.
            var targets = Battle.ActionTargets(_selected, action);
            if (targets.Count == 1 && targets[0] == _selected.Pos)
            {
                _action = null;
                UseAction(action, _selected.Pos);
                return;
            }

            _action = action;
            _hint = $"{Abilities.Name(action)}: right-click a yellow tile. Press its key again or Esc to cancel.";
        }

        private void UseAction(ActionKind action, GridPos tile)
        {
            var events = Battle.PlayerUseAction(_selected, action, tile);
            if (events == null)
            {
                _hint = $"Can't use {Abilities.Name(action)} there.";
                return;
            }

            Record(events);
            StartCoroutine(PlayPlayerAction(events));
        }

        private void UpdatePlayerTurn(bool clicked, bool ordered)
        {
            if (clicked)
                HandlePlayerChoose();
            else if (ordered)
                HandlePlayerOrder(_hovered.Value);
            if (_busy || Battle.Phase != Phase.PlayerTurn)
                return;
            DrawPlayerHighlights();
        }

        /// <summary>
        /// Left-click: selects one of your units, or (on an enemy) only opens its card, keeping your selection.
        /// Empty ground drops the selection and the aim, unless the selected unit is mid-turn.
        /// </summary>
        private void HandlePlayerChoose()
        {
            var target = _hoveredUnit;
            if (target != null && target.Team == Team.Player)
            {
                if (target.IsObjective)
                    _hint = $"The {target.Stats.Name} can't act. Keep it standing until the ritual completes.";
                else if (target == _selected)
                    return;
                else if (Battle.CanAct(target))
                    Select(target);
                else if (target.IsDone)
                    _hint = $"{target.Stats.Name} has finished its turn.";
                else if (Battle.ActiveUnit != null)
                    _hint = $"Finish the {Battle.ActiveUnit.Stats.Name}'s turn first (Done).";
                return;
            }

            if (target != null)
                return;

            _action = null;
            if (_selected != null && Battle.ActiveUnit != _selected)
                Select(null);
        }

        /// <summary>Right-click: the selected unit uses the aimed action there, or attacks, or walks there.</summary>
        private void HandlePlayerOrder(GridPos tile)
        {
            var target = _hoveredUnit;

            if (_action.HasValue && _selected != null)
            {
                var action = _action.Value;
                _action = null;
                if (Battle.CanUseAction(_selected, action, tile))
                    UseAction(action, tile);
                else
                    _hint = $"{Abilities.Name(action)} cancelled.";
                return;
            }

            if (_selected == null)
            {
                _hint = "Left-click a statue to select it first.";
                return;
            }

            if (target != null && target.Team == Team.Player)
                return;

            if (target != null && Battle.CanAttack(_selected, target))
            {
                var attack = Battle.PlayerAttack(_selected, target);
                if (attack != null)
                {
                    Record(attack);
                    StartCoroutine(PlayPlayerAction(attack));
                }

                return;
            }

            // A barricade in reach: hit it (either side's; it breaks at 0 HP).
            if (target == null && Battle.CanAttackBarricade(_selected, tile))
            {
                var hit = Battle.PlayerAttackBarricade(_selected, tile);
                if (hit != null)
                {
                    Record(hit);
                    StartCoroutine(PlayPlayerAction(hit));
                }

                return;
            }

            // A distant enemy: walk to the cheapest tile to hit it from, then attack.
            if (target != null && target.Team == Team.Enemy && Battle.FindAttackSpot(_selected, target) is var spot && spot.HasValue)
            {
                var events = Battle.PlayerMove(_selected, spot.Value.tile);
                if (events == null)
                    return;
                Record(events, autoApproach: true);
                // The walk itself may have changed things; attack only if it still works.
                if (target.IsAlive && Battle.CanAttack(_selected, target))
                {
                    var attack = Battle.PlayerAttack(_selected, target);
                    Record(attack, autoApproach: true);
                    events.AddRange(attack);
                }

                StartCoroutine(PlayPlayerAction(events));
                return;
            }

            if (Battle.GetReachable(_selected).ContainsKey(tile))
            {
                var move = Battle.PlayerMove(_selected, tile);
                if (move != null)
                {
                    Record(move);
                    StartCoroutine(PlayPlayerAction(move));
                }

                return;
            }

            if (target != null && target.Team == Team.Enemy)
            {
                _hint = _selected.Ap < Battle.AttackCost
                    ? $"Attacking needs {Battle.AttackCost} AP."
                    : "Too far to walk up and attack this turn.";
            }
        }

        private void Record(List<BattleEvent> events, bool autoApproach = false)
        {
            if (events == null)
                return;
            foreach (var ev in events)
                _logger.Record(ev, Battle.TurnNumber, autoApproach);
        }

        private void Select(Unit unit)
        {
            if (_selected != null)
                _board.ViewOf(_selected)?.SetSelected(false);
            _selected = unit;
            _action = null;
            if (_selected != null)
                _board.ViewOf(_selected)?.SetSelected(true);
            _hint = unit == null ? "Click a statue to select it." : "Right-click a blue tile to move or a red one to attack.";
            // The selected unit's card shows, unless another unit's card is open.
            _cardUnit = null;
            _cardHidden = false;
        }

        private IEnumerator PlayPlayerAction(List<BattleEvent> events)
        {
            _busy = true;
            _board.ClearHighlights();
            yield return PlayEvents(events);

            // A unit that can't move, attack or use an action has nothing more to do.
            if (_selected != null && (!_selected.IsAlive || !Battle.CanStillAct(_selected)))
                FinishSelectedUnit();
            RefreshUnitStates();
            _board.SyncHazards();
            _board.SyncTiles();
            _board.SyncUnits();
            if (Battle.IsOver)
                _logger.Save();
            _busy = false;
        }

        private void FinishSelectedUnit()
        {
            if (_selected == null)
                return;
            _logger.RecordUnitDone(_selected, Battle.TurnNumber);
            Battle.EndUnitTurn(_selected);
            Select(null);
            RefreshUnitStates();
        }

        /// <summary>Player units that could still act: ending the turn now wastes their actions.</summary>
        private List<Unit> UnitsThatCanStillAct() => Battle.Living(Team.Player).Where(Battle.CanStillAct).ToList();

        /// <summary>The End turn button. Asks first if some units still have something to do.</summary>
        private void RequestEndTurn()
        {
            var idle = UnitsThatCanStillAct();
            if (idle.Count == 0)
            {
                EndTurn();
                return;
            }

            _confirmEndTurn = true;
            _logger.RecordEndTurnPrompt(idle, Battle.TurnNumber, "asked");
        }

        private void EndTurn()
        {
            _confirmEndTurn = false;
            Select(null);
            StartCoroutine(PlayEnemyTurn());
        }

        private IEnumerator PlayEnemyTurn()
        {
            _busy = true;
            _enemyTurnPlaying = true;
            _board.ClearHighlights();
            foreach (var u in Battle.Living(Team.Player))
                _board.ViewOf(u)?.SetDimmed(true);

            var turn = Battle.TurnNumber;
            _logger.RecordEndTurn(turn);
            var events = Battle.EndPlayerTurn();
            foreach (var ev in events)
                _logger.Record(ev, turn);
            yield return PlayEvents(events);

            _hint = "Your turn. Click a statue to select it.";
            RefreshUnitStates();
            _board.SyncHazards();
            _board.SyncTiles();
            _board.SyncUnits();
            if (Battle.IsOver)
                _logger.Save();
            _enemyTurnPlaying = false;
            _busy = false;
        }

        /// <summary>The debug Win battle button only exists in the editor and development builds.</summary>
        private static bool DebugToolsEnabled => Application.isEditor || Debug.isDebugBuild;

        /// <summary>Playtest shortcut: skip this battle as a win with the whole army alive.</summary>
        private void DebugWin()
        {
            Select(null);
            _confirmEndTurn = false;
            Battle.DebugWin();
            _board.SyncUnits();
            foreach (var enemy in Battle.Units.Where(u => u.Team == Team.Enemy))
                _board.ViewOf(enemy)?.gameObject.SetActive(false);
            _board.ClearHighlights();
            _logger.BattleStartedIfNeeded();
            _logger.Save();
        }

        private void DrawDebugButton()
        {
            if (!DebugToolsEnabled)
                return;
            GUILayout.Space(6);
            if (GUILayout.Button("Win battle (debug skip)"))
                DebugWin();
        }

        /// <summary>Saves a battle that was left before it ended (leaving Play mode).</summary>
        private void OnDestroy() => _logger?.Save();

        private void DrawPlayerHighlights()
        {
            _board.ClearHighlights();
            DrawOmenTiles();
            DrawBombZones();
            DrawEnemyHover();
            if (_selected == null)
                return;

            _board.SetHighlight(_selected.Pos, SelectedColor);

            if (_action.HasValue)
            {
                var action = _action.Value;
                foreach (var p in Battle.ActionTargets(_selected, action))
                    _board.SetHighlight(p, MineSpotColor);
                if (_hovered.HasValue && Battle.CanUseAction(_selected, action, _hovered.Value))
                {
                    // Area actions preview the tiles they will hit.
                    if (action == ActionKind.Fireball || action == ActionKind.ThrowBomb)
                        foreach (var p in action == ActionKind.Fireball ? Battle.PlusArea(_hovered.Value) : Battle.BombArea(_hovered.Value))
                            _board.SetHighlight(p, BombZoneColor);
                    if (action == ActionKind.SmokeBomb)
                        foreach (var p in Battle.PlusArea(_hovered.Value))
                            _board.SetHighlight(p, CoverColor);
                    if (action == ActionKind.SuppressingFire)
                        foreach (var p in Battle.SuppressLine(_selected, _hovered.Value))
                            _board.SetHighlight(p, BombZoneColor);
                    _board.SetHighlight(_hovered.Value, HoverColor);
                }

                return;
            }

            // Zone of control: enemy reach underneath, then move tiles, orange where getting there ends movement early.
            foreach (var p in Battle.EnemyReachTiles(_selected))
                if (p != _selected.Pos)
                    _board.SetHighlight(p, EnemyReachColor);
            var reachable = Battle.GetReachable(_selected);
            var costly = Battle.CostlyMoveTiles(_selected);
            foreach (var p in reachable.Keys)
                _board.SetHighlight(p, costly.Contains(p) ? CostlyMoveColor : MoveColor);
            DrawEngagedLinks(_selected);
            foreach (var enemy in Battle.Living(Team.Enemy))
            {
                if (Battle.CanAttack(_selected, enemy))
                {
                    foreach (var c in Battle.Cells(enemy))
                        _board.SetHighlight(c, AttackColor);
                }
                else if (Battle.FindAttackSpot(_selected, enemy).HasValue)
                {
                    foreach (var c in Battle.Cells(enemy))
                        _board.SetHighlight(c, ReachAttackColor);
                }
            }

            if (_hovered.HasValue && reachable.ContainsKey(_hovered.Value))
                _board.SetHighlight(_hovered.Value, HoverColor);

            // Hovering a reachable enemy shows where the unit will stand to hit it.
            if (_hoveredUnit != null && _hoveredUnit.Team == Team.Enemy && !Battle.CanAttack(_selected, _hoveredUnit) &&
                Battle.FindAttackSpot(_selected, _hoveredUnit) is var spot && spot.HasValue)
                _board.SetHighlight(spot.Value.tile, HoverColor);
        }

        private readonly List<IntentView> _engagedLinks = new List<IntentView>();

        /// <summary>A dotted link from the unit to each enemy whose reach holds it.</summary>
        private void DrawEngagedLinks(Unit unit)
        {
            var holders = Battle.EngagedBy(unit, unit.Pos).ToList();
            while (_engagedLinks.Count < holders.Count)
                _engagedLinks.Add(IntentView.Create(transform, EngagedLinkColor));
            var from = _board.ViewOf(unit).transform.position + Vector3.up * 0.1f;
            for (var i = 0; i < holders.Count; i++)
                _engagedLinks[i].Show(from, _board.ViewOf(holders[i]).transform.position + Vector3.up * 0.1f);
        }

        private void HideEngagedLinks()
        {
            foreach (var link in _engagedLinks)
                link.Hide();
        }

        /// <summary>
        /// Shrouded tiles, the tiles a sun beam will strike at the start of the enemy turn, and the tiles lightning
        /// will strike at its end (Storm).
        /// </summary>
        private void DrawOmenTiles()
        {
            foreach (var p in Battle.Shroud.Keys)
                _board.SetHighlight(p, ShroudColor);
            foreach (var p in Battle.SunMarks)
                _board.AddSunMark(p);
            foreach (var p in Battle.StormMarks)
                _board.SetHighlight(p, StormMarkColor);
        }

        /// <summary>Every waiting bomb shows its blast area, so it can be dodged.</summary>
        private void DrawBombZones()
        {
            foreach (var bomb in Battle.Bombs)
            foreach (var p in Battle.BombArea(bomb.Tile))
                _board.SetHighlight(p, BombZoneColor);
            // Placeholders (see the "Art needed" table): caltrops and the tiles a crossbowman is watching.
            foreach (var p in Battle.OverwatchTiles)
                _board.SetHighlight(p, OverwatchColor);
            foreach (var p in Battle.Caltrops)
                _board.SetHighlight(p, CaltropsColor);
            // Placeholders too: your archers' Covering Fire tiles and their hidden snares.
            foreach (var p in Battle.CoverTiles)
                _board.SetHighlight(p, CoverColor);
            foreach (var p in Battle.Snares)
                _board.SetHighlight(p, SnareColor);
        }

        // One intent line per enemy while Alt is held.
        private readonly List<IntentView> _allIntents = new List<IntentView>();

        /// <summary>
        /// While hovering an enemy: what it plans to do next. While Alt is held: every enemy's intent at once.
        /// </summary>
        private void DrawEnemyHover()
        {
            // What's On His Mind?: no intents at all.
            if (Battle.IntentsHidden)
                return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.altKey.isPressed)
            {
                var enemies = Battle.Living(Team.Enemy).Where(e => !e.IsObjective).ToList();
                while (_allIntents.Count < enemies.Count)
                    _allIntents.Add(IntentView.Create(transform));
                for (var i = 0; i < enemies.Count; i++)
                    DrawIntent(enemies[i], _allIntents[i]);
                return;
            }

            var enemy = _hoveredUnit;
            if (enemy == null || enemy.Team != Team.Enemy)
                return;

            DrawIntent(enemy, _intent);
        }

        private void HideAllIntents()
        {
            foreach (var line in _allIntents)
                line.Hide();
        }

        // Enemies whose intent badge shows this frame (filled in Update, drawn in OnGUI).
        private readonly List<(Unit enemy, Intent intent)> _intentBadges = new List<(Unit, Intent)>();

        /// <summary>One enemy's intent: the tiles it will hit, a dotted line to its target and a badge over its head.</summary>
        private void DrawIntent(Unit enemy, IntentView line)
        {
            var intent = EnemyAI.PredictIntent(Battle, enemy);
            if (intent.Target == null)
                return;
            _intentBadges.Add((enemy, intent));

            var from = _board.ViewOf(enemy).transform.position + Vector3.up * 0.15f;
            var to = IsoMath.GridToWorld(intent.Target.Pos) + Vector3.up * 0.15f;
            switch (intent.Kind)
            {
                case IntentKind.Raise:
                    _board.SetHighlight(intent.Target.Pos, RaiseColor);
                    line.Show(from, to);
                    break;
                case IntentKind.Smash:
                    // Every tile around the ogre gets hit, so show the whole ring.
                    foreach (var c in SmashRing(enemy))
                        _board.SetHighlight(c, SmashZoneColor);
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    if (Battle.Distance(enemy, intent.Target) > 1)
                        line.Show(from, to);
                    break;
                case IntentKind.Throw:
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    if (intent.Tile.HasValue)
                        _board.SetHighlight(intent.Tile.Value, RockLandingColor);
                    line.Show(from, to);
                    break;
                case IntentKind.Bomb:
                    foreach (var p in Battle.BombArea(intent.Target.Pos))
                        _board.SetHighlight(p, BombZoneColor);
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    line.Show(from, to);
                    break;
                case IntentKind.Flee:
                    // Show where it's running: the far edge it escapes from.
                    for (var x = 0; x < Battle.Board.Width; x++)
                        _board.SetHighlight(new GridPos(x, Battle.Board.Height - 1), RaiseColor);
                    break;
                case IntentKind.Slam:
                    foreach (var p in Battle.Board.AllTiles())
                        if (Battle.Distance(p, 1, enemy) <= 2 && !Battle.Cells(enemy).Contains(p))
                            _board.SetHighlight(p, SmashZoneColor);
                    break;
                case IntentKind.Fling:
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    if (intent.Thrown != null)
                    {
                        _board.SetHighlight(intent.Thrown.Pos, AuraColor);
                        if (intent.Tile.HasValue)
                            _board.SetHighlight(intent.Tile.Value, RockLandingColor);
                    }

                    line.Show(from, to);
                    break;
                case IntentKind.CorpseBlast:
                    foreach (var p in Battle.BlastArea(intent.Target.Pos))
                        _board.SetHighlight(p, BombZoneColor);
                    line.Show(from, to);
                    break;
                case IntentKind.Howl:
                    foreach (var wolf in Battle.Living(enemy.Team).Where(w => w.IsWolf))
                        _board.SetHighlight(wolf.Pos, AuraColor);
                    break;
                case IntentKind.Guard:
                    // Placeholder: the crossbowman it will guard, and the tile it walks to.
                    _board.SetHighlight(intent.Target.Pos, AuraColor);
                    if (intent.Tile.HasValue && intent.Tile.Value != enemy.Pos)
                        _board.SetHighlight(intent.Tile.Value, RockLandingColor);
                    line.Show(from, to);
                    break;
                case IntentKind.Hellswap:
                    // Placeholder: the target, and the tile it lands on (where the imp swaps from).
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    _board.SetHighlight(intent.Tile ?? enemy.Pos, RockLandingColor);
                    line.Show(from, to);
                    break;
                case IntentKind.FireBreath:
                {
                    // The whole line that burns, and the tile it breathes from if it walks first.
                    var breathFrom = intent.Tile ?? enemy.Pos;
                    foreach (var p in Battle.FireBreathLineFrom(breathFrom, intent.Target.Pos) ?? new List<GridPos> { intent.Target.Pos })
                        _board.SetHighlight(p, BombZoneColor);
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    if (breathFrom != enemy.Pos)
                        _board.SetHighlight(breathFrom, RockLandingColor);
                    line.Show(from, to);
                    break;
                }
                default:
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    line.Show(from, to);
                    break;
            }
        }

        /// <summary>The free-standing tiles next to a unit's footprint.</summary>
        private IEnumerable<GridPos> SmashRing(Unit unit)
        {
            var cells = Battle.Cells(unit).ToList();
            return cells.SelectMany(c => c.Neighbours())
                .Distinct()
                .Where(n => !cells.Contains(n) && Battle.Board.InBounds(n) && !Battle.Board.IsObstacle(n));
        }

        // ---------- Animation ----------

        /// <summary>Plays events in order, with a short pause whenever a different enemy starts acting.</summary>
        private IEnumerator PlayEvents(List<BattleEvent> events)
        {
            Unit lastActor = null;
            foreach (var ev in events)
            {
                var enemyActing = ev.Actor != null && ev.Actor.Team == Team.Enemy && !(ev is EffectEvent) && !(ev is ExplosionEvent);
                if (enemyActing && lastActor != null && ev.Actor != lastActor)
                    yield return new WaitForSeconds(PauseBetweenEnemies);
                if (enemyActing)
                    lastActor = ev.Actor;
                yield return PlayEvent(ev);
            }
        }

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
                    yield return attacker.Lunge(target.transform.position, () => ShowHit(new Hit(attack.Target, attack.Damage, attack.TargetHpAfter)));
                    attacker.SetGridPosition(attack.Attacker.Pos);
                    yield return attack.Killed ? target.Die() : target.Hit();
                    break;
                }
                case SmashEvent smash:
                {
                    var attacker = _board.ViewOf(smash.Attacker);
                    var aim = smash.Hits.Count > 0
                        ? smash.Hits.Aggregate(Vector3.zero, (sum, h) => sum + _board.ViewOf(h.Target).transform.position) / smash.Hits.Count
                        : attacker.transform.position + Vector3.down * 0.2f;
                    yield return attacker.Lunge(aim, () =>
                    {
                        foreach (var hit in smash.Hits)
                            ShowHit(hit);
                    });
                    attacker.SetGridPosition(smash.Attacker.Pos);
                    yield return PlayHitReactions(smash.Hits);
                    break;
                }
                case BoulderEvent boulder:
                {
                    var thrower = _board.ViewOf(boulder.Thrower);
                    var target = _board.ViewOf(boulder.Hit.Target);
                    yield return thrower.Lunge(target.transform.position, null);
                    thrower.SetGridPosition(boulder.Thrower.Pos);
                    yield return FlyProjectile(PlaceholderArt.Rock, 0.45f, thrower.TopWorld, target.transform.position);
                    ShowHit(boulder.Hit);
                    if (boulder.RockTile.HasValue)
                    {
                        _board.RemoveHazard(boulder.RockTile.Value);
                        _board.AddRock(boulder.RockTile.Value);
                    }

                    yield return boulder.Hit.Killed ? target.Die() : target.Hit();
                    break;
                }
                case FlingEvent fling:
                {
                    // Placeholder: a lunge, then the goblin or explosive hops over (see the "Art needed" table).
                    var thrower = _board.ViewOf(fling.Thrower);
                    var target = _board.ViewOf(fling.Target);
                    yield return thrower.Lunge(IsoMath.GridToWorld(fling.From), null);
                    thrower.SetGridPosition(fling.Thrower.Pos);
                    if (fling.Thrown == null)
                    {
                        _board.RemoveHazard(fling.From);
                        yield return FlyProjectile(PlaceholderArt.Pixel, 0.3f, IsoMath.GridToWorld(fling.From), target.transform.position);
                        break;
                    }

                    var thrown = _board.ViewOf(fling.Thrown);
                    thrown.SetGridPosition(fling.To);
                    DamagePopup.Spawn(thrown.TopWorld + Vector3.up * 0.1f, "Flung!", DamagePopup.AbilityColor, 40);
                    if (fling.Hit.HasValue)
                    {
                        ShowHit(fling.Hit.Value);
                        yield return fling.Hit.Value.Killed ? target.Die() : target.Hit();
                    }

                    break;
                }
                case OverwatchEvent watch:
                    DamagePopup.Spawn(_board.ViewOf(watch.Watcher).TopWorld + Vector3.up * 0.25f, "Overwatch!", DamagePopup.AbilityColor, 44);
                    yield return new WaitForSeconds(0.35f);
                    break;
                case HowlEvent howl:
                {
                    // Placeholder: popups only (see the "Art needed" table).
                    var leader = _board.ViewOf(howl.Leader);
                    DamagePopup.Spawn(leader.TopWorld + Vector3.up * 0.25f, "Howl!", DamagePopup.AbilityColor, 48);
                    StartCoroutine(Shake(0.03f, 0.3f));
                    foreach (var wolf in howl.Wolves)
                        DamagePopup.Spawn(_board.ViewOf(wolf).TopWorld + Vector3.up * 0.1f, $"+{howl.Moves} move", DamagePopup.AbilityColor, 36);
                    yield return new WaitForSeconds(0.45f);
                    break;
                }
                case PileReplacedEvent replaced:
                    _board.ViewOf(replaced.Pile)?.gameObject.SetActive(false);
                    break;
                case RaiseEvent raise:
                {
                    var raiser = _board.ViewOf(raise.Raiser);
                    if (raise.From != null)
                    {
                        // Soul Harvest: your fallen unit's pile gives way to a new enemy statue (placeholder look).
                        var pile = _board.ViewOf(raise.From);
                        yield return raiser.Lunge(pile.transform.position, null);
                        raiser.SetGridPosition(raise.Raiser.Pos);
                        pile.gameObject.SetActive(false);
                        var risen = _board.AddView(raise.Raised);
                        DamagePopup.Spawn(risen.TopWorld + Vector3.up * 0.25f, "Harvested!", DamagePopup.AbilityColor, 44);
                        yield return risen.Raise();
                        break;
                    }

                    var raised = _board.ViewOf(raise.Raised);
                    yield return raiser.Lunge(raised.transform.position, null);
                    raiser.SetGridPosition(raise.Raiser.Pos);
                    yield return raised.Raise();
                    break;
                }
                case CorpseBlastEvent blast:
                {
                    // Placeholder: a lunge at the pile, which vanishes; the explosion follows as its own event.
                    var caster = _board.ViewOf(blast.Caster);
                    var pile = _board.ViewOf(blast.Pile);
                    yield return caster.Lunge(pile.transform.position, null);
                    caster.SetGridPosition(blast.Caster.Pos);
                    DamagePopup.Spawn(pile.transform.position + Vector3.up * 0.6f, "Corpse Blast!", DamagePopup.AbilityColor, 44);
                    pile.gameObject.SetActive(false);
                    break;
                }
                case PlaceMineEvent mine:
                {
                    var sapper = _board.ViewOf(mine.Sapper);
                    yield return sapper.Lunge(IsoMath.GridToWorld(mine.Tile), () => _board.AddHazard(mine.Tile, false));
                    sapper.SetGridPosition(mine.Sapper.Pos);
                    break;
                }
                case BombThrowEvent bomb:
                {
                    var thrower = _board.ViewOf(bomb.Thrower);
                    var to = IsoMath.GridToWorld(bomb.Tile);
                    yield return thrower.Lunge(to, null);
                    thrower.SetGridPosition(bomb.Thrower.Pos);
                    yield return FlyProjectile(PlaceholderArt.Bomb, 1f, thrower.TopWorld, to);
                    _board.AddHazard(bomb.Tile, true);
                    break;
                }
                case ExplosionEvent blast:
                {
                    if (blast.Cause == "rockfall" || blast.Cause == "vengeful")
                        DamagePopup.Spawn(IsoMath.GridToWorld(blast.Center) + Vector3.up * 0.9f, blast.Cause == "rockfall" ? "Rockfall!" : "Vengeance!", DamagePopup.DamageColor, 44);

                    // A sun beam leaves any mine or bomb on its tile alone.
                    if (blast.Cause == "lightning")
                    {
                        DamagePopup.Spawn(IsoMath.GridToWorld(blast.Center) + Vector3.up * 0.9f, "Lightning!", DamagePopup.AbilityColor, 44);
                        yield return LightningStrike.Strike(_board.transform, blast.Center);
                        foreach (var hit in blast.Hits)
                            ShowHit(hit);
                        yield return PlayHitReactions(blast.Hits);
                        break;
                    }

                    if (blast.Cause == "sun")
                    {
                        yield return _board.SunStrike(blast.Center, () =>
                            DamagePopup.Spawn(IsoMath.GridToWorld(blast.Center) + Vector3.up * 0.9f, "Sun beam!", DamagePopup.AbilityColor, 44));
                        foreach (var hit in blast.Hits)
                            ShowHit(hit);
                        yield return PlayHitReactions(blast.Hits);
                        break;
                    }

                    _board.RemoveHazard(blast.Center);
                    StartCoroutine(_board.Flash(blast.Area, BlastTime));
                    StartCoroutine(Shake(0.06f + 0.02f * blast.ChainDepth, BlastTime));
                    foreach (var hit in blast.Hits)
                        ShowHit(hit);
                    yield return PlayHitReactions(blast.Hits);
                    break;
                }
                case EffectEvent effect:
                    yield return PlayEffect(effect);
                    break;
                case AreaAttackEvent area:
                {
                    var attacker = _board.ViewOf(area.Attacker);
                    var aim = IsoMath.GridToWorld(area.Center);
                    if (area.Kind == "fireball")
                    {
                        yield return attacker.Lunge(aim, null);
                        attacker.SetGridPosition(area.Attacker.Pos);
                        yield return FlyProjectile(PlaceholderArt.Pixel, 6f, attacker.TopWorld, aim);
                        StartCoroutine(_board.Flash(area.Area, BlastTime));
                        StartCoroutine(Shake(0.05f, BlastTime));
                        foreach (var hit in area.Hits)
                            ShowHit(hit);
                    }
                    else
                    {
                        if (area.Kind == "slam")
                            StartCoroutine(Shake(0.08f, BlastTime));
                        if (area.Kind == "volley" || area.Kind == "pierce" || area.Kind == "slam" || area.Kind == "firewall" || area.Kind == "blizzard" || area.Kind == "fire breath")
                            StartCoroutine(_board.Flash(area.Area, BlastTime * 0.6f));
                        yield return attacker.Lunge(aim, () =>
                        {
                            foreach (var hit in area.Hits)
                                ShowHit(hit);
                        });
                        attacker.SetGridPosition(area.Attacker.Pos);
                    }

                    yield return PlayHitReactions(area.Hits);
                    break;
                }
                case PushEvent push:
                {
                    var view = _board.ViewOf(push.Unit);
                    GridPos? dir = null;
                    if (push.To == push.From && push.Pusher != null)
                    {
                        // Blocked: bump away from the pusher.
                        var p = push.Pusher.Pos;
                        dir = new GridPos(Math.Sign(push.From.X - p.X), Math.Sign(push.From.Y - p.Y));
                    }

                    yield return view.Slide(push.From, push.To, dir);
                    if (push.Collision.HasValue)
                    {
                        ShowHit(push.Collision.Value);
                        yield return push.Collision.Value.Killed ? view.Die() : view.Hit();
                    }

                    break;
                }
                case StatusEvent status:
                {
                    var source = _board.ViewOf(status.Source);
                    var target = _board.ViewOf(status.Target);
                    // Statuses applied by a hit show without an extra lunge; actions lunge at their target.
                    if (status.Kind == StatusKind.Ward || status.Kind == StatusKind.Heal || status.Kind == StatusKind.Grasp || status.Kind == StatusKind.Contract)
                    {
                        yield return source.Lunge(target.transform.position, null);
                        source.SetGridPosition(status.Source.Pos);
                    }

                    var at = target.TopWorld + Vector3.up * 0.25f;
                    switch (status.Kind)
                    {
                        case StatusKind.Pin:
                            DamagePopup.Spawn(at, "Pinned", DamagePopup.AbilityColor, 44);
                            break;
                        case StatusKind.Grasp:
                            DamagePopup.Spawn(at, "Grasped: pinned", DamagePopup.AbilityColor, 40);
                            break;
                        case StatusKind.Wrath:
                            DamagePopup.Spawn(at, "Wrath! 2 actions a turn", DamagePopup.DamageColor, 48);
                            break;
                        case StatusKind.Phase:
                            DamagePopup.Spawn(at, PhaseText(status.Target, status.Amount), DamagePopup.DamageColor, 48);
                            break;
                        case StatusKind.Rockfall:
                            DamagePopup.Spawn(at, "Rocks above!", DamagePopup.DamageColor, 40);
                            break;
                        case StatusKind.Vanish:
                            // Placeholder look: the statue turns see-through (see the "Art needed" table).
                            DamagePopup.Spawn(at, "Vanished", DamagePopup.AbilityColor, 44);
                            target.SetHidden(true);
                            break;
                        case StatusKind.Reveal:
                            DamagePopup.Spawn(at, "Revealed", DamagePopup.AbilityColor, 40);
                            target.SetHidden(false);
                            break;
                        case StatusKind.Slow:
                            DamagePopup.Spawn(at, "Slowed: move or act", DamagePopup.AbilityColor, 40);
                            break;
                        case StatusKind.ShieldUp:
                            DamagePopup.Spawn(at, "Shield Up!", DamagePopup.AbilityColor, 44);
                            break;
                        case StatusKind.FrostNova:
                            DamagePopup.Spawn(at, "Frost Nova!", DamagePopup.AbilityColor, 48);
                            StartCoroutine(_board.Flash(Battle.Board.AllTiles().Where(p => p.StepsTo(status.Source.Pos) == 1).ToList(), BlastTime * 0.6f));
                            break;
                        case StatusKind.Hooked:
                            DamagePopup.Spawn(at, "Hooked!", DamagePopup.AbilityColor, 44);
                            break;
                        case StatusKind.Twincast:
                            DamagePopup.Spawn(at, "Twincast: free", DamagePopup.AbilityColor, 40);
                            break;
                        // Placeholder looks for the tension pieces (see the "Art needed" table).
                        case StatusKind.Planted:
                            DamagePopup.Spawn(at, $"Planted: shots +{status.Amount}", DamagePopup.AbilityColor, 44);
                            break;
                        case StatusKind.Overchannel:
                            DamagePopup.Spawn(at, "Overchannel: next spell x2", DamagePopup.AbilityColor, 44);
                            break;
                        case StatusKind.Battlecry:
                            DamagePopup.Spawn(at, $"+1 move, +{status.Amount} HP", DamagePopup.HealColor, 40);
                            break;
                        case StatusKind.Taunt:
                            DamagePopup.Spawn(at, "Taunt!", DamagePopup.AbilityColor, 48);
                            break;
                        case StatusKind.ThrowShield:
                            DamagePopup.Spawn(at, "Shield thrown!", DamagePopup.DamageColor, 44);
                            target.RefreshHp(status.Target.Hp);
                            break;
                        case StatusKind.Mark:
                            DamagePopup.Spawn(at, "Marked", DamagePopup.AbilityColor, 44);
                            break;
                        case StatusKind.Poison:
                            DamagePopup.Spawn(at, "Poisoned", DamagePopup.AbilityColor, 44);
                            break;
                        case StatusKind.Ward:
                            DamagePopup.Spawn(at, $"Shield {status.Amount}", DamagePopup.HealColor, 44);
                            break;
                        case StatusKind.Heal:
                            DamagePopup.Spawn(at, $"+{status.Amount}", DamagePopup.HealColor);
                            target.RefreshHp(status.Target.Hp);
                            break;
                        case StatusKind.Contract:
                            DamagePopup.Spawn(at, $"Contract: -{status.Amount} next turn", DamagePopup.DamageColor, 40);
                            break;
                        case StatusKind.ContractBroken:
                            DamagePopup.Spawn(at, "Contract broken", DamagePopup.HealColor, 40);
                            break;
                    }

                    target.RefreshStatus();
                    yield return new WaitForSeconds(0.2f);
                    break;
                }
                case TickEvent tick:
                {
                    var view = _board.ViewOf(tick.Hit.Target);
                    DamagePopup.Spawn(view.TopWorld + Vector3.up * 0.1f, $"-{tick.Hit.Damage} {tick.Cause}",
                        tick.Cause == "burn" ? DamagePopup.DamageColor : DamagePopup.AbilityColor, 44);
                    view.RefreshHp(tick.Hit.HpAfter);
                    view.RefreshStatus();
                    yield return tick.Hit.Killed ? view.Die() : view.Hit();
                    break;
                }
                case BarricadeHitEvent barricadeHit:
                {
                    var attacker = _board.ViewOf(barricadeHit.Attacker);
                    var at = IsoMath.GridToWorld(barricadeHit.Tile);
                    yield return attacker.Lunge(at, null);
                    attacker.SetGridPosition(barricadeHit.Attacker.Pos);
                    DamagePopup.Spawn(at + Vector3.up * 0.4f,
                        barricadeHit.HpAfter > 0 ? $"-{barricadeHit.Damage}  ({barricadeHit.HpAfter} left)" : $"-{barricadeHit.Damage}  broken",
                        DamagePopup.DamageColor, 44);
                    break;
                }
                case TileEvent tile:
                {
                    if (tile.Added && tile.Source != null && tile.Kind != "burn")
                    {
                        var source = _board.ViewOf(tile.Source);
                        yield return source.Lunge(IsoMath.GridToWorld(tile.Tile), null);
                        source.SetGridPosition(tile.Source.Pos);
                    }

                    _board.SyncTiles();
                    break;
                }
                case FallEvent fall:
                {
                    // Slides off the edge and falls into the void's black hole (or crumbles, with no void).
                    var view = _board.ViewOf(fall.Unit);
                    DamagePopup.Spawn(view.TopWorld + Vector3.up * 0.25f, "Fell!", DamagePopup.DamageColor, 48);
                    yield return view.Slide(fall.Edge, fall.Beyond, null);
                    ShowHit(fall.Hit);
                    var abyss = SceneryVoid.Active;
                    if (abyss != null)
                        yield return view.FallIntoVoid(abyss.Hole, abyss.BehindTowerOrder, abyss.Flare);
                    else
                        yield return view.Die();
                    view.gameObject.SetActive(false);
                    break;
                }
                case FieldEvent field:
                {
                    var view = _board.ViewOf(field.Unit);
                    if (view == null)
                        break;
                    if (field.Field == Battlefield.FortunesFavored)
                    {
                        // The last round's favored lose their pip.
                        foreach (var u in Battle.Units.Where(u => u.IsAlive))
                            _board.ViewOf(u)?.RefreshStatus();
                        DamagePopup.Spawn(view.TopWorld + Vector3.up * 0.25f, "Favored!", DamagePopup.AbilityColor, 40);
                    }
                    else
                    {
                        DamagePopup.Spawn(view.TopWorld + Vector3.up * 0.25f, "Lightning gathers", DamagePopup.DamageColor, 40);
                        foreach (var p in field.Tiles)
                            _board.SetHighlight(p, StormMarkColor);
                    }

                    yield return new WaitForSeconds(0.35f);
                    break;
                }
                case EscapeEvent escape:
                {
                    var view = _board.ViewOf(escape.Unit);
                    DamagePopup.Spawn(view.TopWorld + Vector3.up * 0.25f,
                        escape.Gold > 0 ? $"Escaped with {escape.Gold} gold" : "Escaped", DamagePopup.DamageColor, 44);
                    yield return view.Slide(escape.Unit.Pos, new GridPos(escape.Unit.Pos.X, escape.Unit.Pos.Y + 1), null);
                    view.gameObject.SetActive(false);
                    break;
                }
                case UpheavalEvent upheaval:
                {
                    // Placeholder: the caster lunges and the target is hit; the push follows as its own event.
                    var caster = _board.ViewOf(upheaval.Caster);
                    var target = _board.ViewOf(upheaval.Hit.Target);
                    yield return caster.Lunge(target.transform.position, null);
                    caster.SetGridPosition(upheaval.Caster.Pos);
                    DamagePopup.Spawn(target.TopWorld + Vector3.up * 0.4f, "Upheaval!", DamagePopup.AbilityColor, 44);
                    ShowHit(upheaval.Hit);
                    yield return upheaval.Hit.Killed ? target.Die() : target.Hit();
                    break;
                }
                case SplitEvent split:
                {
                    // Only add the new views: a full sync would jump units still waiting to be animated.
                    foreach (var child in split.Children)
                        StartCoroutine(_board.AddView(child).Raise());
                    var at = IsoMath.GridToWorld(split.Parent.Pos) + Vector3.up * 0.6f;
                    DamagePopup.Spawn(at, "Split!", DamagePopup.AbilityColor, 48);
                    yield return new WaitForSeconds(0.5f);
                    break;
                }
                case ReinforceEvent reinforce:
                {
                    foreach (var unit in reinforce.Units)
                    {
                        StartCoroutine(_board.AddView(unit).Raise());
                        DamagePopup.Spawn(IsoMath.GridToWorld(unit.Pos) + Vector3.up * 0.6f, "Reinforcements!", DamagePopup.DamageColor, 40);
                    }

                    yield return new WaitForSeconds(0.6f);
                    break;
                }
                case OmenEvent omen:
                {
                    var view = _board.ViewOf(omen.Unit);
                    if (view == null)
                        break;
                    var text = omen.Omen == Omen.Sun ? "Sun sigil" : omen.Omen == Omen.Night ? "Shrouded" : $"Moon shield {Battle.MoonShield}";
                    DamagePopup.Spawn(view.TopWorld + Vector3.up * 0.25f, text, omen.Blessing ? DamagePopup.HealColor : DamagePopup.DamageColor, 40);
                    view.RefreshStatus();
                    if (omen.Omen == Omen.Moon)
                        _board.ShowMoonShield(omen.Unit, Battle.MoonShield, Battle.TurnNumber);
                    foreach (var p in omen.Tiles)
                    {
                        if (omen.Omen == Omen.Sun)
                            _board.AddSunMark(p);
                        else
                            _board.SetHighlight(p, ShroudColor);
                    }
                    yield return new WaitForSeconds(0.35f);
                    break;
                }
                case SwapEvent swap:
                {
                    // Placeholder: both statues jump to their new tiles with a popup (see the "Art needed" table).
                    var caster = _board.ViewOf(swap.Caster);
                    var target = _board.ViewOf(swap.Target);
                    DamagePopup.Spawn(caster.TopWorld + Vector3.up * 0.25f, "Hellswap!", DamagePopup.AbilityColor, 44);
                    yield return new WaitForSeconds(0.2f);
                    caster.SetGridPosition(swap.Caster.Pos);
                    target.SetGridPosition(swap.Target.Pos);
                    DamagePopup.Spawn(target.TopWorld + Vector3.up * 0.1f, "Swapped!", DamagePopup.DamageColor, 40);
                    yield return new WaitForSeconds(0.35f);
                    break;
                }
                case SummonEvent summon:
                {
                    var builder = _board.ViewOf(summon.Summoner);
                    yield return builder.Lunge(IsoMath.GridToWorld(summon.Summoned.Pos), null);
                    builder.SetGridPosition(summon.Summoner.Pos);
                    _board.SyncUnits();
                    break;
                }
            }
        }

        private IEnumerator PlayEffect(EffectEvent effect)
        {
            var view = _board.ViewOf(effect.Unit);
            if (view == null)
                yield break;
            var at = view.TopWorld + Vector3.up * 0.25f;
            switch (effect.Kind)
            {
                case EffectKind.Heal:
                    DamagePopup.Spawn(at, $"+{effect.Amount}", DamagePopup.HealColor);
                    view.RefreshHp(effect.HpAfter);
                    break;
                case EffectKind.Thorns:
                    DamagePopup.Spawn(at, $"-{effect.Amount} thorns", DamagePopup.DamageColor, 48);
                    view.RefreshHp(effect.HpAfter);
                    yield return effect.HpAfter <= 0 ? view.Die() : view.Hit();
                    yield break;
                case EffectKind.LastStand:
                    DamagePopup.Spawn(at, "Last Stand!", DamagePopup.AbilityColor, 48);
                    view.RefreshHp(effect.HpAfter);
                    break;
                case EffectKind.Dodge:
                    DamagePopup.Spawn(at, "Dodge!", DamagePopup.AbilityColor, 48);
                    break;
                case EffectKind.Absorb:
                    var moon = _board.MoonShieldAbsorb(effect.Unit, effect.Amount);
                    DamagePopup.Spawn(at, $"{(moon ? "Moon shield" : "Shield")} -{effect.Amount}", DamagePopup.HealColor, 44);
                    view.RefreshStatus();
                    break;
                case EffectKind.Refund:
                    DamagePopup.Spawn(at, $"+{effect.Amount} AP", DamagePopup.AbilityColor, 48);
                    break;
                case EffectKind.Guard:
                    DamagePopup.Spawn(at, "Guard!", DamagePopup.AbilityColor, 48);
                    break;
                case EffectKind.ZocStrike:
                    DamagePopup.Spawn(at, "Placeholder!", DamagePopup.AbilityColor, 44);
                    break;
                case EffectKind.Mark:
                    DamagePopup.Spawn(at, $"Mark +{effect.Amount}", DamagePopup.DamageColor, 40);
                    break;
                case EffectKind.CoveringFire:
                    DamagePopup.Spawn(at, "Overwatch!", DamagePopup.AbilityColor, 44);
                    break;
                case EffectKind.Crush:
                    // Placeholder: popup only (see the "Art needed" rows in docs/night/mage-sapper.md).
                    DamagePopup.Spawn(at, "Crushed!", DamagePopup.DamageColor, 48);
                    break;
                case EffectKind.Steal:
                    DamagePopup.Spawn(at, $"Stole {effect.Amount} gold!", DamagePopup.DamageColor, 44);
                    view.RefreshStatus();
                    break;
                case EffectKind.Fury:
                    DamagePopup.Spawn(at, $"+{effect.Amount} Fury", DamagePopup.DamageColor, 44);
                    view.RefreshStatus();
                    break;
            }

            yield return new WaitForSeconds(0.25f);
        }

        private void ShowHit(Hit hit)
        {
            var view = _board.ViewOf(hit.Target);
            DamagePopup.Spawn(view.TopWorld + Vector3.up * 0.1f, hit.Damage);
            view.RefreshHp(hit.HpAfter);
        }

        /// <summary>Plays every target's hit or death at the same time and waits for all of them.</summary>
        private IEnumerator PlayHitReactions(IReadOnlyList<Hit> hits)
        {
            var running = hits.Count;
            foreach (var hit in hits)
            {
                var view = _board.ViewOf(hit.Target);
                StartCoroutine(Track(hit.Killed ? view.Die() : view.Hit(), () => running--));
            }

            while (running > 0)
                yield return null;
        }

        private static IEnumerator Track(IEnumerator routine, Action done)
        {
            yield return routine;
            done();
        }

        private IEnumerator FlyProjectile(Sprite sprite, float scale, Vector3 from, Vector3 to)
        {
            var projectile = new GameObject("Projectile").AddComponent<SpriteRenderer>();
            projectile.sprite = sprite;
            projectile.transform.localScale = Vector3.one * scale;
            projectile.sortingOrder = 15000;
            var height = 0.6f + Vector3.Distance(from, to) * 0.15f;
            for (var t = 0f; t < 1f; t += Time.deltaTime / BoulderFlightTime)
            {
                projectile.transform.position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * height);
                yield return null;
            }

            Destroy(projectile.gameObject);
        }

        private IEnumerator Shake(float strength, float duration)
        {
            // Chain blasts overlap their shakes, so always settle back on the framed position.
            var home = _cameraHome;
            for (var t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                var falloff = 1f - t;
                _camera.transform.position = home + (Vector3)(UnityEngine.Random.insideUnitCircle * strength * falloff);
                yield return null;
            }

            _camera.transform.position = home;
        }

        /// <summary>Dims statues that have finished their turn and tints wolves by their pack state (leader alive or dead).</summary>
        private void RefreshUnitStates()
        {
            foreach (var u in Battle.Units.Where(u => u.IsPlaced && u.IsAlive))
            {
                var view = _board.ViewOf(u);
                if (view == null)
                    continue;
                view.SetDimmed(u.Team == Team.Player && Battle.Phase == Phase.PlayerTurn && u.IsDone);
                view.SetHidden(u.IsHidden);
                view.RefreshStatus();
                switch (Battle.GetPackState(u))
                {
                    case PackState.Bonded:
                        view.SetTint(BondedTint);
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

        // ---------- HUD ----------
        //
        // The board is a diamond, so the screen corners around it are empty. The HUD lives in four
        // small panels there: selected unit (top-left), hover info (top-right), commands (bottom-right)
        // and hints (bottom-left). Clicks are only blocked where a panel actually is.

        private const float PanelWidth = 330f;
        private const float CommandWidth = 240f;
        private const float Margin = 10f;
        private const float ButtonHeight = 24f;

        private readonly List<Rect> _panels = new List<Rect>();

        private bool IsOverHud(Vector2 screen)
        {
            var scale = Screen.height / ReferenceHeight;
            var gui = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
            return _panels.Any(r => r.Contains(gui)) || FxPlayground.IsOver(screen);
        }

        private static GUIStyle TextStyle => new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };

        private float GuiWidth => Screen.width / (Screen.height / ReferenceHeight);

        private Rect Panel(float x, float y, float w, float h)
        {
            var r = new Rect(x, y, w, h);
            _panels.Add(r);
            return r;
        }

        /// <summary>Top right: the open unit card, and under it whatever tile or objective is under the mouse.</summary>
        private void DrawHoverPanels()
        {
            var cardBottom = DrawUnitCard();
            TextPanel(HoverText(), true, false, 280f, cardBottom > 0f ? cardBottom + Margin : (float?)null);
        }

        /// <summary>A word-wrapped text box sized to its content (capped), at a corner, or at <paramref name="top"/>.</summary>
        private void TextPanel(string text, bool right, bool bottom, float maxHeight, float? top = null)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var style = TextStyle;
            var h = Mathf.Min(maxHeight, style.CalcHeight(new GUIContent(text), PanelWidth - 12f) + 12f);
            var x = right ? GuiWidth - PanelWidth - Margin : Margin;
            var y = top ?? (bottom ? ReferenceHeight - h - Margin : Margin);
            GUILayout.BeginArea(Panel(x, y, PanelWidth, h), GUI.skin.box);
            GUILayout.Label(text, style);
            GUILayout.EndArea();
        }

        private void OnGUI()
        {
            HudArt.ApplyFont();
            if (_board == null)
                return;
            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (Event.current.type == EventType.Layout)
                _panels.Clear();

            // Under the panels, so a card or button is never hidden by a badge.
            DrawIntentBadges();
            DrawStormBadges();
            DrawKillBadges();
            DrawAttackPreview();
            DrawSummonCountdown();

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

            DrawBannerHover();
            DrawCardTooltip();
            KeywordText.DrawTooltip(GuiWidth, ReferenceHeight);

            if (_markerNote != null && Time.unscaledTime < _markerNoteUntil)
                GUI.Box(new Rect(Margin, ReferenceHeight - 80, 180, 30), _markerNote);
        }

        /// <summary>
        /// Over each enemy whose intent shows (all of them with Alt, or the hovered one): a badge with an icon and
        /// the name of what it will do next.
        /// </summary>
        private void DrawIntentBadges()
        {
            if (Event.current.type != EventType.Repaint || _intentBadges.Count == 0)
                return;
            var scale = Screen.height / ReferenceHeight;
            var word = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            const float iconSize = 20f, pad = 5f, height = 26f;
            foreach (var (enemy, intent) in _intentBadges)
            {
                var view = _board.ViewOf(enemy);
                if (view == null)
                    continue;
                var screen = _camera.WorldToScreenPoint(view.TopWorld + Vector3.up * 0.06f);
                var anchor = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
                var (icon, text, color) = HudArt.ForIntent(intent, enemy);
                var w = pad + iconSize + 4f + word.CalcSize(new GUIContent(text)).x + pad;
                var rect = new Rect(anchor.x - w * 0.5f, anchor.y - height, w, height);

                GUI.DrawTexture(rect, HudArt.Solid(HudArt.PanelColor));
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), HudArt.Solid(color));
                var old = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(new Rect(rect.x + pad, rect.y + (height - iconSize) * 0.5f, iconSize, iconSize), HudArt.IconTexture(icon));
                GUI.color = old;
                word.normal.textColor = color;
                GUI.Label(new Rect(rect.x + pad + iconSize + 4f, rect.y, w - iconSize - pad, height), text, word);
            }
        }

        /// <summary>A pulsing bolt on every tile lightning will strike, seen through move highlights and statues (Storm).</summary>
        private void DrawStormBadges()
        {
            if (Event.current.type != EventType.Repaint || Battle.StormMarks.Count == 0)
                return;
            var scale = Screen.height / ReferenceHeight;
            const float size = 30f;
            var old = GUI.color;
            foreach (var p in Battle.StormMarks)
            {
                var screen = _camera.WorldToScreenPoint(IsoMath.GridToWorld(p));
                var rect = new Rect(screen.x / scale - size * 0.5f, (Screen.height - screen.y) / scale - size * 0.5f, size, size);
                GUI.DrawTexture(rect, HudArt.Solid(HudArt.PanelColor));
                GUI.color = Color.Lerp(new Color(0.55f, 0.8f, 1f), Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f));
                GUI.DrawTexture(rect, HudArt.IconTexture(HudArt.Icon.Bolt));
                GUI.color = old;
            }
        }

        /// <summary>A skull over every unit the selected unit's current action would kill: its attack, or the aimed action on the hovered tile.</summary>
        private void DrawKillBadges()
        {
            if (Event.current.type != EventType.Repaint || _busy || _selected == null || !Battle.CanAct(_selected))
                return;
            List<Unit> kills;
            if (!_action.HasValue)
                kills = Battle.AttackKills(_selected);
            else if (_hovered.HasValue)
                kills = Battle.ActionKills(_selected, _action.Value, _hovered.Value);
            else
                return;
            var scale = Screen.height / ReferenceHeight;
            const float size = 26f, intentHeight = 28f;
            var old = GUI.color;
            foreach (var unit in kills)
            {
                var view = _board.ViewOf(unit);
                if (view == null)
                    continue;
                var screen = _camera.WorldToScreenPoint(view.TopWorld + Vector3.up * 0.06f);
                var y = (Screen.height - screen.y) / scale - size - (_intentBadges.Any(b => b.enemy == unit) ? intentHeight : 0f);
                var rect = new Rect(screen.x / scale - size * 0.5f, y, size, size);
                GUI.DrawTexture(rect, HudArt.Solid(HudArt.PanelColor));
                GUI.color = new Color(1f, 0.35f, 0.3f);
                GUI.DrawTexture(rect, HudArt.IconTexture(HudArt.Icon.Skull));
                GUI.color = old;
            }
        }

        /// <summary>Break the Ritual: top centre, a pip for each turn left before the summoning completes.</summary>
        private void DrawSummonCountdown()
        {
            if (Event.current.type != EventType.Repaint || Battle.SummonTurn == 0 || Battle.SummoningComplete || Battle.IsOver)
                return;
            var left = Battle.SummonTurnsLeft;
            var urgent = left <= 1;
            var color = urgent ? new Color(1f, 0.35f, 0.3f) : HudArt.ControlColor;
            const float width = 300f, height = 58f, pip = 14f, gap = 5f;
            var rect = new Rect((GuiWidth - width) * 0.5f, Margin, width, height);
            GUI.DrawTexture(rect, HudArt.Solid(HudArt.PanelColor));
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), HudArt.Solid(color));
            var style = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = color;
            GUI.Label(new Rect(rect.x, rect.y + 4f, rect.width, 26f), urgent ? "Summoning completes after this turn!" : $"Summoning completes in {left} turns", style);
            var total = Battle.SummonTurn;
            var x = rect.center.x - (total * pip + (total - 1) * gap) * 0.5f;
            for (var i = 0; i < total; i++)
                GUI.DrawTexture(new Rect(x + i * (pip + gap), rect.y + 34f, pip, pip), HudArt.Solid(i < left ? color : new Color(0.25f, 0.25f, 0.28f)));
        }

        private string Header => _context.Test
            ? $"TEST: {(_context.Boss ? "Boss " : "")}{_context.Encounter} ({_context.Layout})"
            : $"Step {_context.Step}/{_context.Steps}:{(_context.Boss ? "Boss " : "")}{(_context.Ambush ? "Ambush! " : "")}{_context.Encounter} ({_context.Layout})    Gold {_context.Gold}";

        /// <summary>For a ritual fight: how long the shrine must hold and when more enemies arrive.</summary>
        private string RitualLine
        {
            get
            {
                if (Battle.SummonTurn > 0)
                {
                    var left = Battle.SummonTurnsLeft;
                    var standing = Battle.Totems.Count(t => t.IsAlive);
                    return $"\nRitual: smash the totems ({standing}/{Battle.Totems.Count} standing). " +
                           (left <= 1 ? "The summoning completes after this turn!" : $"The summoning completes in {left} turns.");
                }

                if (Battle.Objective == null)
                    return "";
                var line = $"\nRitual: survive {Battle.RitualTurnsLeft} more enemy turn{(Battle.RitualTurnsLeft == 1 ? "" : "s")}. " +
                           $"Shrine HP {Battle.Objective.Hp}/{Battle.Objective.MaxHp}";
                var next = Battle.PendingWaves.Select(w => w.afterEnemyTurn).DefaultIfEmpty(0).Min();
                if (next > 0)
                    line += $"\nMore enemies arrive after enemy turn {next}";
                return line;
            }
        }

        /// <summary>The battlefield, blessings and curses in play, one per line. Banners stand on the roof instead.</summary>
        private string OmenLine =>
            (Battle.Field.HasValue ? $"\nBattlefield: {Battlefields.Name(Battle.Field.Value)}" : "") +
            string.Concat(Battle.Omens.Select(o => $"\n{Omens.Name(o.omen, o.side == Team.Player)}"));

        /// <summary>Hovering a banner on the roof shows its name and rule beside the mouse. Tiles and panels come first.</summary>
        private void DrawBannerHover()
        {
            if (Event.current.type != EventType.Repaint || _hovered.HasValue)
                return;
            var screen = Mouse.current.position.ReadValue();
            if (IsOverHud(screen))
                return;
            var banner = BannerView.At(_camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z)));
            if (!banner.HasValue)
                return;

            const float width = 300f, pad = 10f, nameHeight = 24f;
            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            var ruleStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, padding = new RectOffset() };
            var rule = Banners.Describe(banner.Value);
            var height = pad * 2 + nameHeight + ruleStyle.CalcHeight(new GUIContent(rule), width - pad * 2);
            var scale = Screen.height / ReferenceHeight;
            var x = Mathf.Min(screen.x / scale + 16f, GuiWidth - width - Margin);
            var y = Mathf.Min((Screen.height - screen.y) / scale + 16f, ReferenceHeight - height - Margin);
            GUI.Box(new Rect(x, y, width, height), GUIContent.none, HudArt.PanelStyle(HudArt.PanelColor, 0));
            GUI.Label(new Rect(x + pad, y + pad, width - pad * 2, nameHeight), Banners.Name(banner.Value), nameStyle);
            GUI.Label(new Rect(x + pad, y + pad + nameHeight, width - pad * 2, height - pad * 2 - nameHeight), rule, ruleStyle);
        }

        /// <summary>The battlefield's rule in full, for the placement panel. Empty without one.</summary>
        private string FieldRule => Battle.Field.HasValue
            ? $"{Battlefields.Name(Battle.Field.Value)}: {Battlefields.Describe(Battle.Field.Value)}"
            : "";

        private void DrawPlacementHud()
        {
            var army = Battle.Units.Where(u => u.Team == Team.Player && !u.IsObjective).ToList();
            var omens = OmenLine.Trim();
            var rule = FieldRule;
            var ruleHeight = rule.Length > 0 ? TextStyle.CalcHeight(new GUIContent(rule), PanelWidth - 12f) + 6f : 0f;
            var h = 110 + army.Count * (ButtonHeight + 2) + 10 + (omens.Length > 0 ? 20 * omens.Split('\n').Length : 0) + ruleHeight;
            GUILayout.BeginArea(Panel(Margin, Margin, PanelWidth, h), GUI.skin.box);
            GUILayout.Label(Header);
            GUILayout.Label("Place your army in the blue tiles. Click a placed statue to move it.", TextStyle);
            GUILayout.Label(_context.LayoutRule, TextStyle);
            if (rule.Length > 0)
                GUILayout.Label(rule, TextStyle);
            if (omens.Length > 0)
                GUILayout.Label(omens, TextStyle);
            foreach (var u in army)
            {
                var item = u.Equipment != null ? $" [{u.Equipment.Name}]" : "";
                var level = u.Member != null && u.Member.Level > 0 ? $" (level {u.Member.Level})" : "";
                var label = $"{u.Stats.Name}{level}{item}{(u.IsPlaced ? "  (placed)" : "")}{(u == _toPlace ? "  <" : "")}";
                if (GUILayout.Button(label, GUILayout.Height(ButtonHeight)))
                    _toPlace = u;
            }

            GUILayout.EndArea();

            var buttons = 1 + (DebugToolsEnabled ? 1 : 0);
            var ch = buttons * (ButtonHeight + 4) + 16;
            GUILayout.BeginArea(Panel(GuiWidth - CommandWidth - Margin, ReferenceHeight - ch - Margin, CommandWidth, ch), GUI.skin.box);
            GUI.enabled = Battle.AllPlaced;
            if (GUILayout.Button("Start battle", GUILayout.Height(ButtonHeight)))
                StartBattle();
            GUI.enabled = true;
            DrawDebugButton();
            GUILayout.EndArea();

            DrawHoverPanels();
        }

        private void DrawTurnHud()
        {
            var playerTurn = Battle.Phase == Phase.PlayerTurn && !_busy;

            // Top-left: turn, and the selected unit's AP and moves (its card shows the rest).
            var info = $"{Header}\nTurn {Battle.TurnNumber}: {(playerTurn ? "your turn" : "enemy turn")}{RitualLine}{OmenLine}";
            var apLine = "";
            if (_selected != null)
            {
                var cost = playerTurn ? PreviewCost() : 0;
                var moves = playerTurn ? PreviewMoves() : 0;
                apLine = cost > 0 ? $"AP {_selected.Ap} / {_selected.MaxAp}   this action: -{cost} -> {_selected.Ap - cost} left"
                    : $"AP {_selected.Ap} / {_selected.MaxAp}";
                apLine += _selected.ActedThisTurn ? "   Moves: none (acted)"
                    : moves > 0 ? $"   Moves {_selected.Moves} / {_selected.MaxMoves} (-{moves})" : $"   Moves {_selected.Moves} / {_selected.MaxMoves}";
            }

            var style = TextStyle;
            var apStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            apStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
            var infoHeight = Mathf.Min(280f, style.CalcHeight(new GUIContent(info), PanelWidth - 12f) + (apLine.Length > 0 ? 24f : 0f) + 14f);
            GUILayout.BeginArea(Panel(Margin, Margin, PanelWidth, infoHeight), GUI.skin.box);
            if (apLine.Length > 0)
                GUILayout.Label(apLine, apStyle);
            GUILayout.Label(info, style);
            GUILayout.EndArea();

            DrawHoverPanels();

            // Bottom-left: the current hint.
            if (playerTurn && !_confirmEndTurn)
                TextPanel(_hint, false, true, 90f);

            if (playerTurn)
            {
                DrawActionCards();
                DrawCommands();
            }
        }

        private const float ActionCardWidth = 210f;
        private const float ActionCardGap = 10f;

        /// <summary>
        /// Bottom centre: one card per special action of the selected unit, with its key, name, cost, cooldown
        /// and what it does. Clicking a card (or pressing its key) aims it; clicking it again cancels.
        /// </summary>
        private void DrawActionCards()
        {
            if (_confirmEndTurn || _selected == null || !Battle.CanAct(_selected))
                return;
            var actions = _selected.Actions.ToList();
            if (actions.Count == 0)
                return;

            // The free space between the hint panel (left) and the commands (right).
            var left = Margin + PanelWidth + ActionCardGap;
            var right = GuiWidth - CommandWidth - Margin - ActionCardGap;
            var width = Mathf.Min(ActionCardWidth, (right - left - (actions.Count - 1) * ActionCardGap) / actions.Count);
            var total = actions.Count * width + (actions.Count - 1) * ActionCardGap;
            var x = Mathf.Max(left, (GuiWidth - total) * 0.5f);

            var title = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };
            var cost = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 };
            var text = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            text.normal.textColor = new Color(0.9f, 0.89f, 0.85f);
            var inner = width - 2 * 10f;

            // Every card gets the height of the tallest, so the row lines up.
            var height = 0f;
            foreach (var action in actions)
                height = Mathf.Max(height, title.CalcHeight(new GUIContent(Abilities.Name(action)), inner - 28f) +
                                           cost.CalcHeight(new GUIContent("x"), inner) +
                                           KeywordText.Height(Capitalise(Abilities.Short(action)), text, inner) + 30f);
            var y = ReferenceHeight - Margin - height;

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var rect = Panel(x + i * (width + ActionCardGap), y, width, height);
                var aiming = _action == action;
                var cooldown = _selected.CooldownLeft(action);
                var usable = Battle.ActionTargets(_selected, action).Count > 0;
                var hover = rect.Contains(Event.current.mousePosition);

                GUI.enabled = usable || aiming;
                var background = aiming ? HudArt.CardAimColor : hover && usable ? HudArt.CardHoverColor : HudArt.CardColor;
                if (GUI.Button(rect, GUIContent.none, HudArt.PanelStyle(background, 0)))
                    ToggleAction(action);
                GUI.enabled = true;

                var old = GUI.color;
                if (!usable && !aiming)
                    GUI.color = new Color(1f, 1f, 1f, 0.5f);
                // Gold edge on the card being aimed.
                if (aiming)
                    GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), HudArt.Solid(new Color(1f, 0.85f, 0.3f)));

                var keyRect = new Rect(rect.x + 10f, rect.y + 10f, 22f, 22f);
                GUI.DrawTexture(keyRect, HudArt.Solid(new Color(0.3f, 0.3f, 0.34f)));
                GUI.Label(keyRect, $"<b>{i + 1}</b>", new GUIStyle(cost) { alignment = TextAnchor.MiddleCenter });

                GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 10f, inner, rect.height - 20f));
                GUILayout.BeginHorizontal();
                GUILayout.Space(28f);
                GUILayout.Label($"<color=white>{Abilities.Name(action)}</color>", title);
                GUILayout.EndHorizontal();
                var status = aiming ? $"<color={ActionColor}>Aiming: right-click a yellow tile</color>"
                    : cooldown > 0 ? $"<color={NextColor}>Ready in {cooldown} turn{(cooldown == 1 ? "" : "s")}</color>"
                    : !usable ? $"<color={DimColor}>No target in reach</color>"
                    : Battle.CooldownFor(_selected, action) == 0 ? $"<color={ActionColor}>{CardCost(_selected, action)} AP  ·  once per battle</color>"
                    : $"<color={ActionColor}>{CardCost(_selected, action)} AP  ·  cooldown {Battle.CooldownFor(_selected, action)}</color>";
                GUILayout.Label(status, cost);
                KeywordText.LayoutLabel(Capitalise(Abilities.Short(action)), text, inner);
                GUILayout.EndArea();
                GUI.color = old;
            }
        }

        /// <summary>Bottom-right: Done and End turn. Always fully visible.</summary>
        private void DrawCommands()
        {
            float h;
            if (_confirmEndTurn)
                h = 110f;
            else
                h = (2 + (DebugToolsEnabled ? 1 : 0)) * (ButtonHeight + 4) + 30f;

            GUILayout.BeginArea(Panel(GuiWidth - CommandWidth - Margin, ReferenceHeight - h - Margin, CommandWidth, h), GUI.skin.box);
            if (_confirmEndTurn)
            {
                var names = string.Join(", ", UnitsThatCanStillAct().Select(u => u.Stats.Name));
                GUILayout.Label($"{names} can still act. End the turn anyway?", TextStyle);
                if (GUILayout.Button("Yes, end turn", GUILayout.Height(ButtonHeight)))
                {
                    _logger.RecordEndTurnPrompt(UnitsThatCanStillAct(), Battle.TurnNumber, "confirmed");
                    EndTurn();
                }

                if (GUILayout.Button("No, keep playing", GUILayout.Height(ButtonHeight)))
                {
                    _logger.RecordEndTurnPrompt(UnitsThatCanStillAct(), Battle.TurnNumber, "cancelled");
                    _confirmEndTurn = false;
                }

                GUILayout.EndArea();
                return;
            }

            GUI.enabled = _selected != null;
            if (GUILayout.Button(_selected != null ? $"Done with {_selected.Stats.Name}" : "Done (select a unit)", GUILayout.Height(ButtonHeight)))
                FinishSelectedUnit();
            GUI.enabled = true;
            // Extra space so End turn is harder to hit by accident.
            GUILayout.Space(12);
            if (GUILayout.Button("End turn", GUILayout.Height(ButtonHeight)))
                RequestEndTurn();
            DrawDebugButton();
            GUILayout.EndArea();
        }

        private void DrawEndHud()
        {
            const float w = 280, h = 140;
            GUILayout.BeginArea(Panel((GuiWidth - w) * 0.5f, 120, w, h), GUI.skin.box);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            var centred = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            if (Battle.Phase == Phase.Victory)
            {
                var survivors = Battle.LivingArmy.Count();
                GUILayout.Label(Battle.RitualComplete ? "The ritual is complete!" : Battle.TotemsBroken ? "The ritual is broken!" : "Victory!", style);
                GUILayout.Label($"{survivors} survived: +{_context.GoldIfWon(survivors)} gold", centred);
                if (Battle.Objective != null)
                    GUILayout.Label("The shrine held. Choose a blessing next.", centred);
                if (Battle.TotemsBroken)
                    GUILayout.Label("The guards flee. Choose an item next.", centred);
                else if (_context.Elite)
                    GUILayout.Label(_context.Test ? "Elite beaten. In a run: an item pick." : "Elite beaten. Choose an item next.", centred);
                if (Battle.StolenGold > 0)
                    GUILayout.Label($"Cutpurses got away with {Battle.StolenGold} gold.", centred);
            }
            else if (Battle.ScenarioLost)
            {
                GUILayout.Label(Battle.SummoningComplete ? "The summoning completed" : "The shrine broke", style);
                GUILayout.Label(_context.Test ? "In a run: no gold and a random curse." : "No gold, and a curse follows you. The run goes on.", centred);
            }
            else
            {
                GUILayout.Label("Defeat", style);
                GUILayout.Label(_context.Test ? "In a run, this would end it." : "The run is over.", centred);
            }

            if (GUILayout.Button("Continue"))
                _onContinue?.Invoke();
            GUILayout.EndArea();
        }

        private string HoverText()
        {
            if (_hoveredUnit != null && _hoveredUnit.IsObjective)
                return $"{_hoveredUnit.Stats.Name}  HP {_hoveredUnit.Hp}/{_hoveredUnit.MaxHp}\n{_hoveredUnit.Stats.Role}\n" +
                       (_hoveredUnit.Team == Team.Player
                           ? "Enemies go for it unless one of your units is next to them."
                           : $"Guards go for any of your units within {EnemyAI.TotemThreatRange} tiles of a totem. Their own bombs don't hurt it.");


            if (_hovered.HasValue)
            {
                var p = _hovered.Value;
                if (Battle.SunMarks.Contains(p))
                    return $"Sun sigil: a sun beam strikes this tile at the start of the enemy turn for {Battle.SunDamage} damage to whoever stands here.";
                if (Battle.Shroud.TryGetValue(p, out var side))
                    return side == Team.Player
                        ? "Shrouded: enemies can't hit units here from range."
                        : "Shrouded: your units can't hit enemies here from range.";
                if (Battle.BombAt(p) is Bomb bomb)
                    return $"Bomb: explodes at the start of the enemy turn for {bomb.Damage} damage to whoever stands on this tile.";
                if (Battle.Caltrops.Contains(p))
                    return $"Caltrops: a unit of yours stepping here takes {Battle.CaltropsDamage} and its movement ends.";
                if (Battle.OverwatchTiles.Contains(p))
                    return "Overwatch: a crossbowman is aiming here. The first of your units to walk onto a highlighted tile is shot and stops.";
                if (Battle.Snares.Contains(p))
                    return "Your snare: the first enemy to walk onto it stops, takes damage and is pinned. Enemies can't see it.";
                if (Battle.CoverTiles.Contains(p))
                    return "Overwatch: your archer is aiming here. The first enemy to walk onto a highlighted tile is shot and stops.";
                if (Battle.MineAt(p) != null)
                    return $"Your mine: an enemy stepping here sets it off ({Battle.MineAt(p).Damage} damage to whoever stands on this tile).";
                if (Battle.IsBarricade(p))
                    return $"Barricade: {Battle.BarricadeHpAt(p)}/{Battle.BarricadeHp} HP. Blocks paths and shots; either side can attack it, and it breaks at 0." +
                           (_selected != null && Battle.CanAttackBarricade(_selected, p) ? $"\nRight-click: attack it for {Battle.EffectiveDamage(_selected)} ({Battle.AttackCost} AP)." : "");
                if (Battle.IsRubble(p))
                    return $"Rubble: blocks walking but not shots, and gives no cover. A unit pushed into it is crushed for {Battle.CollisionDamage + Battle.CrushDamage} and the rubble breaks. One hit clears it." +
                           (_selected != null && Battle.CanAttackBarricade(_selected, p) ? $"\nRight-click: clear it ({Battle.AttackCost} AP)." : "");
                if (Battle.BurningTiles.TryGetValue(p, out var burnRounds))
                    return $"Burning ground ({burnRounds} round(s) left): {Battle.BurnDamage} damage to whoever starts a turn here. Enemies walk through it but never stop on it.";
                if (Battle.RemainsAt(p) is Unit bones)
                    return $"Bone pile ({bones.Stats.Name}). The Necromancer can raise it.\nStand on it to stop that.";
                if (_selected != null && !_action.HasValue && Battle.GetReachable(_selected).TryGetValue(p, out var cost))
                    return Battle.InEnemyReach(_selected, _selected.Pos)
                        ? $"Move here: {cost} move(s) (leaving an enemy's reach uses up all your moves)"
                        : $"Move here: {cost} move(s)";
            }

            return null;
        }

        // ---------- Unit card (right-click a unit) ----------

        private const float CardWidth = 300f;
        private const float CardPadding = 12f;
        private const float CardHpBarHeight = 16f;
        private const float TooltipWidth = 280f;
        private const string EnemyHeader = "#ff8f7a";
        private const string PlayerHeader = "#8cc8ff";
        private const string NextColor = "#ffb347";
        private const string PassiveColor = "#9be39b";
        private const string ActionColor = "#ffd966";
        private const string DimColor = "#b8b8b8";
        private const string TextColor = "#e6e2da";

        /// <summary>The HP a planned attack would take off (shown over the target's statue).</summary>
        private const string LossColor = "#ff8a6a";

        // Keyword chip backgrounds on the card (placeholders for the art chat).
        private static readonly Color PassiveChip = new Color(0.14f, 0.24f, 0.14f);
        private static readonly Color ActionChip = new Color(0.28f, 0.23f, 0.08f);
        private static readonly Color GoodChip = new Color(0.1f, 0.24f, 0.3f);
        private static readonly Color BadChip = new Color(0.38f, 0.13f, 0.1f);
        private static readonly Color PlainChip = new Color(0.22f, 0.22f, 0.25f);

        /// <summary>
        /// The unit whose card is open when it isn't the selected one: left-click any unit. Without it the selected
        /// unit's card shows, unless Esc hid it (<see cref="_cardHidden"/>, until the next selection).
        /// </summary>
        private Unit _cardUnit;
        private bool _cardHidden;

        /// <summary>The unit whose card shows now, or null.</summary>
        private Unit CardUnit => _cardUnit ?? (_cardHidden ? null : _selected);

        /// <summary>The description of the keyword under the pointer on the card, drawn last, beside it.</summary>
        private string _cardTooltip;
        private Rect _cardTooltipAnchor;

        /// <summary>A left-click on the board: opens that unit's card, or closes the open one on empty ground.</summary>
        private void OpenCardFor(Unit unit)
        {
            if (unit != null && unit.IsObjective)
                return;
            _cardUnit = unit;
            _cardHidden = false;
        }

        /// <summary>Esc cancels the aimed action, or else closes the card.</summary>
        private void UpdateCardInput(Keyboard keyboard)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_action.HasValue)
                {
                    _hint = $"{Abilities.Name(_action.Value)} cancelled.";
                    _action = null;
                }
                else
                {
                    _cardUnit = null;
                    _cardHidden = true;
                }
            }

            if (_cardUnit != null && !_cardUnit.IsAlive)
                _cardUnit = null;
        }

        /// <summary>
        /// The open unit card at the top right: name, HP, statuses, what it does next (enemies), passives and actions,
        /// each as its name only; hovering a name shows what it does. Returns the card's bottom edge, or 0 if none.
        /// </summary>
        private float DrawUnitCard()
        {
            _cardTooltip = null;
            var u = CardUnit;
            if (u == null || !u.IsAlive)
                return 0f;

            var styles = new CardStyles();
            var mouse = Event.current.mousePosition;
            var inner = CardWidth - 2 * CardPadding;
            var height = Mathf.Min(PaintCard(u, new CardPainter(0f, 0f, inner, false, mouse, styles)) + 2 * CardPadding, ReferenceHeight - 2 * Margin);

            var rect = Panel(GuiWidth - CardWidth - Margin, Margin, CardWidth, height);
            GUI.Box(rect, GUIContent.none, HudArt.PanelStyle(HudArt.PanelColor, 0));
            // A strip in the team's colour along the top.
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), HudArt.Solid(u.Team == Team.Enemy ? HudArt.HarmColor : new Color(0.55f, 0.78f, 1f)));

            var painter = new CardPainter(rect.x + CardPadding, rect.y + CardPadding, inner, true, mouse, styles);
            PaintCard(u, painter);
            _cardTooltip = painter.Tooltip;
            _cardTooltipAnchor = painter.TooltipAnchor;
            return rect.yMax;
        }

        /// <summary>Lays the card out top to bottom (measuring only, or drawing); returns its content height.</summary>
        private float PaintCard(Unit u, CardPainter p)
        {
            var s = p.Styles;
            var top = p.Top;

            var team = u.Team == Team.Enemy ? EnemyHeader : PlayerHeader;
            p.Text($"<color={team}>{u.Stats.Name}</color>" + (u.Member != null ? $"  <color={DimColor}><size=13>level {u.Member.Level}</size></color>" : ""), s.Header);
            p.Space(2f);
            p.HpBar(u);
            p.Space(3f);
            p.Text($"<color={DimColor}>{u.MaxAp} AP  ·  {u.MaxMoves} {(u.MaxMoves == 1 ? "move" : "moves")}  ·  range {u.Range}  ·  {u.Stats.Weight.ToString().ToLowerInvariant()}</color>", s.Small);

            var states = StateChips(u);
            if (states.Count > 0)
            {
                p.Space(6f);
                foreach (var (name, text, good) in states)
                    p.Chip(name, good == true ? GoodChip : good == false ? BadChip : PlainChip, text, s.Chip);
            }

            if (u.Team == Team.Enemy && !Battle.IntentsHidden)
            {
                p.Space(8f);
                p.Text($"<color={NextColor}>Next: {IntentText(EnemyAI.PredictIntent(Battle, u))}</color>", s.Body);
            }

            var passives = Abilities.PassivesOf(u.Stats);
            foreach (var technique in TechniquesOf(u).Where(t => !t.Action.HasValue))
                passives.Add((technique.Name, technique.Description));
            var itemAbility = u.Equipment?.Ability ?? Ability.None;
            if (itemAbility != Ability.None)
                passives.Add((u.Equipment.Name, $"Item. {Capitalise(Abilities.Describe(itemAbility))}"));
            if (passives.Count > 0)
            {
                p.Space(8f);
                foreach (var (passive, text) in passives)
                    p.Chip($"<color={PassiveColor}>{passive}</color>", PassiveChip, Capitalise(text), s.Chip);
            }

            var actions = Abilities.ActionsOf(u.Stats, Battle.EffectiveDamage(u));
            foreach (var technique in TechniquesOf(u).Where(t => t.Action.HasValue))
                actions.Add((technique.Name, CardCost(u, technique.Action.Value), technique.Action, technique.Description));
            if (actions.Count > 0)
                p.Space(8f);
            foreach (var (action, baseCost, kind, text) in actions)
            {
                // The same cost the action card shows: Quick Fuse and an unused Twincast make it free.
                var cost = kind.HasValue && u.HasAction(kind.Value) ? CardCost(u, kind.Value) : baseCost;
                var cooldown = kind.HasValue ? $" · cd {Battle.CooldownFor(u, kind.Value)}" : "";
                var left = kind.HasValue ? u.CooldownLeft(kind.Value) : 0;
                p.Chip($"<color={ActionColor}>{action}</color>", ActionChip, Capitalise(text), s.Chip);
                p.Inline($"<color={DimColor}>{cost} AP{cooldown}</color>" + (left > 0 ? $"  <color={NextColor}>ready in {left}</color>" : ""), s.Small);
                p.NewLine();
            }

            return p.Bottom - top;
        }

        /// <summary>The text styles of the unit card.</summary>
        private sealed class CardStyles
        {
            public readonly GUIStyle Header = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 18, fontStyle = FontStyle.Bold, padding = new RectOffset() };
            public readonly GUIStyle Body = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, fontSize = 14, padding = new RectOffset() };
            public readonly GUIStyle Small = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false, fontSize = 13, padding = new RectOffset() };
            public readonly GUIStyle Chip = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = false, fontSize = 14, padding = new RectOffset() };
            public readonly GUIStyle BarText = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, padding = new RectOffset() };

            public CardStyles()
            {
                foreach (var style in new[] { Header, Body, Small, Chip, BarText })
                    style.normal.textColor = new Color(0.9f, 0.89f, 0.85f);
            }
        }

        /// <summary>
        /// Places the card's pieces left to right, wrapping to the next row when one doesn't fit. When measuring it
        /// only advances; when drawing it also remembers which keyword chip the pointer is on.
        /// </summary>
        private sealed class CardPainter
        {
            private const float ChipPad = 6f;
            private const float Gap = 5f;

            public readonly CardStyles Styles;
            private readonly float _left;
            private readonly float _width;
            private readonly bool _draw;
            private readonly Vector2 _mouse;
            private float _x;
            private float _y;
            private float _row;

            public string Tooltip;
            public Rect TooltipAnchor;

            public CardPainter(float left, float top, float width, bool draw, Vector2 mouse, CardStyles styles)
            {
                _left = _x = left;
                _y = Top = top;
                _width = width;
                _draw = draw;
                _mouse = mouse;
                Styles = styles;
            }

            public float Top { get; }
            public float Bottom => _y + _row;

            /// <summary>Ends the current row.</summary>
            public void NewLine()
            {
                _y += _row;
                _row = 0f;
                _x = _left;
            }

            /// <summary>Ends the current row and leaves a gap.</summary>
            public void Space(float gap)
            {
                NewLine();
                _y += gap;
            }

            /// <summary>Word-wrapped text on rows of its own.</summary>
            public void Text(string text, GUIStyle style)
            {
                NewLine();
                var h = style.CalcHeight(new GUIContent(text), _width);
                if (_draw)
                    GUI.Label(new Rect(_left, _y, _width, h), text, style);
                _y += h;
            }

            /// <summary>A short piece of text in the current row.</summary>
            public void Inline(string text, GUIStyle style) => Place(text, style, null, null);

            /// <summary>A keyword on a coloured chip; hovering it shows <paramref name="tooltip"/>.</summary>
            public void Chip(string text, Color color, string tooltip, GUIStyle style) => Place(text, style, color, tooltip);

            private void Place(string text, GUIStyle style, Color? chip, string tooltip)
            {
                var size = style.CalcSize(new GUIContent(text));
                var pad = chip.HasValue ? ChipPad : 0f;
                var w = Mathf.Min(size.x + 2 * pad, _width);
                var h = size.y + (chip.HasValue ? 4f : 0f);
                if (_x > _left && _x + w > _left + _width)
                    NewLine();
                var rect = new Rect(_x, _y, w, h);
                if (_draw)
                {
                    var hovered = tooltip != null && rect.Contains(_mouse);
                    if (chip.HasValue)
                        GUI.DrawTexture(rect, HudArt.Solid(hovered ? Color.Lerp(chip.Value, Color.white, 0.18f) : chip.Value));
                    GUI.Label(new Rect(rect.x + pad, rect.y + (h - size.y) * 0.5f, w - 2 * pad, size.y), text, style);
                    if (hovered)
                    {
                        Tooltip = tooltip;
                        TooltipAnchor = rect;
                    }
                }

                _x += w + Gap;
                _row = Mathf.Max(_row, h + 4f);
            }

            /// <summary>The HP bar across the card, with "HP / max" written on it.</summary>
            public void HpBar(Unit u)
            {
                NewLine();
                if (_draw && Event.current.type == EventType.Repaint)
                {
                    var bar = new Rect(_left, _y, _width, CardHpBarHeight);
                    GUI.DrawTexture(bar, HudArt.Solid(new Color(0.2f, 0.2f, 0.22f)));
                    var frac = u.MaxHp > 0 ? Mathf.Clamp01((float)u.Hp / u.MaxHp) : 0f;
                    var fill = u.Team == Team.Player ? new Color(0.3f, 0.62f, 0.34f) : new Color(0.7f, 0.25f, 0.21f);
                    GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * frac, bar.height), HudArt.Solid(fill));
                    // In pieces, one per point of max HP, like the bar over the statue.
                    var gap = HudArt.Solid(HudArt.PanelColor);
                    for (var i = 1; i < u.MaxHp; i++)
                        GUI.DrawTexture(new Rect(bar.x + bar.width * i / u.MaxHp - 1f, bar.y, 2f, bar.height), gap);
                    GUI.Label(bar, $"{u.Hp} / {u.MaxHp}", Styles.BarText);
                }

                _y += CardHpBarHeight;
            }
        }

        /// <summary>
        /// The hovered name's description beside the card (on top of everything), with its keywords highlighted and
        /// explained underneath, since the pointer can't reach into this box.
        /// </summary>
        private void DrawCardTooltip()
        {
            if (string.IsNullOrEmpty(_cardTooltip))
                return;
            const float pad = 8f;
            var inner = TooltipWidth - 2 * pad;
            var style = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14, padding = new RectOffset() };
            style.normal.textColor = new Color(0.92f, 0.9f, 0.86f);
            var gloss = new GUIStyle(style) { fontSize = 12 };
            gloss.normal.textColor = new Color(0.72f, 0.71f, 0.68f);
            var glossary = KeywordText.Glossary(_cardTooltip);
            var textHeight = KeywordText.Height(_cardTooltip, style, inner);
            var glossHeight = glossary.Length > 0 ? gloss.CalcHeight(new GUIContent(glossary), inner) + 6f : 0f;
            var height = textHeight + glossHeight + 2 * pad;
            // To the left of the card, level with the name.
            var x = Mathf.Max(Margin, GuiWidth - CardWidth - Margin - TooltipWidth - 6f);
            var y = Mathf.Clamp(_cardTooltipAnchor.y - pad, Margin, ReferenceHeight - height - Margin);
            var rect = new Rect(x, y, TooltipWidth, height);
            GUI.Box(rect, GUIContent.none, HudArt.PanelStyle(HudArt.CardColor, 0));
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), HudArt.Solid(new Color(1f, 0.85f, 0.4f)));
            KeywordText.Draw(new Rect(rect.x + pad, rect.y + pad, inner, textHeight), _cardTooltip, style);
            if (glossary.Length > 0)
                GUI.Label(new Rect(rect.x + pad, rect.y + pad + textHeight + 6f, inner, glossHeight), glossary, gloss);
        }

        /// <summary>The techniques a unit has learned (none for enemies and summons).</summary>
        private static IEnumerable<Technique> TechniquesOf(Unit u) => u.Member?.Techniques ?? (IEnumerable<Technique>)new Technique[0];

        // ---------- Attack preview (on the target's statue) ----------

        /// <summary>The statue whose HP bar shows the hovered attack's damage.</summary>
        private UnitView _lossView;

        /// <summary>
        /// The HP the selected unit's attack would take off the hovered enemy: a pale, pulsing chunk on that enemy's
        /// HP bar over the statue.
        /// </summary>
        private void UpdateLossPreview()
        {
            UnitView view = null;
            var loss = 0;
            if (AttackPreviewTarget() is Unit u)
            {
                loss = Battle.PredictHpLoss(_selected, u, out _);
                view = _board.ViewOf(u);
            }

            if (_lossView != null && _lossView != view)
                _lossView.ShowLossPreview(0);
            _lossView = view;
            if (view != null)
                view.ShowLossPreview(loss);
        }

        /// <summary>The hovered enemy a click would attack (or walk to and attack) with the selected unit, or null.</summary>
        private Unit AttackPreviewTarget()
        {
            var u = _hoveredUnit;
            if (_busy || Battle.Phase != Phase.PlayerTurn || _selected == null || _selected.Team != Team.Player || _action.HasValue ||
                u == null || u.Team != Team.Enemy)
                return null;
            return Battle.CanAttack(_selected, u) ? u : null;
        }

        /// <summary>
        /// Over the hovered enemy's statue: "-3" (or who guards it) for the selected unit's attack; for an enemy it
        /// would have to walk to, "walk 2, attack" by the pointer.
        /// </summary>
        private void DrawAttackPreview()
        {
            if (Event.current.type != EventType.Repaint || _busy || Battle.Phase != Phase.PlayerTurn || _selected == null ||
                _selected.Team != Team.Player || _action.HasValue || _hoveredUnit == null || _hoveredUnit.Team != Team.Enemy)
                return;
            var u = _hoveredUnit;
            var style = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15, fontStyle = FontStyle.Bold, padding = new RectOffset(), alignment = TextAnchor.MiddleCenter };
            string text;
            Vector2 centre;
            if (Battle.CanAttack(_selected, u))
            {
                var loss = Battle.PredictHpLoss(_selected, u, out var guard);
                text = guard != null ? $"<color={LossColor}>the {guard.Stats.Name} guards it</color>" : $"<color={LossColor}>-{loss}</color>";
                var view = _board.ViewOf(u);
                if (view == null)
                    return;
                var scale = Screen.height / ReferenceHeight;
                var screen = _camera.WorldToScreenPoint(view.BarLeftWorld);
                // Left of the bar (statuses sit to its right).
                var size = style.CalcSize(new GUIContent(text));
                centre = new Vector2(screen.x / scale - 8f - size.x * 0.5f, (Screen.height - screen.y) / scale);
            }
            else if (Battle.FindAttackSpot(_selected, u) is var spot && spot.HasValue)
            {
                text = $"<color={NextColor}>walk {spot.Value.cost}, attack</color>";
                var mouse = Event.current.mousePosition;
                var size = style.CalcSize(new GUIContent(text));
                centre = new Vector2(mouse.x + 18f + size.x * 0.5f, mouse.y + 22f);
            }
            else
                return;

            var content = new GUIContent(text);
            var box = style.CalcSize(content) + new Vector2(10f, 4f);
            var rect = new Rect(centre.x - box.x * 0.5f, centre.y - box.y * 0.5f, box.x, box.y);
            GUI.DrawTexture(rect, HudArt.Solid(HudArt.PanelColor));
            GUI.Label(rect, content, style);
        }

        /// <summary>The popup for a boss entering a Stone Heart phase.</summary>
        private static string PhaseText(Unit boss, int phase)
        {
            if (boss.Has(Ability.SoulHarvest))
                return $"Phase {phase}! The dead climb the walls";
            if (boss.Has(Ability.Wrath))
                return phase >= Battle.BossPhases ? $"Phase {phase}! The roof cracks: rocks fall" : $"Phase {phase}!";
            return $"Phase {phase}!";
        }

        private static string Capitalise(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        /// <summary>
        /// What is true for the unit right now, as chips: a short name, what it means, and whether it helps (true)
        /// or hurts (false) the unit, or neither (null). Statuses, pack state, run bonuses, zone of control.
        /// </summary>
        private List<(string name, string text, bool? good)> StateChips(Unit u)
        {
            var chips = new List<(string, string, bool?)>();
            switch (Battle.GetPackState(u))
            {
                case PackState.Bonded:
                    chips.Add(($"Pack Bond +{Battle.PackBondHp}", $"+{Battle.PackBondHp} max HP while the Pack Leader lives.", true));
                    break;
                case PackState.Leaderless:
                    chips.Add(($"Leaderless -{Battle.PackBondHp}", $"-{Battle.PackBondHp} max HP: the Pack Leader is dead.", false));
                    break;
            }

            if (u.Shield > 0)
                chips.Add(($"Shield {u.Shield}", $"Blocks the next {u.Shield} damage. Lasts until used up.", true));
            if (u.MoonShield > 0)
                chips.Add(($"Moon shield {u.MoonShield}", $"Blocks the next {u.MoonShield} damage, gone at your next turn.", true));
            if (u.CryShield > 0)
                chips.Add(($"Battlecry shield {u.CryShield}", $"Blocks the next {u.CryShield} damage, gone at your next turn.", true));
            if (u.ShieldUpTurns > 0)
                chips.Add(($"Shield Up {u.ShieldUpTurns}", $"Takes {Battle.ShieldUpReduction} less damage from each hit ({u.ShieldUpTurns} enemy turn(s) left).", true));
            if (u.IsTaunting)
                chips.Add(("Taunting", $"Enemies within {Battle.TauntRange} tiles can only target it.", null));
            if (u.ShieldThrown)
                chips.Add(("Shield thrown", $"+{Battle.ThrowShieldDamage} damage, +{Battle.ThrowShieldMoves} move.", true));
            if (u.Team == Team.Player && u.Has(Ability.Twincast) && !u.TwincastUsed)
                chips.Add(("Twincast ready", "Its next action this turn costs no AP.", true));
            if (u.IsPlanted)
                chips.Add(("Planted", $"Its shots deal +{Battle.PlantedBonus} and leave enemies pinned, until " +
                                      (u.PlantedTurns > 1 ? "the end of your next turn." : "your next turn."), true));
            if (u.Overchanneled)
                chips.Add(("Overchanneled", "Its next damaging spell this turn deals double damage.", true));
            if (u.Favored)
                chips.Add(($"Favored x{Battlefields.FavoredMultiplier}", $"Favored by fortune: {Battlefields.FavoredMultiplier}x damage this round.", true));
            if (u.IsChampion)
                chips.Add(($"Champion: {Champions.Name(u.Affix)}", $"+{Champions.ExtraHp} max HP, {Champions.Describe(u.Affix)}.", null));
            if (u.Has(Ability.StoneHeart))
                chips.Add(($"Phase {u.BossPhase}/{Battle.BossPhases}", $"Stone Heart: phase {u.BossPhase} of {Battle.BossPhases}" +
                    (Battle.NextPhaseHp(u) > 0 ? $"; can't drop below {Math.Max(u.PhaseFloor, Battle.NextPhaseHp(u))} HP this round." : "; last phase."), null));
            if (Battle.OnStormMark(u))
                chips.Add(Battle.IsRockfallMark(u.Pos)
                    ? ("Falling rocks", $"{Battle.RockfallDamage} damage after the enemy turn unless it moves.", false)
                    : ("Storm", $"Lightning gathers over it and strikes for {Battlefields.StormDamage} after the enemy turn.", false));
            if (Battle.FloorCracks.TryGetValue(u.Pos, out var cracksUnder))
                chips.Add(($"Fractured {cracksUnder}/{Battlefields.CracksToHole}", $"On a fractured tile ({cracksUnder} of {Battlefields.CracksToHole}): one more fracture and it falls in, taking this unit with it.", false));
            if (u.Team == Team.Player && Battle.Phase == Phase.PlayerTurn && u.ActedThisTurn && !u.IsDone)
                chips.Add(("Acted", "Has acted: can't move again this turn.", null));
            if (u.IsSnared)
                chips.Add(("Pinned", "Can't move.", false));
            if (u.InWrath)
                chips.Add(("Wrath", "2 actions a turn.", true));
            if (u.IsHidden)
                chips.Add(("Hidden", "Until your next turn: enemies can't target it" + (u.AmbushUsed ? "." : $". +{Battle.VanishBonus} on its next attack."), true));
            if (u.IsSlowed)
                chips.Add(("Slowed", "This turn it can move or act, not both.", false));
            if (u.Marked)
                chips.Add(($"Marked +{Battle.MarkBonus}", $"The next hit on it deals +{Battle.MarkBonus}.", false));
            if (u.PoisonTurns > 0)
                chips.Add(($"Poisoned {u.PoisonTurns}", $"Poisoned ({u.PoisonTurns} more).", false));
            if (u.CarryingLoot > 0)
                chips.Add(($"Loot {u.CarryingLoot}", $"Carrying {u.CarryingLoot} of your gold.", null));
            if (u.Fury > 0)
                chips.Add(($"Fury +{u.Fury}", $"+{u.Fury} damage for the rest of the fight.", true));
            if (u.ContractBy != null)
                chips.Add(("Soul Contract", $"Takes {Battle.ContractDamage} damage at the start of the enemy turn, unless the {u.ContractBy.Stats.Name} takes damage first.", false));
            var engaged = EngagedLine(u);
            if (engaged.Length > 0)
                chips.Add(("Engaged", engaged + ".", false));
            return chips;
        }

        /// <summary>"Engaged by ..." for a unit standing in enemy reach (zone of control), or empty.</summary>
        private string EngagedLine(Unit u)
        {
            if (u.IsObjective)
                return "";
            var holders = Battle.EngagedBy(u, u.Pos).Select(h => h.Stats.Name).ToList();
            if (holders.Count == 0)
                return "";
            var who = u.Team == Team.Player ? "its" : "its (on its turn)";
            return $"Engaged by {string.Join(", ", holders)}: stepping out of their reach uses up {who} remaining moves";
        }

        private string IntentText(Intent intent)
        {
            if (intent.Target == null)
                return "nothing";
            var name = intent.Target.Stats.Name;
            switch (intent.Kind)
            {
                case IntentKind.Raise:
                    return $"raise the {name} bone pile and every other one in range";
                case IntentKind.Smash:
                    return $"smash everyone next to it, going for the {name}";
                case IntentKind.Throw:
                    return $"throw a boulder at the {name} (the rock lands on the dark tile)";
                case IntentKind.Bomb:
                    return $"bomb the {name}'s tile (goes off next enemy turn)";
                case IntentKind.Flee:
                    return $"run off the far edge with {intent.Target.CarryingLoot} of your gold. Kill it first!";
                case IntentKind.Slam:
                    return $"slam: {Battle.SlamDamage} damage to everyone within 2 tiles (red area), then push them away";
                case IntentKind.Fling:
                    return intent.Thrown != null
                        ? $"fling the {intent.Thrown.Stats.Name} at the {name}: {Battle.FlingDamage} damage, it lands on the dark tile and can still act"
                        : $"fling an explosive from next to it at the {name}: it goes off there";
                case IntentKind.Overwatch:
                    return "overwatch: no shot this turn, so it aims; the first of your units to walk into its sight next turn gets shot";
                case IntentKind.BullRush:
                    return $"bull rush the {name}: hit it and push it";
                case IntentKind.Upheaval:
                    return $"upheaval: walk closer, then burst the ground under the {name}: {Battle.UpheavalDamage} damage and it is pushed away";
                case IntentKind.Grasp:
                    return $"grasp the {name}: no damage, but it can't move on your next turn";
                case IntentKind.CorpseBlast:
                    return $"blow up the {name} bone pile: {Battle.CorpseBlastDamage} damage to everyone in the red area, its own side included";
                case IntentKind.Howl:
                    return $"howl: every wolf gets +{Battle.HowlMoves} move this turn (wolves lit up)";
                case IntentKind.Hellswap:
                {
                    // Imps go first, so whoever stands next to the landing tile hits the swapped unit right after.
                    var landing = intent.Tile ?? intent.Target.Pos;
                    var next = Battle.Living(Team.Enemy).Where(e => !e.IsObjective && e.Stats.Damage > 0 && !e.Has(Ability.Hellswap) && Battle.Distance(landing, 1, e) == 1)
                        .Select(e => e.Stats.Name).Distinct().ToList();
                    var follow = next.Count > 0 ? $" next to the {string.Join(" and the ", next)}, which act after the imps and can hit it this turn" : "";
                    return $"hellswap: trade places with the {name}, which lands on the dark tile{follow}";
                }
                case IntentKind.RendingBlow:
                    return $"rending blow on the {name} for {intent.Amount} damage (it pays {Battle.RendingBlowCost} of its own HP)";
                case IntentKind.SoulContract:
                    return $"soul contract on the {name}: it takes {Battle.ContractDamage} damage next enemy turn unless you hurt the Devil first";
                case IntentKind.FireBreath:
                    return $"fire breath at the {name}: {intent.Amount} damage to each of your units on the lit line, and those tiles burn";
                case IntentKind.Guard:
                    return intent.Tile.HasValue && Battle.Distance(intent.Tile.Value, 1, intent.Target) > 1
                        ? $"walk towards the {name} to guard it (nobody to hit this turn)"
                        : $"stand next to the {name} (lit) and take the hits meant for it; nobody to hit this turn";
                default:
                    return $"attack the {name}";
            }
        }
    }
}
