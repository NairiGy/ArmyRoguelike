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

    /// <summary>Every card image is this tall, whatever the source size.</summary>
    private const int CardHeight = 150;

    /// <summary>
    /// The sprite tinted like it is in the game, scaled to <see cref="CardHeight"/>, as a data URI. Pixel art
    /// (point-filtered) stays crisp; painted statues are scaled smoothly.
    /// </summary>
    private static string Png(Sprite sprite, Color tint)
    {
        var key = $"{sprite.GetInstanceID()}{tint}";
        if (PngCache.TryGetValue(key, out var cached))
            return cached;
        var src = ReadableCopy(sprite);
        var scale = (float)CardHeight / src.height;
        var tex = new Texture2D(Mathf.Max(1, Mathf.RoundToInt(src.width * scale)), CardHeight, TextureFormat.RGBA32, false);
        var pixelArt = sprite.texture.filterMode == FilterMode.Point;
        for (var y = 0; y < tex.height; y++)
        for (var x = 0; x < tex.width; x++)
        {
            var c = pixelArt
                ? src.GetPixel(Mathf.Min(src.width - 1, (int)(x / scale)), Mathf.Min(src.height - 1, (int)(y / scale)))
                : src.GetPixelBilinear((x + 0.5f) / tex.width, (y + 0.5f) / tex.height);
            tex.SetPixel(x, y, c.a > 0f ? new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a) : c);
        }

        tex.Apply();
        var uri = "data:image/png;base64," + Convert.ToBase64String(tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.DestroyImmediate(src);
        PngCache[key] = uri;
        return uri;
    }

    /// <summary>
    /// The sprite's own area as a new readable texture. Painted statues are imported without Read/Write
    /// (keeping it off saves memory in the game), so those are copied through a render texture instead.
    /// </summary>
    private static Texture2D ReadableCopy(Sprite sprite)
    {
        var src = sprite.texture;
        var r = sprite.rect;
        int x = Mathf.RoundToInt(r.x), y = Mathf.RoundToInt(r.y), w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = src.filterMode };
        if (src.isReadable)
        {
            copy.SetPixels(src.GetPixels(x, y, w, h));
        }
        else
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            copy.ReadPixels(new Rect(x, y, w, h), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }

        copy.Apply();
        return copy;
    }
}
