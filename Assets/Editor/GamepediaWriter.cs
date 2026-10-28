using System;
using System.Linq;
using System.Text;
using ArmyRoguelike.Core;

/// <summary>
/// Writes the gamepedia HTML from the game's data: every evolution tree, enemy, item, action,
/// ability, encounter, map node and event. Plain C# with no Unity dependency, so it can also run
/// outside the editor; <see cref="GamepediaExporter"/> calls it with statue images.
/// </summary>
public static class GamepediaWriter
{
    private static Func<UnitStats, string> _image;

    /// <summary>Builds the page. <paramref name="image"/> returns a data URI for a unit's statue, or null for none.</summary>
    public static string Build(Func<UnitStats, string> image, string generatedBy)
    {
        _image = image;
        var html = new StringBuilder();
        html.Append(Head());
        html.Append("<header><h1>ArmyRoguelike Gamepedia</h1>");
        html.Append($"<p class=\"muted\">Generated from the game data on {DateTime.Now:yyyy-MM-dd HH:mm} ({generatedBy}). All numbers are placeholders. ");
        html.Append("Re-export with <b>Tools → Export Gamepedia</b>.</p>");
        html.Append("<nav><a href=\"#trees\">Evolution trees</a><a href=\"#enemies\">Enemies</a><a href=\"#enemy-ai\">Enemy AI</a><a href=\"#encounters\">Encounters</a>");
        html.Append("<a href=\"#items\">Items</a><a href=\"#actions\">Actions</a><a href=\"#abilities\">Abilities</a>");
        html.Append("<a href=\"#run\">Run &amp; map</a><a href=\"#rules\">Battle rules</a></nav></header><main>");

        Trees(html);
        Enemies(html);
        EnemyAISection(html);
        Encounters(html);
        Items(html);
        ActionsSection(html);
        AbilitiesSection(html);
        RunSection(html);
        Rules(html);

        html.Append("</main></body></html>");
        _image = null;
        return html.ToString();
    }

    // ---------- Sections ----------

    private static void Trees(StringBuilder html)
    {
        html.Append("<section id=\"trees\"><h2>Evolution trees</h2>");
        html.Append("<p>You draft 3 of these 5 base units. At a Trainer every unit can go up one tier, choosing a branch. ");
        html.Append("Higher tiers are stronger; units on the same tier play differently. Stone colour shows the tier: marble, bronze, gold.</p>");
        foreach (var root in UnitStats.BaseUnits)
        {
            html.Append($"<div class=\"tree\"><h3>{Esc(root.Name)} line</h3>");
            Node(html, root);
            html.Append("</div>");
        }

        html.Append("<h3>Summons</h3><div class=\"grid\">");
        UnitCard(html, UnitStats.Turret);
        html.Append("</div></section>");
    }

    /// <summary>A unit card, with its evolutions laid out underneath it.</summary>
    private static void Node(StringBuilder html, UnitStats unit)
    {
        html.Append("<div class=\"node\">");
        UnitCard(html, unit);
        if (unit.Evolutions.Count > 0)
        {
            html.Append("<div class=\"children\">");
            foreach (var child in unit.Evolutions)
                Node(html, child);
            html.Append("</div>");
        }

        html.Append("</div>");
    }

    private static void UnitCard(StringBuilder html, UnitStats unit)
    {
        html.Append($"<div class=\"card tier{unit.Tier}\" id=\"{Id(unit.Name)}\">");
        var image = _image?.Invoke(unit);
        html.Append(image != null
            ? $"<img src=\"{image}\" alt=\"{Esc(unit.Name)}\">"
            : $"<div class=\"noimg\">{Esc(unit.Label)}</div>");
        html.Append("<div class=\"body\">");
        var tier = unit.Tier > 0 ? $"<span class=\"badge\">Tier {unit.Tier}</span>" : "";
        html.Append($"<h4>{Esc(unit.Name)} {tier}</h4>");
        html.Append($"<p class=\"stats\">HP {unit.MaxHp} · DMG {unit.Damage} · RNG {unit.Range} · AP {unit.MaxAp}");
        if (unit.Size > 1)
            html.Append($" · {unit.Size}x{unit.Size}");
        html.Append("</p>");
        if (!string.IsNullOrEmpty(unit.Role))
            html.Append($"<p class=\"role\">{Esc(unit.Role)}</p>");
        if (unit.Abilities.Count + unit.Actions.Count > 0)
        {
            html.Append("<ul>");
            foreach (var a in unit.Abilities)
            {
                var isNew = unit.EvolvesFrom == null || !unit.EvolvesFrom.Has(a);
                html.Append($"<li class=\"{(isNew ? "new" : "")}\"><b>{Esc(Abilities.Name(a))}</b>: {Esc(Abilities.Describe(a))}</li>");
            }

            foreach (var a in unit.Actions)
            {
                var isNew = unit.EvolvesFrom == null || !unit.EvolvesFrom.HasAction(a);
                html.Append($"<li class=\"action {(isNew ? "new" : "")}\"><b>{Esc(Abilities.Name(a))}</b> ({Battle.ActionCost(a)} AP): {Esc(Abilities.Describe(a))}</li>");
            }

            html.Append("</ul>");
        }

        html.Append("</div></div>");
    }

