# Mage and Sapper redesign (night pass, 2026-11-08, not playtested)

> Build notes from the night pass, kept as written. It was merged into `main` on 2026-11-08, so "main is unchanged" and branch names below are history. Some rules changed since (for example slowed is now "move or act, not both"); the design note and the gamepedia hold the current rules.

Built overnight on branch `claude/mage-sapper-pools-f7250b` while the user slept, following the Warrior redesign
(`ad60a93`). Every number, cooldown and rarity here is a placeholder. Nothing was playtested; the bot sim numbers are at
the end.

Goals from the brief: each line gets far more skills than a run can learn (about 10-12 offered), a mix of passives and
actions, about 2/3 common, a few `Requires` chains, one owned keyword per line that other lines can read, and one rare
rule-breaker per line. The Mage is a fragile (5 HP) controller of space with fire, ice and arcane spells; the Sapper is
the engineer who reshapes the board, and its drafts were the weakest in the bot runs, so it also needed to get stronger.

Result: **Mage 13 offered** (9 common, 4 rare; 3 chains), **Sapper 12 offered** (8 common, 4 rare; 2 chains). Total
`Technique.All` is 44 (Warrior 8, Archer 5, Rogue 6 unchanged on this branch).

## Keywords

| Keyword | Owner | Applied by | Rule | Read by |
|---|---|---|---|---|
| **Slowed** | Mage | Frost Nova (base), Chill, Blizzard | Can't move on its next turn (the old Chill rule, `Unit.IsSlowed`). A Colossus is immune. | Shatter (any of your units: +2 on slowed enemies). Free for other lines to read (`IsSlowed`). |
| **Burning ground** | Mage | Burning Ground (fireballs), Firewall, Searing Bolt; the Sapper's Incendiary | 1 damage to whoever starts a turn on it, for 2 rounds (unchanged). **New: enemies walk through burning ground but never stop on it** (same mechanism as storm marks, in `GetReachable`). | Enemy AI (avoids it). Incendiary is the Sapper's cross-line use. |
| **Rubble** | Sapper | Cave-In, broken barricades, Detonate, Demolish on a rock or barricade | A low heap: blocks walking and standing like a rock, but **not shots or line of sight**, and gives **no Behind Cover**. A unit **pushed into rubble is crushed**: +2 on top of the normal 2 collision damage (4 in all), and the rubble breaks. Any unit clears it with one basic attack (the barricade-attack path); enemies with nobody to hit do. Lasts the battle otherwise. | Every push in the game: Shield Bash, Charge, Bull Rush, Upheaval, Slam, Concussive, Frost Nova, Grappling Hook. Both sides. |

Why these: Slowed and Burning were already the Mage's (Chill, Burning), so they only needed more sources and a reason to
care (Shatter for everyone, enemies avoiding fire). Rubble gives the Sapper something that is *its own* on the board
and that every pushing skill in the game wants, which is the cross-unit combo the brief asked for: the Sapper drops
rubble behind an enemy and the Warrior bashes it in for 2 + 2 + 2 = 6. Rubble not blocking shots keeps it from becoming
a second rock that walls your own archers.

Code: rubble is a "low obstacle" on `Board` (`AddLowObstacle`, `IsLowObstacle`; `HasLineOfSight` ignores it), so
pathing, standing and push collisions needed no new code; `Battle.IsRubble`, `AddRubble`, `ClearRubble`, the crush in
`Push`, rubble in `CanAttackBarricade` / `DoAttackBarricade`, rubble excluded from `InCover` and from the crossbowman
AI's `Covered`.

## Mage Apprentice

**Base kit change:** 5 HP, Bolt 2 (range 3, Arcane Bolt: ignores line of sight), action **Frost Nova** replaces Blink.
Why: playtests showed Blink was never used. A fragile backliner still needs an answer to wolves and goblins in its face,
and Frost Nova is that answer while also being control: everything adjacent is slowed *and* pushed away, so it can't
walk straight back. It applies the Mage's keyword from the first fight and pushes into the Sapper's rubble.

