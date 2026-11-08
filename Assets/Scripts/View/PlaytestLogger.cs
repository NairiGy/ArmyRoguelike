using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArmyRoguelike.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

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

        // A stopwatch, not Time.realtimeSinceStartup: Unity resets that when Play mode stops, which is when an
        // abandoned battle gets saved, so its duration came out negative.
        private readonly Stopwatch _clock = new Stopwatch();
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
            _log.boardSize = battle.Board.Width;
            _log.goldAtStart = context.Gold;
            _goldIfWon = context.GoldIfWon;
            _log.omens = battle.Omens.Select(o => Omens.Name(o.omen, o.side == Team.Player)).ToList();
            _log.banners = battle.Banners.Select(Banners.Name).ToList();
            _log.battlefield = battle.Field.HasValue ? Battlefields.Name(battle.Field.Value) : "";
            var champion = battle.Champion;
            _log.champion = champion == null ? "" : $"{Champions.Name(champion.Affix)} {champion.Stats.Name}";
            _log.bossMaxHp = battle.Units.Where(u => u.Stats.IsBoss).Select(u => u.MaxHp).DefaultIfEmpty(0).Max();
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
            if (!_clock.IsRunning)
                BattleStarted();
        }

        public void BattleStarted()
        {
            _clock.Restart();
            foreach (var u in _battle.Units.Where(u => u.Team == Team.Player && !u.IsObjective))
                _log.placements.Add(new PlacementEntry
                {
                    unit = Name(u), x = u.Pos.X, y = u.Pos.Y, equipment = u.Equipment?.Name ?? "", maxHp = u.MaxHp,
                    damage = u.BaseDamage, range = u.Range, maxAp = u.MaxAp, maxMoves = u.MaxMoves, line = u.Stats.Name,
                    level = u.Member?.Level ?? 0, techniques = u.Member?.Techniques.Select(t => t.Name).ToList() ?? new List<string>(),
                    weight = u.Stats.Weight.ToString().ToLowerInvariant(), mercenary = u.IsMercenary,
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
                        apAfter = ApAfter(move.Unit), movesAfter = move.Unit.Moves, autoApproach = autoApproach, amount = move.Cost, zocSteps = move.ZocSteps,
                        throughAllies = move.Path.Take(move.Path.Count - 1).Count(p => _battle.UnitAt(p) != null), slowed = move.Unit.SlowedThisTurn,
                    });
                    break;
                case AttackEvent attack:
                {
                    var entry = HitEntry(turn, attack.Attacker, attack.Via ?? "attack", new Hit(attack.Target, attack.Damage, attack.TargetHpAfter));
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
                        note = raise.From != null ? "harvested " + Name(raise.From) : null,
                    });
                    break;
                case CorpseBlastEvent blast:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = blast.Caster.Team.ToString(), unit = Name(blast.Caster), type = "corpse blast",
                        target = Name(blast.Pile), toX = blast.Pile.Pos.X, toY = blast.Pile.Pos.Y,
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
                    // "unit" is who caused it (the poisoner, the fireball's caster, the pusher); empty for "leader lost".
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = tick.Source?.Team.ToString() ?? "", unit = tick.Source != null ? Name(tick.Source) : "",
                        type = "tick", note = tick.Cause, target = Name(tick.Hit.Target),
                        damage = tick.Hit.Damage, targetHpAfter = tick.Hit.HpAfter, killed = tick.Hit.Killed,
                        friendlyFire = tick.Source != null && tick.Source.Team == tick.Hit.Target.Team,
                    });
                    break;
                case BarricadeHitEvent barricade:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = barricade.Attacker.Team.ToString(), unit = Name(barricade.Attacker), type = "barricade hit",
                        damage = barricade.Damage, targetHpAfter = barricade.HpAfter, killed = barricade.HpAfter == 0,
                        toX = barricade.Tile.X, toY = barricade.Tile.Y,
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
                case UpheavalEvent upheaval:
                    _log.actions.Add(HitEntry(turn, upheaval.Caster, "upheaval", upheaval.Hit));
                    break;
                case SplitEvent split:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = split.Parent.Team.ToString(), unit = Name(split.Parent), type = "split",
                        target = string.Join(", ", split.Children.Select(Name)), amount = split.Children.Count,
                    });
                    break;
                case FlingEvent fling:
                {
                    var entry = fling.Hit.HasValue
                        ? HitEntry(turn, fling.Thrower, "fling", fling.Hit.Value)
                        : new ActionEntry { turn = turn, team = fling.Thrower.Team.ToString(), unit = Name(fling.Thrower), type = "fling", target = Name(fling.Target) };
                    entry.note = fling.Thrown != null ? Name(fling.Thrown) : "explosive";
                    entry.fromX = fling.From.X;
                    entry.fromY = fling.From.Y;
                    entry.toX = fling.To.X;
                    entry.toY = fling.To.Y;
                    _log.actions.Add(entry);
                    break;
                }
                case OverwatchEvent watch:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = watch.Watcher.Team.ToString(), unit = Name(watch.Watcher), type = "overwatch", amount = watch.Tiles.Count,
                    });
                    break;
                case HowlEvent howl:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = howl.Leader.Team.ToString(), unit = Name(howl.Leader), type = "howl",
                        target = string.Join(", ", howl.Wolves.Select(Name)), amount = howl.Wolves.Count,
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
                case FieldEvent field:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = field.Unit.Team.ToString(), unit = "",
                        type = field.Field == Battlefield.Storm ? "storm mark" : "favored",
                        target = Name(field.Unit), toX = field.Unit.Pos.X, toY = field.Unit.Pos.Y,
                    });
                    break;
                case FallEvent fall:
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = fall.Pusher?.Team.ToString() ?? "", unit = fall.Pusher != null ? Name(fall.Pusher) : "",
                        type = "fall", target = Name(fall.Unit), fromX = fall.Edge.X, fromY = fall.Edge.Y,
                        damage = fall.Hit.Damage, targetHpAfter = 0, killed = true,
                        friendlyFire = fall.Pusher != null && fall.Pusher.Team == fall.Unit.Team,
                    });
                    break;
                case SwapEvent swap:
                    // Hellswap: "from" is where the imp stood (your unit lands there), "to" where your unit stood.
                    _log.actions.Add(new ActionEntry
                    {
                        turn = turn, team = swap.Caster.Team.ToString(), unit = Name(swap.Caster), type = "hellswap", target = Name(swap.Target),
                        fromX = swap.CasterFrom.X, fromY = swap.CasterFrom.Y, toX = swap.TargetFrom.X, toY = swap.TargetFrom.Y,
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
            favored = blast.Owner != null && blast.Owner.Favored,
        };

        private static string EffectType(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.Heal: return "heal";
                case EffectKind.Thorns: return "thorns";
                case EffectKind.LastStand: return "last stand";
                case EffectKind.Dodge: return "dodge";
                case EffectKind.Absorb: return "shield absorb";
                case EffectKind.Refund: return "ap refund";
                case EffectKind.ZocStrike: return "zoc strike";
                case EffectKind.CoveringFire: return "covering fire";
                case EffectKind.Mark: return "mark";
                case EffectKind.Crush: return "crush";
                case EffectKind.Fury: return "fury";
                default: return "guard";
            }
        }

        private ActionEntry HitEntry(int turn, Unit attacker, string type, Hit hit) => new ActionEntry
        {
            turn = turn, team = attacker.Team.ToString(), unit = Name(attacker), type = type, target = Name(hit.Target),
            damage = hit.Damage, targetHpAfter = hit.HpAfter, killed = hit.Killed, apAfter = ApAfter(attacker),
            ranged = attacker.IsRanged && Battle.Distance(attacker, hit.Target) > 1, favored = attacker.Favored, fury = attacker.Fury,
            slowed = attacker.SlowedThisTurn,
        };

        private static int ApAfter(Unit u) => u.Team == Team.Player ? u.Ap : -1;

        public void RecordUnitDone(Unit unit, int turn)
        {
            _log.actions.Add(new ActionEntry
            {
                turn = turn, team = unit.Team.ToString(), unit = Name(unit), type = "done", apAfter = unit.Ap, movesAfter = unit.Moves, slowed = unit.SlowedThisTurn,
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
                secondsIntoBattle = (float)_clock.Elapsed.TotalSeconds,
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
            if (_saved || (!_clock.IsRunning && _log.markers.Count == 0))
                return null;
            _saved = true;

            _log.endedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _log.durationSeconds = (float)_clock.Elapsed.TotalSeconds;
            (_log.gitCommit, _log.gitDirty, _log.gitDirtyCode) = GitStamp.Read();
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

        /// <summary>
        /// Each technique action the unit knows and how often it used it this battle (0 = never). Passive techniques
        /// are listed under the placements; their effects show in the actions (sweep, pierce, counters and so on).
        /// </summary>
        private static List<TechniqueUse> TechniqueUses(Unit u) =>
            (u.Member?.Techniques ?? (IEnumerable<Technique>)new Technique[0]).Where(t => t.Action.HasValue)
                .Select(t => new TechniqueUse { technique = t.Name, uses = u.TimesUsed(t.Action.Value) }).ToList();

        private List<UnitSummary> Summarise()
        {
            // Escaped units have left the board, but what they did before that still counts.
            var summaries = _battle.Units.Where(u => u.IsPlaced || u.Escaped).ToDictionary(Name, u => new UnitSummary
            {
                unit = Name(u), team = u.Team.ToString(), equipment = u.Equipment?.Name ?? "", survived = u.IsAlive, escaped = u.Escaped,
                hpLeft = u.Hp, maxHp = u.MaxHp, level = u.Member?.Level ?? 0,
                techniqueUses = TechniqueUses(u),
            });
            foreach (var a in _log.actions)
            {
                if (a.unit == null || !summaries.TryGetValue(a.unit, out var s))
                {
                    // No unit behind it (a Pack Leader lost, sun beams, lightning): still count what the target took.
                    if ((a.type == "tick" || a.type == "explosion") && summaries.TryGetValue(a.target ?? "", out var ticked))
                    {
                        ticked.damageTaken += a.damage;
                        if (a.type == "tick")
                            ticked.tickDamageTaken += a.damage;
                        else if (a.note == "lightning")
                            ticked.lightningDamageTaken += a.damage;
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
                    case "kick":
                    case "fire arrow":
                    case "throwing knife":
                    case "rupture":
                    case "covering fire":
                    case "smash":
                    case "boulder":
                    case "fling":
                    case "slam":
                    case "explosion":
                    case "thorns":
                    case "sweep":
                    case "pierce":
                    case "volley":
                    case "long reach":
                    case "fireball":
                    case "upheaval":
                    case "push":
                    case "fall":
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
                        else if (a.type == "tick")
                        {
                            s.tickDamageDealt += a.damage;
                            if (a.friendlyFire)
                                s.friendlyFireDealt += a.damage;
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
                            if (a.type == "tick")
                                t.tickDamageTaken += a.damage;
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

            /// <summary>See <see cref="GitStamp"/>: the commit it was played on, and uncommitted changes (anywhere; in the scripts).</summary>
            public string gitCommit;
            public bool gitDirty;
            public bool gitDirtyCode;
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

            /// <summary>Board side length in tiles (6 in the small-board experiment, 8 before).</summary>
            public int boardSize;

            /// <summary>Gold carried off by cutpurses that escaped.</summary>
            public int stolenGold;
            public int goldAtStart;
            public int goldEarned;

            /// <summary>The army's banners in play this battle.</summary>
            public List<string> banners = new List<string>();

            /// <summary>
            /// The battlefield rule ("" for none). Its actions: "fall" (pushed off the board), "storm mark" and
            /// "favored" (the unit picked), explosions with note "lightning", and "favored" on hits a favored unit dealt.
            /// </summary>
            public string battlefield;

            /// <summary>The fight's champion as "Armored Bandit Brute" ("" for none), and the boss's max HP after scaling to the army's levels (0: no boss). Statuses "phase" (amount = the new phase) and "rockfall" mark Stone Heart; explosions "rockfall" and "vengeful".</summary>
            public string champion = "";
            public int bossMaxHp;

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
            public int maxMoves;
            /// <summary>Level (techniques learned) and the techniques it knows.</summary>
            public int level;
            public List<string> techniques = new List<string>();
            public string line;

            /// <summary>"small", "medium" or "huge" (push distance), and whether it was hired for this fight only.</summary>
            public string weight;
            public bool mercenary;
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

            /// <summary>Move and done: moves the unit has left this turn (moves are separate from AP).</summary>
            public int movesAfter;

            /// <summary>Attacks and area hits: a ranged shot from 2+ tiles away (for the ranged vs melee damage split).</summary>
            public bool ranged;

            /// <summary>Move, hit and done: the unit was slowed this turn (it can move or act, not both). A slowed move with no hit after it is an action given up.</summary>
            public bool slowed;

            /// <summary>Hits and explosions: dealt by a unit favored by fortune (Fortune's Favored), so at double damage.</summary>
            public bool favored;

            /// <summary>Hits: the attacker's Fury (demons, +1 damage each) when it struck; type "rending blow" marks a Rending Blow.</summary>
            public int fury;

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

            /// <summary>Move: steps that left an enemy's reach (zone of control: uses up the remaining moves). "amount" is the moves spent.</summary>
            public int zocSteps;
            public int throughAllies;

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
        private sealed class TechniqueUse
        {
            public string technique;
            public int uses;
        }

        [Serializable]
        private sealed class UnitSummary
        {
            public string unit;
            public string team;
            public string equipment;

            /// <summary>Player units: level, and how often each technique action was used.</summary>
            public int level;
            public List<TechniqueUse> techniqueUses = new List<TechniqueUse>();
            public bool survived;

            /// <summary>Left the board alive (a cutpurse with its loot).</summary>
            public bool escaped;
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

            /// <summary>Damage from poison, burning ground, caltrops, a lost Pack Leader and being pushed (the ticks in the actions).</summary>
            public int tickDamageTaken;

            /// <summary>Damage this unit's poison, burning ground, caltrops and pushes dealt; also in damageDealt.</summary>
            public int tickDamageDealt;

            /// <summary>Damage from sun beams (Sun blessing or curse) and lightning (Storm), which no unit gets the credit for.</summary>
            public int sunDamageTaken;

            public int lightningDamageTaken;

            public int pushesDone;
            public int statusesApplied;
            public int actionsUsed;
        }
    }

    /// <summary>
    /// Which version of the game the logs came from. Asked of git once when Play mode starts, in the background so the
    /// game doesn't wait for it: the code running is what was compiled then, whatever gets committed while playing.
    /// Without git (a build, or no git on the machine) the commit is "unknown" and the dirty flags mean nothing.
    /// </summary>
    internal static class GitStamp
    {
        private const string Unknown = "unknown";
        private static Task<(string commit, bool dirty, bool dirtyCode)> _reading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Begin()
        {
            _reading = null;
            if (!Application.isEditor)
                return;
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            _reading = Task.Run(() => AskGit(root));
        }

        public static (string commit, bool dirty, bool dirtyCode) Read() =>
            _reading != null && _reading.Wait(TimeSpan.FromSeconds(2)) ? _reading.Result : (Unknown, false, false);

        private static (string, bool, bool) AskGit(string root)
        {
            try
            {
                var commit = Git(root, "rev-parse HEAD").Trim();
                // --no-optional-locks: don't get in the way of a commit happening at the same time.
                var changes = Git(root, "--no-optional-locks status --porcelain").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                return (commit, changes.Length > 0, changes.Any(c => c.Contains("Assets/Scripts/")));
            }
            catch (Exception)
            {
                return (Unknown, false, false);
            }
        }

        private static string Git(string root, string arguments)
        {
            var start = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            };
            using (var git = Process.Start(start))
            {
                var output = git.StandardOutput.ReadToEnd();
                git.WaitForExit();
                if (git.ExitCode != 0)
                    throw new InvalidOperationException($"git {arguments} failed");
                return output;
            }
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
