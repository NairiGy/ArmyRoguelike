using System.Collections;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>The burn a sun beam leaves: glowing embers that cool, then the char fades away.</summary>
    public sealed class Scorch : MonoBehaviour
    {
        private const float Width = 2f;
        private const float CoolTime = 2.5f;
        private const float StayTime = 4f;
        private const float FadeTime = 3f;

        private static Material _burn;
        private static Material _embers;
        private static Sprite _sprite;

        private SpriteRenderer _burnRenderer;
        private SpriteRenderer _emberRenderer;

        private static Sprite ScorchSprite
        {
            get
            {
                if (_sprite != null)
                    return _sprite;
                var tex = Resources.Load<Texture2D>("Effects/Scorch");
                return _sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
            }
        }

        private static Material Burn
        {
            get
            {
                if (_burn != null)
                    return _burn;
                _burn = new Material(Resources.Load<Shader>("Shaders/Scorch"));
                _burn.SetFloat("_Mode", 0f);
                _burn.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.DstColor);
                _burn.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                return _burn;
            }
        }

        private static Material Embers
        {
            get
            {
                if (_embers != null)
                    return _embers;
                _embers = new Material(Resources.Load<Shader>("Shaders/Scorch"));
                _embers.SetFloat("_Mode", 1f);
                _embers.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                _embers.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                return _embers;
            }
        }

        public static void Create(Transform parent, GridPos tile, int order)
        {
            var scorch = new GameObject($"Scorch {tile}").AddComponent<Scorch>();
            scorch.transform.SetParent(parent, false);
            scorch.transform.position = IsoMath.GridToWorld(tile);
            // A random turn so repeated strikes don't leave identical marks.
            var flat = new GameObject("Floor").transform;
            flat.SetParent(scorch.transform, false);
            flat.localScale = new Vector3(Width, Width * 0.5f, 1f);
            scorch._burnRenderer = scorch.Layer(flat, Burn, order);
            scorch._emberRenderer = scorch.Layer(flat, Embers, order + 1);
            var turn = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            scorch._burnRenderer.transform.localRotation = turn;
            scorch._emberRenderer.transform.localRotation = turn;
        }

        private SpriteRenderer Layer(Transform parent, Material material, int order)
        {
            var sr = new GameObject(material == Burn ? "Burn" : "Embers").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.sprite = ScorchSprite;
            sr.sharedMaterial = material;
            sr.sortingOrder = order;
            return sr;
        }

        private IEnumerator Start()
        {
            for (var t = 0f; t < 1f; t += Time.deltaTime / CoolTime)
            {
                // White-hot, through orange, to a dull red, then out.
                var heat = 1f - t;
                var c = Color.Lerp(new Color(1f, 0.25f, 0.05f), new Color(1f, 0.85f, 0.5f), heat * heat);
                _emberRenderer.color = new Color(c.r, c.g, c.b, heat);
                _burnRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t * 4f));
                yield return null;
            }

            Destroy(_emberRenderer.gameObject);
            yield return new WaitForSeconds(StayTime);
            for (var t = 0f; t < 1f; t += Time.deltaTime / FadeTime)
            {
                _burnRenderer.color = new Color(1f, 1f, 1f, 1f - t);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
