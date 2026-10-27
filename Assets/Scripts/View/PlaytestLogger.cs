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
            _log.battleNumber = context.Step;
            _log.encounter = context.Encounter;
            _log.ambush = context.Ambush;
            _log.elite = context.Elite;
            _log.test = context.Test;
            _log.layout = context.Layout;
            _log.goldAtStart = context.Gold;
            _goldIfWon = context.GoldIfWon;
            _log.omens = battle.Omens.Select(o => Omens.Name(o.omen, o.side == Team.Player)).ToList();
            _log.ritualTurns = battle.RitualTurns;
            _log.summonTurn = battle.SummonTurn;
        }

        private readonly Func<int, int> _goldIfWon;

        public static string Folder =>
            Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Playtests"))
                : Path.Combine(Application.persistentDataPath, "Playtests");

        /// <summary>The file name once saved, otherwise null.</summary>
        public string SavedFile { get; private set; }

        /// <summary>For a battle skipped before it started: record the placements so it still gets a log.</summary>
        public void BattleStartedIfNeeded()
        {
            if (_battleStartTime < 0f)
                BattleStarted();
        }

        public void BattleStarted()
        {
            _battleStartTime = Time.realtimeSinceStartup;
            foreach (var u in _battle.Units.Where(u => u.Team == Team.Player && !u.IsObjective))
                _log.placements.Add(new PlacementEntry
                {
                    unit = Name(u), x = u.Pos.X, y = u.Pos.Y, equipment = u.Equipment?.Name ?? "", maxHp = u.MaxHp,
                    damage = u.BaseDamage, range = u.Range, maxAp = u.MaxAp, tier = u.Stats.Tier, line = u.Stats.Root.Name,
                });
        }

        /// <summary>
        /// Call right after the action happened in the rules, before it is animated.
        /// <paramref name="autoApproach"/>: part of a click-an-enemy "walk up and attack".
        /// </summary>
        public void Record(BattleEvent ev, int turn, bool autoApproach = false)
        {
            switch (ev)
            {
                case MoveEvent move:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = move.Unit.Team.ToString(), unit = Name(move.Unit), type = "move",
                        fromX = move.From.X, fromY = move.From.Y, toX = move.To.X, toY = move.To.Y, tiles = move.Path.Count,
                        apAfter = ApAfter(move.Unit), autoApproach = autoApproach, amount = move.Cost, zocSteps = move.ZocSteps,
                    });
                    break;
                case AttackEvent attack:
                {
                    var entry = HitEntry(turn, attack.Attacker, "attack", new Hit(attack.Target, attack.Damage, attack.TargetHpAfter));
                    entry.autoApproach = autoApproach;
                    _log.actions.Add(entry);
                    break;
                }
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
                case PlaceMineEvent mine:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = mine.Sapper.Team.ToString(), unit = Name(mine.Sapper), type = "place mine",
                        toX = mine.Tile.X, toY = mine.Tile.Y, apAfter = ApAfter(mine.Sapper),
                    });
                    break;
                case BombThrowEvent bomb:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = bomb.Thrower.Team.ToString(), unit = Name(bomb.Thrower), type = "throw bomb",
                        toX = bomb.Tile.X, toY = bomb.Tile.Y,
                    });
                    break;
                case ExplosionEvent blast:
                    // One entry per unit hit (or one with no target if it hit nobody). "unit" is whoever placed or threw it.
                    if (blast.Hits.Count == 0)
                        _log.actions.Add(BlastEntry(turn, blast));
                    foreach (var hit in blast.Hits)
                    {
                        var entry = BlastEntry(turn, blast);
                        entry.target = Name(hit.Target);
                        entry.damage = hit.Damage;
                        entry.targetHpAfter = hit.HpAfter;
                        entry.killed = hit.Killed;
                        entry.friendlyFire = blast.Owner != null && blast.Owner.Team == hit.Target.Team;
                        _log.actions.Add(entry);
                    }

                    break;
                case AreaAttackEvent area:
                    // One entry per unit hit; type is the shape ("sweep", "pierce", "volley", "long reach", "fireball").
                    foreach (var hit in area.Hits)
                    {
                        var entry = HitEntry(turn, area.Attacker, area.Kind, hit);
                        entry.toX = area.Center.X;
                        entry.toY = area.Center.Y;
                        entry.friendlyFire = hit.Target.Team == area.Attacker.Team;
                        entry.smashTargets = area.Hits.Count;
                        entry.autoApproach = autoApproach;
                        _log.actions.Add(entry);
                    }

                    break;
                case PushEvent push:
                {
                    var entry = new ActionEntry
                    {
                        turn = turn, team = push.Pusher?.Team.ToString() ?? "", unit = push.Pusher != null ? Name(push.Pusher) : "",
                        type = "push", target = Name(push.Unit), fromX = push.From.X, fromY = push.From.Y, toX = push.To.X, toY = push.To.Y,
                    };
                    if (push.Collision.HasValue)
                    {
                        entry.note = "collision";
                        entry.damage = push.Collision.Value.Damage;
                        entry.targetHpAfter = push.Collision.Value.HpAfter;
                        entry.killed = push.Collision.Value.Killed;
                    }

                    _log.actions.Add(entry);
                    break;
                }
                case StatusEvent status:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = status.Source.Team.ToString(), unit = Name(status.Source),
                        type = "status", note = status.Kind.ToString().ToLowerInvariant(), target = Name(status.Target), amount = status.Amount,
                    });
                    break;
                case TickEvent tick:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = "", unit = "", type = "tick", note = tick.Cause, target = Name(tick.Hit.Target),
                        damage = tick.Hit.Damage, targetHpAfter = tick.Hit.HpAfter, killed = tick.Hit.Killed,
                    });
                    break;
                case TileEvent tile:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = tile.Source?.Team.ToString() ?? "", unit = tile.Source != null ? Name(tile.Source) : "",
                        type = tile.Added ? "tile added" : "tile removed", note = tile.Kind, toX = tile.Tile.X, toY = tile.Tile.Y,
                    });
                    break;
                case EscapeEvent escape:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = escape.Unit.Team.ToString(), unit = Name(escape.Unit), type = "escape", amount = escape.Gold,
                        toX = escape.Unit.Pos.X, toY = escape.Unit.Pos.Y,
                    });
                    break;
                case SplitEvent split:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = split.Parent.Team.ToString(), unit = Name(split.Parent), type = "split",
                        target = string.Join(", ", split.Children.Select(Name)), amount = split.Children.Count,
                    });
                    break;
                case ReinforceEvent reinforce:
                    _log.wavesArrived += reinforce.Units.Count;
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = Team.Enemy.ToString(), unit = "", type = "reinforce",
                        target = string.Join(", ", reinforce.Units.Select(Name)), amount = reinforce.Units.Count,
                    });
                    break;
                case OmenEvent omen:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = "", unit = "", type = "omen",
                        note = $"{omen.Omen.ToString().ToLowerInvariant()} {(omen.Blessing ? "blessing" : "curse")}",
                        target = Name(omen.Unit), toX = omen.Unit.Pos.X, toY = omen.Unit.Pos.Y,
                    });
                    break;
                case SummonEvent summon:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = summon.Summoner.Team.ToString(), unit = Name(summon.Summoner), type = "summon",
                        target = Name(summon.Summoned), toX = summon.Summoned.Pos.X, toY = summon.Summoned.Pos.Y,
                    });
                    break;
                case EffectEvent effect:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = effect.Source.Team.ToString(), unit = Name(effect.Source),
                        type = EffectType(effect.Kind), target = Name(effect.Unit), damage = effect.Kind == EffectKind.Thorns ? effect.Amount : 0,
                        amount = effect.Amount, targetHpAfter = effect.HpAfter, killed = effect.Kind == EffectKind.Thorns && effect.HpAfter <= 0,
                    });
                    break;
            }
        }

        private static ActionEntry BlastEntry(int turn, ExplosionEvent blast) => new ActionEntry
        {
            turn = turn, team = blast.Owner?.Team.ToString() ?? "", unit = blast.Owner != null ? Name(blast.Owner) : "",
            type = "explosion", note = blast.Cause, toX = blast.Center.X, toY = blast.Center.Y, chainDepth = blast.ChainDepth,
        };

        private static string EffectType(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.Heal: return "heal";
                case EffectKind.Thorns: return "thorns";
                case EffectKind.Bloodthirst: return "bloodthirst";
                case EffectKind.LastStand: return "last stand";
                case EffectKind.Dodge: return "dodge";
                case EffectKind.Absorb: return "shield absorb";
                case EffectKind.Refund: return "ap refund";
                default: return "guard";
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
            if (_battle.Objective != null)
                _log.shrineHpAtEndTurn.Add(_battle.Objective.Hp);
            if (_battle.Totems.Count > 0)
                _log.totemHpAtEndTurn.Add(_battle.Totems.Sum(t => t.Hp));
        }

        /// <summary>
        /// The "units can still act, end turn anyway?" prompt. <paramref name="outcome"/> is "asked",
        /// "confirmed" or "cancelled"; a cancel means the prompt likely caught a misclick.
        /// </summary>
        public void RecordEndTurnPrompt(IEnumerable<Unit> canStillAct, int turn, string outcome)
        {
            _log.actions.Add(new ActionEntry
            {
                turn = turn, team = Team.Player.ToString(), type = "end turn prompt", note = outcome,
                target = string.Join(", ", canStillAct.Select(Name)),
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
            _log.skipped = _battle.WasSkipped;
            _log.stolenGold = _battle.StolenGold;
            _log.survivors = _battle.LivingArmy.Count();
            _log.ritualComplete = _battle.RitualComplete;
            _log.shrineLost = _battle.ObjectiveLost;
            _log.shrineHpLeft = _battle.Objective?.Hp ?? 0;
            _log.totemsSmashed = _battle.Totems.Count(t => !t.IsAlive);
            _log.totemsBroken = _battle.TotemsBroken;
            _log.summoningComplete = _battle.SummoningComplete;
            _log.turnRitualBroken = _battle.TotemsBroken ? _battle.TurnNumber : 0;
            _log.goldEarned = _battle.Phase == Phase.Victory ? _goldIfWon(_log.survivors) : 0;
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
                {
                    // No source (poison, burning ground, sun beams): still count what the target took.
                    if ((a.type == "tick" || a.type == "explosion") && summaries.TryGetValue(a.target ?? "", out var ticked))
                    {
                        ticked.damageTaken += a.damage;
                        if (a.type == "tick")
                            ticked.tickDamageTaken += a.damage;
                        else
                            ticked.sunDamageTaken += a.damage;
                        if (a.killed)
                            ticked.deaths++;
                    }

                    continue;
                }

                if (a.type == "push")
                    s.pushesDone++;
                if (a.type == "status")
                    s.statusesApplied++;
                if (a.type == "status" || a.type == "tile added" || a.type == "summon" || a.type == "place mine" || a.type == "throw bomb")
                    s.actionsUsed++;

                switch (a.type)
                {
                    case "move":
                        s.moves++;
                        s.tilesMoved += a.tiles;
                        if (a.autoApproach)
                            s.walkUpAttacks++;
                        break;
                    case "raise":
                        s.raises++;
                        if (summaries.TryGetValue(a.target, out var raised))
                            raised.timesRaised++;
                        break;
                    case "attack":
                    case "smash":
                    case "boulder":
                    case "explosion":
                    case "thorns":
                    case "sweep":
                    case "pierce":
                    case "volley":
                    case "long reach":
                    case "fireball":
                    case "push":
                    case "tick":
                        if (string.IsNullOrEmpty(a.target))
                            break;
                        if (a.type == "explosion")
                        {
                            s.explosionDamage += a.damage;
                            if (a.friendlyFire)
                                s.friendlyFireDealt += a.damage;
                        }
                        else if (a.type == "thorns")
                        {
                            s.thornsDamage += a.damage;
                        }
                        else
                        {
                            s.attacks++;
                        }

                        s.damageDealt += a.damage;
                        if (a.killed)
                            s.kills++;
                        if (summaries.TryGetValue(a.target, out var t))
                        {
                            t.damageTaken += a.damage;
                            if (a.friendlyFire)
                                t.friendlyFireTaken += a.damage;
                            if (a.killed)
                                t.deaths++;
                        }

                        break;
                    case "place mine":
                        s.minesPlaced++;
                        break;
                    case "throw bomb":
                        s.bombsThrown++;
                        break;
                    case "heal":
                        s.healed += a.amount;
                        break;
                    case "bloodthirst":
                        s.bloodthirstGained += a.amount;
                        break;
                    case "last stand":
                        if (summaries.TryGetValue(a.target, out var saved))
                            saved.lastStands++;
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
            public bool ambush;

            /// <summary>An elite fight (harder rematch of a faction).</summary>
            public bool elite;

            /// <summary>Ended with the debug "Win battle" button instead of being played.</summary>
            public bool skipped;

            /// <summary>Started from the test setup screen (any army, encounter and omen), not part of a run.</summary>
            public bool test;
            public string layout;

            /// <summary>Gold carried off by cutpurses that escaped.</summary>
            public int stolenGold;
            public int goldAtStart;
            public int goldEarned;

            /// <summary>Blessings and curses in play this battle.</summary>
            public List<string> omens = new List<string>();

            /// <summary>
            /// Moon Shrine fights: enemy turns to survive, whether the ritual completed or the shrine broke,
            /// the shrine's HP when each player turn ended and at the end, and how many reinforcements arrived.
            /// </summary>
            public int ritualTurns;
            public bool ritualComplete;
            public bool shrineLost;
            public int shrineHpLeft;
            public List<int> shrineHpAtEndTurn = new List<int>();
            public int wavesArrived;

            /// <summary>
            /// Break the Ritual: the enemy turn the summoning completes on, the totems' total HP when each of your
            /// turns ended, how many were smashed, and whether (and on which turn) the ritual was broken in time.
            /// </summary>
            public int summonTurn;
            public List<int> totemHpAtEndTurn = new List<int>();
            public int totemsSmashed;
            public bool totemsBroken;
            public bool summoningComplete;
            public int turnRitualBroken;
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
            public int tier;
            public string line;
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

            /// <summary>Move or attack done by clicking a distant enemy (walk up and attack).</summary>
            public bool autoApproach;

            /// <summary>Move: steps that left an enemy's reach (zone of control, +2 AP each). "amount" is the AP spent.</summary>
            public int zocSteps;

            /// <summary>
            /// End turn prompt: "asked", "confirmed" or "cancelled" ("target" lists the units that could still act).
            /// Explosion: "mine", "bomb", "barricade" or "sun". Omen: e.g. "sun blessing", "moon curse" ("target" is the unit picked).
            /// </summary>
            public string note;

            /// <summary>Explosion: 0 for the first blast, 1+ for blasts it set off.</summary>
            public int chainDepth;

            /// <summary>Explosion hit a unit on the same team as whoever placed or threw it.</summary>
            public bool friendlyFire;

            /// <summary>Heal, bloodthirst and thorns: how much.</summary>
            public int amount;
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

            /// <summary>Times the player clicked a distant enemy and the unit walked up to attack.</summary>
            public int walkUpAttacks;

            public int minesPlaced;
            public int bombsThrown;

            /// <summary>Damage this unit's mines/bombs dealt (to anyone), and how much of that hit its own side.</summary>
            public int explosionDamage;

            public int friendlyFireDealt;

            /// <summary>Damage this unit took from its own side's explosions.</summary>
            public int friendlyFireTaken;

            public int thornsDamage;
            public int healed;
            public int bloodthirstGained;
            public int lastStands;

            /// <summary>Damage from poison and burning ground.</summary>
            public int tickDamageTaken;

            /// <summary>Damage from sun beams (Sun blessing or curse).</summary>
            public int sunDamageTaken;

            public int pushesDone;
            public int statusesApplied;
            public int actionsUsed;
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
