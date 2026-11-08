// Not part of the Unity project: compiled by tools/dev.sh synergy.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        return 0;
    }

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
