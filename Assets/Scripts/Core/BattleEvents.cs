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

        public MoveEvent(Unit unit, GridPos from, IReadOnlyList<GridPos> path)
        {
            Unit = unit;
            From = from;
            Path = path;
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
}
