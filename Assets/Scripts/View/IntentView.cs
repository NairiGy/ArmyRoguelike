using System.Collections.Generic;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>A dotted line from an enemy to the unit it wants to attack.</summary>
    public sealed class IntentView : MonoBehaviour
    {
        private static readonly Color DotColor = new Color(1f, 0.55f, 0.2f, 0.9f);
        private const float Spacing = 0.12f;
        private const float DotSize = 0.05f;

        private readonly List<SpriteRenderer> _dots = new List<SpriteRenderer>();

        private Color _color = DotColor;

        public static IntentView Create(Transform parent) => Create(parent, DotColor);

        /// <summary>A dotted line in another colour, e.g. the zone-of-control link between engaged units.</summary>
        public static IntentView Create(Transform parent, Color color)
        {
            var go = new GameObject("Intent");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<IntentView>();
            view._color = color;
            return view;
        }

        public void Show(Vector3 from, Vector3 to)
        {
            var count = Mathf.Max(1, Mathf.FloorToInt(Vector3.Distance(from, to) / Spacing));
            while (_dots.Count < count)
                _dots.Add(MakeDot());
            for (var i = 0; i < _dots.Count; i++)
            {
                var dot = _dots[i];
                dot.enabled = i < count;
                if (!dot.enabled)
                    continue;
                // Skip the ends so the line doesn't cover the statues' feet.
                var t = (i + 0.5f) / count;
                dot.transform.position = Vector3.Lerp(from, to, t);
            }
        }

        public void Hide()
        {
            foreach (var dot in _dots)
                dot.enabled = false;
        }

        private SpriteRenderer MakeDot()
        {
            var sr = new GameObject("Dot").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(transform, false);
            sr.transform.localScale = new Vector3(DotSize, DotSize, 1f);
            sr.sprite = PlaceholderArt.Pixel;
            sr.color = _color;
            sr.sortingOrder = 15000;
            return sr;
        }
    }
}
