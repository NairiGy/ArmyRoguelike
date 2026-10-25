using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Live art knobs for the battle: select "Scenery" in the Hierarchy during Play and change them in the
    /// Inspector. Play-mode changes are lost when Play stops; tell the art chat the values you like.
    /// </summary>
    public sealed class ArtTuning : MonoBehaviour
    {
        [Tooltip("Size of every unit (statue, base, shadow, bars). 1 = the art's own size.")]
        [Range(0.4f, 1.3f)] public float unitScale = 1f;

        private void Awake() => unitScale = BattleScenery.UnitScale;

        private void Update() => BattleScenery.UnitScale = unitScale;
    }
}
