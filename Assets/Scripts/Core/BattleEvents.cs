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
