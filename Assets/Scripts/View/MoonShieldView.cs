using System.Collections;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// The moon omen's shield on a statue: a thin shaft of moonlight touches it, a silver moon sigil opens
    /// on the floor and a veil of moonlight wraps the statue, lighting it cool silver. Each hit it absorbs
    /// makes it flash and grow fainter; when it runs out it shatters, otherwise it fades at the next turn.
    /// It follows the statue as it moves.
    /// </summary>
    public sealed class MoonShieldView : MonoBehaviour
    {
        private const float CastTime = 0.6f;
        private const float SigilWidth = 0.8f;
        private static readonly Color Silver = new Color(0.78f, 0.86f, 1f);

        private static Material _sigilMaterial;
        private static Material _veilMaterial;
        private static Material _beamMaterial;
        private static Sprite _sigilSprite;
        private static Sprite _veilSprite;

        private UnitView _view;
        private Transform _sigil;
        private SpriteRenderer _sigilRenderer;
        private SpriteRenderer _veil;
        private FxLight _light;
        private int _maxPoints;
        private float _appear;
        private float _flash;
        private bool _ending;

        public int Points { get; private set; }
        public int CastTurn { get; private set; }

        // Each part has a material of its own: sprites sharing one and drawn next to each other get batched,
        // and the batch can draw them all with one sprite's texture.
        private static Material NewGlow()
        {
            // Silver on the pale floor and marble needs more light than the sun's gold to show.
            var m = new Material(Resources.Load<Shader>("Shaders/Glow"));
            m.SetFloat("_Intensity", 1.6f);
            return m;
        }
        private static Material SigilMaterial => _sigilMaterial != null ? _sigilMaterial : _sigilMaterial = NewGlow();
        private static Material VeilMaterial => _veilMaterial != null ? _veilMaterial : _veilMaterial = NewGlow();

        private static Material BeamMaterial
        {
            get
            {
                if (_beamMaterial != null)
                    return _beamMaterial;
                _beamMaterial = new Material(Resources.Load<Shader>("Shaders/Beam"));
                _beamMaterial.SetFloat("_Core", 0.05f);
                return _beamMaterial;
            }
        }

        private static Sprite Load(ref Sprite cache, string path)
        {
            if (cache != null)
                return cache;
            var tex = Resources.Load<Texture2D>(path);
            return cache = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
        }

        public static MoonShieldView Create(UnitView view, int points, int turn)
        {
            var shield = new GameObject("Moon shield").AddComponent<MoonShieldView>();
            shield.transform.SetParent(view.transform, false);
            shield.Build(view, points, turn);
            return shield;
        }

        private void Build(UnitView view, int points, int turn)
        {
            _view = view;
            Points = points;
            _maxPoints = points;
            CastTurn = turn;

            var flat = new GameObject("Sigil floor").transform;
            flat.SetParent(transform, false);
            flat.localScale = new Vector3(SigilWidth, SigilWidth * 0.5f, 1f);
            _sigilRenderer = NewRenderer("Sigil", Load(ref _sigilSprite, "Effects/MoonSigil"), SigilMaterial);
            _sigil = _sigilRenderer.transform;
            _sigil.SetParent(flat, false);

            _veil = NewRenderer("Veil", Load(ref _veilSprite, "Effects/MoonVeil"), VeilMaterial);
            _light = FxLight.Add(transform.position, Silver, 1.3f);
            Sfx.Play("moon_cast", transform.position);
            StartCoroutine(Cast());
        }

        private SpriteRenderer NewRenderer(string name, Sprite sprite, Material material)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            return sr;
        }

        /// <summary>A thin shaft of moonlight touches the statue while the veil and sigil open.</summary>
        private IEnumerator Cast()
        {
            var shaft = NewRenderer("Moonlight", SunMark.BeamSprite, BeamMaterial);
            const float height = 14f;
            shaft.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            for (var t = 0f; t < 1f; t += Time.deltaTime / CastTime)
            {
                _appear = t;
                var k = Mathf.Sin(t * Mathf.PI);
                shaft.transform.localScale = new Vector3(0.35f * (1f - t * 0.6f), height / SunMark.BeamSprite.bounds.size.y, 1f);
                shaft.color = new Color(Silver.r, Silver.g, Silver.b, 0.7f * k);
                yield return null;
            }

            _appear = 1f;
            Destroy(shaft.gameObject);
            _flash = 1f;
        }

        private void LateUpdate()
        {
            if (_view == null)
                return;
            var ease = _appear * _appear * (3f - 2f * _appear);
            var strength = _maxPoints > 0 ? 0.45f + 0.55f * Points / _maxPoints : 1f;
            var pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.4f);
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 3f);

            // On the floor under the statue, above every tile: turning slowly the other way to the sun's.
            var tile = IsoMath.WorldToGrid(transform.position);
            _sigilRenderer.sortingOrder = -300 + (tile.X + tile.Y) * 4 + 2;
            _sigil.localRotation = Quaternion.Euler(0f, 0f, _sigil.localEulerAngles.z + 8f * Time.deltaTime);
            _sigil.localScale = Vector3.one * (0.5f + 0.5f * ease);
            _sigilRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01((0.85f * strength * pulse + _flash * 0.3f) * ease));

            // The veil wraps the statue, in front of it; it breathes a little and shimmers.
            var height = _view.BodyHeight * 1.7f;
            var size = _veil.sprite.bounds.size;
            var breathe = 1f + 0.02f * Mathf.Sin(Time.time * 1.7f) + 0.08f * _flash;
            _veil.transform.position = _view.BodyBase + Vector3.up * height * 0.36f;
            _veil.transform.localScale = new Vector3(height / size.y * 1.35f, height / size.y, 1f) * breathe * (0.7f + 0.3f * ease);
            _veil.sortingOrder = _view.BodyOrder + 1;
            var shimmer = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 1.3f, transform.position.x);
            _veil.color = new Color(1f, 1f, 1f, Mathf.Clamp01((0.85f * strength * shimmer + _flash * 0.4f) * ease));

            _light.Position = transform.position;
            _light.Strength = (0.35f * strength + _flash * 0.8f) * ease;
        }

        /// <summary>The shield takes a hit; it flashes, and shatters when it runs out. Returns the points it took.</summary>
        public int Absorb(int amount)
        {
            var taken = Mathf.Min(Points, amount);
            Points -= taken;
            _flash = 1f;
            Fx.Ripple(_view.BodyBase, Silver);
            if (Points <= 0)
                Shatter();
            else
                Sfx.Play("moon_absorb", transform.position);
            return taken;
        }

        /// <summary>Used up: the veil bursts into silver specks.</summary>
        public void Shatter()
        {
            if (_ending)
                return;
            _ending = true;
            Fx.Shards(_view.BodyBase + Vector3.up * _view.BodyHeight * 0.5f, Silver, _view.BodyHeight);
            Sfx.Play("moon_shatter", transform.position);
            StartCoroutine(FadeOut(0.25f));
        }

        /// <summary>Not used up by the next turn: it fades quietly.</summary>
        public void Fade()
        {
            if (_ending)
                return;
            _ending = true;
            Sfx.Play("moon_fade", transform.position, 0.6f);
            StartCoroutine(FadeOut(0.8f));
        }

        private IEnumerator FadeOut(float time)
        {
            var from = _appear;
            for (var t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                _appear = from * (1f - t);
                yield return null;
            }

            Destroy(gameObject);
        }

        private void OnDestroy() => _light?.Remove();
    }
}