| Technique | Kind | Rarity | Needs | Cooldown | Rule | Why / combos |
|---|---|---|---|---|---|---|
| Frost Nova (base) | action | - | - | 2 | Every enemy next to it is slowed and pushed 1 tile away (weight rules: small 2, huge 0). Needs an adjacent enemy. | Escape + control; pushes into rubble, mines, off the Edge of the World. Sets up Shatter. |
| Fireball | action | common | - | 3 | 3 to everyone in a plus up to 3 away (unchanged). | The fire core. |
| Burning Ground | passive | common (was rare) | Fireball | - | Fireballs leave the plus burning 2 rounds (unchanged rule, now with enemy avoidance). | Fireball + Burning zones 5 tiles for 2 rounds. |
| Firewall | action | common | - | 3 | 3 tiles in a line across its aim (the chosen tile up to 3 away and its two neighbours across the longer axis) burn for 2 rounds; whoever stands there takes 1 now. | A wall enemies won't stop on: close a gap, protect the back line, split a pack. |
| Searing Bolt | passive | common | - | - | Its bolt sets the target's tile burning. | Every bolt forces the target to move or burn; with Chill/Nova it can't move and burns. |
| Concussive | passive | rare | Fireball | - | Fireballs push units outward and set off mines and bombs (unchanged). | Pushes into rubble and mines: the Mage/Sapper combo. |
| Chill | passive | common | - | - | Its hits slow (unchanged). | Every bolt slows: Shatter fodder. |
| Ice Wall | action | common (was rare) | - | 3 | A free tile within 3 turns to ice for 2 rounds (unchanged). | Cheap space control; pushing into ice is a collision. |
| Shatter | passive | rare | **none** (was Chill) | - | **Changed:** slowed enemies take +2 from every attack of your units (was: double damage from the mage only). | Army-wide reader of the keyword, like Prey for pinned. No longer needs Chill because Frost Nova slows. |
| Blizzard | action | rare | - | 3 | 1 to every enemy in a plus up to 3 away (no line of sight), and they are slowed. Spares your units. | Mass slow: the Warrior or Rogue can then pick them apart, with Shatter for +2 each. |
| Ward | action | common | - | 2 | 3-point shield on an ally within 3 (unchanged). | Arcane support. |
| Mend | action | common (was rare) | Ward | 3 | Heal an ally within 3 for 3 (unchanged). | The only heal in the pools; common so support builds can happen. |
| Transpose | action | common | - | 2 | Swap places with any unit within 3, friend or foe (no line of sight; not the Giant, objectives or hidden enemies). An enemy put down on a mine sets it off. | Arcane space control: pull an enemy onto your mine or burning tile, swap the Warrior to the front, rescue a cornered ally. |
| **Twincast** | passive | rare | - | - | **Rule-breaker:** the first action it uses each turn costs no AP, so it can still bolt or cast a second spell (it can't move afterwards). Works before or after the bolt. | Two spells a turn for a spell-heavy mage, gated by cooldowns. Build-defining. |

Parked (rules kept, not in `Technique.All`): **Wide Ward** (weak and a chain on a chain), **Blink** (moved out of the
base kit; `Technique.Blink` exists for tests and a possible return).

Chains: Burning Ground and Concussive need Fireball, Mend needs Ward. Shatter's chain was dropped on purpose.

## Sapper

**Base kit change:** 7 HP (was 6), Strike 2, Place Mine (cooldown 2) can now go on any free tile **within 2** (was
adjacent only). Why: the Sapper drafts were the weakest in the bot runs; 1 HP more keeps a melee engineer alive one more
hit, and range 2 lets mines go where the enemy will walk instead of next to the sapper.

| Technique | Kind | Rarity | Needs | Cooldown | Rule | Why / combos |
|---|---|---|---|---|---|---|
| Big Mines | passive | common | - | - | Mines deal 5 and it can have 4 out (unchanged). | Mine build core. |
| Grappling Hook | action | common | - | 2 | Pull an enemy 2-3 tiles away in a straight line (in sight; rubble doesn't block the hook) to the tile next to it. Mines on the way go off; whatever blocks it is a collision (rubble crushes). Not huge units or the Giant. | The engineer's push: drag enemies through its minefield or into rubble. Pulls a crossbowman out of cover. |
| **Quick Fuse** | passive | rare | - | - | **Rule-breaker:** Place Mine is free (use it before acting, like Blink) and ready every turn. | A mine every turn on top of the attack: the minefield build. Capped by the mine limit (3, 4 with Big Mines, +1 at level 3). |
| Barricade | action | common | - | 2 | Build a barricade next to it (max 3, 4 HP). **New:** a broken barricade collapses into rubble. | Walls that turn into weapons when broken. |
| Cave-In | action | common | - | 2 | Drop rubble on a free tile within 3. | Cheap rubble anywhere: behind an enemy for the Warrior's Shield Bash, across a corridor, in front of the Giant's slam. |
| Spiked Barricades | passive | common | Barricade | - | An enemy starting its turn next to one of its barricades takes 1. | Makes barricades hurt to hug; cheap chain. |
| Demolish | action | rare | - | 3 | Blow up a rock, rubble, barricade or ice wall within 3: 3 to everyone in the 3x3 around it (Blast Proof applies). A rock or barricade collapses into rubble; rubble and ice are gone. | Reshapes the map: turns the layout's rocks into bombs, opens sight lines, strips Behind Cover from crossbowmen. |
| Build Turret | action | rare | **none** (was Barricade) | 3 | A turret next to it (unchanged). | The chain made no sense for the rule; it's a strong rare on its own. |
| Throw Bomb | action | common (was rare) | - | 2 | Bomb 2-3 tiles away that explodes at the start of the next enemy turn for 4 (unchanged). | Ranged damage for the weakest draft. |
| Blast Proof | passive | common (was rare) | - | - | Its blasts don't hurt your units (unchanged). | Enables Detonate / Demolish next to your own line. |
| Incendiary | passive | common | - | - | Its mines, bombs and blasts leave the ground they hit burning for 2 rounds. | Cross-line: the Sapper applies the Mage's keyword; a detonated wall leaves a 3x3 enemies won't stop in. |
| Detonate | action | rare | Barricade | 3 | Blow up all its barricades for 3 in a 3x3 around each. **New:** each leaves rubble. | Walls → blast → rubble you can crush enemies into. |

Parked: none. Every old Sapper technique stays offered.

## Combos this enables (examples)

- Sapper Cave-In behind an enemy, Warrior Shield Bash: 2 bash + 2 collision + 2 crush = 6, and the bashed unit takes 2 if a unit is behind it instead.
- Mage Frost Nova next to rubble or a wall: slowed and crushed in one action.
- Grappling Hook through a mine field: 4-5 per mine on the way, then the Warrior is next to it.
- Transpose: the mage stands on its own mine (your units can), swaps with an enemy, the mine goes off.
- Blizzard + Shatter: every slowed enemy takes +2 from every attack of your army that round.
- Firewall or Incendiary across a choke point: 1-move enemies can't step onto it, so the fight is split for 2 rounds.
- Demolish the cover rock next to a crossbowman: 3 in the 3x3, and its Behind Cover is gone (rubble gives none).
- Twincast: Fireball and Frost Nova in the same turn, or Ward an ally and still bolt.

## Logs

- Technique uses are counted as before (`techniqueUses`, by action).
- New status notes (`type = status`): `frostnova`, `hooked`, `twincast` (a free cast), and `slow` from Frost Nova and Blizzard.
- New tile kinds (`tile added` / `tile removed`): `rubble`, `rock` (removed by Demolish); burn tiles from Firewall, Searing Bolt and Incendiary are `burn` as before.
- Explosions: `demolish`. Ticks: `spikes` (credited to the sapper). Effects: `crush` (amount = the extra damage, `unit` = the pusher's credit through the collision hit).
- Not logged yet: how often an enemy's route was changed by burning ground (no event exists for "chose not to stop").

## Art needed (rows for the "Art needed for gameplay features" table)

| Feature | What needs art | Placeholder now |
|---|---|---|
| Rubble | A low heap of broken stone on a tile, clearly lower than a rock; a crumble when it breaks or is crushed | The rock block, grey-brown tint, squashed to 55% height (`BoardView.StyleRock`) |
| Crush (pushed into rubble) | An impact: dust burst, the heap breaking | "Crushed!" popup |
| Frost Nova | A ring of frost from the mage over the 4 tiles around it | "Frost Nova!" popup and the 4 tiles flash |
| Firewall | A line of fire rising on 3 tiles | Tiles flash, then the usual burning tile glow |
| Blizzard | Snow and ice falling on the plus | Tiles flash, "Slowed: no move" popups |
| Searing Bolt | The bolt leaving fire where it lands | The usual burning tile glow |
| Transpose | A swap flash on both units | Both statues jump to their new tiles |
| Grappling Hook | A hook and chain flying out and dragging the target | "Hooked!" popup, then the push slide |
| Demolish | A rock exploding into rubble | The normal explosion flash; the rock becomes the rubble block |
| Spiked Barricades | Spikes on the barricade | None (the barricade looks the same); "-1 spikes" tick popup |
| Twincast | A second-cast glow on the mage | "Twincast: free" popup |
| Cave-In | Stones falling onto the tile | The rubble block appears (the sapper lunges at it) |

## Bot sim (look-ahead bot, 6 seeds per draft and fight, random techniques per level)

`SIM_DRAFT=<label> tools/dev.sh sim 6 fights "Wolf Pack,Bandit Camp,Elementals,Giant"`. `SIM_DRAFT` is new on this
branch (`BalanceSim.AllDrafts`): it keeps only the drafts containing that unit. The Warrior drafts are a control (the
Warrior didn't change; its numbers move only because its partners changed). Win rate per level L0-L3.

Before (commit `86093b3`):

| Drafts | Fight | L0 | L1 | L2 | L3 |
|---|---|---|---|---|---|
| Mage | Wolf Pack | 92% | 100% | 100% | 100% |
| Mage | Bandit Camp | 58% | 97% | 100% | 100% |
| Mage | Elementals | 11% | 64% | 81% | 97% |
| Mage | Giant | 53% | 92% | 100% | 97% |
| Sapper | Wolf Pack | 92% | 100% | 100% | 100% |
| Sapper | Bandit Camp | 53% | 92% | 100% | 100% |
| Sapper | Elementals | 3% | 47% | 61% | 92% |
| Sapper | Giant | 47% | 86% | 94% | 92% |
| Warrior | Wolf Pack | 89% | 100% | 100% | 100% |
| Warrior | Bandit Camp | 72% | 94% | 100% | 100% |
| Warrior | Elementals | 8% | 42% | 78% | 92% |
| Warrior | Giant | 53% | 83% | 94% | 94% |

After (commit `9573cd1`; the Sapper run may have been built before the bot learned Quick Fuse was free)

| Drafts | Fight | L0 | L1 | L2 | L3 |
|---|---|---|---|---|---|
| Mage | Wolf Pack | 92% | 100% | 100% | 100% |
| Mage | Bandit Camp | 67% | 92% | 97% | 100% |
| Mage | Elementals | 14% | 44% | 81% | 86% |
| Mage | Giant | 75% | 92% | 97% | 100% |
| Sapper | Wolf Pack | 94% | 100% | 100% | 100% |
| Sapper | Bandit Camp | 61% | 92% | 94% | 100% |
| Sapper | Elementals | 25% | 31% | 69% | 89% |
| Sapper | Giant | 42% | 81% | 86% | 94% |
| Warrior | Wolf Pack | 94% | 100% | 100% | 100% |
| Warrior | Bandit Camp | 75% | 92% | 97% | 100% |
| Warrior | Elementals | 14% | 47% | 75% | 86% |
| Warrior | Giant | 69% | 83% | 89% | 94% |

Reading (6 seeds, so ±15% noise): level 0 got better for both (base kits: Frost Nova, 7 HP and mine range 2), most clearly the Sapper on Elementals (3% → 25%) and the Mage on the Giant (53% → 75%). Levels 1-3 are flat or a little lower: the bot picks random techniques from bigger pools, and many new ones are combo pieces a random pick can't use. The Sapper is still the weakest line at L1-L2; next step would be more seeds and, if it holds, +1 damage at level 4 for the Sapper (`Growth.DamageLevels`).

## Not done / for the lead

- **Merge with the Demons branch:** demons are Hellborn (no burn ticks). My rule "enemies never stop on burning ground" (`Battle.GetReachable`) should skip burn-immune units: add `&& !unit.Has(Ability.Hellborn)` (or whatever their flag is) to that filter when merging, or demons will avoid fire they don't feel.
- **Merge with the Archer/Rogue branch:** both of us added techniques to `Technique.All`; `TechniqueTests.Catalog_EveryRequirementIsInTheSameLine` asserts the count (44 here) and needs the merged number.
- Enemies don't plan around rubble (they path around it and clear it only when they have nobody to hit) and don't avoid being pushed into it. Fine for a first bundle.
- Enemies don't use Transpose-style knowledge: they can't see mines, so pulling them over mines always works. Intended (the Sapper's whole fantasy), but watch the logs.
- The design note's Mage and Sapper sections, `docs/tasks.md` (art rows above), `CLAUDE.md` status and the gamepedia export are left to the lead, as instructed. `GamepediaWriter` text is updated, so a re-export picks everything up.
- The old "Mage line, Blink" and "Sapper line: unchanged" lines in the design note's "Small board, one move" section are now out of date.

## Review fixes (after the merge into `feature/night-fun`, 2026-11-08)

Merged `feature/night-fun` into this branch first (fast-forward), then:

1. **Rubble no longer walls melee enemies in for good.** `EnemyAI.DistanceField` now runs over rubble and barricades at 2 extra steps each (Dijkstra instead of a plain flood), so a walled-off enemy walks up to the rubble or barricade on its route and, having nobody to hit, breaks it with the existing barricade fallback. The Giant's "would this boulder wall me in" check still treats them as solid (`throughBreakable: false`).
2. **Pierce and Long Reach past rubble:** Long Reach in `CanHitFrom` now checks `Board.IsRock` (the strike lines already did after the merge).
3. **Bone piles under rubble can't be raised:** `CanRaise` and `DoRaise` refuse piles under any obstacle. (Cave-In already refused to drop on a pile.)
4. **Frost Nova ignores hidden enemies** (both the check and the effect).
5. **Hellborn demons may stop on burning ground:** the "enemies never stop on it" filter in `GetReachable` skips units with `Hellborn`.
6. **Card costs:** the hover card's action list uses the same cost and cooldown as the action card (Quick Fuse, unused Twincast). `Abilities.ActionsOf` and `GameBootstrap.Describe` describe unit *types*, where the plain cost is right, so they stay.
7. **Bot and Twincast:** SmartBot no longer ends a unit's turn after an attack while a Twincast cast is still free and usable. (A spell first already left the AP for the attack.)
8. **Battering Ram and Grappling Hook:** the ram's extra damage is skipped on the hook's pull (the only push with a set distance).
9. **Shatter needs Chill again** (rare, +2 kept). Why a requirement rather than +1: without the chain it was a single rare pick that lifted the whole army's damage on everything Frost Nova touched from the first fight; with Chill first it costs two picks and only comes online when the mage slows something every turn, which is the build it is meant for. +1 would have made it a dull rare that nobody picks over Blizzard or Twincast. The Archer/Rogue builder's cap on stacking bonuses covers the rest. Mage chains are now 4 of 13.

New tests in `MageSapperKitTests`: a walled-off brute walks up and breaks rubble, a buried pile can't be raised, Frost Nova ignores hidden enemies, the ram doesn't hit hook pulls, Shatter needs Chill.
