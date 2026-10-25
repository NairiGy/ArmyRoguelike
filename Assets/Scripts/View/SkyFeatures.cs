using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Optional night-sky elements behind the tower: a clear or cloudy sky panorama, the moon, stars,
    /// shooting stars, sky lanterns, birds and an aurora. Each one is a live switch: select "Scenery" in
    /// the Hierarchy during Play and mix them in the Inspector. Defaults come from BattleScenery.
    /// </summary>
    public sealed class SkyFeatures : MonoBehaviour
    {
        /// <summary>Which elements a new battle starts with (the Inspector switches start from these).</summary>
        public struct Mix
        {
            public bool ClearSky, Moon, Stars, ShootingStars, Lanterns, Birds, Aurora;
        }

        public static Mix Default;

        [Tooltip("Open night sky with the cloud sea only low down (otherwise the cloudy panorama).")]
        public bool clearSky;
        public bool moon;
        [Tooltip("Where the moon sits on screen, 0..1 from the bottom left.")]
        public Vector2 moonPosition = new Vector2(0.17f, 0.8f);
        public float moonSize = 1.2f;
        public bool stars;
        public bool shootingStars;
        [Tooltip("Seconds between shooting stars (roughly).")]
        public float shootingStarEvery = 12f;
        public bool lanterns;
        public bool birds;
        [Tooltip("Seconds between flocks of birds (roughly).")]
        public float birdsEvery = 18f;
        public bool aurora;

        private const int StarCount = 110;
        private const int LanternCount = 9;
        private const int FlockSize = 6;

        private Material _skyMaterial;
        private Texture _cloudySky;
        private Texture _clearSky;
        private Vector2 _min;
        private Vector2 _max;
        private float _horizon;

        private GameObject _moonRoot;
        private SpriteRenderer _moon;
        private SpriteRenderer _moonHalo;
        private GameObject _starRoot;
        private SpriteRenderer[] _stars;
        private float[] _starPhase;
        private SpriteRenderer _shootingStar;
        private float _shootingStart;
        private float _shootingGap;
        private Vector3 _shootingFrom;
        private Vector3 _shootingTo;
        private GameObject _lanternRoot;
        private SpriteRenderer[] _lanterns;
        private SpriteRenderer[] _lanternHalos;
        private float[] _lanternAge;
        private float[] _lanternLife;
        private float[] _lanternX;
        private GameObject _flockRoot;
        private SpriteRenderer[] _birds;
        private Vector3[] _birdOffset;
        private float _flockStart;
        private float _flockY;
        private GameObject _auroraRoot;

        /// <param name="skyMaterial">The sky panorama's material, whose texture the clear-sky switch swaps.</param>
        /// <param name="horizonY">Where the cloud sea meets the sky (stars and moon stay above it).</param>
        public static SkyFeatures Create(GameObject scenery, Material skyMaterial, Texture cloudySky, Texture clearSkyTexture,
            Vector2 centre, float viewW, float viewH, float horizonY, Color light, Color dark, Material scenerySprites, Material glow)
        {
            var sky = scenery.AddComponent<SkyFeatures>();
            sky.Build(skyMaterial, cloudySky, clearSkyTexture, centre, viewW, viewH, horizonY, light, dark, scenerySprites, glow);
            return sky;
        }

        private SpriteRenderer Dot(Transform parent, string name, Material material, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = SceneryMotes.Dot;
            if (material != null)
                sr.sharedMaterial = material;
            sr.sortingOrder = order;
            return sr;
        }

        private Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private void Build(Material skyMaterial, Texture cloudySky, Texture clearSkyTexture, Vector2 centre, float viewW, float viewH,
            float horizonY, Color light, Color dark, Material scenerySprites, Material glow)
        {
            clearSky = Default.ClearSky;
            moon = Default.Moon;
            stars = Default.Stars;
            shootingStars = Default.ShootingStars;
            lanterns = Default.Lanterns;
            birds = Default.Birds;
            aurora = Default.Aurora;
            _skyMaterial = skyMaterial;
            _cloudySky = cloudySky;
            _clearSky = clearSkyTexture;
            _min = centre - new Vector2(viewW, viewH) * 0.5f;
            _max = centre + new Vector2(viewW, viewH) * 0.5f;
            _horizon = horizonY;
            var rng = new System.Random(5);
            var pale = Color.Lerp(light, Color.white, 0.55f);

            // Moon: the painted disc in the scenery palette, lifted towards light, with a faint halo.
            var moonRoot = Group("Moon");
            _moonRoot = moonRoot.gameObject;
            _moonHalo = Dot(moonRoot, "Halo", glow, -31750);
            _moonHalo.color = new Color(pale.r, pale.g, pale.b, 0.16f);
            _moon = new GameObject("Disc").AddComponent<SpriteRenderer>();
            _moon.transform.SetParent(moonRoot, false);
            _moon.sprite = Resources.Load<Sprite>("Backdrops/moon");
            _moon.sharedMaterial = scenerySprites;
            _moon.color = new Color(1f, 0.55f, 1f, 1f);
            _moon.sortingOrder = -31700;

            // Stars: tiny points, denser and brighter higher up, each twinkling at its own pace.
            var starRoot = Group("Stars");
            _starRoot = starRoot.gameObject;
            _stars = new SpriteRenderer[StarCount];
            _starPhase = new float[StarCount];
            for (var i = 0; i < StarCount; i++)
            {
                var h = Mathf.Pow((float)rng.NextDouble(), 0.7f);
                var pos = new Vector3(Mathf.Lerp(_min.x, _max.x, (float)rng.NextDouble()), Mathf.Lerp(_horizon + 0.3f, _max.y, h), 0f);
                _stars[i] = Dot(starRoot, "Star", null, -31900);
                _stars[i].transform.position = pos;
                _stars[i].transform.localScale = Vector3.one * (0.018f + 0.035f * (float)rng.NextDouble() * (float)rng.NextDouble());
                _stars[i].color = pale;
                _starPhase[i] = (float)rng.NextDouble() * 20f;
            }

            // The first shooting star and flock are already on their way, so a new battle opens with them.
            _shootingStar = Dot(transform, "Shooting star", glow, -31850);
            NewShootingStar();
            _shootingStart = -0.4f;

            // Sky lanterns: warm points rising slowly from the cloud sea far away.
            var lanternRoot = Group("Lanterns");
            _lanternRoot = lanternRoot.gameObject;
            _lanterns = new SpriteRenderer[LanternCount];
            _lanternHalos = new SpriteRenderer[LanternCount];
            _lanternAge = new float[LanternCount];
            _lanternLife = new float[LanternCount];
            _lanternX = new float[LanternCount];
            for (var i = 0; i < LanternCount; i++)
            {
                _lanternHalos[i] = Dot(lanternRoot, "Lantern glow", glow, -31651);
                _lanterns[i] = Dot(lanternRoot, "Lantern", glow, -31650);
                _lanternLife[i] = 30f + 25f * (float)rng.NextDouble();
                _lanternAge[i] = (float)rng.NextDouble() * _lanternLife[i];
                _lanternX[i] = Mathf.Lerp(_min.x, _max.x, (i + (float)rng.NextDouble()) / LanternCount);
            }

            // A small flock of birds, dark silhouettes flapping across with the wind.
            var flockRoot = Group("Birds");
            _flockRoot = flockRoot.gameObject;
            _birds = new SpriteRenderer[FlockSize];
            _birdOffset = new Vector3[FlockSize];
            for (var i = 0; i < FlockSize; i++)
            {
                _birds[i] = new GameObject("Bird").AddComponent<SpriteRenderer>();
                _birds[i].transform.SetParent(flockRoot, false);
                _birds[i].sprite = BirdSprite;
                _birds[i].color = new Color(dark.r, dark.g, dark.b, 0.85f);
                _birds[i].sortingOrder = -31600;
                // A loose V: the leader in front, the rest trailing behind and to either side.
                _birdOffset[i] = new Vector3(-0.22f * ((i + 1) / 2), (i % 2 == 0 ? 1 : -1) * 0.1f * ((i + 1) / 2), 0f) +
                                 new Vector3((float)rng.NextDouble() * 0.06f, (float)rng.NextDouble() * 0.05f, 0f);
            }

            _flockStart = -6f;
            _flockY = Mathf.Lerp(_horizon + 1f, _max.y - 0.6f, 0.5f);

            // Aurora: a wide band of curtains high in the sky.
            var auroraShader = Resources.Load<Shader>("Shaders/Aurora");
            var auroraRoot = Group("Aurora");
            _auroraRoot = auroraRoot.gameObject;
            if (auroraShader != null)
            {
                var mesh = new Mesh { name = "Aurora" };
                var w = viewW * 1.1f;
                var bl = new Vector3(centre.x - w * 0.5f, _horizon + 0.4f, 0f);
                var h = _max.y - bl.y + 0.3f;
                mesh.vertices = new[] { bl, bl + new Vector3(w, 0f), bl + new Vector3(w, h), bl + new Vector3(0f, h) };
                mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                var go = new GameObject("Curtains");
                go.transform.SetParent(auroraRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(auroraShader);
                mr.sortingOrder = -31800;
            }

            Animate(0f);
        }

        private void Update() => Animate(Time.deltaTime);

        private void Animate(float dt)
        {
            var t = Time.time;
            if (_skyMaterial != null)
                _skyMaterial.mainTexture = clearSky && _clearSky != null ? _clearSky : _cloudySky;

            _moonRoot.SetActive(moon && _moon.sprite != null);
            if (_moonRoot.activeSelf)
            {
                var pos = new Vector3(Mathf.Lerp(_min.x, _max.x, moonPosition.x), Mathf.Lerp(_min.y, _max.y, moonPosition.y), 0f);
                _moon.transform.position = pos;
                _moon.transform.localScale = Vector3.one * moonSize;
                _moonHalo.transform.position = pos;
                _moonHalo.transform.localScale = Vector3.one * moonSize * 2.8f;
            }

            _starRoot.SetActive(stars);
            if (stars)
                for (var i = 0; i < _stars.Length; i++)
                {
                    var c = _stars[i].color;
                    c.a = 0.45f + 0.45f * Mathf.Sin(t * (0.8f + (_starPhase[i] % 1.7f)) + _starPhase[i]);
                    _stars[i].color = c;
                }

            AnimateShootingStar(t);
            _lanternRoot.SetActive(lanterns);
            if (lanterns)
                AnimateLanterns(t, dt);
            AnimateBirds(t);
            _auroraRoot.SetActive(aurora);
        }

        /// <summary>A new streak: high in the sky, falling down to the right at a shallow angle.</summary>
        private void NewShootingStar()
        {
            _shootingFrom = new Vector3(Random.Range(_min.x, _max.x - 3f), Random.Range(_max.y - 1.5f, _max.y - 0.3f), 0f);
            _shootingTo = _shootingFrom + new Vector3(Random.Range(2.5f, 3.5f), -Random.Range(0.8f, 1.4f), 0f);
            _shootingGap = Mathf.Max(2f, shootingStarEvery * Random.Range(0.7f, 1.3f));
        }

        private void AnimateShootingStar(float t)
        {
            const float duration = 0.9f;
            var age = t - _shootingStart;
            if (age > _shootingGap)
            {
                _shootingStart = t;
                age = 0f;
                NewShootingStar();
            }

            var flying = shootingStars && age >= 0f && age < duration;
            _shootingStar.enabled = flying;
            if (!flying)
                return;
            var p = age / duration;
            var dir = _shootingTo - _shootingFrom;
            _shootingStar.transform.position = Vector3.Lerp(_shootingFrom, _shootingTo, p);
            _shootingStar.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            _shootingStar.transform.localScale = new Vector3(0.7f * Mathf.Sin(p * Mathf.PI), 0.035f, 1f);
            _shootingStar.color = new Color(0.85f, 0.92f, 1f, Mathf.Sin(p * Mathf.PI));
        }

        private void AnimateLanterns(float t, float dt)
        {
            for (var i = 0; i < _lanterns.Length; i++)
            {
                _lanternAge[i] += dt;
                if (_lanternAge[i] > _lanternLife[i])
                    _lanternAge[i] = 0f;
                var p = _lanternAge[i] / _lanternLife[i];
                var pos = new Vector3(_lanternX[i] + Mathf.Sin(t * 0.3f + i) * 0.15f + p * 0.8f, Mathf.Lerp(_horizon - 0.4f, _max.y + 0.3f, p), 0f);
                // Farther (higher) lanterns look smaller; they fade in out of the clouds and out near the top.
                var size = Mathf.Lerp(0.07f, 0.035f, p);
                var fade = Mathf.Clamp01(p * 8f) * Mathf.Clamp01((1f - p) * 4f);
                var flicker = 0.85f + 0.15f * Mathf.Sin(t * 7f + i * 3f);
                _lanterns[i].transform.position = pos;
                _lanterns[i].transform.localScale = Vector3.one * size;
                _lanterns[i].color = new Color(1f, 0.75f, 0.35f, 0.9f * fade * flicker);
                _lanternHalos[i].transform.position = pos;
                _lanternHalos[i].transform.localScale = Vector3.one * size * 4f;
                _lanternHalos[i].color = new Color(1f, 0.5f, 0.15f, 0.25f * fade * flicker);
            }
        }

        private void AnimateBirds(float t)
        {
            const float crossing = 14f;
            var age = t - _flockStart;
            if (age > Mathf.Max(crossing, birdsEvery))
            {
                _flockStart = t;
                _flockY = Random.Range(_horizon + 0.8f, _max.y - 0.5f);
                age = 0f;
            }

            var flying = birds && age < crossing;
            _flockRoot.SetActive(flying);
            if (!flying)
                return;
            var p = age / crossing;
            var lead = new Vector3(Mathf.Lerp(_min.x - 1f, _max.x + 1.5f, p), _flockY + Mathf.Sin(t * 0.5f) * 0.1f, 0f);
            for (var i = 0; i < _birds.Length; i++)
            {
                _birds[i].transform.position = lead + _birdOffset[i];
                var flap = Mathf.Sin(t * 9f + i * 1.3f);
                _birds[i].transform.localScale = new Vector3(0.16f, 0.16f * (0.35f + 0.65f * Mathf.Abs(flap)), 1f);
            }
        }

        private static Sprite _bird;

        /// <summary>A tiny bird silhouette: two wings in a shallow V.</summary>
        private static Sprite BirdSprite
        {
            get
            {
                if (_bird != null)
                    return _bird;
                const int w = 32, h = 16;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                tex.SetPixels32(new Color32[w * h]);
                for (var x = 0; x < w; x++)
                {
                    // Each wing curves up from the body in the middle.
                    var d = Mathf.Abs(x - w * 0.5f) / (w * 0.5f);
                    var y = 3f + 9f * Mathf.Sqrt(d);
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        var py = Mathf.RoundToInt(y) + dy;
                        if (py >= 0 && py < h)
                            tex.SetPixel(x, py, new Color(1f, 1f, 1f, dy == 0 ? 1f : 0.5f));
                    }
                }

                tex.Apply();
                return _bird = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.3f), w);
            }
        }
    }
}
