using System.Collections.Generic;
using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// The army's banners, standing along the back edges of the roof behind the player's side. One painted pole and
    /// cloth serve every banner; each has its own cloth colour, and the cloth moves in the wind (BannerCloth.shader).
    /// </summary>
    public static class BannerView
    {
        /// <summary>A banner's height on screen, in world units (a tile is 1 wide).</summary>
        private const float Height = 1.5f;

        /// <summary>How far outside the board's edge the poles stand.</summary>
        private const float Outside = 0.14f;

        // Where the pole's foot is in the painting.
        private static readonly Vector2 Foot = new Vector2(0.5f, 0.033f);

        private static readonly Dictionary<Banner, Color> Colours = new Dictionary<Banner, Color>
        {
            [Banner.Vampiric] = new Color32(0x7E, 0x14, 0x18, 0xFF),
            [Banner.ThirdWheel] = new Color32(0x2B, 0x4F, 0x7A, 0xFF),
            [Banner.Loser] = new Color32(0x5C, 0x5C, 0x60, 0xFF),
            [Banner.Mercenaries] = new Color32(0x1F, 0x5A, 0x3A, 0xFF),
            [Banner.BatteringRam] = new Color32(0xA5, 0x50, 0x1C, 0xFF),
        };

        /// <param name="poleMaterial">The scenery's palette material, so the pole's stone matches the roof.</param>
        /// <param name="clothLight">Brightness of the cloth in this scene (dimmer at night).</param>
        public static void Create(Transform root, IEnumerable<Banner> held, Vector2 top, Vector2 left, Vector2 right, Material poleMaterial, float clothLight)
        {
            // Always the same order, so a banner keeps its place from fight to fight.
            var banners = Banners.All.Where(held.Contains).ToList();
            if (banners.Count == 0)
                return;

            var poleTexture = Resources.Load<Texture2D>("Banners/pole");
            var clothTexture = Resources.Load<Texture2D>("Banners/cloth");
            var shader = Resources.Load<Shader>("Shaders/BannerCloth");
            if (poleTexture == null || clothTexture == null || shader == null)
                return;

            var pole = MakeSprite(poleTexture);
            var cloth = MakeSprite(clothTexture);
            var centre = (left + right) * 0.5f;

            // Taking turns between the two back edges, each edge's banners evenly spaced.
            var onLeft = (banners.Count + 1) / 2;
            var onRight = banners.Count / 2;
            for (var i = 0; i < banners.Count; i++)
            {
                var isLeft = i % 2 == 0;
                var end = isLeft ? left : right;
                var share = (i / 2 + 1f) / ((isLeft ? onLeft : onRight) + 1f);
                var outward = ((top + end) * 0.5f - centre).normalized * Outside;
                var foot = Vector2.Lerp(top, end, share) + outward;

                var poleRenderer = AddSprite(root, "Banner pole", foot, pole);
                poleRenderer.sharedMaterial = BattleScenery.PerTexture(poleMaterial, poleTexture);
                poleRenderer.color = new Color(1f, 0f, 0f, 1f);

                var clothRenderer = AddSprite(root, $"Banner {Banners.Name(banners[i])}", foot, cloth);
                var material = new Material(shader) { mainTexture = clothTexture };
                material.SetColor("_Tint", Colours[banners[i]] * clothLight);
                // Each moves to its own time, so they don't sway in step.
                material.SetFloat("_Phase", i * 1.9f);
                clothRenderer.sharedMaterial = material;
                clothRenderer.sortingOrder = poleRenderer.sortingOrder + 1;
            }
        }

        // The full rectangle, not a tight outline: the cloth moves outside its painted shape.
        private static Sprite MakeSprite(Texture2D texture) =>
            Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Foot, texture.height / Height, 0, SpriteMeshType.FullRect);

        private static SpriteRenderer AddSprite(Transform root, string name, Vector2 position, Sprite sprite)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            sr.transform.position = position;
            sr.sprite = sprite;
            sr.sortingOrder = IsoMath.SortingOrder(sr.transform.position) + 5;
            return sr;
        }
    }
}
