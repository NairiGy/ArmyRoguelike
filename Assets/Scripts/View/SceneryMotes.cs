using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Dust motes drifting slowly through the air in front of the scene: each rises and sways on its own,
    /// fades in and out over its life, then starts again somewhere else.
    /// </summary>
    public sealed class SceneryMotes : MonoBehaviour
    {
        private const int Count = 36;
        private const int Order = 20000;

        private SpriteRenderer[] _motes;
        private Vector3[] _velocity;
        private float[] _age;
        private float[] _life;
        private float[] _phase;
        private Vector2 _min;
        private Vector2 _max;
        private Color _color;

        public static SceneryMotes Create(Transform parent, Vector2 centre, float viewW, float viewH, Color color)
        {
            var go = new GameObject("Motes");
            go.transform.SetParent(parent, false);
            var motes = go.AddComponent<SceneryMotes>();
            motes.Build(centre, viewW, viewH, color);
            return motes;
        }

        private void Build(Vector2 centre, float viewW, float viewH, Color color)
        {
            _min = centre - new Vector2(viewW, viewH) * 0.55f;
            _max = centre + new Vector2(viewW, viewH) * 0.55f;
            _color = color;
            _motes = new SpriteRenderer[Count];
            _velocity = new Vector3[Count];
            _age = new float[Count];
            _life = new float[Count];
            _phase = new float[Count];
            for (var i = 0; i < Count; i++)
            {
                var sr = new GameObject("Mote").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = Dot;
                sr.sortingOrder = Order;
                _motes[i] = sr;
                Respawn(i);
                // Start part-way through, so the air is already full on the first frame.
                _age[i] = Random.value * _life[i];
                Place(i, 0f);
            }
        }

        private void Respawn(int i)
        {
            _life[i] = Random.Range(6f, 14f);
            _age[i] = 0f;
            _phase[i] = Random.value * 10f;
            _velocity[i] = new Vector3(Random.Range(-0.08f, 0.08f), Random.Range(0.03f, 0.12f), 0f);
            _motes[i].transform.position = new Vector3(Random.Range(_min.x, _max.x), Random.Range(_min.y, _max.y), 0f);
            _motes[i].transform.localScale = Vector3.one * Random.Range(0.02f, 0.055f);
        }

        private void Place(int i, float dt)
        {
            var t = _motes[i].transform;
            var sway = new Vector3(Mathf.Sin(Time.time * 0.7f + _phase[i]) * 0.04f, 0f, 0f);
            t.position += (_velocity[i] + sway) * dt;
            // Fade in, hold, fade out; a slow twinkle on top.
            var life = _age[i] / _life[i];
            var fade = Mathf.Clamp01(Mathf.Min(life, 1f - life) * 4f);
            var twinkle = 0.6f + 0.4f * Mathf.Sin(Time.time * 2.3f + _phase[i] * 3f);
            _motes[i].color = new Color(_color.r, _color.g, _color.b, 0.55f * fade * twinkle);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            for (var i = 0; i < Count; i++)
            {
                _age[i] += dt;
                if (_age[i] >= _life[i])
                    Respawn(i);
                Place(i, dt);
            }
        }

        private static Sprite _dot;

        /// <summary>A soft round white dot.</summary>
        internal static Sprite Dot
        {
            get
            {
                if (_dot != null)
                    return _dot;
                const int size = 32;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size, size) * 0.5f) / (size * 0.5f);
                    var a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }

                tex.Apply();
                return _dot = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
        }
    }
}
