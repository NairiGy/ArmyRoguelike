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

        public BattleContext(int step, int steps, string encounter, int gold, int runSeed, bool ambush, bool boss)
        {
            Step = step;
            Steps = steps;
            Encounter = encounter;
            Gold = gold;
            RunSeed = runSeed;
            Ambush = ambush;
            Boss = boss;
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
        private bool _mineMode;
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

            if (keyboard != null && keyboard.mKey.wasPressedThisFrame && !_busy && CanUseMines(_selected))
                _mineMode = !_mineMode;

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
            foreach (var u in Battle.Units.Where(u => u.Team == Team.Player && u.IsPlaced && u.IsAlive))
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
            if (_mineMode)
                return Battle.CanPlaceMine(_selected, tile) ? Battle.MineCost : 0;
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

        private bool CanUseMines(Unit unit) => unit != null && unit.Has(Ability.Mines) && Battle.CanAct(unit);

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

            if (_mineMode && _selected != null)
            {
                if (Battle.CanPlaceMine(_selected, tile))
                {
                    var placed = Battle.PlayerPlaceMine(_selected, tile);
                    Record(placed);
                    StartCoroutine(PlayPlayerAction(placed));
                }
                else
                {
                    _hint = "Mine placement cancelled.";
                }

                _mineMode = false;
                return;
            }

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
            _mineMode = false;
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
            DrawBombZones();
            DrawEnemyHover();
            if (_selected == null)
                return;

            _board.SetHighlight(_selected.Pos, SelectedColor);

            if (_mineMode)
            {
                foreach (var p in _selected.Pos.Neighbours())
                    if (Battle.CanPlaceMine(_selected, p))
                        _board.SetHighlight(p, _hovered == p ? HoverColor : MineSpotColor);
                return;
            }

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

        private string Header =>
            $"Step {_context.Step}/{_context.Steps}: {(_context.Boss ? "Boss " : "")}{(_context.Ambush ? "Ambush! " : "")}{_context.Encounter}    Gold {_context.Gold}";

        private void DrawPlacementHud()
        {
            var army = Battle.Units.Where(u => u.Team == Team.Player).ToList();
            var hoverText = HoverText();
            _hudRect = new Rect(10, 10, 300, 100 + army.Count * 30 + 40 + (DebugToolsEnabled ? 34 : 0) + (hoverText != null ? 90 : 0));
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
            DrawDebugButton();
            if (hoverText != null)
                GUILayout.Label(hoverText);
            GUILayout.EndArea();
        }

        private void DrawTurnHud()
        {
            var playerTurn = Battle.Phase == Phase.PlayerTurn && !_busy;
            _hudRect = new Rect(10, 10, 320, 400);
            GUILayout.BeginArea(_hudRect, GUI.skin.box);
            GUILayout.Label(Header);
            GUILayout.Label($"Turn {Battle.TurnNumber}: {(playerTurn ? "your turn" : "enemy turn")}");

            if (_selected != null)
            {
                GUILayout.Label(UnitLine(_selected));
                var apStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                apStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
                GUILayout.Label($"AP {_selected.Ap} / {_selected.MaxAp}", apStyle);
                var cost = playerTurn ? PreviewCost() : 0;
                GUILayout.Label(cost > 0
                    ? $"This action: -{cost} AP  ->  {_selected.Ap - cost} left"
                    : $"Move {Battle.MoveCost} per tile, attack {Battle.AttackCost}, carry up to {Battle.MaxCarriedAp} to next turn");
                GUILayout.Label(AbilityLines(_selected));
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
                GUILayout.Label(_mineMode ? "Click a yellow tile to place the mine (M or any other tile cancels)." : _hint);
                if (CanUseMines(_selected))
                {
                    var left = Battle.MaxMinesPerUnit - Battle.MinesOf(_selected);
                    GUI.enabled = _selected.Pos.Neighbours().Any(p => Battle.CanPlaceMine(_selected, p));
                    if (GUILayout.Button(_mineMode ? "Cancel mine (M)" : $"Place mine: {Battle.MineCost} AP, {left} left (M)"))
                        _mineMode = !_mineMode;
                    GUI.enabled = true;
                }

                GUI.enabled = _selected != null;
                if (GUILayout.Button(_selected != null ? $"Done with {_selected.Stats.Name}" : "Done"))
                    FinishSelectedUnit();
                GUI.enabled = true;
                // Extra space so End turn is harder to hit by accident.
                GUILayout.Space(14);
                if (GUILayout.Button("End turn"))
                    RequestEndTurn();
                DrawDebugButton();
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
                GUILayout.Label($"{survivors} survived: +{_context.GoldIfWon(survivors)} gold", centred);
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
                if (_selected != null && _hoveredUnit.Team == Team.Enemy && !Battle.CanAttack(_selected, _hoveredUnit) &&
                    Battle.FindAttackSpot(_selected, _hoveredUnit) is var spot && spot.HasValue)
                    text += $"\nClick: walk {spot.Value.cost} AP + attack {Battle.AttackCost} AP";
                return text;
            }

            if (_hovered.HasValue)
            {
                var p = _hovered.Value;
                if (Battle.BombAt(p) is Bomb bomb)
                    return $"Bomb: explodes at the start of the enemy turn for {bomb.Damage} damage to everyone in the orange 3x3.";
                if (Battle.MineAt(p) != null)
                    return $"Your mine: an enemy stepping here sets it off ({Battle.MineDamage} damage to everyone around, your units too).";
                if (Battle.RemainsAt(p) is Unit bones)
                    return $"Bone pile ({bones.Stats.Name}). The Necromancer can raise it.\nStand on it to stop that.";
                if (_selected != null && !_mineMode && Battle.GetReachable(_selected).TryGetValue(p, out var cost))
                    return $"Move here: {cost} AP";
            }

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
                case IntentKind.Bomb:
                    return $"Wants to: bomb the {name}'s tile (goes off next enemy turn)";
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
            if (u.Stats.Ability != Ability.None)
                lines.Add($"{Abilities.Name(u.Stats.Ability)}: {Abilities.Describe(u.Stats.Ability)}");
            var itemAbility = u.Equipment?.Ability ?? Ability.None;
            if (itemAbility != Ability.None)
                lines.Add($"{Abilities.Name(itemAbility)} ({u.Equipment.Name}): {Abilities.Describe(itemAbility)}");
            if (u.Member != null && u.Member.BonusDamage + u.GainedDamage > 0)
                lines.Add($"Bloodthirst so far: +{u.Member.BonusDamage + u.GainedDamage} damage");
            return string.Join("\n", lines);
        }
    }
}
