using System.Collections;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Small fire-and-forget battle effects built from soft dots: stone chips, dust, sparks, rubble,
    /// projectiles, explosions and screen shake. Each effect cleans itself up.
    /// </summary>
    public static class Fx
    {
        private const int Order = 15000;

        private static Runner _runner;
        private static Material _glow;

        private sealed class Runner : MonoBehaviour
        {
        }

        private static Runner Host
        {
            get
            {
                if (_runner == null)
                    _runner = new GameObject("Fx").AddComponent<Runner>();
                return _runner;
            }
        }

        private static Material Glow => _glow != null ? _glow : _glow = new Material(Resources.Load<Shader>("Shaders/Glow"));

        private static SpriteRenderer Dot(Vector3 pos, Color color, float size, bool additive, int order = Order)
        {
            var sr = new GameObject("Fx dot").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(Host.transform, false);
            sr.transform.position = pos;
            sr.transform.localScale = Vector3.one * size;
            sr.sprite = SceneryMotes.Dot;
            if (additive)
                sr.sharedMaterial = Glow;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Stone chips knocked off a statue: small pieces flung out that fall and fade.</summary>
        public static void Chips(Vector3 pos, Color stone, int count = 7)
        {
            for (var i = 0; i < count; i++)
            {
                var dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.4f, 1.2f), 0f);
                Host.StartCoroutine(Fling(Dot(pos, stone, Random.Range(0.035f, 0.06f), false), dir * Random.Range(0.9f, 1.6f), 0.45f, 3.5f));
            }
        }

        /// <summary>Rubble from a crumbling statue: more and bigger chunks, falling to the floor around it.</summary>
        public static void Rubble(Vector3 pos, Color stone, float height)
        {
            for (var i = 0; i < 14; i++)
            {
                var start = pos + new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.05f, height), 0f);
                var dir = new Vector3(Random.Range(-0.9f, 0.9f), Random.Range(0f, 0.6f), 0f);
                Host.StartCoroutine(Fling(Dot(start, stone, Random.Range(0.05f, 0.1f), false), dir, 0.6f, 3f, pos.y - 0.05f));
            }

            Dust(pos, 9, 0.5f);
        }

        /// <summary>A soft puff of dust spreading along the floor.</summary>
        public static void Dust(Vector3 pos, int count = 5, float spread = 0.3f)
        {
            for (var i = 0; i < count; i++)
            {
                var drift = new Vector3(Random.Range(-1f, 1f) * spread, Random.Range(0f, 0.15f), 0f);
                Host.StartCoroutine(Puff(Dot(pos, new Color(0.7f, 0.72f, 0.78f, 0.35f), Random.Range(0.08f, 0.14f), false), drift, 0.55f));
            }
        }

        /// <summary>A bright, quick burst where a blow lands.</summary>
        public static void Impact(Vector3 pos, Color tint)
        {
            Host.StartCoroutine(Puff(Dot(pos, new Color(tint.r, tint.g, tint.b, 0.8f), 0.18f, true), Vector3.zero, 0.15f, 2.2f));
            for (var i = 0; i < 5; i++)
            {
                var dir = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.3f, 1f), 0f).normalized * Random.Range(1.2f, 2f);
                Host.StartCoroutine(Fling(Dot(pos, new Color(1f, 0.92f, 0.7f, 1f), 0.025f, true), dir, 0.2f, 0f));
            }
        }

        /// <summary>An arrow or bolt flying from one point to another in a shallow arc; onArrive runs on hitting.</summary>
        public static IEnumerator Projectile(Vector3 from, Vector3 to, Color tint, System.Action onArrive)
        {
            var bolt = Dot(from, new Color(tint.r, tint.g, tint.b, 0.95f), 1f, true);
            var duration = Mathf.Clamp(Vector3.Distance(from, to) * 0.07f, 0.12f, 0.3f);
            var prev = from;
            for (var t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                var pos = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.25f);
                var dir = pos - prev;
                if (dir.sqrMagnitude > 1e-6f)
                    bolt.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                bolt.transform.position = pos;
                bolt.transform.localScale = new Vector3(0.22f, 0.035f, 1f);
                prev = pos;
                yield return null;
            }

            Object.Destroy(bolt.gameObject);
            onArrive?.Invoke();
        }

        /// <summary>A blast: a flash, a widening shockwave ring, flung debris and a screen shake.</summary>
        public static void Explosion(Vector3 pos)
        {
            Host.StartCoroutine(Puff(Dot(pos, new Color(1f, 0.75f, 0.35f, 0.9f), 0.5f, true), Vector3.zero, 0.2f, 3f));
            Host.StartCoroutine(Ring(pos));
            for (var i = 0; i < 12; i++)
            {
                var dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.2f, 1.4f), 0f) * Random.Range(1.2f, 2.4f);
                Host.StartCoroutine(Fling(Dot(pos, new Color(0.35f, 0.3f, 0.3f, 1f), Random.Range(0.04f, 0.08f), false), dir, 0.6f, 4f));
            }

            Dust(pos, 8, 0.7f);
            Shake(0.08f, 0.25f);
        }

        /// <summary>Shakes the main camera for a moment.</summary>
        public static void Shake(float strength, float duration)
        {
            var cam = Camera.main;
            if (cam != null)
                Host.StartCoroutine(ShakeCamera(cam.transform, strength, duration));
        }

        private static Vector3 _shakeOffset;

        private static IEnumerator ShakeCamera(Transform cam, float strength, float duration)
        {
            for (var t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                // Undo the previous frame's offset before adding a new one, so shakes never drift the camera.
                cam.position -= _shakeOffset;
                _shakeOffset = (Vector3)(Random.insideUnitCircle * strength * (1f - t));
                cam.position += _shakeOffset;
                yield return null;
            }

            cam.position -= _shakeOffset;
            _shakeOffset = Vector3.zero;
        }

        private static IEnumerator Fling(SpriteRenderer sr, Vector3 velocity, float life, float gravity, float floorY = float.NegativeInfinity)
        {
            var start = sr.color;
            for (var age = 0f; age < life; age += Time.deltaTime)
            {
                velocity += Vector3.down * gravity * Time.deltaTime;
                var pos = sr.transform.position + velocity * Time.deltaTime;
                if (pos.y < floorY)
                {
                    // Rubble settles on the floor instead of falling through it.
                    pos.y = floorY;
                    velocity = new Vector3(velocity.x * 0.4f, 0f, 0f);
                }

                sr.transform.position = pos;
                sr.color = new Color(start.r, start.g, start.b, start.a * (1f - age / life));
                yield return null;
            }

            Object.Destroy(sr.gameObject);
        }

        private static IEnumerator Puff(SpriteRenderer sr, Vector3 drift, float life, float grow = 1.8f)
        {
            var start = sr.color;
            var size = sr.transform.localScale;
            var from = sr.transform.position;
            for (var age = 0f; age < life; age += Time.deltaTime)
            {
                var p = age / life;
                sr.transform.position = from + drift * p;
                sr.transform.localScale = size * (1f + (grow - 1f) * p);
                sr.color = new Color(start.r, start.g, start.b, start.a * (1f - p));
                yield return null;
            }

            Object.Destroy(sr.gameObject);
        }

        /// <summary>A sun beam hitting the floor: a white-gold flash, a wide shockwave, sparks and dust.</summary>
        public static void SunBurst(Vector3 pos)
        {
            Host.StartCoroutine(Puff(Dot(pos, new Color(1f, 0.95f, 0.8f, 1f), 0.9f, true), Vector3.zero, 0.25f, 2.5f));
            Host.StartCoroutine(Ring(pos, 32, 1.7f, 0.45f, new Color(1f, 0.85f, 0.5f), 0.08f));
            for (var i = 0; i < 22; i++)
            {
                var dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1.8f), 0f) * Random.Range(1.2f, 2.6f);
                var spark = Dot(pos, new Color(1f, Random.Range(0.75f, 0.95f), 0.5f, 1f), Random.Range(0.03f, 0.06f), true);
                Host.StartCoroutine(Fling(spark, dir, Random.Range(0.4f, 0.75f), 4.5f));
            }

            Dust(pos, 10, 0.8f);
        }

        private static IEnumerator Ring(Vector3 pos) => Ring(pos, 16, 0.9f, 0.3f, new Color(1f, 0.85f, 0.6f), 0.06f);

        private static IEnumerator Ring(Vector3 pos, int count, float radius, float time, Color color, float size)
        {
            // Dots racing outwards along a flattened circle read as a shockwave on the iso floor.
            var dots = new SpriteRenderer[count];
            for (var i = 0; i < dots.Length; i++)
                dots[i] = Dot(pos, color, size, true);
            for (var t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                for (var i = 0; i < dots.Length; i++)
                {
                    var a = i * Mathf.PI * 2f / dots.Length;
                    dots[i].transform.position = pos + new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.5f, 0f) * (0.2f + radius * t);
                    dots[i].color = new Color(color.r, color.g, color.b, 0.7f * (1f - t));
                }

                yield return null;
            }

            foreach (var d in dots)
                Object.Destroy(d.gameObject);
        }
    }
}
