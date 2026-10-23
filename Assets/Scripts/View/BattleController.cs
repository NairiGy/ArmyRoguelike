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
        public readonly int Number;
        public readonly int Count;
        public readonly string Encounter;
        public readonly int Gold;
        public readonly int RunSeed;

        public BattleContext(int number, int count, string encounter, int gold, int runSeed)
        {
            Number = number;
            Count = count;
            Encounter = encounter;
            Gold = gold;
            RunSeed = runSeed;
        }
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

        // Wolf tints: warm while the leader's aura reaches them, pale and cold once the leader is dead.
        private static readonly Color NearLeaderTint = new Color(1.25f, 0.8f, 0.75f);
        private static readonly Color LeaderlessTint = new Color(0.8f, 0.9f, 1.15f);

        private const float ReferenceHeight = 720f;
        private const float PauseBetweenEnemies = 0.25f;
        private const float BoulderFlightTime = 0.45f;

        private BoardView _board;
        private IntentView _intent;
        private Camera _camera;
        private BattleContext _context;
        private Action _onContinue;
        private Unit _toPlace;
        private Unit _selected;
        private Unit _hoveredUnit;
        private GridPos? _hovered;
        private bool _busy;
        private bool _confirmEndTurn;
        private string _hint = "";
        private PlaytestLogger _logger;
        private string _markerNote;
        private float _markerNoteUntil;
        private Rect _hudRect;

        private Battle Battle => _board.Battle;

        public void Init(BoardView board, Camera cam, BattleContext context, Action onContinue)
        {
            _board = board;
            _camera = cam;
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

            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            {
                _markerNote = $"Marker {_logger.AddMarker()} saved";
                _markerNoteUntil = Time.unscaledTime + 2f;
            }

            _hovered = HoveredTile();
            _hoveredUnit = _hovered.HasValue ? Battle.UnitAt(_hovered.Value) : null;
            var clicked = !_busy && !_confirmEndTurn && Mouse.current.leftButton.wasPressedThisFrame && _hovered.HasValue;

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
                {
                    _logger.Record(attack, Battle.TurnNumber);
                    StartCoroutine(PlayPlayerAction(attack));
                }
                return;
            }

            // A distant enemy: walk to the cheapest tile to hit it from, then attack.
            if (target != null && target.Team == Team.Enemy && Battle.FindAttackSpot(_selected, target) is var spot && spot.HasValue)
            {
                var move = Battle.PlayerMove(_selected, spot.Value.tile);
                var attack = move != null ? Battle.PlayerAttack(_selected, target) : null;
                if (move != null)
                    _logger.Record(move, Battle.TurnNumber, autoApproach: true);
                if (attack != null)
                    _logger.Record(attack, Battle.TurnNumber, autoApproach: true);
                if (move != null)
                    StartCoroutine(attack != null ? PlayPlayerAction(move, attack) : PlayPlayerAction(move));
                return;
            }

            if (Battle.GetReachable(_selected).ContainsKey(tile))
            {
                var move = Battle.PlayerMove(_selected, tile);
                if (move != null)
                {
                    _logger.Record(move, Battle.TurnNumber);
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

        private void Select(Unit unit)
        {
            if (_selected != null)
                _board.ViewOf(_selected)?.SetSelected(false);
            _selected = unit;
            if (_selected != null)
                _board.ViewOf(_selected)?.SetSelected(true);
            _hint = unit == null ? "Click a statue to select it." : "Click a blue tile to move or a red one to attack.";
        }

        private IEnumerator PlayPlayerAction(params BattleEvent[] events)
        {
            _busy = true;
            _board.ClearHighlights();
            foreach (var ev in events)
                yield return PlayEvent(ev);

            // A unit with no AP left has nothing more to do.
            if (_selected != null && _selected.IsAlive && _selected.Ap == 0)
                FinishSelectedUnit();
            RefreshUnitStates();
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

        /// <summary>Player units that could still move or attack: ending the turn now wastes their actions.</summary>
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
            _board.ClearHighlights();
            foreach (var u in Battle.Living(Team.Player))
                _board.ViewOf(u)?.SetDimmed(true);

            var turn = Battle.TurnNumber;
            _logger.RecordEndTurn(turn);
            var events = Battle.EndPlayerTurn();
            foreach (var ev in events)
                _logger.Record(ev, turn);
            Unit lastActor = null;
            foreach (var ev in events)
            {
                if (lastActor != null && ev.Actor != lastActor)
                    yield return new WaitForSeconds(PauseBetweenEnemies);
                lastActor = ev.Actor;
                yield return PlayEvent(ev);
            }

            _hint = "Your turn. Click a statue to select it.";
            RefreshUnitStates();
            if (Battle.IsOver)
                _logger.Save();
            _busy = false;
        }

        /// <summary>Saves a battle that was left before it ended (leaving Play mode).</summary>
        private void OnDestroy() => _logger?.Save();

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
                    if (intent.RockTile.HasValue)
                        _board.SetHighlight(intent.RockTile.Value, RockLandingColor);
                    _intent.Show(from, to);
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
                    yield return FlyBoulder(thrower.TopWorld, target.transform.position);
                    ShowHit(boulder.Hit);
                    if (boulder.RockTile.HasValue)
                        _board.AddRock(boulder.RockTile.Value);
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
            }
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

        private IEnumerator FlyBoulder(Vector3 from, Vector3 to)
        {
            var rock = new GameObject("Boulder").AddComponent<SpriteRenderer>();
            rock.sprite = PlaceholderArt.Rock;
            rock.transform.localScale = Vector3.one * 0.45f;
            rock.sortingOrder = 15000;
            var height = 0.6f + Vector3.Distance(from, to) * 0.15f;
            for (var t = 0f; t < 1f; t += Time.deltaTime / BoulderFlightTime)
            {
                rock.transform.position = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * height);
                yield return null;
            }

            Destroy(rock.gameObject);
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

            if (_markerNote != null && Time.unscaledTime < _markerNoteUntil)
                GUI.Box(new Rect(10, ReferenceHeight - 40, 180, 30), _markerNote);

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

        private string Header => $"Battle {_context.Number}/{_context.Count}: {_context.Encounter}    Gold {_context.Gold}";

        private void DrawPlacementHud()
        {
            var army = Battle.Units.Where(u => u.Team == Team.Player).ToList();
            var hoverText = HoverText();
            _hudRect = new Rect(10, 10, 280, 100 + army.Count * 30 + 40 + (hoverText != null ? 70 : 0));
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            GUILayout.Label(Header);
            GUILayout.Label("Place your army in the blue tiles.");
            GUILayout.Label("Click a placed statue to move it.");
            foreach (var u in army)
            {
                var item = u.Equipment != null ? $" [{u.Equipment.Name}]" : "";
                var label = $"{u.Stats.Name}{item}{(u.IsPlaced ? "  (placed)" : "")}{(u == _toPlace ? "  <" : "")}";
                if (GUILayout.Button(label))
                    _toPlace = u;
            }

            GUILayout.Space(6);
            GUI.enabled = Battle.AllPlaced;
            if (GUILayout.Button("Start battle"))
                StartBattle();
            GUI.enabled = true;
            if (hoverText != null)
                GUILayout.Label(hoverText);
            GUILayout.EndArea();
        }

        private void DrawTurnHud()
        {
            var playerTurn = Battle.Phase == Phase.PlayerTurn && !_busy;
            _hudRect = new Rect(10, 10, 300, 330);
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            GUILayout.Label(Header);
            GUILayout.Label($"Turn {Battle.TurnNumber}: {(playerTurn ? "your turn" : "enemy turn")}");

            if (_selected != null)
            {
                GUILayout.Label(UnitLine(_selected));
                GUILayout.Label($"AP {_selected.Ap}   (move {Battle.MoveCost} per tile, attack {Battle.AttackCost})");
            }

            var hoverText = HoverText();
            if (hoverText != null)
                GUILayout.Label(hoverText);

            if (playerTurn && _confirmEndTurn)
            {
                var names = string.Join(", ", UnitsThatCanStillAct().Select(u => u.Stats.Name));
                GUILayout.Label($"{names} can still act. End the turn anyway?");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Yes, end turn"))
                {
                    _logger.RecordEndTurnPrompt(UnitsThatCanStillAct(), Battle.TurnNumber, "confirmed");
                    EndTurn();
                }

                if (GUILayout.Button("No, keep playing"))
                {
                    _logger.RecordEndTurnPrompt(UnitsThatCanStillAct(), Battle.TurnNumber, "cancelled");
                    _confirmEndTurn = false;
                }

                GUILayout.EndHorizontal();
            }
            else if (playerTurn)
            {
                GUILayout.Label(_hint);
                GUI.enabled = _selected != null;
                if (GUILayout.Button(_selected != null ? $"Done with {_selected.Stats.Name}" : "Done"))
                    FinishSelectedUnit();
                GUI.enabled = true;
                // Extra space so End turn is harder to hit by accident.
                GUILayout.Space(14);
                if (GUILayout.Button("End turn"))
                    RequestEndTurn();
            }

            GUILayout.EndArea();
        }

        private void DrawEndHud()
        {
            const float w = 260, h = 120;
            _hudRect = new Rect((Screen.width / (Screen.height / ReferenceHeight) - w) * 0.5f, 120, w, h);
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            var centred = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            if (Battle.Phase == Phase.Victory)
            {
                var survivors = Battle.Living(Team.Player).Count();
                GUILayout.Label("Victory!", style);
                GUILayout.Label($"{survivors} survived: +{Run.GoldFor(survivors)} gold", centred);
            }
            else
            {
                GUILayout.Label("Defeat", style);
                GUILayout.Label("The run is over.", centred);
            }

            if (GUILayout.Button("Continue"))
                _onContinue?.Invoke();
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
                if (_hoveredUnit.Team == Team.Enemy)
                    text += "\n" + IntentText(EnemyAI.PredictIntent(Battle, _hoveredUnit));
                if (_selected != null && _hoveredUnit.Team == Team.Enemy && !Battle.CanAttack(_selected, _hoveredUnit) &&
                    Battle.FindAttackSpot(_selected, _hoveredUnit) is var spot && spot.HasValue)
                    text += $"\nClick: walk {spot.Value.cost} AP + attack {Battle.AttackCost} AP";
                return text;
            }

            if (_hovered.HasValue && Battle.RemainsAt(_hovered.Value) is Unit bones)
                return $"Bone pile ({bones.Stats.Name}). The Necromancer can raise it.\nStand on it to stop that.";
            if (_selected != null && _hovered.HasValue && Battle.GetReachable(_selected).TryGetValue(_hovered.Value, out var cost))
                return $"Move here: {cost} AP";
            return null;
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
                default:
                    return $"Wants to attack: {name}";
            }
        }

        private string UnitLine(Unit u)
        {
            var item = u.Equipment != null ? $"  [{u.Equipment.Name}]" : "";
            return $"{u.Stats.Name}  HP {u.Hp}/{u.MaxHp}  DMG {Battle.EffectiveDamage(u)}  RNG {u.Range}{item}";
        }
    }
}
