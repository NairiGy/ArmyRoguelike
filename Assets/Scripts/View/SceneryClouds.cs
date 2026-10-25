using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// The drifting cloud bands of a battle's scenery. Every value is live: select "Scenery" in the
    /// Hierarchy during Play and tune the layers in the Inspector. Play-mode changes are lost when Play
    /// stops, so use the component menu's "Print values" to get them as code for BattleScenery.
    /// </summary>
    public sealed class SceneryClouds : MonoBehaviour
    {
        [Tooltip("Multiplies every layer's speed (0.5 = half speed everywhere).")]
        public float speedMultiplier = 1f;

        public List<Layer> layers = new List<Layer>();

        [Serializable]
        public sealed class Layer
        {
            public string name;
            [Tooltip("Height of the band's bottom edge on screen, in world units.")]
            public float bottom;
            [Tooltip("How tall the band is drawn, in world units.")]
            public float height = 1f;
            [Range(0f, 1f)] public float brightness = 1f;
            [Tooltip("Drift to the right, roughly in screen widths per second. Far layers should be slowest.")]
            public float speed;
            [Range(0f, 1f)] public float opacity = 1f;

            [NonSerialized] public Transform Transform;
            [NonSerialized] public Material Material;
            [NonSerialized] public Mesh Mesh;
            [NonSerialized] public float CentreX;
            [NonSerialized] public float Width;
            [NonSerialized] public float Aspect;
        }

        /// <summary>Adds a band across the screen; it repeats sideways at the strip's own proportions.</summary>
        public void Add(string layerName, Material material, int order, float centreX, float width,
            float bottom, float height, float brightness, float speed, float opacity)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            // A quad from (0, 0) to (width, 1); position and height come from the transform.
            var mesh = new Mesh { name = layerName };
            mesh.vertices = new[] { new Vector3(0f, 0f), new Vector3(width, 0f), new Vector3(width, 1f), new Vector3(0f, 1f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingOrder = order;
            var tex = material.mainTexture;
            layers.Add(new Layer
            {
                name = layerName, bottom = bottom, height = height, brightness = brightness, speed = speed, opacity = opacity,
                Transform = go.transform, Material = material, Mesh = mesh, CentreX = centreX, Width = width,
                Aspect = (float)tex.width / tex.height,
            });
            Apply(layers[layers.Count - 1]);
        }

        private void LateUpdate()
        {
            foreach (var layer in layers)
                Apply(layer);
        }

        private void Apply(Layer layer)
        {
            if (layer.Transform == null)
                return;
            layer.Transform.localPosition = new Vector3(layer.CentreX - layer.Width * 0.5f, layer.bottom, 0f);
            layer.Transform.localScale = new Vector3(1f, Mathf.Max(0.01f, layer.height), 1f);
            var repeats = layer.Width / (Mathf.Max(0.01f, layer.height) * layer.Aspect);
            layer.Material.mainTextureScale = new Vector2(repeats, 1f);
            // A negative scroll moves the texture to the right, with the wind.
            layer.Material.SetFloat("_Scroll", -layer.speed * speedMultiplier * repeats);
            var col = new Color(layer.brightness, 0f, 0f, layer.opacity);
            layer.Mesh.colors = new[] { col, col, col, col };
        }

        [ContextMenu("Print values")]
        private void PrintValues()
        {
            var c = CultureInfo.InvariantCulture;
            var lines = new List<string> { $"speedMultiplier = {speedMultiplier.ToString(c)}" };
            foreach (var l in layers)
                lines.Add(string.Format(c, "{0}: bottom {1:0.###}, height {2:0.###}, brightness {3:0.###}, speed {4:0.#####}, opacity {5:0.###}",
                    l.name, l.bottom, l.height, l.brightness, l.speed, l.opacity));
            Debug.Log("Cloud values:\n" + string.Join("\n", lines));
        }
    }
}
