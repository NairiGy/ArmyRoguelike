using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArmyRoguelike.Core;
using ArmyRoguelike.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// Art experiment (branch experiment/art-style): turns the AI-generated warrior into sprites (a cut-out
/// 2D image, and 3D models baked from the board's isometric angle) and renders the same battle with each.
/// Batch: -executeMethod ArtExperiment.RunFromCommandLine -artOut dir [-artScenery] [-art2DSet] [-artYawTest] [-artYawTripo 90] [-artYawMeshy 90]
/// </summary>
public static class ArtExperiment
{
    private const string Source = "Assets/Art/Experiment/Source/";
    // Height of every baked 3D statue on the board, in world units (a tile is 1 wide).
    private const float StatueHeight = 1.3f;
    // Width of every 2D statue's pedestal on the board, so pedestals match whatever stands on them.
    private const float PedestalWidth = 0.6f;
    private const int SpriteHeightPx = 384;

    public static void RunFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        string Arg(string name, string fallback)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        var outDir = Arg("-artOut", "Temp/ArtExperiment");
        Directory.CreateDirectory(outDir);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");

        if (args.Contains("-artScenery"))
        {
            // The battle as it looks in the game, in every setting and palette, plus one overview sheet:
            // a row per setting, a column per palette.
            ImportStatues();
            var rows = new List<List<Texture2D>>();
            foreach (BattleScenery.Setting setting in Enum.GetValues(typeof(BattleScenery.Setting)))
            {
                var row = new List<Texture2D>();
                for (var p = 0; p < BattleScenery.PaletteCount; p++)
                {
                    var path = Path.Combine(outDir, $"scenery_{setting}_{p}.png");
                    PreviewCapture.Capture(path, true, 0, p, false, setting);
                    var shot = new Texture2D(2, 2);
                    shot.LoadImage(File.ReadAllBytes(path));
                    row.Add(Downscale(shot.GetPixels(), shot.width, shot.height, 360));
                }

                rows.Add(row);
            }

            var sheet = new Texture2D(rows[0].Sum(t => t.width), rows.Count * rows[0][0].height, TextureFormat.RGB24, false);
            for (var r = 0; r < rows.Count; r++)
            {
                var x = 0;
                foreach (var t in rows[r])
                {
                    sheet.SetPixels(x, sheet.height - (r + 1) * t.height, t.width, t.height, t.GetPixels());
                    x += t.width;
                }
            }

            sheet.Apply();
            Save(sheet, Path.Combine(outDir, "scenery_overview.png"));
            EditorApplication.Exit(0);
            return;
        }

        if (args.Contains("-art2DSet"))
        {
            // The 2D images as a set, to judge whether they match and whether the teams read apart.
            Override(UnitKind.Warrior, CutOut2D("warrior_iso.png", "a_2d_image"), outDir);
            Override(UnitKind.Archer, CutOut2D("archer_iso.png", "archer_2d_image"), outDir);
            if (File.Exists(Source + "wolf_iso.png"))
            {
                var wolf = CutOut2D("wolf_iso.png", "wolf_2d_image");
                Override(UnitKind.Wolf, wolf, outDir);
                Override(UnitKind.PackLeader, wolf, outDir);
            }

            var setPath = Path.Combine(outDir, "battle_2d_set.png");
            PreviewCapture.Capture(setPath, true);
            Save(CropAroundWarrior(setPath, 480), Path.Combine(outDir, "set_closeup.png"));
            PlaceholderArt.StatueOverrides.Clear();
            EditorApplication.Exit(0);
            return;
        }

