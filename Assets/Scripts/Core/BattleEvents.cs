using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>Something that happened in a battle, recorded so the view can animate it afterwards.</summary>
    public abstract class BattleEvent
    {
        /// <summary>The unit that acted.</summary>
        public abstract Unit Actor { get; }
    }

    /// <summary>Damage dealt to one unit.</summary>
    public readonly struct Hit
    {
        public readonly Unit Target;
        public readonly int Damage;
        public readonly int HpAfter;

        public Hit(Unit target, int damage, int hpAfter)
        {
            Target = target;
            Damage = damage;
            HpAfter = hpAfter;
        }

        public bool Killed => HpAfter <= 0;
    }

    public sealed class MoveEvent : BattleEvent
    {
        public readonly Unit Unit;
        public readonly GridPos From;

        /// <summary>Tiles stepped onto, in order. Does not include the starting tile.</summary>
        public readonly IReadOnlyList<GridPos> Path;

        /// <summary>Moves spent, and how many steps left an enemy's reach (zone of control: uses up the remaining moves).</summary>
        public readonly int Cost;

        public readonly int ZocSteps;

        public MoveEvent(Unit unit, GridPos from, IReadOnlyList<GridPos> path, int cost = 0, int zocSteps = 0)
        {
            Unit = unit;
            From = from;
            Path = path;
            Cost = cost;
            ZocSteps = zocSteps;
        }

        public override Unit Actor => Unit;

        public GridPos To => Path.Count > 0 ? Path[Path.Count - 1] : From;
    }

    public sealed class AttackEvent : BattleEvent
    {
        public readonly Unit Attacker;
        public readonly Unit Target;
        public readonly int Damage;
        public readonly int TargetHpAfter;

        public AttackEvent(Unit attacker, Unit target, int damage, int targetHpAfter)
        {
            Attacker = attacker;
            Target = target;
            Damage = damage;
            TargetHpAfter = targetHpAfter;
        }

        public override Unit Actor => Attacker;

        public bool Killed => TargetHpAfter <= 0;
    }

    /// <summary>An area melee attack (ogre club) that hit every adjacent enemy.</summary>
    public sealed class SmashEvent : BattleEvent
    {
        public readonly Unit Attacker;
        public readonly IReadOnlyList<Hit> Hits;

        public SmashEvent(Unit attacker, IReadOnlyList<Hit> hits)
        {
            Attacker = attacker;
            Hits = hits;
        }

        public override Unit Actor => Attacker;
    }

    /// <summary>A thrown boulder. The rock stays on <see cref="RockTile"/> as a new obstacle, if there was room.</summary>
    public sealed class BoulderEvent : BattleEvent
    {
        public readonly Unit Thrower;
        public readonly Hit Hit;
        public readonly GridPos? RockTile;

        public BoulderEvent(Unit thrower, Hit hit, GridPos? rockTile)
        {
            Thrower = thrower;
            Hit = hit;
            RockTile = rockTile;
        }

        public override Unit Actor => Thrower;
    }

    /// <summary>A sapper placed a mine. Only the player sees mines.</summary>
    public sealed class PlaceMineEvent : BattleEvent
    {
        public readonly Unit Sapper;
        public readonly GridPos Tile;

        public PlaceMineEvent(Unit sapper, GridPos tile)
        {
            Sapper = sapper;
            Tile = tile;
        }

        public override Unit Actor => Sapper;
    }

    /// <summary>A goblin lobbed a bomb onto a tile. It explodes at the start of the next enemy turn.</summary>
    public sealed class BombThrowEvent : BattleEvent
    {
        public readonly Unit Thrower;
        public readonly GridPos Tile;

        public BombThrowEvent(Unit thrower, GridPos tile)
        {
            Thrower = thrower;
            Tile = tile;
        }

        public override Unit Actor => Thrower;
    }

    /// <summary>A mine or bomb blew up, hitting every unit in the 3x3 area around it, friend or foe.</summary>
    public sealed class ExplosionEvent : BattleEvent
    {
        /// <summary>"mine" or "bomb".</summary>
        public readonly string Cause;

        /// <summary>Who placed or threw it; gets the credit for kills. May be dead by now.</summary>
        public readonly Unit Owner;

        public readonly GridPos Center;
        public readonly IReadOnlyList<GridPos> Area;
        public readonly IReadOnlyList<Hit> Hits;

        /// <summary>0 for the first blast, 1+ for blasts set off by another blast.</summary>
        public readonly int ChainDepth;

        public ExplosionEvent(string cause, Unit owner, GridPos center, IReadOnlyList<GridPos> area, IReadOnlyList<Hit> hits, int chainDepth)
        {
            Cause = cause;
            Owner = owner;
            Center = center;
            Area = area;
            Hits = hits;
            ChainDepth = chainDepth;
        }

        public override Unit Actor => Owner;
    }

    public enum EffectKind
    {
        /// <summary>Vampire Fang: <see cref="EffectEvent.Amount"/> HP healed.</summary>
        Heal,

        /// <summary>Thorned Mail hurt the melee attacker for <see cref="EffectEvent.Amount"/>.</summary>
        Thorns,

        /// <summary>Bloodthirst: +<see cref="EffectEvent.Amount"/> damage for the run.</summary>
        Bloodthirst,

        /// <summary>Lucky Charm kept the unit alive at 1 HP.</summary>
        LastStand,

        /// <summary>Dodge turned a hit into 0 damage.</summary>
        Dodge,

        /// <summary>A ward shield soaked up <see cref="EffectEvent.Amount"/> damage.</summary>
        Absorb,

        /// <summary>Momentum: a kill gave back <see cref="EffectEvent.Amount"/> AP.</summary>
        Refund,

        /// <summary>Guard: <see cref="EffectEvent.Unit"/> stepped in to take a hit for an ally.</summary>
        Guard,

        /// <summary>A cutpurse (<see cref="EffectEvent.Unit"/>) grabbed <see cref="EffectEvent.Amount"/> gold.</summary>
        Steal,
    }

    /// <summary>A unit left the board, e.g. a cutpurse escaping with stolen gold.</summary>
    public sealed class EscapeEvent : BattleEvent
    {
        public readonly Unit Unit;
        public readonly int Gold;

        public EscapeEvent(Unit unit, int gold)
        {
            Unit = unit;
            Gold = gold;
        }

        public override Unit Actor => Unit;
    }

    /// <summary>A dying unit split into smaller ones (Elemental into Shards).</summary>
    public sealed class SplitEvent : BattleEvent
    {
        public readonly Unit Parent;
        public readonly IReadOnlyList<Unit> Children;

        public SplitEvent(Unit parent, IReadOnlyList<Unit> children)
        {
            Parent = parent;
            Children = children;
        }

        public override Unit Actor => null;
    }

    /// <summary>A unit was shoved one or more tiles. If it slammed into something it took <see cref="Collision"/> damage.</summary>
    public sealed class PushEvent : BattleEvent
    {
        public readonly Unit Unit;
        public readonly Unit Pusher;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly Hit? Collision;

        public PushEvent(Unit unit, Unit pusher, GridPos from, GridPos to, Hit? collision)
        {
            Unit = unit;
            Pusher = pusher;
            From = from;
            To = to;
            Collision = collision;
        }

        public override Unit Actor => Pusher;
    }

    /// <summary>One action that hit several units at once: sweep, pierce, volley, fireball.</summary>
    public sealed class AreaAttackEvent : BattleEvent
    {
        public readonly Unit Attacker;

        /// <summary>"sweep", "pierce", "volley", "long reach", "double strike" or "fireball".</summary>
        public readonly string Kind;

        /// <summary>Where the attack was aimed (the fireball's centre, or the main target's tile).</summary>
        public readonly GridPos Center;

        public readonly IReadOnlyList<GridPos> Area;
        public readonly IReadOnlyList<Hit> Hits;

        public AreaAttackEvent(Unit attacker, string kind, GridPos center, IReadOnlyList<GridPos> area, IReadOnlyList<Hit> hits)
        {
            Attacker = attacker;
            Kind = kind;
            Center = center;
            Area = area;
            Hits = hits;
        }

        public override Unit Actor => Attacker;
    }

    public enum StatusKind
    {
        Snare,
        Slow,
        Poison,
        Ward,
        Heal,
    }

    /// <summary>A status or support effect was put on a unit by an action or a hit.</summary>
    public sealed class StatusEvent : BattleEvent
    {
        public readonly Unit Source;
        public readonly Unit Target;
        public readonly StatusKind Kind;
        public readonly int Amount;

        public StatusEvent(Unit source, Unit target, StatusKind kind, int amount)
        {
            Source = source;
            Target = target;
            Kind = kind;
            Amount = amount;
        }

        public override Unit Actor => Source;
    }

    /// <summary>Damage over time at the start of a turn: "poison" or "burn".</summary>
    public sealed class TickEvent : BattleEvent
    {
        public readonly string Cause;
        public readonly Hit Hit;

        public TickEvent(string cause, Hit hit)
        {
            Cause = cause;
            Hit = hit;
        }

        public override Unit Actor => null;
    }

    /// <summary>A temporary or built obstacle appeared or vanished, or a tile started/stopped burning.</summary>
    public sealed class TileEvent : BattleEvent
    {
        /// <summary>"ice", "barricade" or "burn".</summary>
        public readonly string Kind;

        public readonly GridPos Tile;
        public readonly bool Added;
        public readonly Unit Source;

        public TileEvent(string kind, GridPos tile, bool added, Unit source)
        {
            Kind = kind;
            Tile = tile;
            Added = added;
            Source = source;
        }

        public override Unit Actor => Source;
    }

    /// <summary>A unit was created during the battle (a turret).</summary>
    public sealed class SummonEvent : BattleEvent
    {
        public readonly Unit Summoner;
        public readonly Unit Summoned;

        public SummonEvent(Unit summoner, Unit summoned)
        {
            Summoner = summoner;
            Summoned = summoned;
        }

        public override Unit Actor => Summoner;
    }

    /// <summary>An ability went off. <see cref="Unit"/> is the unit it affected.</summary>
    public sealed class EffectEvent : BattleEvent
    {
        public readonly EffectKind Kind;
        public readonly Unit Unit;
        public readonly int Amount;
        public readonly int HpAfter;

        /// <summary>The unit whose ability it was (the Thorned Mail wearer for thorns, otherwise the same unit).</summary>
        public readonly Unit Source;

        public EffectEvent(EffectKind kind, Unit unit, int amount, int hpAfter, Unit source)
        {
            Kind = kind;
            Unit = unit;
            Amount = amount;
            HpAfter = hpAfter;
            Source = source;
        }

        public override Unit Actor => Source;
    }

    /// <summary>A necromancer raised a bone pile back into a full-HP skeleton.</summary>
    public sealed class RaiseEvent : BattleEvent
    {
        public readonly Unit Raiser;
        public readonly Unit Raised;

        public RaiseEvent(Unit raiser, Unit raised)
        {
            Raiser = raiser;
            Raised = raised;
        }

        public override Unit Actor => Raiser;
    }

    /// <summary>A Pack Leader howled: every wolf that hasn't acted yet gets extra moves this turn.</summary>
    public sealed class HowlEvent : BattleEvent
    {
        public readonly Unit Leader;
        public readonly IReadOnlyList<Unit> Wolves;
        public readonly int Moves;

        public HowlEvent(Unit leader, IReadOnlyList<Unit> wolves, int moves)
        {
            Leader = leader;
            Wolves = wolves;
            Moves = moves;
        }

        public override Unit Actor => Leader;
    }

    /// <summary>Enemy reinforcements walked onto the board at the edge. They act from the next enemy turn.</summary>
    public sealed class ReinforceEvent : BattleEvent
    {
        public readonly IReadOnlyList<Unit> Units;

        public ReinforceEvent(IReadOnlyList<Unit> units)
        {
            Units = units;
        }

        public override Unit Actor => null;
    }

    /// <summary>
    /// An omen took effect at the start of the player's turn: a sun mark on a tile, a shroud around a unit,
    /// or a moon shield on a unit. The sun beam itself strikes later as an <see cref="ExplosionEvent"/> ("sun").
    /// </summary>
    public sealed class OmenEvent : BattleEvent
    {
        public readonly Omen Omen;

        /// <summary>True for the player's blessing, false for a curse.</summary>
        public readonly bool Blessing;

        /// <summary>The unit marked, shrouded or shielded.</summary>
        public readonly Unit Unit;

        /// <summary>The marked or shrouded tiles (empty for a shield).</summary>
        public readonly IReadOnlyList<GridPos> Tiles;

        public OmenEvent(Omen omen, bool blessing, Unit unit, IReadOnlyList<GridPos> tiles)
        {
            Omen = omen;
            Blessing = blessing;
            Unit = unit;
            Tiles = tiles;
        }

        public override Unit Actor => null;
    }
}
