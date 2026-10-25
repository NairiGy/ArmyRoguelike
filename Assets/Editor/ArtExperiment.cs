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
/// Batch: -executeMethod ArtExperiment.RunFromCommandLine -artOut dir [-artImport] [-artScenery] [-artPalette] [-artPlayerColour] [-artNoPedestal] [-artGlow] [-artBraziers] [-artGrid] [-art2DSet] [-artYawTest] [-artYawTripo 90] [-artYawMeshy 90]
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
            ImportTowerArt();
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

        if (args.Contains("-artImport"))
        {
            ImportStatues();
            ImportTowerArt();
            EditorApplication.Exit(0);
            return;
        }

        if (args.Contains("-artPalette"))
        {
            // Rows: night, then amber dusk. Columns: as now, statues through team ramps (a), and the whole
            // screen snapped to one small palette (b).
            var rows = new List<List<Texture2D>>();
            foreach (var palette in new[] { 2, 0 })
            {
                var row = new List<Texture2D>();
                foreach (var ramps in new[] { false, true })
                {
                    BattleScenery.StatueRamps = ramps;
                    var path = Path.Combine(outDir, $"palette_{palette}_{(ramps ? "a_ramps" : "now")}.png");
                    PreviewCapture.Capture(path, true, 0, palette);
                    var shot = new Texture2D(2, 2);
                    shot.LoadImage(File.ReadAllBytes(path));
                    row.Add(Downscale(shot.GetPixels(), shot.width, shot.height, 360));
                    if (ramps)
                        continue;
                    var snapped = SnapToPalette(shot, BattleScenery.ScreenPalette(palette));
                    Save(snapped, Path.Combine(outDir, $"palette_{palette}_b_snapped.png"));
                    row.Add(null);
                    row[row.Count - 1] = Downscale(snapped.GetPixels(), snapped.width, snapped.height, 360);
                }

                // Order the columns: now, (a), (b).
                rows.Add(new List<Texture2D> { row[0], row[2], row[1] });
            }

            BattleScenery.StatueRamps = true;
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
            Save(sheet, Path.Combine(outDir, "palette_comparison.png"));
            EditorApplication.Exit(0);
            return;
        }

        if (args.Contains("-artPlayerColour"))
        {
            // The player's statues in the current ramp and each candidate, as a 2x2 sheet.
            var shots = new List<Texture2D>();
            for (var option = -1; option < BattleScenery.PlayerRampOptions.Length; option++)
            {
                BattleScenery.PlayerRampOption = option;
                var name = option < 0 ? "current" : BattleScenery.PlayerRampOptions[option].name.Replace(' ', '_');
                var path = Path.Combine(outDir, $"player_{option + 1}_{name}.png");
                PreviewCapture.Capture(path, true);
                var shot = new Texture2D(2, 2);
                shot.LoadImage(File.ReadAllBytes(path));
                shots.Add(Downscale(shot.GetPixels(), shot.width, shot.height, 540));
            }

            BattleScenery.PlayerRampOption = 0;
            var w = shots[0].width;
            var h = shots[0].height;
            var sheet = new Texture2D(w * 2, h * 2, TextureFormat.RGB24, false);
            for (var i = 0; i < shots.Count; i++)
                sheet.SetPixels(i % 2 * w, (1 - i / 2) * h, w, h, shots[i].GetPixels());
            sheet.Apply();
            Save(sheet, Path.Combine(outDir, "player_colours.png"));
            EditorApplication.Exit(0);
            return;
        }

        if (args.Contains("-artNoPedestal"))
        {
            // The warrior and wolves as now, and redrawn standing on a thin slab instead of a pedestal.
            var shots = new List<Texture2D>();
            foreach (var bare in new[] { false, true })
            {
                PlaceholderArt.StatueOverrides.Clear();
                if (bare)
                {
                    var warrior = CutOut2D("warrior_nopedestal.png", "warrior_nopedestal");
                    var wolf = CutOut2D("wolf_nopedestal.png", "wolf_nopedestal");
                    Override(UnitKind.Warrior, warrior, outDir);
                    Override(UnitKind.Wolf, wolf, outDir);
                    Override(UnitKind.PackLeader, wolf, outDir);
                }

                var path = Path.Combine(outDir, bare ? "pedestal_off.png" : "pedestal_on.png");
                PreviewCapture.Capture(path, true);
                var shot = new Texture2D(2, 2);
                shot.LoadImage(File.ReadAllBytes(path));
                shots.Add(Downscale(shot.GetPixels(), shot.width, shot.height, 540));
            }

            PlaceholderArt.StatueOverrides.Clear();
            Save(SideBySide(shots), Path.Combine(outDir, "pedestal_comparison.png"));
            EditorApplication.Exit(0);
            return;
        }

        if (args.Contains("-artGlow"))
        {
            // The statues with a plain moonlight rim and with team-coloured glow, side by side.
            var shots = new List<Texture2D>();
            foreach (var on in new[] { false, true })
            {
                BattleScenery.TeamGlow = on;
                var path = Path.Combine(outDir, on ? "glow_on.png" : "glow_off.png");
                PreviewCapture.Capture(path, true);
                var shot = new Texture2D(2, 2);
                shot.LoadImage(File.ReadAllBytes(path));
                shots.Add(Downscale(shot.GetPixels(), shot.width, shot.height, 540));
            }

            BattleScenery.TeamGlow = true;
            Save(SideBySide(shots), Path.Combine(outDir, "glow_comparison.png"));
            EditorApplication.Exit(0);
            return;
        }

        if (args.Contains("-artBraziers"))
        {
            // The main battlefield without and with the lit braziers, side by side.
            var shots = new List<Texture2D>();
            foreach (var on in new[] { false, true })
            {
                BattleScenery.Braziers = on;
                var path = Path.Combine(outDir, on ? "braziers_on.png" : "braziers_off.png");
                PreviewCapture.Capture(path, true);
                var shot = new Texture2D(2, 2);
                shot.LoadImage(File.ReadAllBytes(path));
                shots.Add(Downscale(shot.GetPixels(), shot.width, shot.height, 540));
            }

            BattleScenery.Braziers = true;
            Save(SideBySide(shots), Path.Combine(outDir, "braziers_comparison.png"));
            EditorApplication.Exit(0);
            return;
        }

        if (args.Contains("-artGrid"))
        {
            // The main battlefield with no grid, a very soft one and the current one, side by side.
            var shots = new List<Texture2D>();
            foreach (var alpha in new[] { 0f, 0.12f, 0.35f })
            {
                BattleScenery.GridAlpha = alpha;
                var path = Path.Combine(outDir, $"grid_{alpha:0.00}.png");
                PreviewCapture.Capture(path, true);
                var shot = new Texture2D(2, 2);
                shot.LoadImage(File.ReadAllBytes(path));
                shots.Add(Downscale(shot.GetPixels(), shot.width, shot.height, 480));
            }

            Save(SideBySide(shots), Path.Combine(outDir, "grid_comparison.png"));
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
            SaveSprite(StatueFolder + kind + ".png", tex, pivot, ppu);
            Debug.Log($"Imported statue {kind} from {file}");
        }
    }

    private const string TowerFolder = "Assets/Art/Environment/Resources/Tower/";
    // Rocks are scaled so their width is this much of a tile.
    private const float RockWidth = 0.9f;
    private const float BrazierWidth = 0.6f;

    /// <summary>
    /// The tower roof's painted pieces: the three obstacles on green (Source/tower_rocks_sheet.png) become
    /// rock_0..2, and the four top-down slabs (Source/tower_floor_slabs.png) become isometric tiles slab_0..3.
    /// </summary>
    [MenuItem("Tools/Art/Import Tower Art")]
    public static void ImportTowerArt()
    {
        Directory.CreateDirectory(TowerFolder);
        var sheet = LoadSource("tower_rocks_sheet.png");
        var i = 0;
        foreach (var (tex, pivot, widthPx) in KeyOutGreen(sheet))
            SaveSprite(TowerFolder + $"rock_{i++}.png", tex, pivot, widthPx / RockWidth);

        if (File.Exists(Source + "tower_brazier.png"))
            foreach (var (tex, pivot, widthPx) in KeyOutGreen(LoadSource("tower_brazier.png")))
                SaveSprite(TowerFolder + "brazier.png", tex, pivot, widthPx / BrazierWidth);

        // The tower on its own (same framing as the painting), so clouds can pass behind it, and the
        // cloud strips that do.
        const string backdrops = "Assets/Art/Environment/Resources/Backdrops/";
        if (File.Exists(Source + "iso_tower_only.png"))
        {
            var only = LoadSource("iso_tower_only.png");
            var layer = new Texture2D(only.width, only.height, TextureFormat.RGBA32, true);
            layer.SetPixels(KeyGreen(only));
            layer.Apply(true);
            SaveTexture(backdrops + "iso_tower_layer.png", layer, false);
        }

        foreach (var band in new[] { "puffy", "wispy" })
            if (File.Exists(Source + $"clouds_band_{band}.png"))
                SaveTexture(backdrops + $"cloud_{band}.png", SeamlessStrip(LoadSource($"clouds_band_{band}.png")), true);

        var slabs = LoadSource("tower_floor_slabs.png");
        for (var q = 0; q < 4; q++)
        {
            // Quadrants in reading order; texture rows start at the bottom.
            var half = slabs.width / 2;
            var square = TrimDark(slabs.GetPixels(q % 2 * half, (1 - q / 2) * half, half, half), half);
            SaveSprite(TowerFolder + $"slab_{q}.png", IsoTile(square), new Vector2(0.5f, 0.5f), IsoTileWidth);
        }
    }

    private static Texture2D LoadSource(string file)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(Source + file));
        return tex;
    }

    /// <summary>
    /// Removes a pure green background and returns each object (they stand in one row with gaps), with
    /// its pivot at the centre of its footprint and its width in pixels.
    /// </summary>
    /// <summary>The image's pixels with its pure green background made transparent.</summary>
    private static Color[] KeyGreen(Texture2D image)
    {
        var px = image.GetPixels();
        for (var j = 0; j < px.Length; j++)
        {
            var c = px[j];
            var greenness = c.g - Mathf.Max(c.r, c.b);
            if (greenness > 0.25f)
            {
                px[j] = Color.clear;
                continue;
            }

            // Edge pixels pick up green from the background: soften and de-tint them.
            if (greenness > 0.08f)
                px[j].a = 1f - (greenness - 0.08f) / 0.17f;
            px[j].g = Mathf.Min(c.g, Mathf.Max(c.r, c.b));
        }

        return px;
    }

    /// <summary>
    /// A cloud strip from a green-screen image: keyed, cropped to the band's rows, and made to repeat
    /// seamlessly by cross-fading its last columns into its first.
    /// </summary>
    private static Texture2D SeamlessStrip(Texture2D image)
    {
        var w = image.width;
        var h = image.height;
        var px = KeyGreen(image);
        int minY = h, maxY = 0;
        for (var j = 0; j < px.Length; j++)
            if (px[j].a > 0.05f)
            {
                minY = Mathf.Min(minY, j / w);
                maxY = Mathf.Max(maxY, j / w);
            }

        var fade = w / 8;
        var outW = w - fade;
        var outH = maxY - minY + 1;
        var strip = new Texture2D(outW, outH, TextureFormat.RGBA32, true);
        for (var y = 0; y < outH; y++)
        for (var x = 0; x < outW; x++)
        {
            var c = px[(y + minY) * w + x];
            if (x < fade)
                c = Color.Lerp(px[(y + minY) * w + outW + x], c, (float)x / fade);
            strip.SetPixel(x, y, c);
        }

        strip.Apply(true);
        return strip;
    }

    /// <summary>Saves a plain texture (not a sprite) that repeats sideways, e.g. a cloud strip or a scene layer.</summary>
    private static void SaveTexture(string path, Texture2D tex, bool repeatSideways)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapModeU = repeatSideways ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.wrapModeV = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static List<(Texture2D tex, Vector2 pivot, float widthPx)> KeyOutGreen(Texture2D sheet)
    {
        var w = sheet.width;
        var h = sheet.height;
        var px = KeyGreen(sheet);
        var column = new bool[w];
        for (var j = 0; j < px.Length; j++)
            if (px[j].a > 0.5f)
                column[j % w] = true;

        var result = new List<(Texture2D, Vector2, float)>();
        for (var x0 = 0; x0 < w; x0++)
        {
            if (!column[x0])
                continue;
            var x1 = x0;
            while (x1 + 1 < w && column[x1 + 1])
                x1++;
            if (x1 - x0 > 40)
            {
                int minY = h, maxY = 0;
                for (var y = 0; y < h; y++)
                for (var x = x0; x <= x1; x++)
                    if (px[y * w + x].a > 0.5f)
                    {
                        minY = Mathf.Min(minY, y);
                        maxY = Mathf.Max(maxY, y);
                    }

                var cw = x1 - x0 + 1;
                var ch = maxY - minY + 1;
                var crop = new Color[cw * ch];
                for (var y = 0; y < ch; y++)
                    Array.Copy(px, (y + minY) * w + x0, crop, y * cw, cw);
                var tex = Downscale(crop, cw, ch, Mathf.Min(ch, SpriteHeightPx));
                // An isometric footprint is half as tall as it is wide; its centre is a quarter-width up.
                var pivot = new Vector2(0.5f, Mathf.Min(0.45f, cw * 0.25f / ch));
                result.Add((tex, pivot, cw * (float)tex.height / ch));
            }

            x0 = x1;
        }

        return result;
    }

    /// <summary>Crops away the dark gap lines around a slab cut from the 2x2 sheet.</summary>
    private static Color[] TrimDark(Color[] square, int size)
    {
        bool Dark(int x, int y) => square[y * size + x].grayscale < 0.12f;
        int lo = 0, hi = size - 1;
        while (lo < size / 4 && (Dark(lo, size / 2) || Dark(size / 2, lo)))
            lo++;
        while (hi > size * 3 / 4 && (Dark(hi, size / 2) || Dark(size / 2, hi)))
            hi--;
        var n = hi - lo + 1;
        var trimmed = new Color[n * n];
        for (var y = 0; y < n; y++)
            Array.Copy(square, (y + lo) * size + lo, trimmed, y * n, n);
        return trimmed;
    }

    private const int IsoTileWidth = 256;

    /// <summary>Lays a square, top-down texture onto a 2:1 isometric diamond the width of one tile.</summary>
    private static Texture2D IsoTile(Color[] square)
    {
        var n = (int)Mathf.Sqrt(square.Length);
        var src = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        src.SetPixels(square);
        src.Apply();
        const int w = IsoTileWidth, h = IsoTileWidth / 2;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            // Screen offset from the centre, in half-tile units, back to the square's (u, v).
            var a = (x + 0.5f - w * 0.5f) / (w * 0.5f);
            var b = (y + 0.5f - h * 0.5f) / (h * 0.5f);
            var u = (a + b + 1f) * 0.5f;
            var v = (b - a + 1f) * 0.5f;
            var inside = u >= 0f && u <= 1f && v >= 0f && v <= 1f;
            tex.SetPixel(x, y, inside ? src.GetPixelBilinear(u, v) : Color.clear);
        }

        tex.Apply();
        return tex;
    }

    private static void SaveSprite(string path, Texture2D tex, Vector2 pivot, float ppu)
    {
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

    /// <summary>Replaces every pixel with the nearest palette colour (weighted RGB distance), no dithering.</summary>
    private static Texture2D SnapToPalette(Texture2D src, Color[] palette)
    {
        var px = src.GetPixels();
        for (var i = 0; i < px.Length; i++)
        {
            var c = px[i];
            var best = palette[0];
            var bestD = float.MaxValue;
            foreach (var p in palette)
            {
                float dr = c.r - p.r, dg = c.g - p.g, db = c.b - p.b;
                var d = 0.3f * dr * dr + 0.59f * dg * dg + 0.11f * db * db;
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }

            px[i] = best;
        }

        var tex = new Texture2D(src.width, src.height, TextureFormat.RGB24, false);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
