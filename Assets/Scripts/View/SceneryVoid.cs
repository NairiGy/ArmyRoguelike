using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// What moves in the void (the Edge of the World setting): the black hole's ring of light turns slowly,
    /// broken rocks spiral in and vanish into the hole, now and then a stone breaks off the tower's walls and
    /// falls away, and pale specks peel off the roof's edges into the void.
    /// </summary>
    public sealed class SceneryVoid : MonoBehaviour
    {
        // Degrees per second, clockwise like the debris.
        private const float RingSpin = 4f;
        private const float RingAlpha = 0.85f;
        private const int DebrisCount = 9;
        private const int SpeckCount = 16;
        private const int SpeckOrder = 20000;
        // The debris disc looks flattened, as if seen at an angle.
        private const float DiscSquash = 0.75f;

        private Transform _ring;
        private Vector2 _hole;
        private float _holeRadius;
        private Vector2 _min;
        private Vector2 _max;
        private Color _light;

        private Sprite[] _rocks;
        private Material _rockMaterial;
        private SpriteRenderer[] _debris;
        private float[] _radius;
        private float[] _angle;
        private float[] _inward;
        private float[] _around;
        private float[] _spin;
        private float[] _size;
        private float[] _value;
        private float[] _debrisAge;

        private Vector2[] _corners;
        private Vector2 _boardCentre;
        private SpriteRenderer[] _specks;
        private Vector3[] _speckVelocity;
        private float[] _speckAge;
        private float[] _speckLife;
        private float[] _speckPhase;

        private int _fallOrder;
        private SpriteRenderer _falling;
        private Vector3 _fallVelocity;
        private float _fallSpin;
        private float _fallAge;
        private float _nextFall;

        /// <param name="ringHole">The ring image's empty middle as a fraction of its width.</param>
        /// <param name="corners">The board's top, left, bottom and right corners, in order around it.</param>
        public static SceneryVoid Create(Transform parent, Texture2D ring, float ringHole, Vector2 hole, float holeRadius,
            Sprite[] rocks, Material rockMaterial, Material glow, Color light, Vector2[] corners,
            Vector2 centre, float viewW, float viewH, int ringOrder, int debrisOrder, int fallOrder)
        {
            var go = new GameObject("Void");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<SceneryVoid>();
            v._hole = hole;
            v._holeRadius = holeRadius;
            v._min = centre - new Vector2(viewW, viewH) * 0.55f;
            v._max = centre + new Vector2(viewW, viewH) * 0.55f;
            v._light = light;
            v._corners = corners;
            v._boardCentre = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
            v._fallOrder = fallOrder;
            v._nextFall = Random.Range(2f, 5f);
            if (ring != null)
                v.BuildRing(ring, ringHole, glow, ringOrder);
            v._rocks = rocks;
            v._rockMaterial = rockMaterial;
            if (rocks.Length > 0)
                v.BuildDebris(debrisOrder);
            v.BuildSpecks();
            return v;
        }

        private void BuildRing(Texture2D ring, float ringHole, Material glow, int order)
        {
            var sr = new GameObject("Ring").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.position = _hole;
            // Sized so the ring's empty middle sits on the painted hole.
            var width = _holeRadius / ringHole;
            sr.sprite = Sprite.Create(ring, new Rect(0, 0, ring.width, ring.height), new Vector2(0.5f, 0.5f), ring.width / width);
            sr.sharedMaterial = BattleScenery.PerTexture(glow, ring);
            sr.color = new Color(_light.r, _light.g, _light.b, RingAlpha);
            sr.sortingOrder = order;
            _ring = sr.transform;
        }

        // ---------- Debris ----------

        private void BuildDebris(int order)
        {
            _debris = new SpriteRenderer[DebrisCount];
            _radius = new float[DebrisCount];
            _angle = new float[DebrisCount];
            _inward = new float[DebrisCount];
            _around = new float[DebrisCount];
            _spin = new float[DebrisCount];
            _size = new float[DebrisCount];
            _value = new float[DebrisCount];
            _debrisAge = new float[DebrisCount];
            for (var i = 0; i < DebrisCount; i++)
            {
                var sr = new GameObject("Debris").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sortingOrder = order;
                _debris[i] = sr;
                SpawnDebris(i);
                // Already part-way in on the first frame.
                _debrisAge[i] = 10f;
                PlaceDebris(i, 0f);
            }
        }

        private void SpawnDebris(int i)
        {
            // Anywhere on screen that isn't already close to the hole.
            Vector2 offset;
            var tries = 0;
            do
            {
                offset = new Vector2(Random.Range(_min.x, _max.x), Random.Range(_min.y, _max.y)) - _hole;
                offset.y /= DiscSquash;
            } while (offset.magnitude < _holeRadius * 4f && ++tries < 20);

            _radius[i] = offset.magnitude;
            _angle[i] = Mathf.Atan2(offset.y, offset.x);
            _inward[i] = Random.Range(0.03f, 0.08f);
            _around[i] = Random.Range(0.08f, 0.2f);
            _spin[i] = Random.Range(-25f, 25f);
            _size[i] = Random.Range(0.1f, 0.3f);
            _value[i] = Random.Range(0.7f, 1f);
            _debrisAge[i] = 0f;
            var sr = _debris[i];
            sr.sprite = _rocks[Random.Range(0, _rocks.Length)];
            sr.sharedMaterial = BattleScenery.PerTexture(_rockMaterial, sr.sprite.texture);
            sr.flipX = Random.value < 0.5f;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }

        private void PlaceDebris(int i, float dt)
        {
            _debrisAge[i] += dt;
            // Pulled in, and swept round clockwise faster the closer it gets.
            _radius[i] -= _inward[i] * dt * Mathf.Clamp(_holeRadius * 6f / _radius[i], 1f, 4f);
            _angle[i] -= _around[i] / Mathf.Max(_radius[i], _holeRadius) * dt;
            if (_radius[i] <= _holeRadius * 1.05f)
            {
                SpawnDebris(i);
                return;
            }

            var t = _debris[i].transform;
            t.position = _hole + new Vector2(Mathf.Cos(_angle[i]), Mathf.Sin(_angle[i]) * DiscSquash) * _radius[i];
            t.Rotate(0f, 0f, _spin[i] * dt);
            // Shrinks and fades as it falls into the hole; fades in when it appears.
            var near = Mathf.InverseLerp(_holeRadius * 1.05f, _holeRadius * 3f, _radius[i]);
            var scale = _size[i] / _debris[i].sprite.bounds.size.y * Mathf.Lerp(0.25f, 1f, near);
            t.localScale = Vector3.one * scale;
            var alpha = Mathf.Clamp01(_debrisAge[i] / 2f) * Mathf.SmoothStep(0f, 1f, near);
            _debris[i].color = new Color(_value[i], 0f, 0f, alpha);
        }

        // ---------- Stones falling off the walls ----------

        private void UpdateFall(float dt)
        {
            if (_falling == null)
            {
                _nextFall -= dt;
                if (_nextFall <= 0f && _rocks != null && _rocks.Length > 0)
                    StartFall();
                return;
            }

            _fallAge += dt;
            _fallVelocity += Vector3.down * 0.6f * dt;
            var t = _falling.transform;
            t.position += _fallVelocity * dt;
            t.Rotate(0f, 0f, _fallSpin * dt);
            _falling.color = new Color(0.5f, 0f, 0f, Mathf.Clamp01(1.6f - _fallAge * 0.6f));
            if (_fallAge > 2.7f)
            {
                Destroy(_falling.gameObject);
                _falling = null;
                _nextFall = Random.Range(4f, 9f);
            }
        }

        private void StartFall()
        {
            // Somewhere on the two front walls, which hang below the board's left-bottom-right edges.
            var front = Random.value < 0.5f ? (_corners[1], _corners[2]) : (_corners[2], _corners[3]);
            var start = Vector2.Lerp(front.Item1, front.Item2, Random.Range(0.1f, 0.9f)) + Vector2.down * Random.Range(0.15f, 0.8f);
            var sr = new GameObject("Falling stone").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.position = start;
            sr.sprite = _rocks[Random.Range(0, _rocks.Length)];
            sr.sharedMaterial = BattleScenery.PerTexture(_rockMaterial, sr.sprite.texture);
            sr.transform.localScale = Vector3.one * (Random.Range(0.05f, 0.1f) / sr.sprite.bounds.size.y);
            sr.sortingOrder = _fallOrder;
            _falling = sr;
            _fallVelocity = new Vector3(Mathf.Sign(start.x - _boardCentre.x) * Random.Range(0.02f, 0.08f), 0f, 0f);
            _fallSpin = Random.Range(-90f, 90f);
            _fallAge = 0f;
        }

        // ---------- Specks off the roof's edges ----------

        private void BuildSpecks()
        {
            _specks = new SpriteRenderer[SpeckCount];
            _speckVelocity = new Vector3[SpeckCount];
            _speckAge = new float[SpeckCount];
            _speckLife = new float[SpeckCount];
            _speckPhase = new float[SpeckCount];
            for (var i = 0; i < SpeckCount; i++)
            {
                var sr = new GameObject("Speck").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = SceneryMotes.Dot;
                sr.sortingOrder = SpeckOrder;
                _specks[i] = sr;
                SpawnSpeck(i);
                _speckAge[i] = Random.value * _speckLife[i];
                PlaceSpeck(i, 0f);
            }
        }

        private void SpawnSpeck(int i)
        {
            var edge = Random.Range(0, 4);
            var at = Vector2.Lerp(_corners[edge], _corners[(edge + 1) % 4], Random.value);
            var outward = (at - _boardCentre).normalized;
            _specks[i].transform.position = at + outward * 0.03f;
            _specks[i].transform.localScale = Vector3.one * Random.Range(0.02f, 0.045f);
            _speckVelocity[i] = (Vector3)(outward * Random.Range(0.04f, 0.12f)) + Vector3.down * Random.Range(0.03f, 0.1f);
            _speckLife[i] = Random.Range(3f, 6f);
            _speckAge[i] = 0f;
            _speckPhase[i] = Random.value * 10f;
        }

        private void PlaceSpeck(int i, float dt)
        {
            _specks[i].transform.position += _speckVelocity[i] * dt;
            var life = _speckAge[i] / _speckLife[i];
            var fade = Mathf.Clamp01(Mathf.Min(life * 6f, (1f - life) * 2f));
            var twinkle = 0.6f + 0.4f * Mathf.Sin(Time.time * 2.3f + _speckPhase[i] * 3f);
            _specks[i].color = new Color(_light.r, _light.g, _light.b, 0.55f * fade * twinkle);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (_ring != null)
                _ring.Rotate(0f, 0f, -RingSpin * dt);
            if (_debris != null)
                for (var i = 0; i < DebrisCount; i++)
                    PlaceDebris(i, dt);
            UpdateFall(dt);
            for (var i = 0; i < SpeckCount; i++)
            {
                _speckAge[i] += dt;
                if (_speckAge[i] >= _speckLife[i])
                    SpawnSpeck(i);
                PlaceSpeck(i, dt);
            }
        }
    }
}
