using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// The moving sea far below the tower: the Water shader makes the painting flow with the wind and swirl
    /// around rocks, and whitecaps bloom, drift and fade on the open water. Live knobs: select "Land" under
    /// "Scenery" in the Hierarchy during Play and change them in the Inspector. Play-mode changes are lost
    /// when Play stops; tell the art chat the values you like.
    /// </summary>
    public sealed class SceneryWater : MonoBehaviour
    {
        // Across the painted wave crests (which run nearly level): down the screen and a little to the right.
        private static readonly Vector2 Wind = new Vector2(0.45f, -0.9f).normalized;

        [Tooltip("How far the water flows in one cycle, in pixels of the painting.")]
        [Range(0f, 20f)] public float flowDistance = 8f;
        [Tooltip("Flow cycles per second.")]
        [Range(0f, 1f)] public float flowSpeed = 0.25f;
        [Tooltip("How bright foam and spray glint as the sheen passes.")]
        [Range(0f, 0.5f)] public float foamGlint = 0.08f;
        [Tooltip("How many whitecaps are on the water at once.")]
        [Range(0, 40)] public int whitecaps = 14;
        [Tooltip("How white a whitecap gets at its fullest.")]
        [Range(0f, 1f)] public float whitecapStrength = 0.45f;

        private Material _material;
        private Bounds _area;
        private Vector2[] _solid;
        private float _solidRadius;
        private int _order;

        private SpriteRenderer[] _caps = new SpriteRenderer[0];
        private float[] _age;
        private float[] _life;
        private float[] _size;

        /// <param name="swirls">Swirls around rocks, in painting pixels: x, y, radius, turn (+ anticlockwise).</param>
        public static SceneryWater Attach(SpriteRenderer sea, Texture2D mask, Vector4[] swirls, int whitecapOrder)
        {
            var water = sea.gameObject.AddComponent<SceneryWater>();
            water._material = new Material(Resources.Load<Shader>("Shaders/Water"));
            water._material.SetTexture("_Mask", mask);
            water._material.SetVector("_Wind", Wind);
            sea.sharedMaterial = water._material;

            // Swirls go to the shader in uv (rows run up), radius in image heights; whitecaps keep clear of them.
            var tex = sea.sprite.texture;
            var uvSwirls = new Vector4[4];
            water._area = sea.bounds;
            water._solid = new Vector2[swirls.Length];
            for (var i = 0; i < swirls.Length && i < 4; i++)
            {
                var s = swirls[i];
                uvSwirls[i] = new Vector4(s.x / tex.width, 1f - s.y / tex.height, s.z / tex.height, s.w);
                water._solid[i] = new Vector2(water._area.min.x + uvSwirls[i].x * water._area.size.x, water._area.min.y + uvSwirls[i].y * water._area.size.y);
            }

            water._solidRadius = water._area.size.y * 0.06f;
            water._material.SetVectorArray("_Swirls", uvSwirls);
            water._order = whitecapOrder;
            water.Apply();
            return water;
        }

        private void Update()
        {
            Apply();
            for (var i = 0; i < _caps.Length; i++)
            {
                _age[i] += Time.deltaTime;
                if (_age[i] >= _life[i])
                    Spawn(i);
                Place(i, Time.deltaTime);
            }
        }

        private void Apply()
        {
            _material.SetFloat("_Distance", flowDistance);
            _material.SetFloat("_Speed", flowSpeed);
            _material.SetFloat("_Glint", foamGlint);
            if (_caps.Length != whitecaps)
                BuildCaps();
        }

        // ---------- Whitecaps ----------

        private void BuildCaps()
        {
            foreach (var cap in _caps)
                if (cap != null)
                    Destroy(cap.gameObject);
            _caps = new SpriteRenderer[whitecaps];
            _age = new float[whitecaps];
            _life = new float[whitecaps];
            _size = new float[whitecaps];
            for (var i = 0; i < whitecaps; i++)
            {
                var sr = new GameObject("Whitecap").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = Foam;
                sr.sortingOrder = _order;
                // Long side across the wind, like the painted crests.
                sr.transform.rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(-Wind.y, Wind.x, 0f));
                _caps[i] = sr;
                Spawn(i);
                _age[i] = Random.value * _life[i];
                Place(i, 0f);
            }
        }

        private void Spawn(int i)
        {
            // Open water only: not on the rocks and ships, nor in their surf.
            Vector2 at;
            var tries = 0;
            do
            {
                at = new Vector2(Random.Range(_area.min.x, _area.max.x), Random.Range(_area.min.y, _area.max.y));
            } while (NearSolid(at) && ++tries < 10);

            _caps[i].transform.position = at;
            _age[i] = 0f;
            _life[i] = Random.Range(2.5f, 4.5f);
            _size[i] = Random.Range(0.35f, 0.7f);
        }

        private bool NearSolid(Vector2 at)
        {
            foreach (var s in _solid)
                if (Vector2.Distance(at, s) < _solidRadius * 2f)
                    return true;
            return false;
        }

        private void Place(int i, float dt)
        {
            var t = _caps[i].transform;
            t.position += (Vector3)(Wind * 0.06f * dt);
            // Blooms quickly, stretches along its crest, then thins out and fades.
            var life = _age[i] / _life[i];
            var grow = Mathf.SmoothStep(0.4f, 1f, Mathf.Clamp01(life * 3f));
            t.localScale = new Vector3(_size[i] * (0.8f + 0.6f * life), _size[i] * 0.6f * grow, 1f);
            var alpha = Mathf.Clamp01(life * 5f) * Mathf.Pow(1f - life, 1.5f);
            _caps[i].color = new Color(0.92f, 0.96f, 0.97f, whitecapStrength * alpha);
        }

        private static Sprite _foam;

        /// <summary>A soft, ragged streak of foam, white on transparent, wider than tall.</summary>
        private static Sprite Foam
        {
            get
            {
                if (_foam != null)
                    return _foam;
                const int w = 64, h = 32;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var rng = new System.Random(5);
                var grain = new float[w, h];
                for (var x = 0; x < w; x++)
                for (var y = 0; y < h; y++)
                    grain[x, y] = (float)rng.NextDouble();
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var dx = (x + 0.5f) / w * 2f - 1f;
                    var dy = (y + 0.5f) / h * 2f - 1f;
                    var shape = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                    // Broken up like spray (grain blurred over neighbours, so no single-pixel speckle), soft edges.
                    var g = (grain[x, y] + grain[(x + 1) % w, y] + grain[x, (y + 1) % h] + grain[(x + w - 1) % w, y] + grain[x, (y + h - 1) % h]) / 5f;
                    var a = shape * shape * shape * Mathf.Lerp(0.3f, 1f, g);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

                tex.Apply();
                return _foam = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            }
        }
    }
}