    private static void Enemies(StringBuilder html)
    {
        html.Append("<section id=\"enemies\"><h2>Enemies</h2>");
        html.Append("<p>Each card lists what the enemy can do and which <a href=\"#enemy-ai\">AI behaviour</a> decides when it does it.</p><div class=\"grid\">");
        foreach (var e in UnitStats.Enemies)
            EnemyCard(html, e);
        html.Append("</div><h3>The Necromancer's raised army</h3>");
        html.Append("<p>Raised versions of each beaten faction's representative: same numbers, but each leaves a bone pile and can be raised again.</p><div class=\"grid\">");
        foreach (var e in RaisedArmy())
            EnemyCard(html, e);
        html.Append("</div></section>");
    }

    private static UnitStats[] RaisedArmy() =>
        Encounter.Regular.Where(enc => enc.RaisedRepresentative != null).Select(enc => UnitStats.Raised(enc.RaisedRepresentative)).ToArray();

    private static void EnemyCard(StringBuilder html, UnitStats unit)
    {
        html.Append($"<div class=\"card wide\" id=\"{Id(unit.Name)}\">");
        var image = _image?.Invoke(unit);
        html.Append(image != null
            ? $"<img src=\"{image}\" alt=\"{Esc(unit.Name)}\">"
            : $"<div class=\"noimg\">{Esc(unit.Label)}</div>");
        html.Append($"<div class=\"body\"><h4>{Esc(unit.Name)}</h4>");
        html.Append($"<p class=\"stats\">HP {unit.MaxHp} · DMG {unit.Damage} · RNG {unit.Range} · AP {unit.MaxAp}");
        if (unit.Size > 1)
            html.Append($" · {unit.Size}x{unit.Size}");
        html.Append("</p>");
        if (!string.IsNullOrEmpty(unit.Role))
            html.Append($"<p class=\"role\">{Esc(unit.Role)}</p>");
        html.Append("<ul>");
        foreach (var line in EnemyActions(unit))
            html.Append($"<li class=\"action\">{line}</li>");
        foreach (var line in EnemyTraits(unit))
            html.Append($"<li>{line}</li>");
        html.Append("</ul>");
        var behaviour = EnemyAI.BehaviourOf(unit);
        html.Append($"<p class=\"ai\">AI: <a href=\"#{BehaviourId(behaviour)}\">{Esc(BehaviourName(behaviour))}</a></p>");
        html.Append("</div></div>");
    }

    /// <summary>What the enemy can spend AP on, built from its stats.</summary>
    private static string[] EnemyActions(UnitStats e)
    {
        var lines = new System.Collections.Generic.List<string>();
        var cost = Battle.AttackCost;
        if (e.AreaMelee)
            lines.Add($"<b>Smash</b> ({cost} AP): {e.Damage} damage to every unit next to it.");
        else if (e.IsRanged)
            lines.Add($"<b>Ranged attack</b> ({cost} AP): {e.Damage} damage up to {e.Range} tiles away; needs line of sight.");
        else
            lines.Add($"<b>Attack</b> ({cost} AP): {e.Damage} damage to a unit next to it.");
        if (e.Slams)
            lines.Add($"<b>Slam</b> ({cost} AP): shoves every other 1x1 unit within 2 tiles (yours and its own) 1 tile away from it. " +
                      $"A unit pushed into a rock, unit or the edge takes {Battle.CollisionDamage} damage.");
        if (e.CanThrow)
            lines.Add($"<b>Boulder</b> ({e.ThrowCost} AP, once a turn): {e.ThrowDamage} damage to a unit 2-{e.ThrowRange} tiles away, ignoring line of sight. " +
                      "The boulder stays as a rock on the free tile next to the target that is closest to the thrower, burying any mine or bomb there.");
        if (e.CanBomb)
            lines.Add($"<b>Bomb</b> ({e.BombCost} AP, once a turn): lobbed onto the tile of a unit 2-{e.BombRange} tiles away that has no bomb on it yet, ignoring line of sight. " +
                      $"It explodes at the start of the next enemy turn for {e.BombDamage} damage to everyone in the 3x3, its own side included, and sets off mines and bombs in the blast.");
        if (e.CanRaise)
            lines.Add($"<b>Raise</b> ({e.RaiseCost} AP): brings one of its side's bone piles within {e.RaiseRange} tiles back at full HP, if nobody stands on it. " +
                      "The raised unit acts from the next enemy turn.");
        return lines.ToArray();
    }

