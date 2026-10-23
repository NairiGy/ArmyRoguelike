using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Records one battle for playtest analysis and writes it as JSON to a Playtests folder:
    /// the project root in the editor, persistentDataPath in a build. Markers (F9) save a
    /// snapshot of the board at a moment the player wants to talk about later.
    /// </summary>
    public sealed class PlaytestLogger
    {
        private readonly Battle _battle;
        private readonly PlaytestLog _log = new PlaytestLog();
        private readonly DateTime _created = DateTime.Now;
        private float _battleStartTime = -1f;
        private bool _saved;

        public PlaytestLogger(Battle battle)
        {
            _battle = battle;
            _log.startedAt = _created.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static string Folder =>
            Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Playtests"))
                : Path.Combine(Application.persistentDataPath, "Playtests");

        public void BattleStarted()
        {
            _battleStartTime = Time.realtimeSinceStartup;
            foreach (var u in _battle.Units.Where(u => u.Team == Team.Player))
                _log.placements.Add(new PlacementEntry { unit = Name(u), x = u.Pos.X, y = u.Pos.Y });
        }

        /// <summary>Call right after the action happened in the rules, before it is animated.</summary>
        public void Record(BattleEvent ev, int turn)
        {
            var entry = new ActionEntry { turn = turn };
            switch (ev)
            {
                case MoveEvent move:
                    entry.team = move.Unit.Team.ToString();
                    entry.unit = Name(move.Unit);
                    entry.type = "move";
                    entry.fromX = move.From.X;
                    entry.fromY = move.From.Y;
                    entry.toX = move.To.X;
                    entry.toY = move.To.Y;
                    entry.tiles = move.Path.Count;
                    entry.apAfter = move.Unit.Team == Team.Player ? move.Unit.Ap : -1;
                    break;
                case AttackEvent attack:
                    entry.team = attack.Attacker.Team.ToString();
                    entry.unit = Name(attack.Attacker);
                    entry.type = "attack";
                    entry.target = Name(attack.Target);
                    entry.damage = attack.Damage;
                    entry.targetHpAfter = attack.TargetHpAfter;
                    entry.killed = attack.Killed;
                    entry.apAfter = attack.Attacker.Team == Team.Player ? attack.Attacker.Ap : -1;
                    break;
                default:
                    return;
            }

            _log.actions.Add(entry);
        }

        public void RecordUnitDone(Unit unit, int turn)
        {
            _log.actions.Add(new ActionEntry
            {
                turn = turn, team = unit.Team.ToString(), unit = Name(unit), type = "done", apAfter = unit.Ap,
            });
        }

        public void RecordEndTurn(int turn)
        {
            _log.actions.Add(new ActionEntry { turn = turn, team = Team.Player.ToString(), type = "end turn" });
        }

        public int AddMarker()
        {
            var marker = new MarkerEntry
            {
                turn = _battle.TurnNumber,
                phase = _battle.Phase.ToString(),
                secondsIntoBattle = _battleStartTime < 0f ? 0f : Time.realtimeSinceStartup - _battleStartTime,
                actionIndex = _log.actions.Count,
            };
            foreach (var u in _battle.Units.Where(u => u.IsPlaced))
                marker.units.Add(new UnitSnapshot
                {
                    unit = Name(u), team = u.Team.ToString(), x = u.Pos.X, y = u.Pos.Y, hp = u.Hp, ap = u.Ap, alive = u.IsAlive,
                });
            _log.markers.Add(marker);
            return _log.markers.Count;
        }

        /// <summary>Writes the file once. A battle left before it ended is saved as "abandoned".</summary>
        public string Save()
        {
            if (_saved || (_battleStartTime < 0f && _log.markers.Count == 0))
                return null;
            _saved = true;

            _log.endedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _log.durationSeconds = _battleStartTime < 0f ? 0f : Time.realtimeSinceStartup - _battleStartTime;
            _log.result = _battle.Phase == Phase.Victory ? "victory" : _battle.Phase == Phase.Defeat ? "defeat" : "abandoned";
            _log.turns = _battle.TurnNumber;
            _log.units = Summarise();

            try
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, $"battle-{_created:yyyyMMdd-HHmmss}.json");
                File.WriteAllText(path, JsonUtility.ToJson(_log, true));
                Debug.Log($"Playtest log saved: {path}");
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not save playtest log: {e.Message}");
                return null;
            }
        }

        private List<UnitSummary> Summarise()
        {
            var summaries = _battle.Units.Where(u => u.IsPlaced).ToDictionary(u => Name(u), u => new UnitSummary
            {
                unit = Name(u), team = u.Team.ToString(), survived = u.IsAlive, hpLeft = u.Hp, maxHp = u.Stats.MaxHp,
            });
            foreach (var a in _log.actions)
            {
                if (a.unit == null || !summaries.TryGetValue(a.unit, out var s))
                    continue;
                if (a.type == "move")
                {
                    s.moves++;
                    s.tilesMoved += a.tiles;
                }
                else if (a.type == "attack")
                {
                    s.attacks++;
                    s.damageDealt += a.damage;
                    if (a.killed)
                        s.kills++;
                    if (summaries.TryGetValue(a.target, out var t))
                        t.damageTaken += a.damage;
                }
            }

            return summaries.Values.ToList();
        }

        private static string Name(Unit u) => $"{u.Stats.Name}#{u.Id}";

        // ---------- JSON shape (JsonUtility needs public fields on serializable classes) ----------

        [Serializable]
        private sealed class PlaytestLog
        {
            public string startedAt;
            public string endedAt;
            public float durationSeconds;
            public string result;
            public int turns;
            public List<PlacementEntry> placements = new List<PlacementEntry>();
            public List<UnitSummary> units = new List<UnitSummary>();
            public List<MarkerEntry> markers = new List<MarkerEntry>();
            public List<ActionEntry> actions = new List<ActionEntry>();
        }

        [Serializable]
        private sealed class PlacementEntry
        {
            public string unit;
            public int x;
            public int y;
        }

        [Serializable]
        private sealed class ActionEntry
        {
            public int turn;
            public string team;
            public string unit;
            public string type;
            public int fromX;
            public int fromY;
            public int toX;
            public int toY;
            public int tiles;
            public string target;
            public int damage;
            public int targetHpAfter;
            public bool killed;
            public int apAfter;
        }

        [Serializable]
        private sealed class MarkerEntry
        {
            public int turn;
            public string phase;
            public float secondsIntoBattle;

            /// <summary>Number of actions recorded before the marker, so it can be placed in the action list.</summary>
            public int actionIndex;

            public List<UnitSnapshot> units = new List<UnitSnapshot>();
        }

        [Serializable]
        private sealed class UnitSnapshot
        {
            public string unit;
            public string team;
            public int x;
            public int y;
            public int hp;
            public int ap;
            public bool alive;
        }

        [Serializable]
        private sealed class UnitSummary
        {
            public string unit;
            public string team;
            public bool survived;
            public int hpLeft;
            public int maxHp;
            public int damageDealt;
            public int damageTaken;
            public int kills;
            public int attacks;
            public int moves;
            public int tilesMoved;
        }
    }
}
