using System.Collections.Generic;
using System.Linq;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// The army's banners, standing along the back edges of the roof behind the player's side. One painted post and
    /// cloth serve every banner: the crossbar runs along the edge it stands on (mirrored on the right edge), each
    /// banner has its own colour ramp, and the cloth moves in the wind (BannerCloth.shader).
    /// </summary>
    public static class BannerView
    {
        /// <summary>The painting's full height in world units (a tile is 1 wide); the post itself is two thirds of it.</summary>
        private const float ImageHeight = 2.1f;

        /// <summary>How far outside the board's edge the posts stand.</summary>
        private const float Outside = 0.14f;

        // Where the post's foot is in the painting.
        private static readonly Vector2 Foot = new Vector2(0.645f, 0.215f);

        // Dark to light. The shadows lean cool and dark like the statues', so the cloth sits in the scene.
        private static readonly Dictionary<Banner, string[]> Ramps = new Dictionary<Banner, string[]>
        {
            [Banner.Vampiric] = new[] { "#1c0a14", "#4a0f1a", "#7e1a20", "#a8322e", "#c85a48" },
            [Banner.ThirdWheel] = new[] { "#0e1626", "#1c3050", "#2f5480", "#4f7aa4", "#86a8c4" },
            [Banner.Loser] = new[] { "#16161c", "#32343c", "#56585e", "#7e8084", "#a8a8a6" },
            [Banner.Mercenaries] = new[] { "#0c1a18", "#163a2c", "#25603f", "#428556", "#76aa78" },
            [Banner.BatteringRam] = new[] { "#1e100e", "#4e2414", "#8c481c", "#b86c2c", "#dc9a52" },
        };

        /// <param name="poleMaterial">The scenery's palette material, so the post's stone matches the roof.</param>
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

                var poleRenderer = AddSprite(root, "Banner post", foot, pole, !isLeft);
                poleRenderer.sharedMaterial = BattleScenery.PerTexture(poleMaterial, poleTexture);
                poleRenderer.color = new Color(1f, 0f, 0f, 1f);

                var clothRenderer = AddSprite(root, $"Banner {Banners.Name(banners[i])}", foot, cloth, !isLeft);
                clothRenderer.sharedMaterial = ClothMaterial(shader, clothTexture, banners[i], clothLight);
                clothRenderer.sortingOrder = poleRenderer.sortingOrder + 1;
            }
        }

        // The banner's part of the painting (with room for the cloth to move), as a share of the image from its bottom left.
        private static readonly Rect CardCrop = new Rect(0.156f, 0.2f, 0.6f, 0.69f);

        // The crop's width over its height, in pixels of the 768 x 1376 painting.
        private const float CardCropAspect = 0.6f * 768f / (0.69f * 1376f);

        // The shader's pass for screens, which stays inside scroll views.
        private const int GuiPass = 1;

        private static readonly Dictionary<Banner, Material> CardMaterials = new Dictionary<Banner, Material>();

        /// <summary>The banner, swaying, fitted into the area on a screen. Call from OnGUI.</summary>
        public static void DrawCardArt(Rect area, Banner banner)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var poleTexture = Resources.Load<Texture2D>("Banners/pole");
            var clothTexture = Resources.Load<Texture2D>("Banners/cloth");
            var shader = Resources.Load<Shader>("Shaders/BannerCloth");
            if (poleTexture == null || clothTexture == null || shader == null)
                return;
            if (!CardMaterials.TryGetValue(banner, out var material) || material == null)
                CardMaterials[banner] = material = ClothMaterial(shader, clothTexture, banner, 1f);

            var height = Mathf.Min(area.height, area.width / CardCropAspect);
            var width = height * CardCropAspect;
            var rect = new Rect(area.center.x - width * 0.5f, area.center.y - height * 0.5f, width, height);
            GUI.DrawTextureWithTexCoords(rect, poleTexture, CardCrop);
            Graphics.DrawTexture(rect, clothTexture, CardCrop, 0, 0, 0, 0, new Color(1f, 1f, 1f, GUI.color.a), material, GuiPass);
        }

        private static Material ClothMaterial(Shader shader, Texture2D texture, Banner banner, float light)
        {
            var material = new Material(shader) { mainTexture = texture };
            material.SetTexture("_Ramp", BattleScenery.MakeRamp(BattleScenery.ParseStops(Ramps[banner])));
            material.SetFloat("_Light", light);
            // Each moves to its own time, so they don't sway in step.
            material.SetFloat("_Phase", (int)banner * 1.9f);
            return material;
        }

        // The full rectangle, not a tight outline: the cloth moves outside its painted shape.
        private static Sprite MakeSprite(Texture2D texture) =>
            Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Foot, texture.height / ImageHeight, 0, SpriteMeshType.FullRect);

        private static SpriteRenderer AddSprite(Transform root, string name, Vector2 position, Sprite sprite, bool mirrored)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            sr.transform.position = position;
            sr.sprite = sprite;
            sr.flipX = mirrored;
            sr.sortingOrder = IsoMath.SortingOrder(sr.transform.position) + 5;
            return sr;
        }
    }
}
