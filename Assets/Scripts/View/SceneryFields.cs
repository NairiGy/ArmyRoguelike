using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// What moves over the fields far below the tower by day: soft cloud shadows drift across the land with
    /// the wind, and faint gusts run through the wheat as pale streaks.
    /// </summary>
    public sealed class SceneryFields : MonoBehaviour
    {
        private const int ShadowCount = 5;
        private const int GustCount = 7;
        // Down and to the right, the way the shadows fall.
        private static readonly Vector3 Wind = new Vector3(1f, -0.35f, 0f).normalized;

        private Vector2 _min;
        private Vector2 _max;
        private Color _dark;
        private Color _light;

        private SpriteRenderer[] _shadows;
        private float[] _shadowSpeed;
        private SpriteRenderer[] _gusts;
        private float[] _gustSpeed;
        private float[] _gustAge;
        private float[] _gustLife;

        public static SceneryFields Create(Transform parent, Vector2 centre, float viewW, float viewH, Color dark, Color light, int order)
        {
            var go = new GameObject("Fields");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<SceneryFields>();
            f._min = centre - new Vector2(viewW, viewH) * 0.6f;
            f._max = centre + new Vector2(viewW, viewH) * 0.6f;
            f._dark = dark;
            f._light = light;
            f.Build(order);
            return f;
        }

        private void Build(int order)
        {
            _shadows = new SpriteRenderer[ShadowCount];
            _shadowSpeed = new float[ShadowCount];
            for (var i = 0; i < ShadowCount; i++)
            {
                _shadows[i] = NewDot("Cloud shadow", order);
                SpawnShadow(i, true);
            }

            _gusts = new SpriteRenderer[GustCount];
            _gustSpeed = new float[GustCount];
            _gustAge = new float[GustCount];
            _gustLife = new float[GustCount];
            for (var i = 0; i < GustCount; i++)
            {
                _gusts[i] = NewDot("Gust", order + 1);
                SpawnGust(i);
                _gustAge[i] = Random.value * _gustLife[i];
                PlaceGust(i, 0f);
            }
        }

        private SpriteRenderer NewDot(string name, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = SceneryMotes.Dot;
            sr.sortingOrder = order;
            return sr;
        }

        /// <param name="anywhere">On the first frame shadows are already spread over the land; later ones drift in from upwind.</param>
        private void SpawnShadow(int i, bool anywhere)
        {
            var t = _shadows[i].transform;
            var y = Random.Range(_min.y, _max.y);
            var x = anywhere ? Random.Range(_min.x, _max.x) : _min.x - 2f;
            t.position = new Vector3(x, y, 0f);
            // Wide, flattened blobs: clouds seen from far above.
            var size = Random.Range(2.5f, 5f);
            t.localScale = new Vector3(size, size * Random.Range(0.45f, 0.65f), 1f);
            _shadowSpeed[i] = Random.Range(0.08f, 0.15f);
            _shadows[i].color = new Color(_dark.r, _dark.g, _dark.b, Random.Range(0.18f, 0.28f));
        }

        private void SpawnGust(int i)
        {
            var t = _gusts[i].transform;
            t.position = new Vector3(Random.Range(_min.x - 1f, _max.x), Random.Range(_min.y, _max.y), 0f);
            // A long thin streak along the wind.
            t.localScale = new Vector3(Random.Range(2.5f, 4f), Random.Range(0.35f, 0.6f), 1f);
            t.rotation = Quaternion.FromToRotation(Vector3.right, Wind);
            _gustSpeed[i] = Random.Range(0.6f, 1.1f);
            _gustAge[i] = 0f;
            _gustLife[i] = Random.Range(2.5f, 4.5f);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            for (var i = 0; i < ShadowCount; i++)
            {
                var t = _shadows[i].transform;
                t.position += Wind * (_shadowSpeed[i] * dt);
                if (t.position.x - t.localScale.x * 0.5f > _max.x || t.position.y + t.localScale.y < _min.y)
                    SpawnShadow(i, false);
            }

            for (var i = 0; i < GustCount; i++)
            {
                _gustAge[i] += dt;
                if (_gustAge[i] >= _gustLife[i])
                    SpawnGust(i);
                PlaceGust(i, dt);
            }
        }

        private void PlaceGust(int i, float dt)
        {
            _gusts[i].transform.position += Wind * (_gustSpeed[i] * dt);
            // Swells and fades as it runs through the wheat.
            var life = _gustAge[i] / _gustLife[i];
            _gusts[i].color = new Color(_light.r, _light.g, _light.b, 0.06f * Mathf.Sin(life * Mathf.PI));
        }
    }
}
