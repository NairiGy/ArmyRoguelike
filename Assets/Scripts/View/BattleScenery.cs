using System.Collections.Generic;
using ArmyRoguelike.Core;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Everything around the board, which is the roof of a tall tower above the clouds: a greyscale sky
    /// and cloud sea behind, the tower's walls dropping into haze, battlements on the back edges and a
    /// drifting band of cloud in front. All of it goes through one palette ramp, so a new palette changes
    /// the whole mood.
    /// </summary>
    public static class BattleScenery
    {
        /// <summary>
        /// Where the battle takes place. TowerSky builds a tower from pieces in front of a flat sky picture;
        /// the others are single isometric paintings whose empty central platform the board is fitted onto.
        /// </summary>
        public enum Setting { TowerSky, IsoTower, IsoTemple, IsoGraveyard, IsoVoid, IsoFields, IsoSea }

        public static readonly string[] PaletteNames = { "Amber dusk", "Toxic green", "Cold night", "Void", "Harvest", "Storm stone" };

        /// <summary>What battles use: the isometric tower at night. The other settings and palettes are kept for later.</summary>
        public const Setting MainSetting = Setting.IsoTower;
        public const int MainPalette = 2;

        /// <summary>
        /// Edge of the World battles swap the main setting for the void: the tower's roof hanging over a
        /// black hole, with no clouds below, in the void palette.
        /// </summary>
        public const int VoidPalette = 3;

        /// <summary>Fortune's Favored battles: the tower over golden wheat fields in daylight.</summary>
        public const int FieldsPalette = 4;

        /// <summary>Storm battles: the tower over a stormy sea, in cold wet stone.</summary>
        public const int SeaPalette = 5;

        /// <summary>Colour painted statues through their team's ramp of the current palette, so units match the scene.</summary>
        public static bool StatueRamps = true;

        // Per palette, the ramps statues are drawn with: the player's warm, light stone and the enemy's
        // dark, cool stone, so the teams stay apart in every palette.
        private static readonly string[][] PlayerRamps =
        {
            new[] { "#2a1a12", "#6b4a2e", "#b08850", "#e8c88a", "#fff0c8" },
            new[] { "#2a2620", "#6a6048", "#b0a478", "#e2d8b0", "#fbf6e0" },
            new[] { "#2a2030", "#6a5060", "#b08a80", "#e6c0a0", "#fff0d8" },
            new[] { "#241e30", "#5a4e6a", "#9c8ea8", "#d8cce0", "#fbf6ff" },
            new[] { "#2a1a12", "#6b4a2e", "#b08850", "#e8c88a", "#fff0c8" },
            new[] { "#2a2030", "#6a5060", "#b08a80", "#e6c0a0", "#fff0d8" },
        };

        /// <summary>Player ramp choices for the night palette, and which one to use (-1 = the palette's own).</summary>
        public static readonly (string name, string[] ramp)[] PlayerRampOptions =
        {
            ("Moon marble", new[] { "#1e2433", "#4a5670", "#8a9ab4", "#c8d4e6", "#f4f8ff" }),
            ("Torch gold", new[] { "#2a1c14", "#6a4a2c", "#b8844a", "#e8bc78", "#fff0c0" }),
            ("Pale bone", new[] { "#26241e", "#5e5a4c", "#a09a84", "#d8d2bc", "#f8f4e6" }),
        };

        // Moon marble is the main look; torch gold and pale bone stay as alternatives.
        public static int PlayerRampOption = 0;

        /// <summary>
        /// Enemy factions whose stone leans towards a colour of their own (a hint, not a paint job), so the
        /// teams still read apart by brightness first. Goblins (and their totem) lean mossy green.
        /// </summary>
        private static readonly (UnitKind[] kinds, Color tint)[] FactionTints =
        {
            (new[] { UnitKind.Goblin, UnitKind.GoblinBomber, UnitKind.Totem }, new Color(0.45f, 0.62f, 0.3f)),
        };

        /// <summary>How far a faction's stone moves towards its colour (0 = plain enemy stone).</summary>
        public static float FactionTintAmount = 0.45f;

        /// <summary>Each stop moved towards the tint at the stop's own brightness, so only the hue changes.</summary>
        private static Color[] Tinted(Color[] stops, Color tint, float amount)
        {
            var tintLuma = Luma(tint);
            var tinted = new Color[stops.Length];
            for (var i = 0; i < stops.Length; i++)
            {
                var target = tint * (Luma(stops[i]) / tintLuma);
                target.a = 1f;
                tinted[i] = Color.Lerp(stops[i], target, amount);
            }

            return tinted;
        }

        private static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        private static readonly string[][] EnemyRamps =
        {
            new[] { "#1a1212", "#3a2a28", "#5c4a44", "#8a7468", "#b8a490" },
            new[] { "#14181c", "#2a3434", "#465652", "#6a8278", "#98b0a0" },
            new[] { "#10141e", "#222e40", "#3a4a60", "#5e7290", "#90a4c0" },
            new[] { "#100e1a", "#231e36", "#3b3452", "#5f5678", "#9288b0" },
            new[] { "#140f0c", "#2e2420", "#4c3e36", "#74624e", "#a89276" },
            new[] { "#10141e", "#222e40", "#3a4a60", "#5e7290", "#90a4c0" },
        };

        /// <summary>
        /// Palette test: every colour of a palette (scenery, both teams' stone, and the health-bar colours),
        /// for snapping the whole screen to it.
        /// </summary>
        public static Color[] ScreenPalette(int palette)
        {
            var p = ((palette % Palettes.Length) + Palettes.Length) % Palettes.Length;
            var colours = new List<Color>();
            colours.AddRange(ParseStops(Palettes[p]));
            colours.AddRange(ParseStops(PlayerRamps[p]));
            colours.AddRange(ParseStops(EnemyRamps[p]));
            colours.Add(new Color(0.35f, 0.8f, 0.4f));
            colours.Add(new Color(0.85f, 0.3f, 0.25f));
            colours.Add(new Color(0.1f, 0.1f, 0.12f));
            colours.Add(new Color(0.97f, 0.95f, 0.9f));
            return colours.ToArray();
        }

        /// <summary>Size of every unit (statue, base, shadow, bars) relative to the art's own size. Live via ArtTuning.</summary>
        public static float UnitScale = 0.7f;

        /// <summary>
        /// Figure-only statue art (no painted pedestal) stands on a stone base drawn in code, square to the tile.
        /// Off: figures stand straight on the floor (the chosen look; a base can be added back later).
        /// </summary>
        public static bool DrawnBases;

        /// <summary>Statue rims glow in team colours (pale blue for the player, ember red for enemies).</summary>
        public static bool TeamGlow = true;

        /// <summary>Lit braziers on the board's side corners, where the setting has brazier art.</summary>
        public static bool Braziers = true;

        /// <summary>Opacity of the tile outlines on painted floors; 0 draws none.</summary>
        public static float GridAlpha = 0f;

        /// <summary>An isometric painting and where its empty platform's corners are, in image pixels from the top left.</summary>
        private struct IsoScene
        {
            public string Texture;
            public Vector2 Top, Left, Bottom, Right;
            // Resources folder with this setting's painted floor slabs (slab_*) and rocks (rock_*), if any.
            public string ArtFolder;
            // A sky with clouds: parallax cloud layers drift past (needs TowerLayer), and dust motes.
            public bool Clouds;
            // The painting's tower alone (same framing), drawn over the clouds that pass behind it.
            public string TowerLayer;
            // The void: Texture is the void painting behind the tower layer (same framing), whose walls fade
            // out from FadeFrom to FadeTo (image rows); Hole is the black hole's centre and radius in the painting.
            public bool Void;
            public float FadeFrom, FadeTo;
            public Vector3 Hole;
            // Land or sea far below: Texture is a full-colour painting filling the screen behind the tower layer,
            // whose walls fade into haze of brightness Haze from FadeFrom to FadeTo; cloud shadows and gusts sweep it.
            public bool FarBelow;
            public float Haze;
            // No braziers lit.
            public bool Daylight;
            // A FarBelow sea: its painting flows like water except where this mask (same framing) is dark, swirling
            // around Swirls (painting pixels: x, y, radius, turn; turn 0 just keeps whitecaps off, e.g. a ship).
            // Storm cloud drifts over it in layers, like the night sky's (the painting itself has none).
            public string WaterMask;
            public Vector4[] Swirls;
        }

        private static readonly Dictionary<Setting, IsoScene> IsoScenes = new Dictionary<Setting, IsoScene>
        {
            [Setting.IsoTower] = new IsoScene { Texture = "Backdrops/iso_tower", Top = new Vector2(838, 155), Left = new Vector2(410, 388), Bottom = new Vector2(842, 632), Right = new Vector2(1262, 380), ArtFolder = "Tower", Clouds = true, TowerLayer = "Backdrops/iso_tower_layer" },
            [Setting.IsoTemple] = new IsoScene { Texture = "Backdrops/iso_temple", Top = new Vector2(745, 220), Left = new Vector2(355, 430), Bottom = new Vector2(880, 735), Right = new Vector2(1265, 470) },
            [Setting.IsoGraveyard] = new IsoScene { Texture = "Backdrops/iso_graveyard", Top = new Vector2(840, 210), Left = new Vector2(325, 470), Bottom = new Vector2(905, 765), Right = new Vector2(1330, 490) },
            // The void painting is framed like the tower painting, so the tower layer fits it as it is.
            [Setting.IsoVoid] = new IsoScene { Texture = "Backdrops/void_backdrop", Top = new Vector2(838, 155), Left = new Vector2(410, 388), Bottom = new Vector2(842, 632), Right = new Vector2(1262, 380), ArtFolder = "Tower", TowerLayer = "Backdrops/iso_tower_layer", Void = true, FadeFrom = 640, FadeTo = 770, Hole = new Vector3(415, 191, 57) },
            [Setting.IsoFields] = new IsoScene { Texture = "Backdrops/wheat_backdrop_colour", Top = new Vector2(838, 155), Left = new Vector2(410, 388), Bottom = new Vector2(842, 632), Right = new Vector2(1262, 380), ArtFolder = "Tower", TowerLayer = "Backdrops/iso_tower_layer", FarBelow = true, Daylight = true, Haze = 0.72f, FadeFrom = 620, FadeTo = 800 },
            [Setting.IsoSea] = new IsoScene { Texture = "Backdrops/sea_backdrop_clear", Top = new Vector2(838, 155), Left = new Vector2(410, 388), Bottom = new Vector2(842, 632), Right = new Vector2(1262, 380), ArtFolder = "Tower", TowerLayer = "Backdrops/iso_tower_layer", FarBelow = true, Haze = 0.55f, FadeFrom = 620, FadeTo = 800, WaterMask = "Backdrops/sea_backdrop_mask",
                Swirls = new[] { new Vector4(930, 500, 150, 1.2f), new Vector4(115, 540, 90, -1f), new Vector4(750, 685, 40, 0f), new Vector4(1145, 440, 30, 0f) } },
        };

        /// <summary>The ring image's empty middle, as a fraction of its width: fitted over the painted hole.</summary>
        private const float RingHoleFraction = 0.235f;

        // Each palette runs from its darkest to its brightest colour.
        private static readonly string[][] Palettes =
        {
            new[] { "#110c0c", "#2d201c", "#4c3a2c", "#6f6343", "#9a9567", "#f0b850" },
            new[] { "#12141c", "#232a3a", "#34463c", "#4f6b3a", "#7fa046", "#c9ec7a" },
            new[] { "#0b0f18", "#16223a", "#25384f", "#3a5268", "#5c7486", "#c48d8c" },
            // Void: black through indigo and violet stone to the pale gold of the light around the hole.
            new[] { "#05040a", "#151129", "#2b2347", "#4a3f68", "#7a6e98", "#e6cf9a" },
            // Harvest: sunlit stone over coloured fields (the land keeps its own colours), warm greys up to near-white.
            new[] { "#1e1a16", "#3e3832", "#6a6056", "#988a78", "#c8b89c", "#fff6e2" },
            // Storm stone: cold wet greys over the stormy sea, up to the white of foam and cloud.
            new[] { "#0e1114", "#22282d", "#3e464d", "#626b72", "#959ea4", "#e8ecee" },
        };

        private const float WallDepth = 2f;
        private const float HazeValue = 0.8f;
        private const int BackdropOrder = -32000;
        private const int BackCloudOrder = -31000;
        private const int TowerLayerOrder = -30000;
        private const int WallOrder = -1500;
        private const int FrontCloudOrder = 31000;
        private static readonly Color NeutralGround = new Color(0.5f, 0.48f, 0.44f);

        public static int PaletteCount => Palettes.Length;

        private static readonly Dictionary<(Material, Texture), Material> PerTextureMaterials = new Dictionary<(Material, Texture), Material>();

        /// <summary>
        /// A copy of a sprite material for one texture. Sprites with different textures must not share a
        /// custom material: Unity batched them and drew some sprites with another sprite's texture.
        /// </summary>
        public static Material PerTexture(Material material, Texture texture)
        {
            if (material == null || texture == null)
                return material;
            if (!PerTextureMaterials.TryGetValue((material, texture), out var copy) || copy == null)
                PerTextureMaterials[(material, texture)] = copy = new Material(material) { mainTexture = texture };
            return copy;
        }

        /// <summary>Builds the scenery for a framed board. Call after the camera has been framed.</summary>
        public static void Apply(Camera cam, BoardView board, int palette, bool pixelateBackdrop = false, Setting setting = Setting.TowerSky)
        {
            if (setting == MainSetting && board.Battle.EdgeKills)
            {
                setting = Setting.IsoVoid;
                palette = VoidPalette;
            }
            else if (setting == MainSetting && board.Battle.Field == Battlefield.FortunesFavored)
            {
                setting = Setting.IsoFields;
                palette = FieldsPalette;
            }
            else if (setting == MainSetting && board.Battle.Field == Battlefield.Storm)
            {
                setting = Setting.IsoSea;
                palette = SeaPalette;
            }

            var p =((palette % Palettes.Length) + Palettes.Length) % Palettes.Length;
            var stops = ParseStops(Palettes[p]);
            var shader = Resources.Load<Shader>("Shaders/PaletteRamp");
            if (shader == null)
                return;
            var ramp = MakeRamp(stops);

            var statueShader = Resources.Load<Shader>("Shaders/StatueRamp");
            if (StatueRamps && statueShader != null)
            {
                // The art's own brightness range: sandstone is light, basalt dark; each fills its whole ramp.
                var playerRamp = PlayerRampOption >= 0 ? PlayerRampOptions[PlayerRampOption].ramp : PlayerRamps[p];
                var player = NewMaterial(statueShader, Texture2D.whiteTexture, MakeRamp(ParseStops(playerRamp)));
                player.SetFloat("_LumMin", 0.3f);
                player.SetFloat("_LumMax", 0.95f);
                var enemy = NewMaterial(statueShader, Texture2D.whiteTexture, MakeRamp(ParseStops(EnemyRamps[p])));
                enemy.SetFloat("_LumMin", 0.1f);
                enemy.SetFloat("_LumMax", 0.5f);
                // A thin pulsing rim picks the statues out of the dark floor: with team glow it is the team's
                // magic colour, otherwise the palette's brightest colour.
                var moon = Sample(stops, 1f);
                player.SetColor("_RimColor", TeamGlow ? new Color(0.6f, 0.85f, 1f, 0.85f) : new Color(moon.r, moon.g, moon.b, 0.6f));
                enemy.SetColor("_RimColor", TeamGlow ? new Color(1f, 0.4f, 0.2f, 0.85f) : new Color(moon.r, moon.g, moon.b, 0.6f));
                // Factions lean their enemy stone towards a hue of their own, keeping its darkness.
                var byKind = new Dictionary<UnitKind, Material>();
                foreach (var (kinds, tint) in FactionTints)
                {
                    var faction = new Material(enemy);
                    faction.SetTexture("_Ramp", MakeRamp(Tinted(ParseStops(EnemyRamps[p]), tint, FactionTintAmount)));
                    foreach (var kind in kinds)
                        byKind[kind] = faction;
                }

                board.SetStatueMaterials(player, enemy, byKind);
            }
            var root = new GameObject("Scenery").transform;
            root.gameObject.AddComponent<ArtTuning>();
            root.SetParent(board.transform, false);
            cam.backgroundColor = stops[0];

            var viewH = cam.orthographicSize * 2f;
            var viewW = viewH * Mathf.Max(cam.aspect, 16f / 9f);
            var centre = (Vector2)cam.transform.position;
            BuildVignette(root, centre, viewW, viewH, stops[0]);

            if (IsoScenes.TryGetValue(setting, out var scene))
            {
                var tower = scene.TowerLayer != null ? Resources.Load<Texture2D>(scene.TowerLayer) : null;
                var sky = Resources.Load<Texture2D>("Backdrops/sky_panorama");
                var puffy = Resources.Load<Texture2D>("Backdrops/cloud_puffy");
                var wispy = Resources.Load<Texture2D>("Backdrops/cloud_wispy");
                if (scene.Void && tower != null)
                {
                    // The void painting behind, the tower's walls dissolving into it, and its black hole's
                    // ring of light turning over the painted hole; debris drifts in, specks peel off the roof.
                    var voidMaterial = NewMaterial(shader, Resources.Load<Texture2D>(scene.Texture), ramp);
                    // The painting stops short of the screen's top: its top rows fade into the background colour.
                    BuildIsoScene(root, board, scene, voidMaterial, BackdropOrder, (0f, Value(1f, 0f)), (140f, Value(1f)));
                    BuildIsoScene(root, board, scene, NewMaterial(shader, tower, ramp), TowerLayerOrder, (scene.FadeFrom, Value(1f)), (scene.FadeTo, Value(1f, 0f)));
                    var map = ImageToWorld(board, scene);
                    var hole = map(scene.Hole.x, scene.Hole.y);
                    var holeRadius = (map(scene.Hole.x + scene.Hole.z, scene.Hole.y) - hole).magnitude;
                    var corners = BoardCorners(board);
                    var rockArt = System.Array.FindAll(Resources.LoadAll<Sprite>(scene.ArtFolder), s => s.name.StartsWith("rock_"));
                    SceneryVoid.Create(root, Resources.Load<Texture2D>("Backdrops/void_ring"), RingHoleFraction, hole, holeRadius,
                        rockArt, NewMaterial(shader, Texture2D.whiteTexture, ramp), new Material(Resources.Load<Shader>("Shaders/Glow")),
                        Sample(stops, 1f), new[] { corners.top, corners.left, corners.bottom, corners.right }, centre, viewW, viewH,
                        BackdropOrder + 1, BackCloudOrder, TowerLayerOrder + 1);
                }
                else if (scene.FarBelow && tower != null && wispy != null)
                {
                    // The land far below fills the screen (it needn't line up with the tower), in its own colours
                    // rather than the palette: blue river, green hedges, white clouds. Thin cloud drifts between it
                    // and the tower, whose walls sink into the bright haze.
                    var land = Resources.Load<Texture2D>(scene.Texture);
                    if (land != null)
                    {
                        var w = Mathf.Max(viewW, viewH * land.width / land.height) * 1.04f;
                        var sr = new GameObject("Land").AddComponent<SpriteRenderer>();
                        sr.transform.SetParent(root, false);
                        sr.transform.position = centre;
                        sr.sprite = Sprite.Create(land, new Rect(0, 0, land.width, land.height), new Vector2(0.5f, 0.5f), land.width / w);
                        sr.sortingOrder = BackdropOrder;
                        var mask = scene.WaterMask != null ? Resources.Load<Texture2D>(scene.WaterMask) : null;
                        if (mask != null)
                            SceneryWater.Attach(sr, mask, scene.Swirls ?? new Vector4[0], BackdropOrder + 3);
                    }

                    var screenBottom = centre.y - viewH * 0.5f;
                    SceneryFields.Create(root, centre, viewW, viewH, stops[0], Sample(stops, 1f), BackdropOrder + 1);
                    if (scene.WaterMask != null && puffy != null)
                    {
                        // Storm cloud drifting over the sea on its own layers, the farther the slower.
                        var screenTop = centre.y + viewH * 0.5f;
                        CloudBand(root, "Storm wisps", NewMaterial(shader, wispy, ramp), BackCloudOrder + 1, centre.x, viewW, screenTop - 1.7f, 0.9f, 0.8f, 0.0016f, 0.7f);
                        CloudBand(root, "Storm clouds", NewMaterial(shader, puffy, ramp), BackCloudOrder + 2, centre.x, viewW, screenTop - 3.2f, 1.2f, 0.82f, 0.0035f, 0.8f);
                    }
                    CloudBand(root, "Wisps below", NewMaterial(shader, wispy, ramp), BackCloudOrder, centre.x, viewW, screenBottom - 0.2f, 1.6f, 1f, 0.003f, 0.55f);
                    BuildIsoScene(root, board, scene, NewMaterial(shader, tower, ramp), TowerLayerOrder,
                        (scene.FadeFrom, new Color(1f, 0f, scene.Haze, 1f)), (scene.FadeTo, new Color(1f, 1f, scene.Haze, 1f)));
                }
                else if (scene.Clouds && tower != null && sky != null && puffy != null && wispy != null)
                {
                    // Everything in the sky moves. Parallax: a seamless sky panorama and cloud layers slide right
                    // with one wind, the farther (higher, fainter, smaller) the slower; all of them pass behind
                    // the tower, which is on its own layer.
                    var screenTop = centre.y + viewH * 0.5f;
                    var screenBottom = centre.y - viewH * 0.5f;
                    var skyMaterial = NewMaterial(shader, sky, ramp);
                    CloudBand(root, "Sky panorama", skyMaterial, BackdropOrder, centre.x, viewW, screenBottom - 0.3f, viewH + 0.6f, 1f, 0.0004f, 1f);
                    // The clear panorama's horizon is about 64% of the way down the image.
                    var horizon = screenBottom - 0.3f + (viewH + 0.6f) * 0.36f;
                    SkyFeatures.Create(root.gameObject, skyMaterial, sky, Resources.Load<Texture2D>("Backdrops/sky_clear_panorama"),
                        centre, viewW, viewH, horizon, Sample(stops, 1f), stops[0], NewMaterial(shader, Texture2D.whiteTexture, ramp),
                        new Material(Resources.Load<Shader>("Shaders/Glow")));
                    CloudBand(root, "Far wisps", NewMaterial(shader, wispy, ramp), BackCloudOrder, centre.x, viewW, screenTop - 1.9f, 0.7f, 0.8f, 0.0013f, 0.75f);
                    CloudBand(root, "Middle (behind tower)", NewMaterial(shader, puffy, ramp), BackCloudOrder + 1, centre.x, viewW, screenTop - 3.4f, 1.1f, 0.88f, 0.003f, 0.85f);
                    CloudBand(root, "Near (behind tower)", NewMaterial(shader, puffy, ramp), BackCloudOrder + 2, centre.x, viewW, screenBottom + 0.4f, 1.6f, 0.95f, 0.0055f, 0.95f);
                    BuildIsoScene(root, board, scene, NewMaterial(shader, tower, ramp), TowerLayerOrder);
                }
                else
                    BuildIsoScene(root, board, scene, NewMaterial(shader, Resources.Load<Texture2D>(scene.Texture), ramp));

                // Dust motes at night; chaff blown up from the fields by day.
                if (scene.Clouds || scene.FarBelow)
                    SceneryMotes.Create(root, centre, viewW, viewH, Sample(stops, 0.95f));

                var art = scene.ArtFolder != null ? Resources.LoadAll<Sprite>(scene.ArtFolder) : new Sprite[0];
                var slabs = System.Array.FindAll(art, s => s.name.StartsWith("slab_"));
                var rocks = System.Array.FindAll(art, s => s.name.StartsWith("rock_"));
                if (slabs.Length > 0)
                {
                    // Lifted towards light (sprite colours clamp at 1, so through the haze channel) so the slab
                    // joints read as the grid; rocks lifted more so they stand out from the floor.
                    var spriteMaterial = NewMaterial(shader, Texture2D.whiteTexture, ramp);
                    board.SetTileArt(slabs, spriteMaterial, new Color(1f, 0.25f, 0.9f, 1f));
                    var line = Sample(stops, 0.85f);
                    line.a = GridAlpha;
                    if (GridAlpha > 0f)
                        board.SetGridLines(line);
                    if (rocks.Length > 0)
                        board.SetRockArt(rocks, spriteMaterial, new Color(1f, 0.55f, 1f, 1f));
                    var brazier = System.Array.Find(art, s => s.name == "brazier");
                    // No fires lit in daylight.
                    if (Braziers && brazier != null && !scene.Daylight)
                        BuildBraziers(root, board, brazier, spriteMaterial);
                    var (roofTop, roofLeft, _, roofRight) = BoardCorners(board);
                    BannerView.Create(root, board.Battle.Banners, roofTop, roofLeft, roofRight, spriteMaterial, scene.Daylight ? 1f : 0.8f);
                    return;
                }

                // The painting has its own floor: tiles only tint it lightly, so their edges read as a grid.
                var tint = Color.Lerp(Sample(stops, 0.5f), NeutralGround, 0.45f);
                tint.a = 0.3f;
                board.SetGroundTint(tint);
                board.SetRockTint(Color.Lerp(Color.white, Sample(stops, 0.7f), 0.55f));
                return;
            }

            var backdrop = Resources.Load<Texture2D>("Backdrops/clouds");
            if (backdrop != null)
            {
                var aspect = (float)backdrop.width / backdrop.height;
                var w = Mathf.Max(viewW, viewH * aspect) * 1.04f;
                var h = w / aspect;
                var material = NewMaterial(shader, backdrop, ramp);
                material.SetFloat("_PixelSize", pixelateBackdrop ? 6f : 0f);
                var bl = centre - new Vector2(w, h) * 0.5f;
                Quad(root, "Backdrop", material, BackdropOrder, bl, bl + new Vector2(w, 0f), bl + new Vector2(w, h), bl + new Vector2(0f, h), Value(1f));
            }

            var clouds = CloudTexture();
            var bottom = centre.y - viewH * 0.5f;
            // Behind the tower the cloud sea rises around its walls; in front a lower band drifts the other way.
            CloudBand(root, "Back clouds", NewMaterial(shader, clouds, ramp), BackCloudOrder, centre.x, viewW, bottom, 2.2f, 0.85f, 0.006f, 1f);
            CloudBand(root, "Front clouds", NewMaterial(shader, clouds, ramp), FrontCloudOrder, centre.x, viewW, bottom - 0.1f, 1.1f, 1f, 0.01f, 1f);

            BuildWalls(root, board, NewMaterial(shader, StoneBlocks(), ramp));
            BuildBattlements(root, board, NewMaterial(shader, MerlonTexture().texture, ramp));

            // Darker than the sandstone statues, so they stand out on it.
            board.SetGroundTint(Color.Lerp(Sample(stops, 0.5f), NeutralGround, 0.45f));
            board.SetRockTint(Color.Lerp(Color.white, Sample(stops, 0.7f), 0.55f));
        }

        // ---------- Pieces ----------

        /// <summary>
        /// Places an isometric painting so its platform lies under the board: the best affine fit of the
        /// platform's four corners onto the board's four corners (the painting's angle is never exactly 2:1).
        /// </summary>
        /// <param name="fades">
        /// Optional (image row, vertex colour) pairs, top to bottom: the painting's opacity and haze (see
        /// <see cref="Value"/>, <see cref="Haze"/>) blend between them.
        /// </param>
        private static void BuildIsoScene(Transform root, BoardView board, IsoScene scene, Material material, int order = BackdropOrder,
            params (float row, Color color)[] fades)
        {
            var tex = (Texture2D)material.mainTexture;
            if (tex == null)
                return;
            var map = ImageToWorld(board, scene);
            if (fades.Length == 0)
            {
                // Image rows run downwards; the quad goes counter-clockwise from the image's bottom left.
                Quad(root, "Scene", material, order, map(0, tex.height), map(tex.width, tex.height), map(tex.width, 0), map(0, 0), Value(1f));
                return;
            }

            // Horizontal bands from the image's top to its bottom, colours blending between the given rows.
            var stops = new List<(float row, Color color)> { (0f, fades[0].color) };
            stops.AddRange(fades);
            stops.Add((tex.height, fades[fades.Length - 1].color));
            var vertices = new Vector3[stops.Count * 2];
            var uv = new Vector2[stops.Count * 2];
            var colors = new Color[stops.Count * 2];
            var triangles = new List<int>();
            for (var i = 0; i < stops.Count; i++)
            {
                var (row, color) = stops[i];
                vertices[i * 2] = map(0, row);
                vertices[i * 2 + 1] = map(tex.width, row);
                uv[i * 2] = new Vector2(0f, 1f - row / tex.height);
                uv[i * 2 + 1] = new Vector2(1f, 1f - row / tex.height);
                colors[i * 2] = colors[i * 2 + 1] = color;
                if (i > 0)
                    triangles.AddRange(new[] { i * 2 - 2, i * 2, i * 2 + 1, i * 2 - 2, i * 2 + 1, i * 2 - 1 });
            }

            var mesh = new Mesh { name = "Scene", vertices = vertices, uv = uv, colors = colors };
            mesh.triangles = triangles.ToArray();
            var go = new GameObject("Scene");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingOrder = order;
        }

        /// <summary>Where a pixel of the setting's painting lands in the world, with its platform fitted under the board.</summary>
        private static System.Func<float, float, Vector2> ImageToWorld(BoardView board, IsoScene scene)
        {
            var c = BoardCorners(board);
            var image = new[] { scene.Top, scene.Left, scene.Bottom, scene.Right };
            var world = new[] { c.top, c.left, c.bottom, c.right };
            var fx = FitAffine(image, world, p => p.x);
            var fy = FitAffine(image, world, p => p.y);
            return (px, py) => new Vector2(fx.x * px + fx.y * py + fx.z, fy.x * px + fy.y * py + fy.z);
        }

        /// <summary>Least-squares (a, b, c) with a*px + b*py + c ≈ target(world) over the point pairs.</summary>
        private static Vector3 FitAffine(Vector2[] image, Vector2[] world, System.Func<Vector2, float> target)
        {
            // Normal equations of the 3-parameter linear fit, solved by Cramer's rule.
            double sxx = 0, sxy = 0, sx = 0, syy = 0, sy = 0, n = image.Length, tx = 0, ty = 0, t = 0;
            for (var i = 0; i < image.Length; i++)
            {
                double x = image[i].x, y = image[i].y, v = target(world[i]);
                sxx += x * x; sxy += x * y; sx += x; syy += y * y; sy += y;
                tx += x * v; ty += y * v; t += v;
            }

            double Det(double a, double b, double c, double d, double e, double f, double g, double hh, double k) =>
                a * (e * k - f * hh) - b * (d * k - f * g) + c * (d * hh - e * g);
            var det = Det(sxx, sxy, sx, sxy, syy, sy, sx, sy, n);
            return new Vector3(
                (float)(Det(tx, sxy, sx, ty, syy, sy, t, sy, n) / det),
                (float)(Det(sxx, tx, sx, sxy, ty, sy, sx, t, n) / det),
                (float)(Det(sxx, sxy, tx, sxy, syy, ty, sx, sy, t) / det));
        }

        /// <summary>The tower's two front walls, hanging below the board's front edges and fading into haze.</summary>
        private static void BuildWalls(Transform root, BoardView board, Material material)
        {
            var (_, left, bottom, right) = BoardCorners(board);
            var down = Vector2.down * WallDepth;
            var courses = WallDepth / 0.65f;
            // Light comes from the top left, as on the statues: the left wall is the lit one.
            Quad(root, "Wall left", material, WallOrder, left + down, bottom + down, bottom, left, Value(0.5f), Tiles(left, bottom) * 0.5f, courses, Haze(0.5f));
            Quad(root, "Wall right", material, WallOrder, bottom + down, right + down, right, bottom, Value(0.33f), Tiles(bottom, right) * 0.5f, courses, Haze(0.33f));
        }

        /// <summary>The board's outer corners as they appear on screen, whichever way the grid runs.</summary>
        private static (Vector2 top, Vector2 left, Vector2 bottom, Vector2 right) BoardCorners(BoardView board)
        {
            var w = board.Battle.Board.Width;
            var h = board.Battle.Board.Height;
            var corners = new[]
            {
                (Vector2)IsoMath.GridToWorld(-0.5f, -0.5f), (Vector2)IsoMath.GridToWorld(w - 0.5f, -0.5f),
                (Vector2)IsoMath.GridToWorld(-0.5f, h - 0.5f), (Vector2)IsoMath.GridToWorld(w - 0.5f, h - 0.5f),
            };
            Vector2 Most(System.Func<Vector2, float> key)
            {
                var best = corners[0];
                foreach (var p in corners)
                    if (key(p) > key(best))
                        best = p;
                return best;
            }

            return (Most(p => p.y), Most(p => -p.x), Most(p => -p.y), Most(p => p.x));
        }

        /// <summary>How many tiles long a board edge between two corners is.</summary>
        private static int Tiles(Vector2 a, Vector2 b) =>
            Mathf.RoundToInt(Vector2.Distance(a, b) / ((Vector2)IsoMath.GridToWorld(1f, 0f) - (Vector2)IsoMath.GridToWorld(0f, 0f)).magnitude);

        /// <summary>Merlons along the two back edges, every other tile, straddling the edge.</summary>
        private static void BuildBattlements(Transform root, BoardView board, Material material)
        {
            var (top, left, _, right) = BoardCorners(board);
            var centre = (left + right) * 0.5f;
            var sprite = MerlonTexture();
            var spots = new List<Vector2> { top + (top - centre).normalized * 0.08f };
            foreach (var end in new[] { left, right })
            {
                var n = Tiles(top, end);
                var outward = ((top + end) * 0.5f - centre).normalized * 0.08f;
                for (var i = 0; i < n; i += 2)
                    spots.Add(Vector2.Lerp(top, end, (i + 0.5f) / n) + outward);
            }

            foreach (var s in spots)
            {
                var sr = new GameObject("Merlon").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root, false);
                sr.transform.position = s;
                sr.sprite = sprite;
                sr.sharedMaterial = PerTexture(material, sprite.texture);
                sr.color = Value(1f);
                sr.sortingOrder = IsoMath.SortingOrder(sr.transform.position) + 5;
            }
        }

        /// <summary>A lit brazier just outside each of the board's side corners.</summary>
        private static void BuildBraziers(Transform root, BoardView board, Sprite sprite, Material material)
        {
            var (_, left, _, right) = BoardCorners(board);
            var centre = (left + right) * 0.5f;
            var glow = new Material(Resources.Load<Shader>("Shaders/Glow"));
            foreach (var corner in new[] { left, right })
            {
                var pos = corner + (corner - centre).normalized * 0.3f;
                var sr = new GameObject("Brazier").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root, false);
                sr.transform.position = pos;
                sr.sprite = sprite;
                sr.sharedMaterial = PerTexture(material, sprite.texture);
                sr.color = new Color(1f, 0.6f, 1f, 1f);
                sr.sortingOrder = IsoMath.SortingOrder(sr.transform.position) + 5;
                // The bowl's rim is about two thirds of the way up the sprite.
                SceneryFire.Create(root, pos, sprite.bounds.max.y * 0.72f, sr.sortingOrder, glow);
            }
        }

        /// <summary>Darkens the screen's edges and corners towards the palette's darkest colour.</summary>
        private static void BuildVignette(Transform root, Vector2 centre, float viewW, float viewH, Color dark)
        {
            const int w = 128, h = 72;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var dx = (x + 0.5f) / w * 2f - 1f;
                var dy = (y + 0.5f) / h * 2f - 1f;
                var d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy);
                tex.SetPixel(x, y, new Color(dark.r, dark.g, dark.b, Mathf.SmoothStep(0f, 0.6f, Mathf.InverseLerp(0.75f, 1.35f, d))));
            }

            tex.Apply();
            var sr = new GameObject("Vignette").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            sr.transform.position = centre;
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w / viewW);
            sr.transform.localScale = new Vector3(1f, viewH / (h * viewW / w), 1f);
            sr.sortingOrder = 30000;
        }

        /// <summary>A band of drifting cloud across the screen, soft along its top edge.</summary>
        /// <param name="speed">How fast it drifts right, in screen widths per second (roughly).</param>
        /// <param name="alpha">Opacity: far layers are fainter, as if seen through haze.</param>
        private static void CloudBand(Transform root, string name, Material material, int order, float centreX, float viewW, float bottomY, float height, float value, float speed, float alpha)
        {
            // All bands live on one component, so they can be tuned in the Inspector while the game runs.
            var clouds = root.GetComponent<SceneryClouds>();
            if (clouds == null)
                clouds = root.gameObject.AddComponent<SceneryClouds>();
            clouds.Add(name, material, order, centreX, viewW * 1.1f, bottomY, height, value, speed, alpha);
        }

        // ---------- Helpers ----------

        private static Color Value(float v, float alpha = 1f) => new Color(v, 0f, 0f, alpha);

        /// <summary>Vertex colour for the bottom of a wall: fully faded into the haze.</summary>
        private static Color Haze(float v) => new Color(v, 1f, HazeValue, 1f);

        /// <summary>
        /// A quad from four corners (counter-clockwise from bottom left). Its vertex colour sets brightness
        /// (and haze, see the shader); the bottom two corners can take a different colour.
        /// </summary>
        private static void Quad(Transform root, string name, Material material, int order, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
            Color col, float uRepeat = 1f, float vRepeat = 1f, Color? bottomCol = null)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new Vector3[] { a, b, c, d };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(uRepeat, 0f), new Vector2(uRepeat, vRepeat), new Vector2(0f, vRepeat) };
            var low = bottomCol ?? col;
            mesh.colors = new[] { low, low, col, col };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingOrder = order;
        }

        private static Material NewMaterial(Shader shader, Texture tex, Texture ramp)
        {
            var m = new Material(shader) { mainTexture = tex };
            m.SetTexture("_Ramp", ramp);
            return m;
        }

        private static Color[] ParseStops(string[] hex)
        {
            var stops = new Color[hex.Length];
            for (var i = 0; i < hex.Length; i++)
                ColorUtility.TryParseHtmlString(hex[i], out stops[i]);
            return stops;
        }

        private static Color Sample(Color[] stops, float t)
        {
            var f = Mathf.Clamp01(t) * (stops.Length - 1);
            var i = Mathf.Min(Mathf.FloorToInt(f), stops.Length - 2);
            return Color.Lerp(stops[i], stops[i + 1], f - i);
        }

        private static Texture2D MakeRamp(Color[] stops)
        {
            var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (var x = 0; x < 256; x++)
                tex.SetPixel(x, 0, Sample(stops, x / 255f));
            tex.Apply();
            return tex;
        }

        /// <summary>Greyscale stone blocks in two staggered courses, tiling both ways.</summary>
        private static Texture2D StoneBlocks()
        {
            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var rng = new System.Random(3);
            var shade = new float[8];
            for (var i = 0; i < shade.Length; i++)
                shade[i] = 0.85f + (float)rng.NextDouble() * 0.15f;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var course = y / (h / 2);
                var bx = (x + course * 16) % w;
                var mortar = y % (h / 2) == 0 || bx % 32 == 0;
                var v = mortar ? 0.45f : shade[course * 2 + bx / 32] - (y % (h / 2)) * 0.006f;
                tex.SetPixel(x, y, new Color(v, v, v, 1f));
            }

            tex.Apply();
            return tex;
        }

        private static Sprite _merlon;

        /// <summary>A small stone block, drawn in greyscale for the palette: lit top, lit left, shaded right.</summary>
        private static Sprite MerlonTexture()
        {
            if (_merlon != null)
                return _merlon;
            const int w = 26, h = 34, raise = 20;
            const float halfW = 12f, halfH = 6f;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(new Color32[w * h]);
            for (var lift = 0; lift <= raise; lift++)
            {
                var cy = halfH + 1f + lift;
                for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    if (Mathf.Abs(x + 0.5f - w * 0.5f) / halfW + Mathf.Abs(y + 0.5f - cy) / halfH > 1f)
                        continue;
                    var v = lift == raise ? 0.9f : x < w / 2 ? 0.7f : 0.48f;
                    tex.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            }

            tex.Apply();
            return _merlon = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, (halfH + 1f) / h), PlaceholderArt.PixelsPerUnit);
        }

        /// <summary>Soft cloud puffs that tile sideways and thin out towards the top.</summary>
        private static Texture2D CloudTexture()
        {
            const int w = 256, h = 128;
            // Repeats sideways only: wrapping vertically would bleed the dense bottom into the faded top.
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var rng = new System.Random(11);
            var lattice = new float[64, 64];
            for (var i = 0; i < 64; i++)
            for (var j = 0; j < 64; j++)
                lattice[i, j] = (float)rng.NextDouble();

            // Value noise that wraps horizontally after `cells` lattice cells.
            float Noise(float x, float y, int cells)
            {
                var x0 = Mathf.FloorToInt(x);
                var y0 = Mathf.FloorToInt(y);
                var fx = x - x0;
                var fy = y - y0;
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);
                float L(int i, int j) => lattice[((i % cells) + cells) % cells, ((j % 64) + 64) % 64];
                return Mathf.Lerp(Mathf.Lerp(L(x0, y0), L(x0 + 1, y0), fx), Mathf.Lerp(L(x0, y0 + 1), L(x0 + 1, y0 + 1), fx), fy);
            }

            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var u = (float)x / w;
                var v = (float)y / h;
                var n = Noise(u * 6f, v * 3f, 6) * 0.55f + Noise(u * 12f, v * 6f, 12) * 0.3f + Noise(u * 24f, v * 12f, 24) * 0.15f;
                var density = n * 0.9f + (1f - v) * 0.85f - 0.75f;
                // The top rows always fade out, so the band never ends in a hard line.
                var alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.2f, density)) * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.6f, 0.95f, v));
                // Puff tops catch the light; their insides are a little greyer.
                var lum = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(density * 1.6f)) * (0.9f + 0.1f * n);
                tex.SetPixel(x, y, new Color(lum, lum, lum, alpha));
            }

            tex.Apply();
            return tex;
        }
    }
}
