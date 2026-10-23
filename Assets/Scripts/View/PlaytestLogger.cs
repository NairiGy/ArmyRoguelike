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

        public PlaytestLogger(Battle battle, BattleContext context)
        {
            _battle = battle;
            _log.startedAt = _created.ToString("yyyy-MM-dd HH:mm:ss");
            _log.runSeed = context.RunSeed;
            _log.battleNumber = context.Number;
            _log.encounter = context.Encounter;
            _log.goldAtStart = context.Gold;
        }

        public static string Folder =>
            Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Playtests"))
                : Path.Combine(Application.persistentDataPath, "Playtests");

        /// <summary>The file name once saved, otherwise null.</summary>
        public string SavedFile { get; private set; }

        public void BattleStarted()
        {
            _battleStartTime = Time.realtimeSinceStartup;
            foreach (var u in _battle.Units.Where(u => u.Team == Team.Player))
                _log.placements.Add(new PlacementEntry
                {
                    unit = Name(u), x = u.Pos.X, y = u.Pos.Y, equipment = u.Equipment?.Name ?? "", maxHp = u.MaxHp,
                    damage = u.BaseDamage, range = u.Range, maxAp = u.MaxAp,
                });
        }

        /// <summary>Call right after the action happened in the rules, before it is animated.</summary>
        public void Record(BattleEvent ev, int turn)
        {
            switch (ev)
            {
                case MoveEvent move:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = move.Unit.Team.ToString(), unit = Name(move.Unit), type = "move",
                        fromX = move.From.X, fromY = move.From.Y, toX = move.To.X, toY = move.To.Y, tiles = move.Path.Count,
                        apAfter = ApAfter(move.Unit),
                    });
                    break;
                case AttackEvent attack:
                    _log.actions.Add(HitEntry(turn, attack.Attacker, "attack", new Hit(attack.Target, attack.Damage, attack.TargetHpAfter)));
                    break;
                case SmashEvent smash:
                    // One entry per unit hit, so per-unit damage adds up; "smashTargets" tells them apart from single attacks.
                    if (smash.Hits.Count == 0)
                        _log.actions.Add(new ActionEntry { turn = turn, team = smash.Attacker.Team.ToString(), unit = Name(smash.Attacker), type = "smash" });
                    foreach (var hit in smash.Hits)
                    {
                        var entry = HitEntry(turn, smash.Attacker, "smash", hit);
                        entry.smashTargets = smash.Hits.Count;
                        _log.actions.Add(entry);
                    }

                    break;
                case BoulderEvent boulder:
                {
                    var entry = HitEntry(turn, boulder.Thrower, "boulder", boulder.Hit);
                    entry.rockX = boulder.RockTile?.X ?? -1;
                    entry.rockY = boulder.RockTile?.Y ?? -1;
                    _log.actions.Add(entry);
                    break;
                }
                case RaiseEvent raise:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = raise.Raiser.Team.ToString(), unit = Name(raise.Raiser), type = "raise",
                        target = Name(raise.Raised), toX = raise.Raised.Pos.X, toY = raise.Raised.Pos.Y,
                    });
                    break;
            }
        }

        private ActionEntry HitEntry(int turn, Unit attacker, string type, Hit hit) => new ActionEntry
        {
            turn = turn, team = attacker.Team.ToString(), unit = Name(attacker), type = type, target = Name(hit.Target),
            damage = hit.Damage, targetHpAfter = hit.HpAfter, killed = hit.Killed, apAfter = ApAfter(attacker),
        };

        private static int ApAfter(Unit u) => u.Team == Team.Player ? u.Ap : -1;

        public void RecordUnitDone(Unit unit, int turn)
        {
            _log.actions.Add(new ActionEntry
            {
                turn = turn, team = unit.Team.ToString(), unit = Name(unit), type = "done", apAfter = unit.Ap,
            });
        }

        /// <summary>Also notes how many bone piles exist and how many a player statue is standing on.</summary>
        public void RecordEndTurn(int turn)
        {
            var piles = _battle.Units.Where(u => u.IsRemains).ToList();
            _log.actions.Add(new ActionEntry
            {
                turn = turn, team = Team.Player.ToString(), type = "end turn",
                bonePiles = piles.Count,
                bonePilesBlocked = piles.Count(p => _battle.UnitAt(p.Pos) is Unit u && u.Team == Team.Player),
            });
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
            for (var y = 0; y < _battle.Board.Height; y++)
            for (var x = 0; x < _battle.Board.Width; x++)
                if (_battle.Board.IsObstacle(new GridPos(x, y)))
                    marker.rocks.Add(new Tile { x = x, y = y });
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
            _log.survivors = _battle.Living(Team.Player).Count();
            _log.goldEarned = _battle.Phase == Phase.Victory ? Run.GoldFor(_log.survivors) : 0;
            _log.units = Summarise();

            var name = $"battle-{_created:yyyyMMdd-HHmmss}-{_log.battleNumber}.json";
            if (PlaytestFiles.Write(name, JsonUtility.ToJson(_log, true)))
                SavedFile = name;
            return SavedFile;
        }

        private List<UnitSummary> Summarise()
        {
            var summaries = _battle.Units.Where(u => u.IsPlaced).ToDictionary(Name, u => new UnitSummary
            {
                unit = Name(u), team = u.Team.ToString(), equipment = u.Equipment?.Name ?? "", survived = u.IsAlive,
                hpLeft = u.Hp, maxHp = u.MaxHp,
            });
            foreach (var a in _log.actions)
            {
                if (a.unit == null || !summaries.TryGetValue(a.unit, out var s))
                    continue;
                switch (a.type)
                {
                    case "move":
                        s.moves++;
                        s.tilesMoved += a.tiles;
                        break;
                    case "raise":
                        s.raises++;
                        if (summaries.TryGetValue(a.target, out var raised))
                            raised.timesRaised++;
                        break;
                    case "attack":
                    case "smash":
                    case "boulder":
                        if (a.target == null)
                            break;
                        s.attacks++;
                        s.damageDealt += a.damage;
                        if (a.killed)
                            s.kills++;
                        if (summaries.TryGetValue(a.target, out var t))
                        {
                            t.damageTaken += a.damage;
                            if (a.killed)
                                t.deaths++;
                        }

                        break;
                }
            }

            return summaries.Values.ToList();
        }

        public static string Name(Unit u) => $"{u.Stats.Name}#{u.Id}";

        // ---------- JSON shape (JsonUtility needs public fields on serializable classes) ----------

        [Serializable]
        private sealed class PlaytestLog
        {
            public string startedAt;
            public string endedAt;
            public int runSeed;
            public int battleNumber;
            public string encounter;
            public float durationSeconds;
            public string result;
            public int turns;
            public int survivors;
            public int goldAtStart;
            public int goldEarned;
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
            public string equipment;
            public int maxHp;
            public int damage;
            public int range;
            public int maxAp;
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

            /// <summary>Smash: how many units the one swing hit.</summary>
            public int smashTargets;

            /// <summary>Boulder: where the rock landed, -1 if there was no room.</summary>
            public int rockX = -1;

            public int rockY = -1;

            /// <summary>End turn: bone piles on the board, and how many a player statue stands on.</summary>
            public int bonePiles;

            public int bonePilesBlocked;
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
            public List<Tile> rocks = new List<Tile>();
        }

        [Serializable]
        private sealed class Tile
        {
            public int x;
            public int y;
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
            public string equipment;
            public bool survived;
            public int hpLeft;
            public int maxHp;
            public int damageDealt;
            public int damageTaken;
            public int kills;
            public int deaths;
            public int attacks;
            public int moves;
            public int tilesMoved;
            public int raises;
            public int timesRaised;
        }
    }

    /// <summary>Writes playtest JSON files to <see cref="PlaytestLogger.Folder"/>.</summary>
    internal static class PlaytestFiles
    {
        public static bool Write(string fileName, string json)
        {
            try
            {
                Directory.CreateDirectory(PlaytestLogger.Folder);
                var path = Path.Combine(PlaytestLogger.Folder, fileName);
                File.WriteAllText(path, json);
                Debug.Log($"Playtest log saved: {path}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not save playtest log {fileName}: {e.Message}");
                return false;
            }
        }
    }
}
