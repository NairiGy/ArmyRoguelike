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

        public BattleContext(int step, int steps, string encounter, int gold, int runSeed, bool ambush, bool boss,
            string layout = "", string layoutRule = "", bool test = false)
        {
            Test = test;
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
        // Zone of control, while one of your units is selected: tiles next to enemies (their reach), move tiles
        // that cost extra AP to get to, and a dotted link to each enemy holding the selected unit.
        private static readonly Color EnemyReachColor = new Color(0.9f, 0.25f, 0.2f, 0.18f);
        private static readonly Color CostlyMoveColor = new Color(1f, 0.6f, 0.2f, 0.45f);
        private static readonly Color EngagedLinkColor = new Color(0.95f, 0.3f, 0.25f, 0.9f);
        private static readonly Color ShroudColor = new Color(0.25f, 0.2f, 0.55f, 0.45f);

        // Wolf tints: warm while the leader's aura reaches them, pale and cold once the leader is dead.
        private static readonly Color NearLeaderTint = new Color(1.25f, 0.8f, 0.75f);
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
        }

        /// <summary>File name of this battle's playtest log once saved, for the run log.</summary>
        public string LogFile => _logger?.SavedFile;

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
                var actions = _selected.Stats.Actions;
                for (var i = 0; i < actions.Count && i < keys.Length; i++)
                    if (keys[i].wasPressedThisFrame)
                        ToggleAction(actions[i]);
            }

            _hovered = HoveredTile();
            _hoveredUnit = _hovered.HasValue ? Battle.UnitAt(_hovered.Value) : null;
            var clicked = !_busy && !_confirmEndTurn && Mouse.current.leftButton.wasPressedThisFrame && _hovered.HasValue;

            _intent.Hide();
            HideEngagedLinks();
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

            RefreshApPips();
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

        /// <summary>AP the selected unit would spend if the player clicked the hovered tile now (0 if nothing).</summary>
        private int PreviewCost()
        {
            if (_selected == null || !_hovered.HasValue || !Battle.CanAct(_selected))
                return 0;
            var tile = _hovered.Value;
            if (_action.HasValue)
                return Battle.CanUseAction(_selected, _action.Value, tile) ? Battle.ActionCost(_action.Value) : 0;
            if (_hoveredUnit != null && _hoveredUnit.Team == Team.Enemy)
            {
                if (Battle.CanAttack(_selected, _hoveredUnit))
                    return Battle.AttackCost;
                var spot = Battle.FindAttackSpot(_selected, _hoveredUnit);
                return spot.HasValue ? spot.Value.cost + Battle.AttackCost : 0;
            }

            return Battle.GetReachable(_selected).TryGetValue(tile, out var move) ? move : 0;
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
                if (Battle.IsPlayerZone(p) && !Battle.Board.IsObstacle(p))
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

            if (action == ActionKind.Detonate)
            {
                _action = null;
                UseAction(action, _selected.Pos);
                return;
            }

            _action = action;
            _hint = $"{Abilities.Name(action)}: click a yellow tile. Press its key again to cancel.";
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

            if (target != null && target.Team == Team.Player)
            {
                if (target.IsObjective)
                    _hint = $"The {target.Stats.Name} can't act. Keep it standing until the ritual completes.";
                else if (Battle.CanAct(target))
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
                {
                    Record(attack);
                    StartCoroutine(PlayPlayerAction(attack));
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
                return;
            }

            // Clicking elsewhere drops the selection, unless that unit is mid-turn.
            if (Battle.ActiveUnit != _selected)
                Select(null);
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
            _hint = unit == null ? "Click a statue to select it." : "Click a blue tile to move or a red one to attack.";
        }

        private IEnumerator PlayPlayerAction(List<BattleEvent> events)
        {
            _busy = true;
            _board.ClearHighlights();
            yield return PlayEvents(events);

            // A unit with no AP left has nothing more to do.
            if (_selected != null && (!_selected.IsAlive || _selected.Ap == 0))
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
                    // Area actions preview the 3x3 they will hit.
                    if (action == ActionKind.Fireball || action == ActionKind.ThrowBomb)
                        foreach (var p in Battle.BlastArea(_hovered.Value))
                            _board.SetHighlight(p, BombZoneColor);
                    _board.SetHighlight(_hovered.Value, HoverColor);
                }

                return;
            }

            // Zone of control: enemy reach underneath, then move tiles, orange where getting there costs extra AP.
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

        /// <summary>Shrouded tiles, and the tiles a sun beam will strike at the start of the enemy turn.</summary>
        private void DrawOmenTiles()
        {
            foreach (var p in Battle.Shroud.Keys)
                _board.SetHighlight(p, ShroudColor);
            foreach (var p in Battle.SunMarks)
                _board.AddSunMark(p);
        }

        /// <summary>Every waiting bomb shows its blast area, so it can be dodged.</summary>
        private void DrawBombZones()
        {
            foreach (var bomb in Battle.Bombs)
            foreach (var p in Battle.BlastArea(bomb.Tile))
                _board.SetHighlight(p, BombZoneColor);
        }

        /// <summary>While hovering an enemy: what it plans to do next, and for the pack leader its aura.</summary>
        private void DrawEnemyHover()
        {
            var enemy = _hoveredUnit;
            if (enemy == null || enemy.Team != Team.Enemy)
                return;

            if (enemy.IsPackLeader)
                foreach (var p in Battle.Board.AllTiles())
                    if (p.StepsTo(enemy.Pos) <= Battle.LeaderAuraRange && !Battle.Board.IsObstacle(p))
                        _board.SetHighlight(p, AuraColor);

            var intent = EnemyAI.PredictIntent(Battle, enemy);
            if (intent.Target == null)
                return;

            var from = _board.ViewOf(enemy).transform.position + Vector3.up * 0.15f;
            var to = IsoMath.GridToWorld(intent.Target.Pos) + Vector3.up * 0.15f;
            switch (intent.Kind)
            {
                case IntentKind.Raise:
                    _board.SetHighlight(intent.Target.Pos, RaiseColor);
                    _intent.Show(from, to);
                    break;
                case IntentKind.Smash:
                    // Every tile around the ogre gets hit, so show the whole ring.
                    foreach (var c in SmashRing(enemy))
                        _board.SetHighlight(c, SmashZoneColor);
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    if (Battle.Distance(enemy, intent.Target) > 1)
                        _intent.Show(from, to);
                    break;
                case IntentKind.Throw:
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    if (intent.Tile.HasValue)
                        _board.SetHighlight(intent.Tile.Value, RockLandingColor);
                    _intent.Show(from, to);
                    break;
                case IntentKind.Bomb:
                    foreach (var p in Battle.BlastArea(intent.Target.Pos))
                        _board.SetHighlight(p, BombZoneColor);
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    _intent.Show(from, to);
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
                default:
                    _board.SetHighlight(intent.Target.Pos, IntentTargetColor);
                    _intent.Show(from, to);
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
                case RaiseEvent raise:
                {
                    var raiser = _board.ViewOf(raise.Raiser);
                    var raised = _board.ViewOf(raise.Raised);
                    yield return raiser.Lunge(raised.transform.position, null);
                    raiser.SetGridPosition(raise.Raiser.Pos);
                    yield return raised.Raise();
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
                    // A sun beam leaves any mine or bomb on its tile alone.
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
                        if (area.Kind == "volley" || area.Kind == "pierce" || area.Kind == "slam")
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
                    if (status.Kind == StatusKind.Snare || status.Kind == StatusKind.Ward || status.Kind == StatusKind.Heal)
                    {
                        yield return source.Lunge(target.transform.position, null);
                        source.SetGridPosition(status.Source.Pos);
                    }

                    var at = target.TopWorld + Vector3.up * 0.25f;
                    switch (status.Kind)
                    {
                        case StatusKind.Snare:
                            DamagePopup.Spawn(at, "Snared", DamagePopup.AbilityColor, 44);
                            break;
                        case StatusKind.Slow:
                            DamagePopup.Spawn(at, $"Slowed -{status.Amount} AP", DamagePopup.AbilityColor, 40);
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
                case EscapeEvent escape:
                {
                    var view = _board.ViewOf(escape.Unit);
                    DamagePopup.Spawn(view.TopWorld + Vector3.up * 0.25f,
                        escape.Gold > 0 ? $"Escaped with {escape.Gold} gold" : "Escaped", DamagePopup.DamageColor, 44);
                    yield return view.Slide(escape.Unit.Pos, new GridPos(escape.Unit.Pos.X, escape.Unit.Pos.Y + 1), null);
                    view.gameObject.SetActive(false);
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
                    var text = omen.Omen == Omen.Sun ? "Sun mark" : omen.Omen == Omen.Night ? "Shrouded" : $"Moon shield {Battle.MoonShield}";
                    DamagePopup.Spawn(view.TopWorld + Vector3.up * 0.25f, text, omen.Blessing ? DamagePopup.HealColor : DamagePopup.DamageColor, 40);
                    view.RefreshStatus();
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
                case EffectKind.Bloodthirst:
                    DamagePopup.Spawn(at, "+1 DMG", DamagePopup.AbilityColor, 48);
                    break;
                case EffectKind.LastStand:
                    DamagePopup.Spawn(at, "Last Stand!", DamagePopup.AbilityColor, 48);
                    view.RefreshHp(effect.HpAfter);
                    break;
                case EffectKind.Dodge:
                    DamagePopup.Spawn(at, "Dodge!", DamagePopup.AbilityColor, 48);
                    break;
                case EffectKind.Absorb:
                    DamagePopup.Spawn(at, $"Shield -{effect.Amount}", DamagePopup.HealColor, 44);
                    view.RefreshStatus();
                    break;
                case EffectKind.Refund:
                    DamagePopup.Spawn(at, $"+{effect.Amount} AP", DamagePopup.AbilityColor, 48);
                    break;
                case EffectKind.Guard:
                    DamagePopup.Spawn(at, "Guard!", DamagePopup.AbilityColor, 48);
                    break;
                case EffectKind.Steal:
                    DamagePopup.Spawn(at, $"Stole {effect.Amount} gold!", DamagePopup.DamageColor, 44);
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

        /// <summary>Dims statues that have finished their turn and tints wolves by their pack state.</summary>
        private void RefreshUnitStates()
        {
            foreach (var u in Battle.Units.Where(u => u.IsPlaced && u.IsAlive))
            {
                var view = _board.ViewOf(u);
                if (view == null)
                    continue;
                view.SetDimmed(u.Team == Team.Player && Battle.Phase == Phase.PlayerTurn && u.IsDone);
                view.RefreshStatus();
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
            return _panels.Any(r => r.Contains(gui));
        }

        private static GUIStyle TextStyle => new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };

        private float GuiWidth => Screen.width / (Screen.height / ReferenceHeight);

        private Rect Panel(float x, float y, float w, float h)
        {
            var r = new Rect(x, y, w, h);
            _panels.Add(r);
            return r;
        }

        /// <summary>A word-wrapped text box sized to its content (capped), at a corner.</summary>
        private void TextPanel(string text, bool right, bool bottom, float maxHeight)
        {
            if (string.IsNullOrEmpty(text))
                return;
            var style = TextStyle;
            var h = Mathf.Min(maxHeight, style.CalcHeight(new GUIContent(text), PanelWidth - 12f) + 12f);
            var x = right ? GuiWidth - PanelWidth - Margin : Margin;
            var y = bottom ? ReferenceHeight - h - Margin : Margin;
            GUILayout.BeginArea(Panel(x, y, PanelWidth, h), GUI.skin.box);
            GUILayout.Label(text, style);
            GUILayout.EndArea();
        }

        private void OnGUI()
        {
            if (_board == null)
                return;
            var scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (Event.current.type == EventType.Layout)
                _panels.Clear();

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

            if (_markerNote != null && Time.unscaledTime < _markerNoteUntil)
                GUI.Box(new Rect(Margin, ReferenceHeight - 80, 180, 30), _markerNote);
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

        /// <summary>The blessing or curse in play, if any.</summary>
        private string OmenLine =>
            string.Concat(Battle.Omens.Select(o => $"\n{Omens.Name(o.omen, o.side == Team.Player)}"));

        private void DrawPlacementHud()
        {
            var army = Battle.Units.Where(u => u.Team == Team.Player && !u.IsObjective).ToList();
            var omens = OmenLine.Trim();
            var h = 110 + army.Count * (ButtonHeight + 2) + 10 + (omens.Length > 0 ? 20 * Battle.Omens.Count : 0);
            GUILayout.BeginArea(Panel(Margin, Margin, PanelWidth, h), GUI.skin.box);
            GUILayout.Label(Header);
            GUILayout.Label("Place your army in the blue tiles. Click a placed statue to move it.", TextStyle);
            GUILayout.Label(_context.LayoutRule, TextStyle);
            if (omens.Length > 0)
                GUILayout.Label(omens, TextStyle);
            foreach (var u in army)
            {
                var item = u.Equipment != null ? $" [{u.Equipment.Name}]" : "";
                var label = $"{u.Stats.Name}{item}{(u.IsPlaced ? "  (placed)" : "")}{(u == _toPlace ? "  <" : "")}";
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

            TextPanel(HoverText(), true, false, 280f);
        }

        private void DrawTurnHud()
        {
            var playerTurn = Battle.Phase == Phase.PlayerTurn && !_busy;

            // Top-left: turn and the selected unit.
            var info = $"{Header}\nTurn {Battle.TurnNumber}: {(playerTurn ? "your turn" : "enemy turn")}{RitualLine}{OmenLine}";
            var apLine = "";
            if (_selected != null)
            {
                info += "\n" + UnitLine(_selected);
                var cost = playerTurn ? PreviewCost() : 0;
                apLine = cost > 0 ? $"AP {_selected.Ap} / {_selected.MaxAp}   this action: -{cost} -> {_selected.Ap - cost} left"
                    : $"AP {_selected.Ap} / {_selected.MaxAp}";
                var abilities = AbilityLines(_selected);
                if (abilities.Length > 0)
                    info += "\n" + abilities;
                var engaged = EngagedLine(_selected);
                if (engaged.Length > 0)
                    info += "\n" + engaged;
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

            // Top-right: whatever is under the mouse.
            TextPanel(HoverText(), true, false, 280f);

            // Bottom-left: the current hint, or the description of the hovered action button.
            if (playerTurn && !_confirmEndTurn)
                TextPanel(string.IsNullOrEmpty(_actionTooltip) ? _hint : _actionTooltip, false, true, 90f);

            if (playerTurn)
                DrawCommands();
        }

        private string _actionTooltip;

        /// <summary>Bottom-right: action buttons, Done and End turn. Always fully visible.</summary>
        private void DrawCommands()
        {
            var actions = _selected != null && Battle.CanAct(_selected) ? _selected.Stats.Actions : new ActionKind[0];
            float h;
            if (_confirmEndTurn)
                h = 110f;
            else
                h = (actions.Count + 2 + (DebugToolsEnabled ? 1 : 0)) * (ButtonHeight + 4) + 30f;

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

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                GUI.enabled = Battle.ActionTargets(_selected, action).Count > 0;
                var label = _action == action
                    ? $"Cancel {Abilities.Name(action)} ({i + 1})"
                    : $"{Abilities.Name(action)}: {Battle.ActionCost(action)} AP ({i + 1})";
                if (GUILayout.Button(new GUIContent(label, $"{Abilities.Name(action)}: {Abilities.Describe(action)}"), GUILayout.Height(ButtonHeight)))
                    ToggleAction(action);
                GUI.enabled = true;
            }

            if (Event.current.type == EventType.Repaint)
                _actionTooltip = GUI.tooltip;

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

            if (_hoveredUnit != null && _hoveredUnit != _selected)
            {
                var text = "Hover: " + UnitLine(_hoveredUnit);
                var abilities = AbilityLines(_hoveredUnit);
                if (abilities.Length > 0)
                    text += "\n" + abilities;
                switch (Battle.GetPackState(_hoveredUnit))
                {
                    case PackState.NearLeader:
                        text += "\n+1 damage: near the Pack Leader";
                        break;
                    case PackState.Leaderless:
                        text += "\n-1 damage: the Pack Leader is dead";
                        break;
                }

                if (_hoveredUnit.IsPackLeader)
                    text += $"\nWolves within {Battle.LeaderAuraRange} tiles get +1 damage";
                if (_hoveredUnit.Stats.CanRaise)
                    text += $"\nRaises a bone pile within {_hoveredUnit.Stats.RaiseRange} tiles instead of attacking";
                if (_hoveredUnit.Stats.AreaMelee)
                    text += $"\nSmash hits everyone next to it. Throws boulders up to {_hoveredUnit.Stats.ThrowRange} tiles; they stay as rocks";
                if (_hoveredUnit.Stats.CanBomb)
                    text += $"\nLobs a bomb ({_hoveredUnit.Stats.BombDamage} damage, 3x3) that explodes next enemy turn";
                if (_hoveredUnit.Team == Team.Enemy)
                    text += "\n" + IntentText(EnemyAI.PredictIntent(Battle, _hoveredUnit));
                var engaged = EngagedLine(_hoveredUnit);
                if (engaged.Length > 0)
                    text += "\n" + engaged;
                if (_selected != null && _hoveredUnit.Team == Team.Enemy && !Battle.CanAttack(_selected, _hoveredUnit) &&
                    Battle.FindAttackSpot(_selected, _hoveredUnit) is var spot && spot.HasValue)
                    text += $"\nClick: walk {spot.Value.cost} AP + attack {Battle.AttackCost} AP";
                return text;
            }

            if (_hovered.HasValue)
            {
                var p = _hovered.Value;
                if (Battle.SunMarks.Contains(p))
                    return $"Sun mark: a sun beam strikes this tile at the start of the enemy turn for {Battle.SunDamage} damage to whoever stands here.";
                if (Battle.Shroud.TryGetValue(p, out var side))
                    return side == Team.Player
                        ? "Shrouded: enemies can't hit units here from range."
                        : "Shrouded: your units can't hit enemies here from range.";
                if (Battle.BombAt(p) is Bomb bomb)
                    return $"Bomb: explodes at the start of the enemy turn for {bomb.Damage} damage to everyone in the orange 3x3.";
                if (Battle.MineAt(p) != null)
                    return $"Your mine: an enemy stepping here sets it off ({Battle.MineDamage} damage to everyone around, your units too).";
                if (Battle.RemainsAt(p) is Unit bones)
                    return $"Bone pile ({bones.Stats.Name}). The Necromancer can raise it.\nStand on it to stop that.";
                if (_selected != null && !_action.HasValue && Battle.GetReachable(_selected).TryGetValue(p, out var cost))
                    return Battle.InEnemyReach(_selected, _selected.Pos)
                        ? $"Move here: {cost} AP (includes +{Battle.ZocCost} for leaving an enemy's reach)"
                        : $"Move here: {cost} AP";
            }

            return null;
        }

        /// <summary>"Engaged by ..." for a unit standing in enemy reach (zone of control), or empty.</summary>
        private string EngagedLine(Unit u)
        {
            if (u.IsObjective)
                return "";
            var holders = Battle.EngagedBy(u, u.Pos).Select(h => h.Stats.Name).ToList();
            if (holders.Count == 0)
                return "";
            var who = u.Team == Team.Player ? "it" : "it (on its turn)";
            return $"Engaged by {string.Join(", ", holders)}: each step out of their reach costs {who} +{Battle.ZocCost} AP";
        }

        private static string IntentText(Intent intent)
        {
            if (intent.Target == null)
                return "Wants to: nothing";
            var name = intent.Target.Stats.Name;
            switch (intent.Kind)
            {
                case IntentKind.Raise:
                    return $"Wants to: raise the {name} bone pile";
                case IntentKind.Smash:
                    return $"Wants to: smash, going for the {name}";
                case IntentKind.Throw:
                    return $"Wants to: throw a boulder at the {name} (rock lands on the dark tile)";
                case IntentKind.Bomb:
                    return $"Wants to: bomb the {name}'s tile (goes off next enemy turn)";
                case IntentKind.Flee:
                    return $"Wants to: run off the far edge with {intent.Target.CarryingLoot} of your gold. Kill it first!";
                case IntentKind.Slam:
                    return "Wants to: slam, shoving everyone within 2 tiles (red area) 1 tile away";
                default:
                    return $"Wants to attack: {name}";
            }
        }

        private string UnitLine(Unit u)
        {
            var item = u.Equipment != null ? $"  [{u.Equipment.Name}]" : "";
            return $"{u.Stats.Name}  HP {u.Hp}/{u.MaxHp}  DMG {Battle.EffectiveDamage(u)}  RNG {u.Range}{item}";
        }

        private static string AbilityLines(Unit u)
        {
            var lines = new List<string>();
            foreach (var ability in u.Stats.Abilities)
                lines.Add($"{Abilities.Name(ability)}: {Abilities.Describe(ability)}");
            var itemAbility = u.Equipment?.Ability ?? Ability.None;
            if (itemAbility != Ability.None)
                lines.Add($"{Abilities.Name(itemAbility)} ({u.Equipment.Name}): {Abilities.Describe(itemAbility)}");
            if (u.Member != null && u.Member.BonusDamage + u.GainedDamage > 0)
                lines.Add($"Bloodthirst so far: +{u.Member.BonusDamage + u.GainedDamage} damage");
            var status = new List<string>();
            if (u.Shield > 0)
                status.Add($"shield {u.Shield}");
            if (u.IsSnared)
                status.Add("snared (can't move)");
            if (u.IsSlowed)
                status.Add("slowed");
            if (u.PoisonTurns > 0)
                status.Add($"poisoned ({u.PoisonTurns} more)");
            if (status.Count > 0)
                lines.Add("Status: " + string.Join(", ", status));
            return string.Join("\n", lines);
        }
    }
}
