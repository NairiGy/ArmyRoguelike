using System.Collections.Generic;

namespace ArmyRoguelike.Core
{
    /// <summary>Something that happened in a battle, recorded so the view can animate it afterwards.</summary>
    public abstract class BattleEvent
    {
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

        public bool Killed => TargetHpAfter <= 0;
    }
}
