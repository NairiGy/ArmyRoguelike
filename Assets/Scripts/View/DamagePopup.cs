using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// A short text (damage number, heal, ability name) that pops in with a little bounce, floats up and
    /// fades out. A dark copy behind it keeps it readable on any background.
    /// </summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        private const float Lifetime = 0.9f;

        public static readonly Color DamageColor = new Color(1f, 0.85f, 0.3f);
        public static readonly Color HealColor = new Color(0.45f, 1f, 0.5f);
        public static readonly Color AbilityColor = new Color(1f, 0.55f, 0.95f);

        private TextMesh _text;
        private TextMesh _shadow;
        private float _age;
        private Vector3 _start;

        public static void Spawn(Vector3 world, int damage) => Spawn(world, $"-{damage}", DamageColor);

        public static void Spawn(Vector3 world, string label, Color color, int size = 64)
        {
            var go = new GameObject("Popup");
            var popup = go.AddComponent<DamagePopup>();
            popup._start = world;
            go.transform.position = world;
            popup._shadow = Text(go.transform, "Shadow", label, new Color(0.05f, 0.05f, 0.08f, 0.85f), size, 19999);
            popup._shadow.transform.localPosition = new Vector3(0.018f, -0.018f, 0f);
            popup._text = Text(go.transform, "Text", label, color, size, 20000);
            popup.Animate();
        }

        private static TextMesh Text(Transform parent, string name, string label, Color color, int size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = size;
            text.characterSize = 0.05f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = color;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = text.font.material;
            r.sortingOrder = order;
            return text;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            Animate();
            if (_age >= Lifetime)
                Destroy(gameObject);
        }

        private void Animate()
        {
            var t = Mathf.Clamp01(_age / Lifetime);
            // Pops in oversized and settles with a small overshoot, rises quickly then drifts, fades at the end.
            var pop = _age < 0.15f ? Mathf.Lerp(1.6f, 0.9f, _age / 0.15f) : Mathf.Lerp(0.9f, 1f, Mathf.Clamp01((_age - 0.15f) / 0.1f));
            transform.localScale = Vector3.one * pop;
            transform.position = _start + Vector3.up * (0.4f * (1f - (1f - t) * (1f - t)));
            var fade = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
            var c = _text.color;
            c.a = fade;
            _text.color = c;
            var s = _shadow.color;
            s.a = 0.85f * fade;
            _shadow.color = s;
        }
    }
}
