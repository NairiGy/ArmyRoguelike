using System.Collections.Generic;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Short-lived light from effects (a sun beam striking, say) that warms the floor and the statues
    /// around it. The scenery and statue shaders read up to four of these as global arrays.
    /// </summary>
    public sealed class FxLight
    {
        private const int Max = 4;

        public Vector2 Position;
        public Color Color;
        public float Radius;
        public float Strength;

        private static readonly List<FxLight> Lights = new List<FxLight>();
        private static readonly Vector4[] Positions = new Vector4[Max];
        private static readonly Vector4[] Colors = new Vector4[Max];
        private static readonly int LightsId = Shader.PropertyToID("_FxLights");
        private static readonly int ColorsId = Shader.PropertyToID("_FxLightColors");
        private static Runner _runner;

        private sealed class Runner : MonoBehaviour
        {
            private void LateUpdate() => Upload();

            private void OnDestroy()
            {
                Lights.Clear();
                Upload();
            }
        }

        /// <summary>Adds a light (strength 0 until set). Remove it when the effect ends.</summary>
        public static FxLight Add(Vector2 position, Color color, float radius)
        {
            if (_runner == null)
                _runner = new GameObject("Fx lights").AddComponent<Runner>();
            var light = new FxLight { Position = position, Color = color, Radius = radius };
            Lights.Add(light);
            return light;
        }

        public void Remove() => Lights.Remove(this);

        private static void Upload()
        {
            for (var i = 0; i < Max; i++)
            {
                var l = i < Lights.Count ? Lights[i] : null;
                Positions[i] = l == null ? Vector4.zero : new Vector4(l.Position.x, l.Position.y, l.Radius, l.Strength);
                Colors[i] = l == null ? Vector4.zero : (Vector4)l.Color;
            }

            Shader.SetGlobalVectorArray(LightsId, Positions);
            Shader.SetGlobalVectorArray(ColorsId, Colors);
        }
    }
}
