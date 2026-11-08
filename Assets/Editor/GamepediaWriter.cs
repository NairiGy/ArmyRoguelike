using System;
using System.Linq;
using System.Text;
using ArmyRoguelike.Core;

/// <summary>
/// Writes the gamepedia HTML from the game's data: every unit and technique, enemy, item, action,
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
        html.Append("<nav><a href=\"#units\">Units &amp; techniques</a><a href=\"#enemies\">Enemies</a><a href=\"#enemy-ai\">Enemy AI</a><a href=\"#encounters\">Encounters</a>");
        html.Append("<a href=\"#items\">Items</a><a href=\"#actions\">Actions</a><a href=\"#abilities\">Abilities</a>");
        html.Append("<a href=\"#run\">Run &amp; map</a><a href=\"#rules\">Battle rules</a></nav></header><main>");

        Units(html);
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

    private static void Units(StringBuilder html)
    {
        html.Append("<section id=\"units\"><h2>Units and techniques</h2>");
        html.Append($"<p>You draft {Run.DraftSize} of these 5 units. They grow during the run by learning <b>techniques</b>: every won fight ");
        html.Append($"(except the boss and the Moon Shrine) offers {Run.RewardCount} that your units can learn; one unit learns one, or you skip for {Run.SkipGold} gold. ");
        html.Append($"Shops sell them too ({Technique.CommonPrice} gold common, {Technique.RarePrice} rare). Each technique belongs to one line, and some need another first. ");
        html.Append("A unit's <b>level</b> is the number of different techniques it has learned; forgetting one (at a shop) keeps the level, and relearning it adds none.</p>");
        foreach (var unit in UnitStats.BaseUnits)
        {
            html.Append($"<div class=\"line\"><h3>{Esc(unit.Name)} line</h3><div class=\"grid\">");
            UnitCard(html, unit);
            html.Append("</div>");
            html.Append($"<p class=\"stats\">Per level: {Esc(GrowthText(unit))}</p>");
            html.Append("<table><tr><th>Technique</th><th>Kind</th><th>Rarity</th><th>Needs</th><th>Effect</th></tr>");
            foreach (var t in Technique.All.Where(t => t.Line == unit))
            {
                var kind = t.Action.HasValue ? $"action ({Battle.ActionCost(t.Action.Value)} AP, {(Battle.Cooldown(t.Action.Value) == 0 ? "once per battle" : $"cooldown {Battle.Cooldown(t.Action.Value)}")})" : "passive";
                var rarity = $"{(t.Rarity == TechniqueRarity.Rare ? "rare" : "common")}, {t.Price} gold";
                html.Append($"<tr id=\"t-{Id(t.Name)}\"><td><b>{Esc(t.Name)}</b></td><td>{kind}</td><td>{rarity}</td><td>{(t.Requires != null ? TechniqueLink(t.Requires) : "")}</td><td>{Esc(t.Description)}</td></tr>");
            }

            html.Append("</table></div>");
        }

        html.Append("<h3>Summons</h3><div class=\"grid\">");
        UnitCard(html, UnitStats.Turret);
        html.Append("</div></section>");
    }

    /// <summary>What each level gives a line, e.g. "+1 HP; +1 damage at levels 3 and 6".</summary>
    private static string GrowthText(UnitStats line)
    {
        var parts = new System.Collections.Generic.List<string> { $"+{Growth.HpPerLevel(line)} max HP" };
        var damage = Growth.DamageLevels(line);
        if (damage.Count > 0)
            parts.Add($"+1 damage at level {string.Join(", ", damage)}");
        if (Growth.MineLevel(line) > 0)
            parts.Add($"+1 mine on the board at level {Growth.MineLevel(line)}");
        return string.Join("; ", parts);
    }

    private static void UnitCard(StringBuilder html, UnitStats unit)
    {
        html.Append($"<div class=\"card tier{unit.Tier}\" id=\"{Id(unit.Name)}\">");
        var image = _image?.Invoke(unit);
        html.Append(image != null
            ? $"<img src=\"{image}\" alt=\"{Esc(unit.Name)}\">"
            : $"<div class=\"noimg\">{Esc(unit.Label)}</div>");
        html.Append("<div class=\"body\">");
        html.Append($"<h4>{Esc(unit.Name)}</h4>");
        html.Append($"<p class=\"stats\">HP {unit.MaxHp} · DMG {unit.Damage} · RNG {unit.Range} · AP {unit.MaxAp} · MV {unit.Moves} · {unit.Weight}");
        if (unit.Size > 1)
            html.Append($" · {unit.Size}x{unit.Size}");
        html.Append("</p>");
        if (!string.IsNullOrEmpty(unit.Role))
            html.Append($"<p class=\"role\">{Esc(unit.Role)}</p>");
        if (unit.Damage > 0 || unit.Abilities.Count + unit.Actions.Count > 0)
        {
            html.Append("<ul>");
            if (unit.Damage > 0)
                html.Append($"<li class=\"action\"><b>{Esc(Abilities.AttackName(unit))}</b> ({Battle.AttackCost} AP): {Esc(Abilities.DescribeAttack(unit, unit.Damage))}</li>");
            foreach (var a in unit.Abilities)
            {
                html.Append($"<li><b>{Esc(Abilities.Name(a))}</b>: {Esc(Abilities.Describe(a))}</li>");
            }

            foreach (var a in unit.Actions)
            {
                html.Append($"<li class=\"action\"><b>{Esc(Abilities.Name(a))}</b> ({Battle.ActionCost(a)} AP, cooldown {Battle.Cooldown(a)}): {Esc(Abilities.Describe(a))}</li>");
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
        html.Append($"<p class=\"stats\">HP {unit.MaxHp} · DMG {unit.Damage} · RNG {unit.Range} · AP {unit.MaxAp} · MV {unit.Moves} · {unit.Weight}");
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
        lines.Add($"<b>{Esc(Abilities.AttackName(e))}</b> ({cost} AP): {Esc(Abilities.DescribeAttack(e, e.Damage))}.");
        foreach (var (ability, kind) in new[]
                 {
                     (Ability.Caltrops, ActionKind.Caltrops), (Ability.Overwatch, ActionKind.Overwatch), (Ability.BullRush, ActionKind.BullRush),
                     (Ability.Upheaval, ActionKind.Upheaval), (Ability.Grasp, ActionKind.Grasp), (Ability.CorpseBlast, ActionKind.CorpseBlast),
                     (Ability.Hellswap, ActionKind.Hellswap), (Ability.RendingBlow, ActionKind.RendingBlow),
                     (Ability.SoulContract, ActionKind.SoulContract), (Ability.FireBreath, ActionKind.FireBreath),
                 })
            if (e.Has(ability))
                lines.Add($"<b>{Esc(Abilities.Name(ability))}</b> ({cost} AP, cooldown {Battle.Cooldown(kind)}): {Esc(Abilities.Describe(ability))}.");
        if (e.Has(Ability.Fling))
            lines.Add($"<b>Fling</b> ({cost} AP, cooldown {Battle.Cooldown(ActionKind.Fling)}): {Esc(Abilities.Describe(Ability.Fling))}.");
        if (e.Has(Ability.Howl))
            lines.Add($"<b>Howl</b> ({cost} AP, cooldown {Battle.Cooldown(ActionKind.Howl)}): {Esc(Abilities.Describe(Ability.Howl))}. The leader acts before the other enemies, so the move reaches wolves that haven't acted.");
        if (e.Slams)
            lines.Add($"<b>Slam</b> ({cost} AP, cooldown {Battle.Cooldown(ActionKind.Slam)}): {Battle.SlamDamage} damage to each of your units within 2 tiles, then shoves every other 1x1 unit within 2 tiles (yours and its own) 1 tile away from it. " +
                      $"A unit pushed into a rock, unit or the edge takes {Battle.CollisionDamage} damage.");
        if (e.CanThrow)
            lines.Add($"<b>Boulder</b> ({e.ThrowCost} AP, cooldown {Battle.Cooldown(ActionKind.Boulder)}): {e.ThrowDamage} damage to a unit 2-{e.ThrowRange} tiles away, ignoring line of sight. " +
                      "The boulder stays as a rock on the free tile next to the target that is closest to the thrower, burying any mine or bomb there.");
        if (e.CanBomb)
            lines.Add($"<b>Bomb</b> ({e.BombCost} AP, cooldown {Battle.Cooldown(ActionKind.Bomb)}): lobbed onto the tile of a unit 2-{e.BombRange} tiles away that has no bomb on it yet, ignoring line of sight. " +
                      $"It explodes at the start of the next enemy turn for {e.BombDamage} damage to whoever stands on that tile, its own side included, and sets off a mine or bomb on it.");
        if (e.CanRaise)
            lines.Add($"<b>Raise</b> ({e.RaiseCost} AP, cooldown {Battle.Cooldown(ActionKind.Raise)}): brings every one of its side's bone piles within {e.RaiseRange} tiles back at full HP at once, unless someone stands on it. " +
                      "The raised unit acts from the next enemy turn.");
        return lines.ToArray();
    }

    /// <summary>Passive rules of an enemy type, built from its stats.</summary>
    private static string[] EnemyTraits(UnitStats e)
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (var a in e.Abilities.Where(a => !Abilities.IsAction(a)))
            lines.Add($"<b>{Esc(Abilities.Name(a))}</b>: {Esc(Abilities.Describe(a))}.");
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
        html.Append($"<li>At the start of the enemy phase, burning ground and poison tick, then every enemy gets its {Battle.AttackCost} AP back (unused AP doesn't carry over) and its moves ({Battle.BaseMoves}, or {Battle.BaseMoves + 1} with Swift; none if slowed).</li>");
        html.Append("<li>Next, every bomb on the board explodes, one by one (a blast sets off other bombs and mines inside it).</li>");
        html.Append("<li>Then the enemies act one at a time, in the order they entered the battle. Each finishes its whole turn before the next starts. Units raised or split off during this phase wait until the next enemy turn.</li>");
        html.Append("<li>A turn is a loop: the enemy picks a target, does the first thing on its behaviour's list that it can, and starts over. It stops when it runs out of AP and moves, or nothing on the list helps. The target is picked again before every action, so after a kill it turns to the next one.</li>");
        html.Append($"<li>Enemies move by your rules: {Battle.MoveCost} move per tile (moves are separate from AP), no diagonals, stepping out of a tile next to one of your units uses up their remaining moves (zone of control), no moving after spending AP, and pinned or slowed units can't move.</li>");
        html.Append("<li>Enemies can't see mines, except a goblin standing next to one, which can fling it. One that steps on a mine stops there and sets it off.</li>");
        html.Append("<li>Enemies walk through burning ground but never stop on it. One with nobody to hit attacks a barricade or rubble in its reach.</li></ul>");

        html.Append("<h3 id=\"ai-target\">Targeting</h3><ol>");
        html.Append($"<li>A unit that used Taunt within {Battle.TauntRange} tiles: the nearest such unit. It is then the only unit this enemy aims attacks or abilities at (area hits like the Giant's Smash and Slam still hit everyone in them).</li>");
        html.Append("<li>Blood Scent (wolves): the lowest-HP unit it can reach and attack this turn, walking up first if needed.</li>");
        html.Append("<li>Otherwise the nearest of your units, counted in steps and ignoring rocks. Ties go to the one with the lowest HP, then to the one listed first in your army. If that unit has a shield, it goes for the nearest unshielded unit it can reach this turn instead, and hits the shield only when there is none.</li></ol>");
        html.Append("<p>Every behaviour uses this target. Only the Bomber may bomb a different unit when its target can't be bombed, and the Necromancer raises bone piles before it looks for a target at all.</p>");

        html.Append("<h3 id=\"ai-approach\">Walking up</h3><ol>");
        html.Append($"<li>If it has its AP and can reach a tile from which it can hit the target, it walks to the cheapest such tile. Ranged units need range and line of sight from that tile.</li>");
        html.Append("<li>Otherwise it walks as close to the target as it can: to the reachable tile with the shortest walk to any tile next to the target, the cheapest of those. That walk goes around rocks but through units, so an enemy stuck behind its friends still edges forward.</li>");
        html.Append("<li>If no reachable tile is closer than where it stands, it stays put.</li></ol>");
        html.Append("<p>Ranged enemies that walk up (the Crossbowman) walk to the cheapest tile with a shot. They don't look for cover; the Camp layout just starts them behind rocks.</p>");

        html.Append("<h3 id=\"ai-retreat\">Backing off</h3>");
        html.Append("<p>Used by enemies that want distance, once per turn, when their target is too close: it moves to the reachable tile furthest from the nearest of your units, the cheapest of those. If no tile is further away than where it stands, it stays.</p>");

        html.Append("<h3 id=\"ai-range\">Getting into range from a distance</h3>");
        html.Append("<p>Used by enemies that fight from afar, once per turn. They only consider tiles at least their comfort distance from every one of your units:</p><ol>");
        html.Append("<li>The cheapest tile from which it can use its ranged action on the target, if it still has the AP for it.</li>");
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
        html.Append("Hold Alt to see every enemy's intent at once.</p>");
        html.Append("<table><tr><th>Shows</th><th>When (checked in this order)</th><th>On the board</th></tr>");
        html.Append("<tr><td>Flee</td><td>It carries stolen gold.</td><td>The enemies' back row, where it will escape.</td></tr>");
        html.Append("<tr><td>Howl</td><td>It can howl, and the extra move would let at least one wolf bite this turn that otherwise couldn't (two if the leader could bite itself).</td><td>Every wolf.</td></tr>");
        html.Append("<tr><td>Hellswap</td><td>An imp with Hellswap ready and a swap that drops one of your units next to a fighting ally (see the Imp routine).</td><td>The target, and the tile it will land on.</td></tr>");
        html.Append($"<tr><td>Soul Contract</td><td>A devil with Soul Contract ready that would sign one in its coming turn, after backing off or walking up (its turn is played out on a copy of the board).</td><td>The target, with an arrow.</td></tr>");
        html.Append("<tr><td>Fire Breath</td><td>A fiend with Fire Breath ready that can breathe at one of your units this turn, from its tile or one it can walk to.</td><td>The target, and the tile it breathes from.</td></tr>");
        html.Append($"<tr><td>Rending Blow</td><td>It has Rending Blow ready, more than {Battle.RendingBlowCost} HP, and can reach its target to attack this turn.</td><td>The target, with an arrow.</td></tr>");
        html.Append("<tr><td>Fling</td><td>A goblin that can't reach anyone to strike this turn, with a bomb, mine or unacted goblin next to it and one of your units 2-3 tiles away.</td><td>The target, the goblin to be thrown and its landing tile.</td></tr>");
        html.Append("<tr><td>Grasp</td><td>It grasps, Grasp is ready, it can reach its target to attack this turn, and the target is ranged or Swift and not already held.</td><td>The target, with an arrow.</td></tr>");
        html.Append($"<tr><td>Upheaval</td><td>It has Upheaval ready, can't reach its target to strike this turn, and the target is within {Battle.UpheavalRange} tiles plus its moves. It may still pick a nearer unit once it has walked.</td><td>The target, with an arrow.</td></tr>");
        html.Append("<tr><td>Slam</td><td>It can slam, 2 or more of your units are within 2 tiles, and the slam would deal more in total than a smash.</td><td>The area within 2 tiles.</td></tr>");
        html.Append("<tr><td>Corpse Blast</td><td>It has Corpse Blast ready and a bone pile in range whose 3x3 holds more of your units than of its own (at least one).</td><td>The 3x3 around the pile.</td></tr>");
        html.Append("<tr><td>Raise</td><td>It can raise and a bone pile it can raise (its own side's, or one of your fallen with Soul Harvest) is in raise range and free. AP isn't checked.</td><td>The bone pile.</td></tr>");
        html.Append("<tr><td>Bomb</td><td>It bombs and its target is 2 to bomb-range tiles away with no bomb on its tile. Otherwise it shows Attack, though it may still bomb another unit or back off.</td><td>The target's tile, where the bomb lands.</td></tr>");
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
            case EnemyBehaviour.PackLeader: return "Pack Leader";
            case EnemyBehaviour.Goblin: return "Goblin";
            case EnemyBehaviour.Elemental: return "Elemental";
            case EnemyBehaviour.Shooter: return "Shooter";
            case EnemyBehaviour.Necromancer: return "Necromancer";
            case EnemyBehaviour.Imp: return "Imp";
            case EnemyBehaviour.Devil: return "Devil";
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
                    $"If it can hit its {target} from where it stands: attack, or Bull Rush instead when it has it ready (Bandit Brute), " +
                    "or Grasp instead when it has it ready and the target is ranged or Swift and not already held (Skeleton), " +
                    $"or Rending Blow instead when it has it ready and more than {Battle.RendingBlowCost} HP (Demon).",
                    "Fire Breath when it has it ready (Fiend): from its tile or one it can walk to, at whichever of your units puts the most of them in the line; it beats a strike.",
                    $"Otherwise {approach} to it.",
                };
            case EnemyBehaviour.Shooter:
                return new[]
                {
                    $"If it can shoot its {target} from where it stands: shoot.",
                    "Otherwise walk to the cheapest tile with a shot, preferring one next to a rock (Behind Cover), and shoot.",
                    "With no shot this turn: walk closer, into cover if it can, then go on Overwatch if it's ready.",
                };
            case EnemyBehaviour.Thief:
                return new[]
                {
                    "Carrying stolen gold: if it is on the enemies' back row, escape with the gold. Otherwise move to the reachable tile furthest back (the cheapest of those). Once it can't get any further back this turn: if it ran, it drops caltrops on the free tile next to it nearest your units; if it's cornered, it strikes whoever it can. Light Fingers lets it run in the same turn it steals.",
                    $"Not carrying gold: attack its {target} if it can, otherwise {approach}, like Walk up and hit. Its first hit that does damage steals the gold, and it spends the rest of its moves running.",
                };
            case EnemyBehaviour.Goblin:
                return new[]
                {
                    $"If a bomb or mine lies on an empty tile next to it: fling it at its {target}, or at the nearest of your units 2-{Battle.FlingRange} tiles away.",
                    $"If it can hit its {target} from where it stands: strike.",
                    $"If it can't reach anyone to strike this turn: fling a goblin next to it that hasn't acted at its target, or at the nearest of your units 2-{Battle.FlingRange} tiles away.",
                    $"Otherwise {approach} to it (Slippery: your reach doesn't stop it), then try again.",
                };
            case EnemyBehaviour.Elemental:
                return new[]
                {
                    $"If it can hit its {target} from where it stands: strike.",
                    $"If it can reach a tile to strike from this turn: {approach} and strike.",
                    $"Otherwise walk as close as it can, then use Upheaval on its target, or on the nearest of your units within {Battle.UpheavalRange} tiles.",
                };
            case EnemyBehaviour.PackLeader:
                return new[]
                {
                    "It acts before every other enemy.",
                    "Howl (its action for the turn) if the extra move lets at least one wolf that hasn't acted reach and bite its target this turn, or at least two if the leader could bite someone itself.",
                    "Then like Walk up and hit: bite if it still has its AP, otherwise walk up.",
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
                    $"With its action (two actions a turn from its second phase, Wrath; it loops until both are spent): smash or slam, whichever deals more in total (slam needs 2 or more of your units within 2 tiles; {Battle.SlamDamage} damage to each), ties going to smash.",
                    "Or, with nobody close enough for either: throw a boulder at its target if it is 2 to throw-range tiles away and the rock wouldn't wall the Giant off from it.",
                    $"Without an action to take: {approach} to the {target}, then try again.",
                };
            case EnemyBehaviour.Necromancer:
                return new[]
                {
                    $"If its {target} is closer than {EnemyAI.NecromancerComfortDistance} tiles: {retreat} (once a turn).",
                    "If blowing up a bone pile in range (its side's or one of your fallen) would hit more of your units than of its own, at least one: Corpse Blast, the pile with the most of yours first.",
                    "Otherwise, if any bone pile it can raise is in range and free: raise them all, your fallen included (Soul Harvest).",
                    "If it can hit the target from where it stands: fire a bolt.",
                    $"Otherwise {range} for a bolt (once a turn), with a comfort distance of {EnemyAI.NecromancerComfortDistance}. It never walks closer than that to shoot.",
                };
            case EnemyBehaviour.Imp:
                return new[]
                {
                    "Imps act before the other enemies (after any howl), so the unit they swap in meets demons that still have their turn.",
                    $"If Hellswap is ready: from its tile or one it can walk to that is next to a fighting ally, swap with one of your units 2-{Battle.HellswapRange} tiles away that isn't already in an enemy's reach, so your unit lands next to that ally. Ranged units first, then the tile next to the most allies, then the weakest unit, then the shortest walk.",
                    "Otherwise like Walk up and hit.",
                };
            case EnemyBehaviour.Devil:
                return new[]
                {
                    $"If its {target} is closer than {EnemyAI.DevilComfortDistance} tiles: {retreat} (once a turn).",
                    $"If Soul Contract is ready and one of your units is within {Battle.ContractRange} tiles: sign it on the unit it would kill first, else the hardest hitter, else the weakest.",
                    "If it can hit the target from where it stands: strike with the trident (Long Reach).",
                    $"Otherwise {range} for a strike, or for a contract when one is ready (once a turn), with a comfort distance of {EnemyAI.DevilComfortDistance}.",
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(b), b, "Add a name and steps for this behaviour to the gamepedia.");
        }
    }

    private static void Encounters(StringBuilder html)
    {
        html.Append($"<section id=\"encounters\"><h2>Encounters</h2><p>Battles are on a {Encounter.Size}x{Encounter.Size} board; you place your army in the back two rows. ");
        html.Append("Kill every enemy to win. Battles bring factions you haven't beaten yet, then normal-strength rematches; Act 1 ends with one of the bosses. ");
        html.Append("Every encounter has a layout template: fixed rules, with the exact rocks and start tiles varied each time.</p>");
        html.Append("<h3>Factions</h3><div class=\"grid\">");
        foreach (var enc in Encounter.Regular)
            EncounterCard(html, enc, enc.EnemySummary());
        html.Append("</div><h3>Elites</h3><p>A harder rematch of a faction you've beaten: more of the same enemies. High risk, high reward: ");
        html.Append("normal gold, and its technique pick always holds a rare one.</p><div class=\"grid\">");
        foreach (var enc in Encounter.Elites)
            EncounterCard(html, enc, enc.EnemySummary());
        html.Append("</div><h3>Bosses</h3><div class=\"grid\">");
        EncounterCard(html, Encounter.Necromancer, new[] { "Necromancer", "1 raised unit from each faction you beat this act (Skeletons fill up to 4)" });
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
        html.Append($"Guard; your own blasts and volleys hurt it. Reinforcements arrive on edge tiles at least {Battle.RitualSpawnDistance} steps from it.</p>");
        html.Append($"<p><b>Break the Ritual</b>: {Encounter.TotemCount} totems ({UnitStats.Totem.MaxHp} HP) stand on the far half, guarded by goblins. ");
        html.Append($"Smash them all before the summoning completes at the start of enemy turn {Encounter.SummonTurn}: you win, the guards flee, ");
        html.Append($"and you get the usual technique pick. If the summoning completes, the fight is lost (no gold) and you take a random curse; ");
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
            var users = UnitStats.BaseUnits.Where(u => u.HasAction(a)).Select(u => Link(u.Name))
                .Concat(Technique.All.Where(t => t.Action == a).Select(t => $"{TechniqueLink(t)} ({Esc(t.Line.Name)} technique)"));
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
            var from = UnitStats.BaseUnits.Where(u => u.Has(a)).Select(u => Link(u.Name))
                .Concat(Technique.All.Where(t => t.Ability == a).Select(t => $"{TechniqueLink(t)} ({Esc(t.Line.Name)} technique)"))
                .Concat(Equipment.Catalog.Where(e => e.Ability == a).Select(e => $"<i>{Esc(e.Name)}</i>"))
                .Concat(a == Ability.Immobile ? new[] { "Turret", "Moon Shrine", "Totem" } : new string[0]);
            html.Append($"<tr><td>{Esc(Abilities.Name(a))}</td><td>{Esc(Abilities.Describe(a))}</td><td>{string.Join(", ", from)}</td></tr>");
        }

        html.Append("</table></section>");
    }

    private static void RunSection(StringBuilder html)
    {
        html.Append("<section id=\"run\"><h2>Run &amp; map</h2><ul>");
        html.Append($"<li>Pick 1 of {Banners.StartChoices} random <b>banners</b>, then draft {Run.DraftSize} different base units ({Banners.ThirdWheelArmySize} with Third Wheel). Army size up to {Run.MaxArmySize}.</li>");
        html.Append($"<li>Act 1 has {Run.Steps} steps; each step offers 2 nodes. At steps {string.Join(", ", Run.FightSteps.Select(s => s + 1))} both choices are fights, ");
        html.Append($"so every run has at least {Run.FightSteps.Count} fights before the boss. ");
        html.Append("Step 9 offers one more optional battle; the last step is a boss (Necromancer or Giant).</li>");
        html.Append("<li>A <b>Battle</b> brings a faction you haven't beaten yet; once you've met them all it's a normal-strength rematch. ");
        html.Append($"Step 1 only brings the easier factions ({string.Join(", ", Encounter.Regular.Where(f => f.Pool == Encounter.FirstPool).Select(f => f.Name))}). ");
        html.Append("An <b>Elite</b> (steps 6 and 10) is a harder rematch of a faction you've beaten. Both show their faction when you reach their step.</li>");
        html.Append("<li>The two scenario fights sit at steps 4 and 8, next to a battle: the Moon Shrine at one, Break the Ritual at the other. Shops and ? events fill the rest.</li>");
        html.Append($"<li>Won battle: {Run.BaseGold} gold + {Run.GoldPerSurvivor} per surviving unit (+{Run.AmbushBonusGold} for an ambush). Gold carried off by escaped cutpurses is lost.</li>");
        html.Append($"<li><b>Technique pick</b>: every won fight except the boss and the Moon Shrine offers {Run.RewardCount} techniques your units can learn; one unit learns one and goes up a level (not when relearning a forgotten one), or skip for {Run.SkipGold} gold.</li>");
        html.Append($"<li><b>Shop</b>: {Run.ShopSize} techniques ({Technique.CommonPrice} gold common, {Technique.RarePrice} rare), {Run.ShopItemCount} items, forgetting a technique for {Run.ForgetPrice} gold, a random base-unit recruit for {Run.RecruitPrice} gold (if the army has room), and one banner you don't hold yet for {Banners.Price} gold.</li>");
        html.Append("<li>Every battle starts at full HP and dead units come back. Techniques, levels, items and shrine HP last the whole run.</li>");
        html.Append("</ul><h3>? events</h3><table><tr><th>Event</th><th>What happens</th></tr>");
        html.Append($"<tr><td>Wandering Recruit</td><td>A random base unit joins for free ({Run.FullArmyGold} gold instead if the army is full).</td></tr>");
        html.Append("<tr><td>Abandoned Cart</td><td>A random technique one of your units can learn (a new one gives a level) or a random item, or leave it.</td></tr>");
        html.Append($"<tr><td>Gambler</td><td>Pay {Run.GambleCost} gold for a 50% chance at {Run.GambleWin}.</td></tr>");
        html.Append($"<tr><td>Ambush</td><td>A fight against a faction you haven't met (and won't meet later on the map) that pays +{Run.AmbushBonusGold} gold. If none is left, they flee and drop gold.</td></tr>");
        html.Append($"<tr><td>Shrine</td><td>One unit gets +{Run.ShrineHp} max HP for the run.</td></tr>");
        html.Append("</table><h3>Blessings and curses</h3><p>Hold the Moon Shrine and pick a blessing. Lose a scenario fight and take a random curse ");
        html.Append("(losing both gives two different curses). ");
        html.Append("Either lasts the rest of the run and works every turn of every battle. Omens never pick the shrine itself.</p>");
        html.Append("<table><tr><th>Omen</th><th>Blessing</th><th>Curse</th></tr>");
        foreach (var omen in Omens.All)
            html.Append($"<tr><td>{omen}</td><td>{Esc(Omens.Describe(omen, true))}</td><td>{Esc(Omens.Describe(omen, false))}</td></tr>");
        html.Append("</table><h3 id=\"banners\">Banners</h3><p>A banner bends a rule for the whole army for the rest of the run. You start with one (pick 1 of " + Banners.StartChoices + "), ");
        html.Append($"shops sell one you don't hold for {Banners.Price} gold, and an elite offers one instead of its technique. No limit on how many you hold; each is held once. ");
        html.Append("Taking Third Wheel with more units than it allows: choose who stays, the rest leave.</p>");
        html.Append("<table><tr><th>Banner</th><th>Rule</th></tr>");
        foreach (var banner in Banners.All)
            html.Append($"<tr><td>{Esc(Banners.Name(banner))}</td><td>{Esc(Banners.Describe(banner))}</td></tr>");
        html.Append("</table><h3 id=\"battlefields\">Battlefields</h3><p>Every battle, elite and boss (and an ambush) has one battlefield rule, ");
        html.Append("named on its map node from the start of the run, so you can route towards or around it. The scenario fights have none. ");
        html.Append("A step's fights never share a battlefield or repeat one from the step before. Every rule works the same for both sides.</p>");
        html.Append("<table><tr><th>Battlefield</th><th>Rule</th></tr>");
        foreach (var field in Battlefields.All)
            html.Append($"<tr><td>{Esc(Battlefields.Name(field))}</td><td>{Esc(Battlefields.Describe(field))}</td></tr>");
        html.Append("</table><p>Enemy AI: on Edge of the World an enemy that can push one of your units off the board with Bull Rush or Upheaval does that first. ");
        html.Append("In a Storm, enemies never stop on a marked tile and step off one before acting.</p></section>");
    }

    private static void Rules(StringBuilder html)
    {
        html.Append("<section id=\"rules\"><h2>Battle rules</h2><ul>");
        html.Append($"<li>Moves are separate from AP: every unit walks {Battle.BaseMoves} tile per turn ({Battle.BaseMoves + 1} with Swift, +1 with Spring Boots), 4 directions, no diagonals. Units can walk through their allies (not through objectives) but can't stop on them, and never through enemies. Move first: once a unit spends AP (an attack or action) it can't move again that turn, for both sides. Unused moves don't carry over. AP pays for attacks and actions: every unit has {Battle.AttackCost} AP, so one attack or action per turn, and unused AP doesn't carry over. Blink, Vanish and Throw Shield (and Place Mine with Quick Fuse) are free, usable only before the unit has acted.</li>");
        html.Append("<li><b>Cooldowns</b>: basic attacks have none. Every other action, yours and the enemies', is ready again a set number of the unit's own turns after it's used (cooldown 2: used now, ready again two turns later).</li>");
        html.Append("<li>Side phases: all your units act (one at a time), then all enemies.</li>");
        html.Append($"<li><b>Zone of control</b>: stepping out of a tile next to an enemy uses up all your remaining moves, for both sides. You can always back off one tile, but you can't slip past.</li>");
        html.Append("<li>Ranged attacks need line of sight (rocks block); Arcane Bolt ignores it. Bombs, boulders and fireballs are lobbed.</li>");
        html.Append($"<li>Mines: {Battle.MineDamage} damage ({Battle.BigMineDamage} with Big Mines) to whoever stands on its tile, set off when an enemy steps on one (or a blast, fling or fireball sets it off). Bombs explode at the start of the next enemy turn and hit only their own tile. Fireballs and Volley hit a plus: the tile and its 4 neighbours. Blasts set each other off.</li>");
        html.Append($"<li>Push: a unit pushed into a rock, unit or the edge stops there and takes {Battle.CollisionDamage} damage. An enemy pushed onto a mine sets it off. <b>Weight</b>: a push moves a medium unit 1 tile, a small one 2 and a huge one none (no collision damage either); Battering Ram adds 1 tile to your pushes.</li>");
        html.Append($"<li>Poison: {Battle.PoisonDamage} damage at the start of the target's turn, {Battle.PoisonTurns} times. Burning ground: {Battle.BurnDamage} damage at the start of the turn of whoever stands on it; enemies never stop on it.</li>");
        html.Append($"<li><b>Rubble</b> (the Sapper's keyword): a low heap that blocks walking like a rock but not shots, and gives no cover. A unit pushed into it is crushed for {Battle.CrushDamage} more than a normal collision ({Battle.CollisionDamage + Battle.CrushDamage} in all) and the rubble breaks. One hit from any unit clears it. Broken barricades collapse into rubble.</li>");
        html.Append("<li>Pinned units (Pinning Shot) and slowed units (the Mage's keyword: Frost Nova, Chill, Blizzard) can't move on their next turn.</li>");
        html.Append("<li>Hidden units (Vanish, Smoke Bomb, Into the Shadows) can't be targeted or attacked by enemies until your next turn, though area blasts still hit them. Attacking doesn't reveal them; the first attack while hidden deals +2.</li>");
        html.Append("<li>Snares (Snare Trap) are hidden from enemies: the first enemy to walk onto one stops, takes damage and is pinned. Covering Fire shoots the first enemy that walks onto a tile the archer can shoot at, and stops it there.</li>");
        html.Append("<li>Hover any enemy to see what it plans to do next.</li>");
        html.Append("</ul></section>");
    }

    // ---------- Helpers ----------

    private static string Esc(string s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string Id(string name) => "u-" + new string(name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string Link(string name) => $"<a href=\"#{Id(name)}\">{Esc(name)}</a>";

    private static string TechniqueLink(Technique t) => $"<a href=\"#t-{Id(t.Name)}\">{Esc(t.Name)}</a>";

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
.line { margin-bottom:18px; }
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