    /// <summary>Passive rules of an enemy type, built from its stats.</summary>
    private static string[] EnemyTraits(UnitStats e)
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var a in e.Abilities)
            lines.Add($"<b>{Esc(Abilities.Name(a))}</b>: {Esc(Abilities.Describe(a))}.");
        if (e.Kind == UnitKind.PackLeader)
            lines.Add($"<b>Pack aura</b>: wolves within {Battle.LeaderAuraRange} tiles deal +1 damage. When it dies, every wolf deals 1 less (minimum 1).");
        else if (e.Kind == UnitKind.Wolf && !e.IsUndead)
            lines.Add($"<b>Pack</b>: +1 damage within {Battle.LeaderAuraRange} tiles of a living Pack Leader, -1 once the leader is dead (minimum 1).");
        if (e.StealsGold > 0)
            lines.Add($"<b>Steal</b>: its first hit that does damage takes {e.StealsGold} of your gold. From then on it runs for the enemies' back row and escapes from there, and the gold is lost. Kill it first and you keep the gold.");
        if (e.SplitsInto != null)
            lines.Add($"<b>Split</b>: when it dies it becomes 2 {Link(e.SplitsInto.Name)}s, one on its tile and one on a free neighbour (fewer if there is no room). They act from the next enemy turn.");
        if (e.LeavesRemains)
            lines.Add("<b>Bone pile</b>: leaves one when killed, which a Necromancer can raise. A unit standing on the pile stops that.");
        return lines.ToArray();
    }

    private static void EnemyAISection(StringBuilder html)
    {
        html.Append("<section id=\"enemy-ai\"><h2>Enemy AI</h2>");
        html.Append("<p>How enemies decide what to do. The rules every enemy shares come first; each behaviour below only lists what it does differently.</p>");

        html.Append("<h3 id=\"ai-turn\">The enemy turn</h3><ul>");
        html.Append($"<li>At the start of the enemy phase, burning ground and poison tick, then every enemy gets its AP back: its full pool plus up to {Battle.MaxCarriedAp} unused AP from last turn (minus any slow).</li>");
        html.Append("<li>Next, every bomb on the board explodes, one by one (a blast sets off other bombs and mines inside it).</li>");
        html.Append("<li>Then the enemies act one at a time, in the order they entered the battle. Each finishes its whole turn before the next starts. Units raised or split off during this phase wait until the next enemy turn.</li>");
        html.Append("<li>A turn is a loop: the enemy picks a target, does the first thing on its behaviour's list that it can, and starts over. It stops when it runs out of AP or nothing on the list helps. The target is picked again before every action, so after a kill it turns to the next one.</li>");
        html.Append($"<li>Enemies move by your rules: {Battle.MoveCost} AP per tile, no diagonals, +{Battle.ZocCost} AP per step out of a tile next to one of your units (zone of control), and snared units can't move.</li>");
        html.Append("<li>Enemies can't see mines. One that steps on a mine stops there and sets it off.</li></ul>");

        html.Append("<h3 id=\"ai-target\">Targeting</h3><ol>");
        html.Append($"<li>A unit with Taunt within {Battle.TauntRange} tiles: the nearest such unit.</li>");
        html.Append("<li>Otherwise the nearest of your units, counted in steps and ignoring rocks. Ties go to the one with the lowest HP, then to the one listed first in your army.</li></ol>");
        html.Append("<p>Every behaviour uses this target. Only the Bomber may bomb a different unit when its target can't be bombed, and the Necromancer raises bone piles before it looks for a target at all.</p>");

        html.Append("<h3 id=\"ai-approach\">Walking up</h3><ol>");
        html.Append($"<li>If it can reach a tile from which it can hit the target and still have {Battle.AttackCost} AP left for the attack, it walks to the cheapest such tile. Ranged units need range and line of sight from that tile.</li>");
        html.Append("<li>Otherwise it walks as close to the target as it can: to the reachable tile with the shortest walk to any tile next to the target, the cheapest of those. That walk goes around rocks but through units, so an enemy stuck behind its friends still edges forward.</li>");
        html.Append("<li>If no reachable tile is closer than where it stands, it stays put.</li></ol>");
        html.Append("<p>Ranged enemies that walk up (the Crossbowman) walk to the cheapest tile with a shot. They don't look for cover; the Camp layout just starts them behind rocks.</p>");

        html.Append("<h3 id=\"ai-retreat\">Backing off</h3>");
        html.Append("<p>Used by enemies that want distance, once per turn, when their target is too close: it moves to the reachable tile furthest from the nearest of your units, the cheapest of those. If no tile is further away than where it stands, it stays.</p>");

        html.Append("<h3 id=\"ai-range\">Getting into range from a distance</h3>");
        html.Append("<p>Used by enemies that fight from afar, once per turn. They only consider tiles at least their comfort distance from every one of your units:</p><ol>");
        html.Append("<li>The cheapest tile from which it can use its ranged action on the target, with enough AP left to do it.</li>");
        html.Append("<li>If there is none, the tile closest to the target, if it is closer than where it stands.</li></ol>");

        html.Append("<h3 id=\"ai-behaviours\">Behaviours</h3>");
        html.Append("<table><tr><th>Behaviour</th><th>Each loop, the first that applies</th><th>Used by</th></tr>");
        var everyone = UnitStats.Enemies.Concat(RaisedArmy()).ToArray();
        foreach (EnemyBehaviour b in Enum.GetValues(typeof(EnemyBehaviour)))
        {
            var users = everyone.Where(u => EnemyAI.BehaviourOf(u) == b).Select(u => Link(u.Name));
            html.Append($"<tr id=\"{BehaviourId(b)}\"><td><b>{Esc(BehaviourName(b))}</b></td><td><ol>");
            foreach (var step in BehaviourSteps(b))
                html.Append($"<li>{step}</li>");
            html.Append($"</ol></td><td>{string.Join(", ", users)}</td></tr>");
        }

        html.Append("</table>");

        html.Append("<h3 id=\"ai-intent\">Hover intent</h3>");
        html.Append("<p>Hovering an enemy shows the first thing it would do if its turn started now. It is a forecast, not a promise: your moves change it, and it only shows the first action. ");
        html.Append("Hovering a Pack Leader also lights up its aura.</p>");
        html.Append("<table><tr><th>Shows</th><th>When (checked in this order)</th><th>On the board</th></tr>");
        html.Append("<tr><td>Flee</td><td>It carries stolen gold.</td><td>The enemies' back row, where it will escape.</td></tr>");
        html.Append("<tr><td>Slam</td><td>It can slam and 2 or more of your units are within 2 tiles.</td><td>The area within 2 tiles.</td></tr>");
        html.Append("<tr><td>Raise</td><td>It can raise and one of its bone piles within raise range is free. AP isn't checked.</td><td>The bone pile.</td></tr>");
        html.Append("<tr><td>Bomb</td><td>It bombs and its target is 2 to bomb-range tiles away with no bomb on its tile. Otherwise it shows Attack, though it may still bomb another unit or back off.</td><td>The target and the 3x3 blast.</td></tr>");
        html.Append("<tr><td>Smash</td><td>It smashes and the target is next to it, or the target is out of boulder range.</td><td>The ring around it and the target.</td></tr>");
        html.Append("<tr><td>Throw</td><td>It throws boulders and the target is 2 to throw-range tiles away.</td><td>The target and the tile where the rock will land.</td></tr>");
        html.Append("<tr><td>Attack</td><td>Anything else, whether or not it can reach the target this turn.</td><td>The target, with an arrow.</td></tr>");
        html.Append("</table></section>");
    }

    private static string BehaviourId(EnemyBehaviour b) => "ai-" + b.ToString().ToLowerInvariant();

    private static string BehaviourName(EnemyBehaviour b)
    {
        switch (b)
        {
            case EnemyBehaviour.Advance: return "Walk up and hit";
            case EnemyBehaviour.Thief: return "Thief";
            case EnemyBehaviour.Bomber: return "Bomber";
            case EnemyBehaviour.Giant: return "Giant";
            case EnemyBehaviour.Necromancer: return "Necromancer";
            default: throw new ArgumentOutOfRangeException(nameof(b), b, "Add a name and steps for this behaviour to the gamepedia.");
        }
    }

    /// <summary>
    /// The decision list of each behaviour, written to match <see cref="EnemyAI"/>'s turn routines.
    /// Update it whenever a routine changes; a new behaviour without text stops the export.
    /// </summary>
    private static string[] BehaviourSteps(EnemyBehaviour b)
    {
        const string target = "<a href=\"#ai-target\">target</a>";
        const string approach = "<a href=\"#ai-approach\">walk up</a>";
        const string retreat = "<a href=\"#ai-retreat\">back off</a>";
        const string range = "<a href=\"#ai-range\">get into range</a>";
        switch (b)
        {
            case EnemyBehaviour.Advance:
                return new[]
                {
                    $"If it can hit its {target} from where it stands: attack.",
                    $"Otherwise {approach} to it.",
                };
            case EnemyBehaviour.Thief:
                return new[]
                {
                    "Carrying stolen gold: if it is on the enemies' back row, escape with the gold. Otherwise move to the reachable tile furthest back (the cheapest of those). If it can't get any further back this turn, it stops.",
                    $"Not carrying gold: attack its {target} if it can, otherwise {approach}, like Walk up and hit. Its first hit that does damage steals the gold, and it spends the rest of its AP running.",
                };
            case EnemyBehaviour.Bomber:
                return new[]
                {
                    $"If its {target} is closer than {EnemyAI.BomberComfortDistance} tiles: {retreat} (once a turn).",
                    "Bomb its target if it can; if not, the nearest of your units it can bomb (once a turn).",
                    "If the target is next to it: stab.",
                    $"If it hasn't bombed yet: {range} for a bomb (once a turn), with a comfort distance of {EnemyAI.BomberComfortDistance}.",
                };
            case EnemyBehaviour.Giant:
                return new[]
                {
                    $"Once, at the very start of its turn: slam if 2 or more of your units are within 2 tiles and it has {Battle.AttackCost} AP.",
                    $"If any of your units are next to it: smash (stops if it has less than {Battle.AttackCost} AP).",
                    $"Throw a boulder at its {target} if it is 2 to throw-range tiles away (once a turn).",
                    $"Otherwise {approach} to the target.",
                };
            case EnemyBehaviour.Necromancer:
                return new[]
                {
                    "Raise the nearest bone pile it can, as long as its AP lasts.",
                    $"If its {target} is closer than {EnemyAI.NecromancerComfortDistance} tiles: {retreat} (once a turn).",
                    "If it can hit the target from where it stands: fire a bolt.",
                    $"Otherwise {range} for a bolt (once a turn), with a comfort distance of {EnemyAI.NecromancerComfortDistance}. It never walks closer than that to shoot.",
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(b), b, "Add a name and steps for this behaviour to the gamepedia.");
        }
    }

    private static void Encounters(StringBuilder html)
    {
        html.Append("<section id=\"encounters\"><h2>Encounters</h2><p>Battles are on an 8x8 board; you place your army in the back two rows. ");
        html.Append("Kill every enemy to win. Battles bring factions you haven't beaten yet, then normal-strength rematches; Act 1 ends with one of the bosses. ");
        html.Append("Every encounter has a layout template: fixed rules, with the exact rocks and start tiles varied each time.</p>");
        html.Append("<h3>Factions</h3><div class=\"grid\">");
        foreach (var enc in Encounter.Regular)
            EncounterCard(html, enc, enc.EnemySummary());
        html.Append("</div><h3>Elites</h3><p>A harder rematch of a faction you've beaten: more of the same enemies. High risk, high reward: ");
        html.Append("normal gold plus a pick of 1 of " + Run.RewardItemCount + " items.</p><div class=\"grid\">");
        foreach (var enc in Encounter.Elites)
            EncounterCard(html, enc, enc.EnemySummary());
        html.Append("</div><h3>Bosses</h3><div class=\"grid\">");
        EncounterCard(html, Encounter.Necromancer, new[] { "Necromancer", "2 raised units from each faction you beat this act (Skeletons fill up to 4)" });
        EncounterCard(html, Encounter.Giant, Encounter.Giant.EnemySummary());
        html.Append("</div><h3>Scenarios</h3><div class=\"grid\">");
        var waves = $"{Encounter.RitualWaveSize} more Elementals after enemy turns {string.Join(" and ", Encounter.RitualWaveTurns)}";
        EncounterCard(html, Encounter.MoonShrine, Encounter.MoonShrine.EnemySummary().Concat(new[] { waves }));
        EncounterCard(html, Encounter.BreakRitual, Encounter.BreakRitual.EnemySummary().Concat(new[] { $"{Encounter.TotemCount} Totems" }));
        html.Append("</div>");
        html.Append($"<p>The Moon Shrine ({UnitStats.MoonShrine.MaxHp} HP) stands at the centre; you deploy within {Encounter.RitualDeployRange} steps of it. ");
        html.Append($"Survive {Encounter.RitualTurns} enemy turns and the ritual completes: you win and pick a blessing. If the shrine breaks, ");
        html.Append("the fight is lost (no gold) and you take a random curse, but the run goes on. Losing the whole army still ends the run. ");
        html.Append("Enemies go for the shrine unless one of your units is next to them (Taunt still wins). The shrine counts as your ally for ");
        html.Append($"Shield Wall and Guard; your own blasts and volleys hurt it. Reinforcements arrive on edge tiles at least {Battle.RitualSpawnDistance} steps from it.</p>");
        html.Append($"<p><b>Break the Ritual</b>: {Encounter.TotemCount} totems ({UnitStats.Totem.MaxHp} HP) stand on the far half, guarded by goblins. ");
        html.Append($"Smash them all before the summoning completes at the start of enemy turn {Encounter.SummonTurn}: you win, the guards flee, ");
        html.Append($"and you pick 1 of {Run.RewardItemCount} items for one unit. If the summoning completes, the fight is lost (no gold) and you take a random curse; ");
        html.Append($"the run goes on. Guards go for whichever of your units is within {EnemyAI.TotemThreatRange} tiles of a totem; their own bombs don't hurt the totems. ");
        html.Append("The shrine and totems can't attack, so they have no zone of control.</p>");
        html.Append("</section>");
    }

    private static void EncounterCard(StringBuilder html, Encounter enc, System.Collections.Generic.IEnumerable<string> enemies)
    {
        html.Append($"<div class=\"card\"><div class=\"body\"><h4>{Esc(enc.Name)} <span class=\"badge\">{Esc(enc.Layout)}</span></h4>");
        html.Append($"<p class=\"role\">{Esc(enc.LayoutRule)}</p><p>{Esc(string.Join(", ", enemies))}</p>");
        if (enc.RaisedRepresentative != null)
            html.Append($"<p class=\"stats\">Necromancer raises: {Esc(UnitStats.Raised(enc.RaisedRepresentative).Name)}</p>");
        html.Append("</div></div>");
    }

    private static void Items(StringBuilder html)
    {
        html.Append("<section id=\"items\"><h2>Items</h2><p>One slot per unit; a new item replaces the old one. ");
        html.Append("Items only bend rules: each grants an ability. Sold in shops and found in carts.</p>");
        html.Append("<table><tr><th>Item</th><th>Price</th><th>Effect</th></tr>");
        foreach (var item in Equipment.Catalog)
            html.Append($"<tr><td>{Esc(item.Name)}</td><td>{item.Price} gold</td><td>{Esc(item.Effect)}</td></tr>");
        html.Append("</table></section>");
    }

    private static void ActionsSection(StringBuilder html)
    {
        html.Append("<section id=\"actions\"><h2>Special actions</h2><p>Used from the HUD buttons or number keys 1-4.</p>");
        html.Append("<table><tr><th>Action</th><th>AP</th><th>Effect</th><th>Used by</th></tr>");
        foreach (ActionKind a in Enum.GetValues(typeof(ActionKind)))
        {
            var users = UnitStats.AllPlayerTypes.Where(u => u.HasAction(a)).Select(u => Link(u.Name));
            html.Append($"<tr><td>{Esc(Abilities.Name(a))}</td><td>{Battle.ActionCost(a)}</td><td>{Esc(Abilities.Describe(a))}</td><td>{string.Join(", ", users)}</td></tr>");
        }

        html.Append("</table></section>");
    }

    private static void AbilitiesSection(StringBuilder html)
    {
        html.Append("<section id=\"abilities\"><h2>Abilities</h2>");
        html.Append("<table><tr><th>Ability</th><th>Effect</th><th>From</th></tr>");
        foreach (Ability a in Enum.GetValues(typeof(Ability)))
        {
            if (a == Ability.None)
                continue;
            var from = UnitStats.AllPlayerTypes.Where(u => u.Has(a)).Select(u => Link(u.Name))
                .Concat(Equipment.Catalog.Where(e => e.Ability == a).Select(e => $"<i>{Esc(e.Name)}</i>"))
                .Concat(a == Ability.Immobile ? new[] { "Turret", "Moon Shrine", "Totem" } : new string[0]);
            html.Append($"<tr><td>{Esc(Abilities.Name(a))}</td><td>{Esc(Abilities.Describe(a))}</td><td>{string.Join(", ", from)}</td></tr>");
        }

        html.Append("</table></section>");
    }

    private static void RunSection(StringBuilder html)
    {
        html.Append("<section id=\"run\"><h2>Run &amp; map</h2><ul>");
        html.Append($"<li>Draft {Run.DraftSize} different base units. Army size up to {Run.MaxArmySize}.</li>");
        html.Append($"<li>Act 1 has {Run.Steps} steps; each step offers 2 nodes. At steps {string.Join(", ", Run.FightSteps.Select(s => s + 1))} both choices are fights, ");
        html.Append($"so every run has at least {Run.FightSteps.Count} fights before the boss. ");
        html.Append($"Steps {string.Join(" and ", Run.TrainerSteps.Select(s => s + 1))} offer a Trainer; the last step is a boss (Necromancer or Giant).</li>");
        html.Append("<li>A <b>Battle</b> brings a faction you haven't beaten yet; once you've met them all it's a normal-strength rematch. ");
        html.Append("An <b>Elite</b> (steps 6 and 10) is a harder rematch of a faction you've beaten. Both show their faction when you reach their step.</li>");
        html.Append("<li>The two scenario fights sit at steps 4 and 8, next to a battle: the Moon Shrine at one, Break the Ritual at the other. Shops and ? events fill the rest.</li>");
        html.Append($"<li>Won battle: {Run.BaseGold} gold + {Run.GoldPerSurvivor} per surviving unit (+{Run.AmbushBonusGold} for an ambush). Gold carried off by escaped cutpurses is lost.</li>");
        html.Append($"<li><b>Trainer</b>: each unit can evolve one tier per visit. The first evolution of a visit is free, the second costs {Run.SecondEvolutionPrice} gold, each after that {Run.LaterEvolutionPrice}.</li>");
        html.Append($"<li><b>Shop</b>: {Run.ShopSize} items and a random base-unit recruit for {Run.RecruitPrice} gold (if the army has room).</li>");
        html.Append("<li>Every battle starts at full HP and dead units come back. Evolutions, items, Bloodthirst damage and shrine HP last the whole run.</li>");
        html.Append("</ul><h3>? events</h3><table><tr><th>Event</th><th>What happens</th></tr>");
        html.Append($"<tr><td>Wandering Recruit</td><td>A random base unit joins for free ({Run.FullArmyGold} gold instead if the army is full).</td></tr>");
        html.Append("<tr><td>Abandoned Cart</td><td>A random item for any unit, or leave it.</td></tr>");
        html.Append($"<tr><td>Gambler</td><td>Pay {Run.GambleCost} gold for a 50% chance at {Run.GambleWin}.</td></tr>");
        html.Append($"<tr><td>Ambush</td><td>A fight against a faction you haven't met (and won't meet later on the map) that pays +{Run.AmbushBonusGold} gold. If none is left, they flee and drop gold.</td></tr>");
        html.Append($"<tr><td>Shrine</td><td>One unit gets +{Run.ShrineHp} max HP for the run.</td></tr>");
        html.Append("</table><h3>Blessings and curses</h3><p>Hold the Moon Shrine and pick a blessing. Lose a scenario fight and take a random curse ");
        html.Append("(losing both gives two different curses). ");
        html.Append("Either lasts the rest of the run and works every turn of every battle. Omens never pick the shrine itself.</p>");
        html.Append("<table><tr><th>Omen</th><th>Blessing</th><th>Curse</th></tr>");
        foreach (var omen in Omens.All)
            html.Append($"<tr><td>{omen}</td><td>{Esc(Omens.Describe(omen, true))}</td><td>{Esc(Omens.Describe(omen, false))}</td></tr>");
        html.Append("</table></section>");
    }

    private static void Rules(StringBuilder html)
    {
        html.Append("<section id=\"rules\"><h2>Battle rules</h2><ul>");
        html.Append($"<li>Moving costs {Battle.MoveCost} AP per tile (4 directions, no diagonals). A basic attack costs {Battle.AttackCost} AP. Up to {Battle.MaxCarriedAp} unused AP carry over.</li>");
        html.Append("<li>Side phases: all your units act (one at a time), then all enemies.</li>");
        html.Append($"<li><b>Zone of control</b>: each step out of a tile next to an enemy costs +{Battle.ZocCost} AP, for both sides. Hit-and-run is possible but expensive.</li>");
        html.Append("<li>Ranged attacks need line of sight (rocks block); Arcane Bolt ignores it. Bombs, boulders and fireballs are lobbed.</li>");
        html.Append($"<li>Mines: {Battle.MineDamage} damage ({Battle.BigMineDamage} with Big Mines) to everyone in the 3x3, set off when an enemy steps on one. Bombs explode at the start of the next enemy turn. Blasts set each other off.</li>");
        html.Append($"<li>Push: a unit pushed into a rock, unit or the edge takes {Battle.CollisionDamage} damage. An enemy pushed onto a mine sets it off.</li>");
        html.Append($"<li>Poison: {Battle.PoisonDamage} damage at the start of the target's turn, {Battle.PoisonTurns} times. Burning ground: {Battle.BurnDamage} damage at the start of the turn of whoever stands on it.</li>");
        html.Append("<li>Snared units can't move on their next turn. Slowed units start their next turn with less AP.</li>");
        html.Append("<li>Hover any enemy to see what it plans to do next.</li>");
        html.Append("</ul></section>");
    }

    // ---------- Helpers ----------

    private static string Esc(string s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Id(string name) => "u-" + new string(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string Link(string name) => $"<a href=\"#{Id(name)}\">{Esc(name)}</a>";

    private static string Head() => @"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>ArmyRoguelike Gamepedia</title>
<style>
:root { --bg:#16161b; --panel:#212129; --line:#34343f; --text:#e8e6e0; --muted:#9c9aa3; --accent:#f2c14e; --new:#8fd694; }
* { box-sizing:border-box; }
body { margin:0; background:var(--bg); color:var(--text); font:15px/1.45 system-ui, sans-serif; }
header { padding:24px 32px 8px; border-bottom:1px solid var(--line); position:sticky; top:0; background:var(--bg); z-index:2; }
h1 { margin:0 0 4px; font-size:26px; } h2 { margin:32px 0 12px; font-size:21px; border-bottom:1px solid var(--line); padding-bottom:6px; }
h3 { margin:18px 0 10px; font-size:17px; color:var(--accent); } h4 { margin:0 0 4px; font-size:15px; }
nav { display:flex; flex-wrap:wrap; gap:14px; margin:10px 0; } nav a, a { color:var(--accent); text-decoration:none; } a:hover { text-decoration:underline; }
main { padding:0 32px 48px; } .muted { color:var(--muted); margin:0; }
.tree { overflow-x:auto; padding-bottom:8px; }
.node { display:flex; flex-direction:column; align-items:center; }
.children { display:flex; gap:12px; margin-top:12px; padding-top:12px; border-top:2px solid var(--line); align-items:flex-start; }
.card { display:flex; gap:10px; background:var(--panel); border:1px solid var(--line); border-radius:8px; padding:10px; width:300px; }
.card.wide { width:440px; } .ai { margin:6px 0 0; font-size:13px; }
.card.tier2 { border-color:#7a6040; } .card.tier3 { border-color:#a08a30; }
.card img { image-rendering:pixelated; width:72px; height:auto; align-self:flex-start; }
.noimg { width:56px; height:56px; flex:none; border-radius:6px; background:var(--line); display:flex; align-items:center; justify-content:center; font-weight:bold; color:var(--accent); }
.card .body { min-width:0; } .stats { margin:0 0 4px; color:var(--muted); font-size:13px; } .role { margin:0 0 6px; font-style:italic; }
.card ul { margin:0; padding-left:16px; font-size:13px; } .card li.new b { color:var(--new); }
.badge { font-size:11px; background:var(--line); border-radius:4px; padding:1px 6px; margin-left:4px; color:var(--muted); }
.grid { display:flex; flex-wrap:wrap; gap:12px; }
table { border-collapse:collapse; width:100%; max-width:1100px; } th, td { border:1px solid var(--line); padding:6px 8px; text-align:left; vertical-align:top; }
th { background:var(--panel); }
</style></head><body>";
}
