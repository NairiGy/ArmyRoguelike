using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Fire in a brazier: a glow over the bowl, flame tongues that rise and fade, and the odd spark. All
    /// additive. (A warm pool of light on the floor was tried and looked out of place.)
    /// </summary>
    public sealed class SceneryFire : MonoBehaviour
    {
        private const int Tongues = 6;
        private const int Sparks = 4;

        private static readonly Color HaloColor = new Color(1f, 0.45f, 0.1f, 0.35f);
        // Saturated: the tongues overlap and add up, and paler colours burn out to white.
        private static readonly Color FlameBase = new Color(1f, 0.55f, 0.12f, 0.7f);
        private static readonly Color FlameTip = new Color(0.9f, 0.18f, 0.04f, 0f);

        private SpriteRenderer _halo;
        private SpriteRenderer[] _tongues;
        private SpriteRenderer[] _sparks;
        private float[] _tongueAge;
        private float[] _tongueLife;
        private float[] _sparkAge;
        private float[] _sparkLife;
        private Vector3 _bowl;
        private float _seed;

        /// <summary>Lights a fire in a brazier standing at floorPos, its bowl bowlHeight above the floor.</summary>
        public static SceneryFire Create(Transform parent, Vector3 floorPos, float bowlHeight, int order, Material glow)
        {
            var go = new GameObject("Fire");
            go.transform.SetParent(parent, false);
            go.transform.position = floorPos;
            var fire = go.AddComponent<SceneryFire>();
            fire.Build(bowlHeight, order, glow);
            return fire;
        }

        private SpriteRenderer Glow(string name, Material glow, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.sprite = SceneryMotes.Dot;
            sr.sharedMaterial = glow;
            sr.sortingOrder = order;
            return sr;
        }

        private void Build(float bowlHeight, int order, Material glow)
        {
            _seed = Random.value * 100f;
            _bowl = Vector3.up * bowlHeight;
            _halo = Glow("Halo", glow, order + 1);
            _halo.transform.localPosition = _bowl;
            _tongues = new SpriteRenderer[Tongues];
            _tongueAge = new float[Tongues];
            _tongueLife = new float[Tongues];
            for (var i = 0; i < Tongues; i++)
            {
                _tongues[i] = Glow("Flame", glow, order + 2);
                _tongueLife[i] = Random.Range(0.5f, 0.9f);
                _tongueAge[i] = Random.value * _tongueLife[i];
            }

            _sparks = new SpriteRenderer[Sparks];
            _sparkAge = new float[Sparks];
            _sparkLife = new float[Sparks];
            for (var i = 0; i < Sparks; i++)
            {
                _sparks[i] = Glow("Spark", glow, order + 3);
                _sparkLife[i] = Random.Range(1.2f, 2.4f);
                _sparkAge[i] = Random.value * _sparkLife[i];
            }

            Animate(0f);
        }

        private void Update() => Animate(Time.deltaTime);

        private void Animate(float dt)
        {
            var t = Time.time + _seed;
            // Two out-of-step waves make the flicker irregular.
            var flicker = 0.85f + 0.1f * Mathf.Sin(t * 9.1f) + 0.05f * Mathf.Sin(t * 23.7f);
            _halo.transform.localScale = Vector3.one * (0.7f + 0.06f * flicker);
            _halo.color = new Color(HaloColor.r, HaloColor.g, HaloColor.b, HaloColor.a * flicker);

            for (var i = 0; i < Tongues; i++)
            {
                _tongueAge[i] += dt;
                if (_tongueAge[i] >= _tongueLife[i])
                {
                    _tongueAge[i] = 0f;
                    _tongueLife[i] = Random.Range(0.5f, 0.9f);
                }

                var life = _tongueAge[i] / _tongueLife[i];
                var spread = (i - (Tongues - 1) * 0.5f) * 0.035f;
                var x = spread * (1f - life) + Mathf.Sin(t * 5f + i) * 0.015f;
                _tongues[i].transform.localPosition = _bowl + new Vector3(x, life * 0.3f, 0f);
                _tongues[i].transform.localScale = new Vector3(0.2f, 0.32f, 1f) * (1f - life * 0.7f);
                _tongues[i].color = Color.Lerp(FlameBase, FlameTip, life);
            }

            for (var i = 0; i < Sparks; i++)
            {
                _sparkAge[i] += dt;
                if (_sparkAge[i] >= _sparkLife[i])
                {
                    _sparkAge[i] = 0f;
                    _sparkLife[i] = Random.Range(1.2f, 2.4f);
                }

                var life = _sparkAge[i] / _sparkLife[i];
                _sparks[i].transform.localPosition = _bowl + new Vector3(Mathf.Sin(t * 1.3f + i * 2f) * 0.1f, life * 0.9f, 0f);
                _sparks[i].transform.localScale = Vector3.one * 0.035f;
                _sparks[i].color = new Color(1f, 0.7f, 0.3f, (1f - life) * 0.9f);
            }
        }
    }
}
