using System.Collections.Generic;
using UnityEngine;

namespace ArmyRoguelike.View
{
    /// <summary>
    /// Everything around the board: a greyscale backdrop coloured by a palette, the board's stone sides
    /// (so it reads as a plateau) and a dark strip of rocks along the bottom of the screen. All of it
    /// goes through one palette ramp, so a new palette changes the whole mood.
    /// </summary>
    public static class BattleScenery
    {
        public static readonly string[] PaletteNames = { "Amber dusk", "Toxic green", "Cold night" };

        // Each palette runs from its darkest to its brightest colour.
        private static readonly string[][] Palettes =
        {
            new[] { "#110c0c", "#2d201c", "#4c3a2c", "#6f6343", "#9a9567", "#f0b850" },
            new[] { "#12141c", "#232a3a", "#34463c", "#4f6b3a", "#7fa046", "#c9ec7a" },
            new[] { "#0b0f18", "#16223a", "#25384f", "#3a5268", "#5c7486", "#c48d8c" },
        };

        private const float SlabDepth = 0.65f;
        private const int BackdropOrder = -32000;
        private const int SlabOrder = -1500;
        private const int ForegroundOrder = 31000;
        private static readonly Color NeutralGround = new Color(0.5f, 0.48f, 0.44f);

        public static int PaletteCount => Palettes.Length;

        /// <summary>Builds the scenery for a framed board. Call after the camera has been framed.</summary>
        public static void Apply(Camera cam, BoardView board, int palette, bool pixelateBackdrop = false)
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

            var backdrop = Resources.Load<Texture2D>("Backdrops/ruins");
            if (backdrop != null)
            {
                var aspect = (float)backdrop.width / backdrop.height;
                var w = Mathf.Max(viewW, viewH * aspect) * 1.04f;
                var h = w / aspect;
                var material = NewMaterial(shader, backdrop, ramp);
                material.SetFloat("_PixelSize", pixelateBackdrop ? 6f : 0f);
                var bl = centre - new Vector2(w, h) * 0.5f;
                Quad(root, "Backdrop", material, BackdropOrder, bl, bl + new Vector2(w, 0f), bl + new Vector2(w, h), bl + new Vector2(0f, h), 1f);
            }

            BuildSlab(root, board, NewMaterial(shader, StoneBlocks(), ramp));
            BuildForeground(root, NewMaterial(shader, Texture2D.whiteTexture, ramp), centre, viewW, viewH);

            // Darker than the sandstone statues, so they stand out on it.
            board.SetGroundTint(Color.Lerp(Sample(stops, 0.5f), NeutralGround, 0.45f));
            board.SetRockTint(Color.Lerp(Color.white, Sample(stops, 0.7f), 0.55f));
        }

        // ---------- Pieces ----------

        /// <summary>The two front sides of the board, hanging below its front edges.</summary>
        private static void BuildSlab(Transform root, BoardView board, Material material)
        {
            var w = board.Battle.Board.Width;
            var h = board.Battle.Board.Height;
            var left = (Vector2)IsoMath.GridToWorld(-0.5f, h - 0.5f);
            var bottom = (Vector2)IsoMath.GridToWorld(-0.5f, -0.5f);
            var right = (Vector2)IsoMath.GridToWorld(w - 0.5f, -0.5f);
            var down = Vector2.down * SlabDepth;
            // Light comes from the top left, as on the statues: the left side is the lit one.
            Quad(root, "Slab left", material, SlabOrder, left + down, bottom + down, bottom, left, 0.5f, h * 0.5f);
            Quad(root, "Slab right", material, SlabOrder, bottom + down, right + down, right, bottom, 0.33f, w * 0.5f);
        }

        /// <summary>Two layers of dark jagged rock along the bottom edge, higher towards the corners.</summary>
        private static void BuildForeground(Transform root, Material material, Vector2 centre, float viewW, float viewH)
        {
            var rng = new System.Random(7);
            var bottomY = centre.y - viewH * 0.5f - 0.05f;
            for (var layer = 0; layer < 2; layer++)
            {
                var verts = new List<Vector3>();
                var tris = new List<int>();
                var lift = layer == 0 ? 1.35f : 1f;
                for (var x = -viewW * 0.55f; x <= viewW * 0.55f + 0.01f; x += 0.22f)
                {
                    var edge = Mathf.Abs(x) / (viewW * 0.5f);
                    var height = (0.08f + 0.75f * edge * edge * edge + (float)rng.NextDouble() * 0.12f) * lift;
                    verts.Add(new Vector3(centre.x + x, bottomY));
                    verts.Add(new Vector3(centre.x + x, bottomY + height));
                    if (verts.Count >= 4)
                    {
                        var i = verts.Count - 4;
                        tris.AddRange(new[] { i, i + 1, i + 3, i, i + 3, i + 2 });
                    }
                }

                var value = layer == 0 ? 0.14f : 0.02f;
                var colors = new Color[verts.Count];
                for (var i = 0; i < colors.Length; i++)
                    colors[i] = new Color(value, 0f, 0f, 1f);
                var mesh = new Mesh { name = "Foreground" };
                mesh.SetVertices(verts);
                mesh.SetTriangles(tris, 0);
                mesh.colors = colors;
                mesh.uv = new Vector2[verts.Count];
                AddRenderer(root, $"Foreground {layer}", mesh, material, ForegroundOrder + layer);
            }
        }

        // ---------- Helpers ----------

        /// <summary>A quad from four corners (counter-clockwise from bottom left), its brightness scaled by value.</summary>
        private static void Quad(Transform root, string name, Material material, int order, Vector2 a, Vector2 b, Vector2 c, Vector2 d, float value, float uRepeat = 1f)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new Vector3[] { a, b, c, d };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(uRepeat, 0f), new Vector2(uRepeat, 1f), new Vector2(0f, 1f) };
            var col = new Color(value, 0f, 0f, 1f);
            mesh.colors = new[] { col, col, col, col };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            AddRenderer(root, name, mesh, material, order);
        }

        private static void AddRenderer(Transform root, string name, Mesh mesh, Material material, int order)
        {
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

        /// <summary>Greyscale stone blocks in two staggered courses, tiling sideways.</summary>
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
    }
}
