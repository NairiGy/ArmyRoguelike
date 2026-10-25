using System.Collections.Generic;
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
        public enum Setting { TowerSky, IsoTower, IsoTemple, IsoGraveyard }

        public static readonly string[] PaletteNames = { "Amber dusk", "Toxic green", "Cold night" };

        /// <summary>An isometric painting and where its empty platform's corners are, in image pixels from the top left.</summary>
        private struct IsoScene
        {
            public string Texture;
            public Vector2 Top, Left, Bottom, Right;
        }

        private static readonly Dictionary<Setting, IsoScene> IsoScenes = new Dictionary<Setting, IsoScene>
        {
            [Setting.IsoTower] = new IsoScene { Texture = "Backdrops/iso_tower", Top = new Vector2(838, 155), Left = new Vector2(410, 388), Bottom = new Vector2(842, 632), Right = new Vector2(1262, 380) },
            [Setting.IsoTemple] = new IsoScene { Texture = "Backdrops/iso_temple", Top = new Vector2(745, 220), Left = new Vector2(355, 430), Bottom = new Vector2(880, 735), Right = new Vector2(1265, 470) },
            [Setting.IsoGraveyard] = new IsoScene { Texture = "Backdrops/iso_graveyard", Top = new Vector2(840, 210), Left = new Vector2(325, 470), Bottom = new Vector2(905, 765), Right = new Vector2(1330, 490) },
        };

        // Each palette runs from its darkest to its brightest colour.
        private static readonly string[][] Palettes =
        {
            new[] { "#110c0c", "#2d201c", "#4c3a2c", "#6f6343", "#9a9567", "#f0b850" },
            new[] { "#12141c", "#232a3a", "#34463c", "#4f6b3a", "#7fa046", "#c9ec7a" },
            new[] { "#0b0f18", "#16223a", "#25384f", "#3a5268", "#5c7486", "#c48d8c" },
        };

        private const float WallDepth = 2f;
        private const float HazeValue = 0.8f;
        private const int BackdropOrder = -32000;
        private const int BackCloudOrder = -31000;
        private const int WallOrder = -1500;
        private const int FrontCloudOrder = 31000;
        private static readonly Color NeutralGround = new Color(0.5f, 0.48f, 0.44f);

        public static int PaletteCount => Palettes.Length;

        /// <summary>Builds the scenery for a framed board. Call after the camera has been framed.</summary>
        public static void Apply(Camera cam, BoardView board, int palette, bool pixelateBackdrop = false, Setting setting = Setting.TowerSky)
        {
            var stops = ParseStops(Palettes[((palette % Palettes.Length) + Palettes.Length) % Palettes.Length]);
            var shader = Resources.Load<Shader>("Shaders/PaletteRamp");
            if (shader == null)
                return;
            var ramp = MakeRamp(stops);
            var root = new GameObject("Scenery").transform;
            root.SetParent(board.transform, false);
            cam.backgroundColor = stops[0];

            var viewH = cam.orthographicSize * 2f;
            var viewW = viewH * Mathf.Max(cam.aspect, 16f / 9f);
            var centre = (Vector2)cam.transform.position;

            if (IsoScenes.TryGetValue(setting, out var scene))
            {
                BuildIsoScene(root, board, scene, NewMaterial(shader, Resources.Load<Texture2D>(scene.Texture), ramp));
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
            CloudBand(root, NewMaterial(shader, clouds, ramp), BackCloudOrder, centre.x, viewW, bottom, 2.2f, 0.85f, 0.006f);
            CloudBand(root, NewMaterial(shader, clouds, ramp), FrontCloudOrder, centre.x, viewW, bottom - 0.1f, 1.1f, 1f, -0.01f);

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
        private static void BuildIsoScene(Transform root, BoardView board, IsoScene scene, Material material)
        {
            var tex = (Texture2D)material.mainTexture;
            if (tex == null)
                return;
            var w = board.Battle.Board.Width;
            var h = board.Battle.Board.Height;
            var image = new[] { scene.Top, scene.Left, scene.Bottom, scene.Right };
            var world = new[]
            {
                (Vector2)IsoMath.GridToWorld(w - 0.5f, h - 0.5f), (Vector2)IsoMath.GridToWorld(-0.5f, h - 0.5f),
                (Vector2)IsoMath.GridToWorld(-0.5f, -0.5f), (Vector2)IsoMath.GridToWorld(w - 0.5f, -0.5f),
            };
            var fx = FitAffine(image, world, p => p.x);
            var fy = FitAffine(image, world, p => p.y);
            Vector2 Map(float px, float py) => new Vector2(fx.x * px + fx.y * py + fx.z, fy.x * px + fy.y * py + fy.z);
            // Image rows run downwards; the quad goes counter-clockwise from the image's bottom left.
            Quad(root, "Scene", material, BackdropOrder, Map(0, tex.height), Map(tex.width, tex.height), Map(tex.width, 0), Map(0, 0), Value(1f));
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
            var w = board.Battle.Board.Width;
            var h = board.Battle.Board.Height;
            var left = (Vector2)IsoMath.GridToWorld(-0.5f, h - 0.5f);
            var bottom = (Vector2)IsoMath.GridToWorld(-0.5f, -0.5f);
            var right = (Vector2)IsoMath.GridToWorld(w - 0.5f, -0.5f);
            var down = Vector2.down * WallDepth;
            var courses = WallDepth / 0.65f;
            // Light comes from the top left, as on the statues: the left wall is the lit one.
            Quad(root, "Wall left", material, WallOrder, left + down, bottom + down, bottom, left, Value(0.5f), h * 0.5f, courses, Haze(0.5f));
            Quad(root, "Wall right", material, WallOrder, bottom + down, right + down, right, bottom, Value(0.33f), w * 0.5f, courses, Haze(0.33f));
        }

        /// <summary>Merlons along the two back edges, every other tile, straddling the edge.</summary>
        private static void BuildBattlements(Transform root, BoardView board, Material material)
        {
            var w = board.Battle.Board.Width;
            var h = board.Battle.Board.Height;
            var sprite = MerlonTexture();
            var spots = new List<Vector2>();
            for (var i = 0; i < w; i += 2)
                spots.Add(new Vector2(i, h - 0.5f + 0.15f));
            for (var j = 0; j < h; j += 2)
                spots.Add(new Vector2(w - 0.5f + 0.15f, j));
            spots.Add(new Vector2(w - 0.5f + 0.15f, h - 0.5f + 0.15f));
            foreach (var s in spots)
            {
                var sr = new GameObject("Merlon").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root, false);
                sr.transform.position = IsoMath.GridToWorld(s.x, s.y);
                sr.sprite = sprite;
                sr.sharedMaterial = material;
                sr.color = Value(1f);
                sr.sortingOrder = IsoMath.SortingOrder(sr.transform.position) + 5;
            }
        }

        /// <summary>A band of drifting cloud across the screen, soft along its top edge.</summary>
        private static void CloudBand(Transform root, Material material, int order, float centreX, float viewW, float bottomY, float height, float value, float drift)
        {
            material.SetFloat("_Scroll", drift);
            var w = viewW * 1.1f;
            var bl = new Vector2(centreX - w * 0.5f, bottomY);
            Quad(root, "Clouds", material, order, bl, bl + new Vector2(w, 0f), bl + new Vector2(w, height), bl + new Vector2(0f, height), Value(value), w / (height * 2f));
        }

        // ---------- Helpers ----------

        private static Color Value(float v) => new Color(v, 0f, 0f, 1f);

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
