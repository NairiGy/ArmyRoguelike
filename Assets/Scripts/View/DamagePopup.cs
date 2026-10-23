using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>A damage number that floats up and fades out.</summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        private const float Lifetime = 0.8f;

        private TextMesh _text;
        private float _age;
        private Vector3 _start;

        public static void Spawn(Vector3 world, int damage)
        {
            var go = new GameObject("DamagePopup");
            var popup = go.AddComponent<DamagePopup>();
            popup._start = world;
            go.transform.position = world;

            var text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = $"-{damage}";
            text.fontSize = 64;
            text.characterSize = 0.05f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.85f, 0.3f);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = text.font.material;
            r.sortingOrder = 20000;
            popup._text = text;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            var t = _age / Lifetime;
            transform.position = _start + Vector3.up * (0.35f * t);
            var c = _text.color;
            c.a = 1f - t * t;
            _text.color = c;
            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
