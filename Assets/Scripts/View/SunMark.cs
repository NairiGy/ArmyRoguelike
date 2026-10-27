using System.Collections;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// A tile the sun omen has marked: a gold sigil turning on the floor under a faint shaft of light,
    /// with dust drifting in it. At the start of the enemy turn it charges up and a beam strikes, lighting
    /// up everything around it, and leaves a glowing scorch mark that cools down and fades.
    /// </summary>
    public sealed class SunMark : MonoBehaviour
    {
        private const float SigilWidth = 0.9f;
        private const float BeamHeight = 14f;
        // The painted shaft takes up about the middle 40% of its image.
        private const float PaintedWidth = 2.4f;
        private static readonly Color Gold = new Color(1f, 0.78f, 0.35f);
        private static readonly Color LightColor = new Color(1f, 0.7f, 0.35f);

        private static Material _glow;
        private static Material _sigilMaterial;
        private static Sprite _sigilSprite;
        private static Sprite _beamSprite;

        private Transform _sigil;
        private SpriteRenderer _sigilRenderer;
        private SpriteRenderer _pool;
        private SpriteRenderer _beam;
        private Material _beamMaterial;
        private FxLight _light;
        private int _floorOrder;

        // What the idle animation shows; the charge and the strike push these up.
        private float _appear;
        private float _spin = 12f;
        private float _sigilGlow = 1f;
        private float _shaft = 0.12f;
        private float _shaftWidth = 0.22f;
        private float _lightStrength = 0.12f;
        private float _moteTimer;
        private bool _striking;

        public GridPos Tile { get; private set; }

        private static Material Glow => _glow != null ? _glow : _glow = new Material(Resources.Load<Shader>("Shaders/Glow"));

        // The sigil needs a material of its own: sharing one with the light pool drawn just before it, the
        // two were batched and the sigil came out drawn with the pool's soft-dot texture.
        private static Material SigilMaterial => _sigilMaterial != null ? _sigilMaterial : _sigilMaterial = new Material(Resources.Load<Shader>("Shaders/Glow"));

        private static Sprite SigilSprite
        {
            get
            {
                if (_sigilSprite != null)
                    return _sigilSprite;
                var tex = Resources.Load<Texture2D>("Effects/SunSigil");
                return _sigilSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
        }

        /// <summary>The painted shaft of light, one unit wide; the shader repeats it up the beam.</summary>
        internal static Sprite BeamSprite
        {
            get
            {
                if (_beamSprite != null)
                    return _beamSprite;
                var tex = Resources.Load<Texture2D>("Effects/SunBeam");
                return _beamSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
        }

        /// <summary>Sizes the beam to a visible width; the painted shaft fills only the middle of its image.</summary>
        private void SizeBeam(float width)
        {
            _beam.transform.localScale = new Vector3(width * PaintedWidth, BeamHeight / BeamSprite.bounds.size.y, 1f);
        }

        private void SetCore(float core) => _beamMaterial.SetFloat("_Core", core / PaintedWidth);

        public static SunMark Create(Transform parent, GridPos tile)
        {
            var mark = new GameObject($"Sun mark {tile}").AddComponent<SunMark>();
            mark.transform.SetParent(parent, false);
            mark.Build(tile);
            return mark;
        }

        private void Build(GridPos tile)
        {
            Tile = tile;
            var pos = IsoMath.GridToWorld(tile);
            transform.position = pos;
            // On the floor: above every tile (tiles sort from about -1000 to -400; the tiles in front
            // would otherwise cover the spill onto them), below anything standing on the board.
            _floorOrder = -300 + (tile.X + tile.Y) * 4;

            // A soft pool of light on the floor, flattened to the isometric angle.
            _pool = NewRenderer("Light pool", SceneryMotes.Dot, Glow, _floorOrder);
            _pool.transform.localScale = new Vector3(1.6f, 0.8f, 1f);

            // The sigil turns flat on the floor: rotate it inside a parent squashed to 2:1.
            var flat = new GameObject("Sigil floor").transform;
            flat.SetParent(transform, false);
            flat.localScale = new Vector3(SigilWidth, SigilWidth * 0.5f, 1f);
            _sigilRenderer = NewRenderer("Sigil", SigilSprite, SigilMaterial, _floorOrder + 1);
            _sigil = _sigilRenderer.transform;
            _sigil.SetParent(flat, false);

            // The shaft of light: drawn in front of the statue on this tile, behind anything further forward.
            _beamMaterial = new Material(Resources.Load<Shader>("Shaders/Beam"));
            _beam = NewRenderer("Beam", BeamSprite, _beamMaterial, IsoMath.SortingOrder(tile) + 6);
            SetCore(0.25f);
            _beam.transform.localPosition = new Vector3(0f, BeamHeight * 0.5f, 0f);

            _light = FxLight.Add(pos, LightColor, 1f);
            Sfx.Play("sun_mark", pos);
            Apply();
        }

        private SpriteRenderer NewRenderer(string name, Sprite sprite, Material material, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = order;
            return sr;
        }

        private void Update()
        {
            _appear = Mathf.MoveTowards(_appear, 1f, Time.deltaTime / 0.5f);
            _sigil.localRotation = Quaternion.Euler(0f, 0f, _sigil.localEulerAngles.z - _spin * Time.deltaTime);

            // Dust drifting down through the shaft, catching the light.
            if (!_striking)
            {
                _moteTimer -= Time.deltaTime;
                if (_moteTimer <= 0f)
                {
                    _moteTimer = Random.Range(0.08f, 0.2f);
                    StartCoroutine(Mote());
                }
            }

            Apply();
        }

        /// <summary>Pushes the current levels to the renderers and the light.</summary>
        private void Apply()
        {
            var ease = _appear * _appear * (3f - 2f * _appear);
            var pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f);
            _sigilRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.9f * _sigilGlow * pulse * ease));
            _sigilRenderer.transform.localScale = Vector3.one * (0.6f + 0.4f * ease);
            _pool.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Clamp01(0.25f * _sigilGlow * pulse * ease));

            var flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 4f, Tile.X * 3.1f);
            _beam.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Clamp01(_shaft * flicker * ease));
            SizeBeam(_shaftWidth);

            _light.Strength = _lightStrength * pulse * ease;
        }

        private IEnumerator Mote()
        {
            var sr = NewRenderer("Mote", SceneryMotes.Dot, Glow, IsoMath.SortingOrder(Tile) + 6);
            var x = Random.Range(-0.5f, 0.5f) * _shaftWidth;
            var y = Random.Range(0.6f, 2.2f);
            var size = Random.Range(0.025f, 0.05f);
            var life = Random.Range(1.2f, 2f);
            for (var age = 0f; age < life && !_striking; age += Time.deltaTime)
            {
                var p = age / life;
                sr.transform.localPosition = new Vector3(x + Mathf.Sin(age * 2f + y) * 0.03f, y - p * 0.6f, 0f);
                sr.transform.localScale = Vector3.one * size;
                sr.color = new Color(1f, 0.9f, 0.6f, Mathf.Sin(p * Mathf.PI) * 0.8f);
                yield return null;
            }

            Destroy(sr.gameObject);
        }

        /// <summary>The beam gathering: the sigil spins up and brightens, the shaft and the light grow.</summary>
        public IEnumerator Charge()
        {
            _appear = 1f;
            Sfx.Play("sun_charge", transform.position, pitch: 0f);
            const float time = 0.5f;
            for (var t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                var k = t * t;
                _spin = Mathf.Lerp(12f, 260f, k);
                _sigilGlow = Mathf.Lerp(1f, 2.2f, k);
                _shaft = Mathf.Lerp(0.12f, 0.5f, k);
                _shaftWidth = Mathf.Lerp(0.22f, 0.12f, k);
                _lightStrength = Mathf.Lerp(0.12f, 0.6f, k);
                _light.Radius = Mathf.Lerp(1f, 1.6f, k);
                yield return null;
            }
        }

        /// <summary>The beam strikes now; the afterglow plays out on its own and the mark removes itself.</summary>
        public void Impact()
        {
            _striking = true;
            StartCoroutine(StrikeAndFade());
        }

        private IEnumerator StrikeAndFade()
        {
            var pos = transform.position;
            Fx.SunBurst(pos);
            Fx.Shake(0.13f, 0.35f);
            Sfx.Play("sun_impact", pos);
            Sfx.Play("sun_sizzle", pos, 0.6f);
            Scorch.Create(transform.parent, Tile, _floorOrder);

            // The blast: wide, white-hot, everything around lit gold.
            const float strike = 0.18f;
            for (var t = 0f; t < 1f; t += Time.deltaTime / strike)
            {
                _shaftWidth = Mathf.Lerp(1.4f, 0.95f, t);
                _shaft = 1f;
                SetCore(Mathf.Lerp(0.6f, 0.35f, t));
                _sigilGlow = 3f;
                _sigilRenderer.transform.localScale = Vector3.one * (1f + t * 0.6f);
                _lightStrength = 2.4f;
                _light.Radius = 2.8f;
                ApplyStrike(1f - t);
                yield return null;
            }

            // The afterglow: the beam narrows and fades, the light dies down, embers float up.
            StartCoroutine(Embers(pos));
            const float fade = 1.3f;
            for (var t = 0f; t < 1f; t += Time.deltaTime / fade)
            {
                var k = 1f - t;
                _shaftWidth = Mathf.Lerp(0.95f, 0.05f, t * (2f - t));
                _shaft = k * k;
                _sigilGlow = 3f * k * k * k;
                _lightStrength = 2.4f * k * k;
                _light.Radius = Mathf.Lerp(2.8f, 2f, t);
                ApplyStrike(0f);
                yield return null;
            }

            _light.Remove();
            Destroy(_beamMaterial);
            Destroy(gameObject);
        }

        /// <summary>Like Apply, without the appear easing; flash 0..1 tints the sigil and pool white-hot.</summary>
        private void ApplyStrike(float flash)
        {
            _sigilRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_sigilGlow * 0.5f));
            var pool = Color.Lerp(Gold, Color.white, flash);
            _pool.color = new Color(pool.r, pool.g, pool.b, Mathf.Clamp01(0.2f * _sigilGlow));
            _pool.transform.localScale = new Vector3(1.5f, 0.75f, 1f);
            _beam.color = new Color(1f, Mathf.Lerp(0.82f, 1f, flash), Mathf.Lerp(0.5f, 0.95f, flash), _shaft);
            SizeBeam(_shaftWidth);
            _light.Strength = _lightStrength;
        }

        private IEnumerator Embers(Vector3 pos)
        {
            for (var i = 0; i < 18; i++)
            {
                StartCoroutine(Ember(pos + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.12f, 0.12f), 0f)));
                yield return new WaitForSeconds(0.04f);
            }
        }

        private IEnumerator Ember(Vector3 from)
        {
            var sr = new GameObject("Ember").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform.parent, false);
            sr.sprite = SceneryMotes.Dot;
            sr.sharedMaterial = Glow;
            sr.sortingOrder = IsoMath.SortingOrder(Tile) + 6;
            var size = Random.Range(0.03f, 0.06f);
            var life = Random.Range(1f, 1.8f);
            var drift = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.6f, 1.2f), 0f);
            for (var age = 0f; age < life; age += Time.deltaTime)
            {
                var p = age / life;
                sr.transform.position = from + drift * p + Vector3.right * Mathf.Sin(age * 5f + from.x * 9f) * 0.05f;
                sr.transform.localScale = Vector3.one * size * (1f - p * 0.5f);
                var c = Color.Lerp(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.35f, 0.1f), p);
                sr.color = new Color(c.r, c.g, c.b, 1f - p);
                yield return null;
            }

            Destroy(sr.gameObject);
        }

        private void OnDestroy()
        {
            _light?.Remove();
            if (_beamMaterial != null)
                Destroy(_beamMaterial);
        }
    }
}
