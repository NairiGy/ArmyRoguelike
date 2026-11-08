// Not part of the Unity project: compiled by tools/dev.sh synergy.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ArmyRoguelike.Core;

// Fills docs/synergy-map.html with the game's own rule texts, so the map's hover cards say exactly what the game
// says. Writes the block between the GAME TEXTS START / END markers: an object keyed "Owner|Name" with each entry's
// text and its cost line. The page's own tags and notes stay hand-written.
public static class Program
{
    private const string Start = "/* GAME TEXTS START: written by tools/dev.sh synergy, don't edit by hand */";
    private const string End = "/* GAME TEXTS END */";

    public static int Main(string[] args)
    {
        var path = args[0];
        var texts = new List<(string key, string text, string meta)>();

        var heroes = new (UnitStats stats, string owner)[]
        {
            (UnitStats.Warrior, "Warrior"), (UnitStats.Archer, "Archer"), (UnitStats.Rogue, "Rogue"),
            (UnitStats.Apprentice, "Mage"), (UnitStats.Sapper, "Sapper"),
        };
        foreach (var (stats, owner) in heroes)
        {
            foreach (var (name, cost, kind, text) in Abilities.ActionsOf(stats, stats.Damage))
                texts.Add(($"{owner}|{name}", text, ActionMeta(cost, kind)));
            foreach (var (name, text) in Abilities.PassivesOf(stats))
                texts.Add(($"{owner}|{name}", text, "passive"));
            foreach (var t in Technique.All.Where(t => t.Line == stats))
            {
                var needs = t.Requires != null ? $"needs {t.Requires.Name}" : null;
                var kindLine = t.Action.HasValue ? ActionMeta(Battle.ActionCost(t.Action.Value), t.Action) : "passive";
                texts.Add(($"{owner}|{t.Name}", t.Description, string.Join(" · ", new[] { kindLine, needs }.Where(s => s != null))));
            }
        }

        foreach (var item in Equipment.Catalog)
            texts.Add(($"Item|{item.Name}", item.Effect, ""));
        foreach (var banner in Banners.All)
            texts.Add(($"Banner|{Banners.Name(banner)}", Banners.Describe(banner), ""));
        foreach (var field in Battlefields.All)
            texts.Add(($"Battlefield|{Battlefields.Name(field)}", Battlefields.Describe(field), ""));
        foreach (var omen in Omens.All)
        {
            var name = omen.ToString();
            texts.Add(($"Omen|{name} (blessing)", Omens.Describe(omen, true), ""));
            texts.Add(($"Omen|{name} (curse)", Omens.Describe(omen, false), ""));
        }
        foreach (var affix in Champions.All)
            texts.Add(($"Champion|{Champions.Name(affix)}", Upper(Champions.Describe(affix)), ""));

        // Keyword meanings, keyed by the page's keyword ids (the name in lower case: "pinned", "wall").
        foreach (var keyword in Keywords.All)
            texts.Add(($"Keyword|{Keywords.Name(keyword).ToLowerInvariant()}", Keywords.Describe(keyword), ""));

        var block = new StringBuilder();
        block.AppendLine(Start);
        block.AppendLine("const GAME_TEXTS = {");
        foreach (var (key, text, meta) in texts.GroupBy(t => t.key).Select(g => g.First()))
            block.AppendLine($"  {Js(key)}: {{ text: {Js(Upper(text))}, meta: {Js(meta)} }},");
        block.AppendLine("};");
        block.Append(End);

        var page = File.ReadAllText(path);
        var from = page.IndexOf(Start, StringComparison.Ordinal);
        var to = page.IndexOf(End, StringComparison.Ordinal);
        if (from < 0 || to < from)
        {
            Console.Error.WriteLine("No GAME TEXTS block in " + path);
            return 1;
        }

        page = page.Substring(0, from) + block + page.Substring(to + End.Length);
        File.WriteAllText(path, page, new UTF8Encoding(false));
        Console.WriteLine($"wrote {texts.Count} game texts into {path}");

        // The map's own entries (the E table: ["Name", "Owner", ...]) against the game's, so a new or renamed skill,
        // item or rule gets noticed.
        var table = page.Substring(page.IndexOf("const E = [", StringComparison.Ordinal));
        table = table.Substring(0, table.IndexOf("\n];", StringComparison.Ordinal));
        var onPage = new HashSet<string>(Regex.Matches(table, "^\\s*\\[\"([^\"]+)\", \"([^\"]+)\",", RegexOptions.Multiline)
            .Cast<Match>().Select(m => $"{m.Groups[2].Value}|{m.Groups[1].Value}"));
        var inGame = texts.Select(t => t.key).Where(k => !k.StartsWith("Keyword|")).ToList();
        var notOnMap = inGame.Where(k => !onPage.Contains(k) && !Ignored.Contains(k)).ToList();
        var notInGame = onPage.Where(k => !inGame.Contains(k) && !k.StartsWith("Keyword|")).ToList();
        if (notOnMap.Count > 0)
            Console.WriteLine("In the game but not on the map (add and tag them): " + string.Join(", ", notOnMap));
        if (notInGame.Count > 0)
            Console.WriteLine("On the map but not in the game (renamed or removed?): " + string.Join(", ", notInGame));
        if (notOnMap.Count == 0 && notInGame.Count == 0)
            Console.WriteLine("Every game entry is on the map.");
        return 0;
    }

    /// <summary>Game entries the map leaves out on purpose: they make, use or hurt no theme.</summary>
    private static readonly HashSet<string> Ignored = new HashSet<string>
    {
        "Sapper|Mines", "Omen|Sun (curse)", "Omen|Night (curse)", "Omen|Moon (curse)",
    };

    private static string ActionMeta(int cost, ActionKind? kind)
    {
        var parts = new List<string> { cost == 0 ? "free" : $"{cost} AP" };
        if (kind.HasValue && Battle.Cooldown(kind.Value) > 0)
            parts.Add($"cooldown {Battle.Cooldown(kind.Value)}");
        return string.Join(", ", parts);
    }

    private static string Upper(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    private static string Js(string s)
    {
        var b = new StringBuilder("\"");
        foreach (var c in s ?? "")
        {
            switch (c)
            {
                case '\\': b.Append("\\\\"); break;
                case '"': b.Append("\\\""); break;
                case '\n': b.Append("\\n"); break;
                case '\r': break;
                case '<': b.Append("\\u003c"); break;
                default: b.Append(c); break;
            }
        }

        return b.Append('"').ToString();
    }
}
