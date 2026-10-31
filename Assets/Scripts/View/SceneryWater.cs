using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Live knobs for the moving sea (the Water shader on the "Land" backdrop): select "Land" under
    /// "Scenery" in the Hierarchy during Play and change them in the Inspector. Play-mode changes are lost
    /// when Play stops; tell the art chat the values you like.
    /// </summary>
    public sealed class SceneryWater : MonoBehaviour
    {
        [Tooltip("How far the water wobbles, in pixels of the painting.")]
        [Range(0f, 8f)] public float wobble = 2.5f;
        [Tooltip("How many waves fit across the screen's height: more = smaller, busier waves.")]
        [Range(2f, 30f)] public float waveSize = 9f;
        [Range(0f, 1f)] public float speed = 0.12f;
        [Tooltip("How bright foam and spray glint as the sheen passes.")]
        [Range(0f, 0.5f)] public float foamGlint = 0.12f;

        private Material _material;

        public static SceneryWater Attach(SpriteRenderer sea, Texture2D mask)
        {
            var water = sea.gameObject.AddComponent<SceneryWater>();
            water._material = new Material(Resources.Load<Shader>("Shaders/Water"));
            water._material.SetTexture("_Mask", mask);
            sea.sharedMaterial = water._material;
            water.Apply();
            return water;
        }

        private void Update() => Apply();

        private void Apply()
        {
            _material.SetFloat("_Strength", wobble);
            _material.SetFloat("_Scale", waveSize);
            _material.SetFloat("_Speed", speed);
            _material.SetFloat("_Glint", foamGlint);
        }
    }
}