        var tripo = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "warrior_tripo.fbx");
        var meshy = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "warrior_meshy.fbx");
        var tripoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "warrior_tripo_basecolor.jpg");
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Art/Experiment/StatueBake.shader");
        var textured = new Material(shader) { mainTexture = tripoTex };
        textured.SetFloat("_UseTex", 1f);
        var stone = new Material(shader);
        stone.SetColor("_Color", new Color(0.86f, 0.75f, 0.53f));

        if (args.Contains("-artYawTest"))
        {
            // Turntable of both models, to find which yaw faces down-left like the 2D image.
            foreach (var yaw in new[] { 0, 90, 180, 270 })
            {
                Save(Bake(tripo, yaw, stone).tex, Path.Combine(outDir, $"yaw_tripo_{yaw}.png"));
                Save(Bake(meshy, yaw, stone).tex, Path.Combine(outDir, $"yaw_meshy_{yaw}.png"));
            }

            EditorApplication.Exit(0);
            return;
        }

        var tripoYaw = float.Parse(Arg("-artYawTripo", "270"));
        var meshyYaw = float.Parse(Arg("-artYawMeshy", "270"));
        var variants = new List<(string name, Texture2D tex, Vector2 pivot, float ppu)>
        {
            CutOut2D("warrior_iso.png", "a_2d_image"),
            Named("b_tripo_textured", Bake(tripo, tripoYaw, textured)),
            Named("c_tripo_stone", Bake(tripo, tripoYaw, stone)),
            Named("d_meshy_stone", Bake(meshy, meshyYaw, stone)),
        };

        var crops = new List<Texture2D>();
        foreach (var art in variants)
        {
            Override(UnitKind.Warrior, art, outDir);
            var name = art.name;
            var battlePath = Path.Combine(outDir, $"battle_{name}.png");
            PreviewCapture.Capture(battlePath, true);
            crops.Add(CropAroundWarrior(battlePath));
        }

        PlaceholderArt.StatueOverrides.Clear();
        Save(SideBySide(crops), Path.Combine(outDir, "comparison.png"));
        EditorApplication.Exit(0);
    }

    private const string StatueFolder = "Assets/Art/Units/Resources/Statues/";

    /// <summary>
    /// Cuts out every Source/&lt;unit&gt;_iso.png whose name matches a unit type and saves it as that
    /// unit's painted statue sprite, which the game then uses instead of the placeholder.
    /// </summary>
    [MenuItem("Tools/Art/Import Statues")]
    public static void ImportStatues()
    {
        Directory.CreateDirectory(StatueFolder);
        foreach (var file in Directory.GetFiles(Source, "*_iso.png"))
        {
            var unit = Path.GetFileName(file).Replace("_iso.png", "");
            if (!Enum.TryParse<UnitKind>(unit, true, out var kind))
                continue;
            var (_, tex, pivot, ppu) = CutOut2D(Path.GetFileName(file), unit);
            var path = StatueFolder + kind + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spritePixelsPerUnit = ppu;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            Debug.Log($"Imported statue {kind} from {file}");
        }
    }

    private static void Override(UnitKind kind, (string name, Texture2D tex, Vector2 pivot, float ppu) art, string outDir)
    {
        Save(art.tex, Path.Combine(outDir, $"sprite_{art.name}.png"));
        PlaceholderArt.StatueOverrides[kind] = Sprite.Create(art.tex, new Rect(0, 0, art.tex.width, art.tex.height), art.pivot, art.ppu);
    }

    private static (string, Texture2D, Vector2, float) Named(string name, (Texture2D tex, Vector2 pivot) baked) =>
        (name, baked.tex, baked.pivot, baked.tex.height / StatueHeight);

    // ---------- 2D: cut the statue out of the generated image ----------

    private static (string, Texture2D, Vector2, float) CutOut2D(string file, string name)
    {
        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes(Source + file));
        var w = src.width;
        var h = src.height;
        var px = src.GetPixels();

        // Flood fill from the border through grey (unsaturated, light) pixels: that's the background
        // and the statue's soft shadow on it. The sandstone itself is clearly yellow.
        bool IsBackground(Color c) => Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b) < 0.09f && c.grayscale > 0.55f;
        var bg = new bool[w * h];
        var queue = new Queue<int>();
        for (var x = 0; x < w; x++)
        {
            queue.Enqueue(x);
            queue.Enqueue((h - 1) * w + x);
        }

        for (var y = 0; y < h; y++)
        {
            queue.Enqueue(y * w);
            queue.Enqueue(y * w + w - 1);
        }

        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            if (bg[i] || !IsBackground(px[i]))
                continue;
            bg[i] = true;
            var x = i % w;
            var y = i / w;
            if (x > 0) queue.Enqueue(i - 1);
            if (x < w - 1) queue.Enqueue(i + 1);
            if (y > 0) queue.Enqueue(i - w);
            if (y < h - 1) queue.Enqueue(i + w);
        }

        // Background enclosed by the statue (between a bowstring and the bow, under an arm) can't be
        // reached from the border; pure light grey anywhere is background too.
        for (var i = 0; i < px.Length; i++)
            if (Mathf.Max(px[i].r, px[i].g, px[i].b) - Mathf.Min(px[i].r, px[i].g, px[i].b) < 0.05f && px[i].grayscale > 0.68f)
                bg[i] = true;

        // Soften the edge: statue pixels touching the background get half alpha.
        int minX = w, minY = h, maxX = 0, maxY = 0;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            if (bg[i])
            {
                px[i] = Color.clear;
                continue;
            }

            var edge = (x > 0 && bg[i - 1]) || (x < w - 1 && bg[i + 1]) || (y > 0 && bg[i - w]) || (y < h - 1 && bg[i + w]);
            if (edge)
                px[i].a = 0.5f;
            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        var cw = maxX - minX + 1;
        var ch = maxY - minY + 1;
        var cropped = new Color[cw * ch];
        for (var y = 0; y < ch; y++)
            Array.Copy(px, (y + minY) * w + minX, cropped, y * cw, cw);

        // Find the pedestal: its widest row in the bottom quarter is its left and right corners. Those
        // corners are vertical edges, and the lowest widest row is level with the centre of the base.
        // Rows start at the bottom of the texture.
        int Span(int y, out int first)
        {
            first = -1;
            var last = -1;
            for (var x = 0; x < cw; x++)
            {
                if (cropped[y * cw + x].a < 0.5f)
                    continue;
                if (first < 0)
                    first = x;
                last = x;
            }

            return first < 0 ? 0 : last - first + 1;
        }

        var widest = 0;
        for (var y = 0; y < ch / 4; y++)
            widest = Mathf.Max(widest, Span(y, out _));
        var baseY = 0;
        var baseX = cw / 2f;
        for (var y = 0; y < ch / 4; y++)
        {
            var span = Span(y, out var first);
            if (span < widest * 0.98f)
                continue;
            baseY = y;
            baseX = first + span / 2f;
            break;
        }

        var tex = Downscale(cropped, cw, ch, SpriteHeightPx);
        var ppu = widest * ((float)tex.height / ch) / PedestalWidth;
        return (name, tex, new Vector2(baseX / cw, (float)baseY / ch), ppu);
    }

    // ---------- 3D: bake a model from the board's isometric angle ----------

    private static (Texture2D tex, Vector2 pivot) Bake(GameObject model, float yaw, Material material)
    {
        var go = Object.Instantiate(model);
        try
        {
            // Keep the importer's own root rotation (its Z-up to Y-up fix) and turn on top of it.
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f) * model.transform.rotation);
            var renderers = go.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            // The board is 2:1 isometric: 30 degrees down, 45 degrees round.
            var rot = Quaternion.Euler(30f, 45f, 0f);
            var camPos = bounds.center - rot * Vector3.forward * (bounds.extents.magnitude * 4f + 1f);
            var view = Matrix4x4.TRS(camPos, rot, new Vector3(1f, 1f, -1f)).inverse;

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            for (var c = 0; c < 8; c++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                var v = view.MultiplyPoint(corner);
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
                minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z);
            }

            var proj = Matrix4x4.Ortho(minX, maxX, minY, maxY, -maxZ - 0.1f, -minZ + 0.1f);
            var h = SpriteHeightPx * 2;
            var w = Mathf.CeilToInt(h * (maxX - minX) / (maxY - minY));
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };

            material.SetVector("_LightDir", rot * new Vector3(-0.75f, 0.6f, -0.15f));
            material.SetVector("_ViewDir", rot * Vector3.back);
            var cmd = new CommandBuffer();
            cmd.SetRenderTarget(rt);
            cmd.ClearRenderTarget(true, true, new Color(0.6f, 0.55f, 0.45f, 0f));
            cmd.SetViewProjectionMatrices(view, proj);
            foreach (var r in renderers)
            {
                Mesh mesh;
                Matrix4x4 matrix;
                if (r is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh();
                    skinned.BakeMesh(mesh);
                    matrix = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                }
                else
                {
                    mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    matrix = r.localToWorldMatrix;
                }

                for (var sub = 0; sub < mesh.subMeshCount; sub++)
                    cmd.DrawMesh(mesh, matrix, material, sub, 0);
            }

            Graphics.ExecuteCommandBuffer(cmd);
            RenderTexture.active = rt;
            var full = new Texture2D(w, h, TextureFormat.RGBA32, false);
            full.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = null;
            rt.Release();

            var basePoint = view.MultiplyPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            var pivot = new Vector2((basePoint.x - minX) / (maxX - minX), (basePoint.y - minY) / (maxY - minY));
            return (Downscale(full.GetPixels(), w, h, SpriteHeightPx), pivot);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    // ---------- Image helpers ----------

    /// <summary>Area-averaged downscale with premultiplied alpha, so edges don't get dark fringes.</summary>
    private static Texture2D Downscale(Color[] src, int sw, int sh, int targetH)
    {
        var th = Mathf.Min(targetH, sh);
        var tw = Mathf.Max(1, Mathf.RoundToInt(sw * (float)th / sh));
        var dst = new Color[tw * th];
        for (var y = 0; y < th; y++)
        for (var x = 0; x < tw; x++)
        {
            int x0 = x * sw / tw, x1 = Mathf.Max(x0 + 1, (x + 1) * sw / tw);
            int y0 = y * sh / th, y1 = Mathf.Max(y0 + 1, (y + 1) * sh / th);
            var sum = Vector4.zero;
            for (var sy = y0; sy < y1; sy++)
            for (var sx = x0; sx < x1; sx++)
            {
                var c = src[sy * sw + sx];
                sum += new Vector4(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
            }

            var n = (x1 - x0) * (y1 - y0);
            var a = sum.w / n;
            dst[y * tw + x] = a > 0f ? new Color(sum.x / sum.w, sum.y / sum.w, sum.z / sum.w, a) : Color.clear;
        }

        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels(dst);
        tex.Apply(true);
        return tex;
    }

    /// <summary>A crop of the battle render around the warrior (placed on tile 2,1), reaching to the right.</summary>
    private static Texture2D CropAroundWarrior(string battlePath, int width = 360)
    {
        var shot = new Texture2D(2, 2);
        shot.LoadImage(File.ReadAllBytes(battlePath));
        var cam = Camera.main;
        var rt = new RenderTexture(shot.width, shot.height, 0);
        cam.targetTexture = rt;
        var vp = cam.WorldToViewportPoint(IsoMath.GridToWorld(new GridPos(2, 1)));
        cam.targetTexture = null;
        rt.Release();

        const int size = 360;
        var cx = Mathf.Clamp(Mathf.RoundToInt(vp.x * shot.width) - size / 2, 0, shot.width - width);
        var cy = Mathf.Clamp(Mathf.RoundToInt(vp.y * shot.height) - 100, 0, shot.height - size);
        var crop = new Texture2D(width, size, TextureFormat.RGB24, false);
        crop.SetPixels(shot.GetPixels(cx, cy, width, size));
        crop.Apply();
        return crop;
    }

    private static Texture2D SideBySide(List<Texture2D> images)
    {
        const int gap = 8;
        var w = images.Sum(i => i.width) + gap * (images.Count - 1);
        var h = images.Max(i => i.height);
        var sheet = new Texture2D(w, h, TextureFormat.RGB24, false);
        sheet.SetPixels(Enumerable.Repeat(Color.black, w * h).ToArray());
        var x = 0;
        foreach (var img in images)
        {
            sheet.SetPixels(x, 0, img.width, img.height, img.GetPixels());
            x += img.width + gap;
        }

        sheet.Apply();
        return sheet;
    }

    private static void Save(Texture2D tex, string path) => File.WriteAllBytes(path, tex.EncodeToPNG());
}
