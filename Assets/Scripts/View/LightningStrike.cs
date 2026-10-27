using System.Collections;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// A lightning bolt striking a tile: the sky flickers, then a painted bolt cracks down from above the
    /// screen, flickering between three shapes, and the whole board flashes white-blue. Sparks, a shake
    /// and a scorch that flashes blue-white and goes dark. Visual only; no rule uses it yet (test key L).
    /// </summary>
    public static class LightningStrike
    {
        private const int Shapes = 3;
        private static readonly Color BoltColor = new Color(0.8f, 0.9f, 1f);
        private static readonly Color FlashColor = new Color(0.7f, 0.8f, 1f);

        private static Sprite[] _bolts;
        private static Material _material;

        private static Material Material => _material != null ? _material : _material = new Material(Resources.Load<Shader>("Shaders/Glow"));

        /// <summary>The three bolts, cut from one painted sheet (each a third of its width).</summary>
        private static Sprite[] Bolts
        {
            get
            {
                if (_bolts != null)
                    return _bolts;
                var tex = Resources.Load<Texture2D>("Effects/Lightning");
                var w = tex.width / Shapes;
                _bolts = new Sprite[Shapes];
                for (var i = 0; i < Shapes; i++)
                    _bolts[i] = Sprite.Create(tex, new Rect(i * w, 0, w, tex.height), new Vector2(0.5f, 0f), w);
                return _bolts;
            }
        }

        public static IEnumerator Strike(Transform parent, GridPos tile)
        {
            var pos = IsoMath.GridToWorld(tile);
            var sky = FxLight.Add(pos, FlashColor, 40f);
            var near = FxLight.Add(pos, FlashColor, 3f);

            // The sky flickers twice before the bolt, under a far-off rumble.
            Sfx.Play("lightning_rumble", pos, 0.5f, 0.1f);
            foreach (var (on, off) in new[] { (0.05f, 0.12f), (0.04f, 0.18f) })
            {
                sky.Strength = 0.25f;
                yield return new WaitForSeconds(on);
                sky.Strength = 0f;
                yield return new WaitForSeconds(off);
            }

            // The bolt: from above the top of the screen down to the floor of the tile.
            var bolt = new GameObject("Lightning").AddComponent<SpriteRenderer>();
            bolt.transform.SetParent(parent, false);
            bolt.transform.position = pos;
            bolt.sharedMaterial = Material;
            bolt.sortingOrder = IsoMath.SortingOrder(tile) + 6;
            var cam = Camera.main;
            var top = cam != null ? cam.transform.position.y + cam.orthographicSize + 1f : pos.y + 10f;
            var height = top - pos.y;

            Fx.BoltBurst(pos);
            Fx.Shake(0.16f, 0.3f);
            Sfx.Play("lightning_strike", pos);
            Scorch.Create(parent, tile, -300 + (tile.X + tile.Y) * 4, true);

            // Flicker between the shapes, bright then fading, with the light pulsing along.
            const float strike = 0.32f;
            var shape = Random.Range(0, Shapes);
            var next = 0f;
            for (var t = 0f; t < 1f; t += Time.deltaTime / strike)
            {
                if (t >= next)
                {
                    shape = (shape + Random.Range(1, Shapes)) % Shapes;
                    bolt.sprite = Bolts[shape];
                    bolt.flipX = Random.value < 0.5f;
                    next = t + Random.Range(0.12f, 0.22f);
                }

                var size = bolt.sprite.bounds.size;
                bolt.transform.localScale = new Vector3(height / size.y * 0.55f, height / size.y, 1f);
                var flicker = Random.Range(0.75f, 1f);
                var k = 1f - t * t;
                bolt.color = new Color(BoltColor.r, BoltColor.g, BoltColor.b, k * flicker);
                sky.Strength = 0.6f * k * k * flicker;
                near.Strength = 3f * k * flicker;
                yield return null;
            }

            Object.Destroy(bolt.gameObject);

            // The light dies away.
            for (var t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
            {
                sky.Strength = 0f;
                near.Strength = 0.8f * (1f - t) * (1f - t);
                yield return null;
            }

            sky.Remove();
            near.Remove();
        }
    }
}
