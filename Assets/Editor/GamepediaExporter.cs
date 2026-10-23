using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ArmyRoguelike.Core;
using ArmyRoguelike.View;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Writes docs/gamepedia.html from the game's data (see <see cref="GamepediaWriter"/>), with the
/// statue art embedded. Because it reads the real data it can't drift out of date; re-export after
/// changing numbers or content.
/// Menu: Tools/Export Gamepedia. Batch mode: -executeMethod GamepediaExporter.ExportFromCommandLine
/// </summary>
public static class GamepediaExporter
{
    private static string OutputPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "gamepedia.html"));

    [MenuItem("Tools/Export Gamepedia")]
    public static void ExportFromMenu()
    {
        Export();
        EditorUtility.RevealInFinder(OutputPath);
    }

    public static void ExportFromCommandLine()
    {
        Export();
        EditorApplication.Exit(0);
    }

    public static void Export()
    {
        var html = GamepediaWriter.Build(Image, "Unity export");
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, html, new UTF8Encoding(false));
        Debug.Log($"Gamepedia written to {OutputPath}");
    }

    private static string Image(UnitStats unit)
    {
        var tint = unit.IsPlayerType ? UnitView.TierStone(unit.Tier) :
            unit.IsSummon ? PlaceholderArt.PlayerStone : PlaceholderArt.EnemyStone;
        return Png(PlaceholderArt.Statue(unit.Kind), tint);
    }

    private static readonly Dictionary<string, string> PngCache = new Dictionary<string, string>();

    /// <summary>The sprite tinted like it is in the game, upscaled 3x without blurring, as a data URI.</summary>
    private static string Png(Sprite sprite, Color tint)
    {
        var key = $"{sprite.GetInstanceID()}{tint}";
        if (PngCache.TryGetValue(key, out var cached))
            return cached;
        var src = sprite.texture;
        const int scale = 3;
        var tex = new Texture2D(src.width * scale, src.height * scale, TextureFormat.RGBA32, false);
        for (var y = 0; y < tex.height; y++)
        for (var x = 0; x < tex.width; x++)
        {
            var c = src.GetPixel(x / scale, y / scale);
            tex.SetPixel(x, y, c.a > 0f ? new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a) : c);
        }

        tex.Apply();
        var uri = "data:image/png;base64," + Convert.ToBase64String(tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        PngCache[key] = uri;
        return uri;
    }
}
