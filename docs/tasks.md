# Tasks

Open work across every part of the game. Any chat working on the game reads this at the start and
updates it when it finishes something or finds something new. Decisions themselves go in
[design-note.md](design-note.md); this file only tracks what is left to do.

Format: `- [ ] task (where it stands / what it needs)`. When one is done, move it to "Done" at the bottom as one
line with the date (`- [x] 2026-11-08 what was done; what is still unchecked`). Anything left over becomes a new open task.

## Visuals (art chat)

### Art needed for gameplay features

Gameplay chats add a row when a feature ships with placeholder visuals. The art chat takes work from this
table and sets the Status (Needed / In progress / Done, with the date) when it starts and finishes a row.

| Feature | What needs art | Now (placeholder) | Status |
|---|---|---|---|
| Moon Shrine | Shrine statue | Painted (Leonardo, the warrior as reference): a sandstone pillar with moon phases and an upturned crescent; the code-drawn `PlaceholderArt.MakeShrine` is the fallback | Done 2026-10-30, not playtested |
| Break the Ritual | Totem statue | Painted (Leonardo, the goblin as reference): a basalt pole of three grinning goblin faces with a spiked crown, ropes and bones; the code-drawn `PlaceholderArt.MakeTotem` is the fallback | Done 2026-10-30, not playtested |
| Omens | Sun mark and the sun beam striking | Painted sigil on the tile under a faint shaft, charge, painted beam, gold light on the floor and statues, sparks, embers and a cooling scorch (`SunMark`, `Scorch`) | Done 2026-10-27, not playtested. Open: the "Sun beam!" popup is pink, clashing with the gold |
| Omens | Shroud tiles | Flat tile colour (`ShroudColor` in `BattleController`) | Needed |
| Omens | Moon shield on a unit | Moonlight touches the unit, a silver moon sigil on the floor and a veil of moonlight around the statue; flashes and fades as it absorbs, shatters when used up, fades at the next turn; its own silver status pip (`MoonShieldView`) | Done 2026-10-27, not playtested. Silver on both sides for now |
| Barricade HP | A barricade that shows its damage (cracks as HP drops) and breaks apart; attackable barricades marked while a unit is selected | The rock tint stays the same until it vanishes; a "-3 (1 left)" / "broken" popup; HP only on the unit card | Needed |
| Attack preview | The HP an attack would take off, on the target statue's HP bar (in pieces, one per HP); "walk 2, attack" by the pointer | A pale pulsing chunk on the statue's bar (`UnitView.ShowLossPreview`) and "-3" or "the Guard guards it" on a dark box left of the bar, "walk N, attack" in orange by the pointer (`DrawAttackPreview` in `BattleController`) | Needed |
| Unit level | A unit's level (techniques learned); statues no longer grow or change stone by tier | "level N" as text on the unit card, deploy list and army panel; `UnitView` still has its tier-based look, unused | Needed (decide whether level shows on the statue) |
| Moon Shrine | Reinforcements arriving at the edge | The rise animation and a text popup | Needed |
| Scenarios and techniques | Blessing pick, technique pick and shop screens (technique list with who-learns-it buttons, items, Forget) | Shop restyled 2026-10-30: offer cards in rows (name, tags, rule, price in gold or red), a Services row (recruit card with statue, Forget chips), and an army row of unit cards with statue portraits that becomes the "who gets it?" picker (`DrawShop` in `GameBootstrap`, colours from `HudArt`). Blessing and technique pick screens still plain. Not playtested | Started |
| Zone of control | Enemy reach tiles, orange "uses your moves" move tiles, dotted link to the enemies holding the selected unit | Flat colours (`EnemyReachColor`, `CostlyMoveColor`) and a red dotted line (`EngagedLinkColor`) in `BattleController` | Needed |
| Unit card | The unit card's look: panel, header, HP bar, keyword chips (status good / bad / neutral, passive, action) and the keyword tooltip | 2026-11-08 (UI chat): opens on left-click and on selection, names only as flat IMGUI chips, hovered chip lightens, description in a dark box left of the card with a gold edge (`DrawUnitCard`, `CardPainter`, `DrawCardTooltip` in `BattleController`; chip colours there). Not playtested | Needed (final look; maybe icons per status) |
| Action cards | The selected unit's action cards at the bottom centre | Solid IMGUI cards with a grey key square, gold edge while aiming (`DrawActionCards` in `BattleController`, `HudArt`). Not playtested | Needed |
| Intent badges | An icon per intent kind over an enemy's head (attack melee / ranged, area, buff, overwatch, grasp, push or flee) | Code-drawn white icons tinted by kind on a dark plate, with the action's name (`HudArt.IconTexture`, `DrawIntentBadges`). Not playtested | Needed |
| Bandits | Caltrops on a tile; the tiles a crossbowman is watching (Overwatch) and its shot; Bull Rush | Grey tile tint for caltrops, a faint pink tint for watched tiles, an "Overwatch!" popup; the shot and the rush use the normal attack and push | Needed |
| Goblins | Fling (a goblin or explosive flying through the air), Powder Keg explosion | Thrower lunges, the thrown goblin pops to its landing tile with a "Flung!" popup; an explosive flies as a small square; the keg uses the normal blast | Needed |
| Howl | The Pack Leader's howl, and the "+1 move" on each wolf | "Howl!" and "+1 move" popups and a small shake (`HowlEvent` in `BattleController`) | Needed |
| Elementals and skeletons | Tremor (the ground shaking around an elemental; the area it reaches in the intent), Aftershock (a dying shard), Grasp (bony hands holding a unit) | Tremor (2026-11-09, replaced Upheaval): a "Tremor!" popup and a screen shake, "Slowed" and "Pinned" popups; the intent lights the area like a smash; Grasp: a lunge and a "Grasped: pinned" popup | Needed |
| Bosses | Wrath, Soul Harvest (your fallen unit rising as a dark bone version), Corpse Blast (a bone pile exploding), bone piles of your own units | "Wrath!" popup; the harvested unit uses your statue with the enemy look and a "Harvested!" popup; Corpse Blast is a lunge, the pile vanishing and the normal explosion; your fallen units use the skeleton bone-pile sprite | Needed |
| Base-line actions | Vanish (hidden rogue), Shield Bash, Pinning Shot, Frost Nova | Vanished statue at 40% opacity (`UnitView.SetHidden`); Shield Bash is just the push; Pinning Shot is a normal shot plus a "Pinned" popup | Needed |
| Small board | Moves left on a statue (moves are separate from AP); the tower-roof painting fitted under a 6x6 board | Moves only as text in the HUD ("Moves 1 / 1"); the scenery fits itself to the board size but hasn't been looked at on 6x6 | Needed |
| Walking through allies | A statue passing over an ally's tile (hop or squeeze past?) | Slides straight through the other statue | Needed |
| Battle badges | Storm bolt on a marked tile, kill skull over a unit, summoning countdown panel | Icons drawn in code on a dark square (`HudArt`, `DrawStormBadges`, `DrawKillBadges`, `DrawSummonCountdown`) | Needed |
| Banners | A symbol per banner | Started 2026-11-04: held banners stand on the battle roof, painted isometric with a shared post and cloth, a colour ramp each and a wind sway (`BannerView`, `BannerCloth.shader`). Cards with the swaying banner in the start screen, the shop and the elite reward (`DrawBannerCard` in `GameBootstrap`), a small row on the map (`DrawBannerRow`), a name and rule on hovering one on the map or the roof (`DrawBannerHover` in `BattleController`). Seen in stills; neither hover has been seen working on a banner. Symbols parked by the user | Started |
| Banners | Mercenaries hire screen and a hired unit in battle; Third Wheel's choose-who-stays screen | Plain IMGUI lists (`DrawMercs`, `DrawDismiss`); a mercenary looks like any other unit in battle | Needed |
| Banners | Vampiric heal on hit, Battering Ram's extra push and hit, a small unit flying 2 tiles | The normal heal popup, a "-2 ram" tick popup, the normal push slide over 2 tiles | Needed |
| Battlefields | The battlefield on a map node (an icon per battlefield) and in the battle HUD | "· Storm" after the node's label, the rule as the map's hover text, "Battlefield: X" text in the HUD and the rule in the placement panel (`DrawMap` in `GameBootstrap`, `OmenLine` / `FieldRule` in `BattleController`) | Needed |
| Battlefields | Edge of the World: a unit falling off the tower | It drops past the edge tumbling, then is pulled behind the tower into the void's black hole, spinning and shrinking, and the ring flares (`UnitView.FallIntoVoid`) | Done 2026-10-31, not playtested |
| Battlefields | Storm: a tile marked for lightning; the strike (the painted `LightningStrike` is used) | Flat pale-blue tile tint (`StormMarkColor` in `BattleController`), "Lightning gathers" and "Lightning!" popups, a "Storm" status chip | Needed (mark only) |
| Battlefields | Fortune's Favored: the favored unit (double damage this round) | An orange status pip (`FavoredColor` in `UnitView`), a "Favored!" popup | Needed |
| Battlefields | What's On His Mind?: something that says intents are hidden | Nothing: hovering an enemy just shows no intent | Needed (maybe a "?" badge) |
| Warrior redesign | Shield Up, Battlecry, Taunt and Throw Shield on the statue; Battlecry's shield on the HP bar (chip "Battlecry shield"); Sweep; Charge with enemies knocked aside; the Placeholder attack; the extra Vanguard row | Text popups and unit-card lines, no status pips. Sweep uses the normal area attack, Charge the normal walk, hit and push; the Vanguard row uses the zone highlight | Needed |
| Run end | The run-end screen with its "serious run / just testing" and 1-5 difficulty answers; the "Abandon run" button on the map | Plain IMGUI buttons with a "> " on the chosen answer (`DrawRunEnd`, `DrawMap` in `GameBootstrap`) | Needed |
| Night pass: cracks | Cracks on a statue (1, 2, scars), a shatter when the 3rd crack breaks it; the Mend and Hone cards | Text only: "N cracks" on the member cards and map army list, a note after the battle, IMGUI chip cards (`DrawMendCard`, `DrawHoneCard` in `GameBootstrap`) | Needed |
| Night pass: champions | A champion look (crown, glow?) and an icon per affix (Armored, Vengeful, Regenerating, Mighty, Fleet) on the statue and the map node | "· Armored champion" after the node label, a unit-card line, "Vengeance!" popup, the normal heal popup | Needed |
| Night pass: Stone Heart | The boss's phase (3 pips or a cracked heart), the phase change, Enraged, falling rocks marked and landing | "Phase N!" popups, the storm-mark highlight and bolt badge for rockfall marks, "Rocks above!" / "Rockfall!" popups, the normal explosion | Needed |
| Night pass: Crumbling Roof | Fractured tiles (1 fracture), holes in the roof, a unit falling through; the scene could look old and broken | Dark brown tile tint for fractures, near-black tile for holes (`BoardView.SyncTiles`), the edge-fall slide and crumble | Needed |
| Night pass: Archer and Rogue | Snare, Overwatch, Fire Arrow, Suppressing Fire, Kick, Smoke Bomb, Shadowstep, Throwing Knife, Rupture, Mark Target / Death Mark, Into the Shadows (full list in `docs/night/archer-rogue.md`) | Tile tints, popups and the normal lunge, shot and slide | Needed |
| Night pass: Mage and Sapper | Rubble (a low heap), Crush, Frost Nova, Firewall, Blizzard, Searing Bolt, Transpose, Grappling Hook, Demolish, Twincast, Cave-In (full list in `docs/night/mage-sapper.md`) | Rubble is the rock block tinted and squashed; popups and tile flashes for the rest | Needed |
| Night pass: Demons | Import the four statues (Tools → Art → Import Statues; heights are set), Hellswap, Rending Blow, Soul Contract sigil, Fire Breath, Fury pips, intent icons (`docs/night/demons.md`) | Code-drawn horned figures (`PlaceholderArt.MakeFigure`), popups, reused icons | Needed |
| Bodyguard (Bandit Brute) | A "Guard" intent icon (the brute about to stand by a crossbowman) | Reused hold icon in the buff colour; the crossbowman lit and the tile it walks to dark | Needed |
| Bombs as ammo (Goblin Bomber) | The bomb lobbed onto a tile next to a goblin, then flung at your unit; the intent showing both steps | The normal bomb throw and the normal fling (an explosive as a small square); intent lights the goblin and the landing tile, badge "Bomb + Fling" | Needed |
| Tension pieces (2026-11-09) | Planted (an archer rooted in place, maybe feet braced or roots of stone, while its shots deal more and pin), Overchannel (the mage charged up for its next spell, then drained and slowed), a Rigged Barricade (the barricade looks rigged, and its blast when it breaks) | Popups "Planted: shots +2" and "Overchannel: next spell x2", "Planted" and "Overchanneled" chips on the unit card; the rigged barricade looks like any barricade and its blast is the normal explosion (`BattleController`) | Needed |

### Other visual work

- [ ] Painted aurora: ChatGPT's free image limit was hit on 2026-10-25; retry the prompt saved in the ChatGPT chat "Generate Crescent Moon Image". Then replace the code aurora with it (maybe for another setting).
- [ ] Archer, rogue, sapper and wolf keep their painted pedestals or slab for now (user's call); redraw them standing on nothing later if the mix looks off. The pack leader already stands on nothing while the wolves keep their slab.
- [ ] Improve the player statues' colour (moon marble is better than before, not final).
- [ ] Labels and health bars over the statues: restyle (part of the UI pass).
- [ ] Statue rim (2026-10-30): at 3 texels it is under a screen pixel and barely shows; 8 texels was too much and was reverted. The user will think about what they want.
- [ ] Faction colours (2026-10-30): goblins (and bomber, totem) lean mossy green through their own enemy ramp (`BattleScenery.FactionTints`). Not playtested. If it reads well, add other factions (demons red, elementals blue-grey).
- [ ] UI/HUD pass (IMGUI panels are still placeholders; see the Art needed rows). Not playtested.
- [ ] Decide whether effects change colour by team (e.g. a moon shield on an enemy in ember red instead of silver). User's call; everything is silver/gold for both sides for now.
- [ ] Lightning at night (ambient bolts around the tower, later); can reuse the bolt art.
- [ ] Void, next: the ring's light faintly on the tower walls (`FxLight`); tune the ring's size and gold and the fall's timing after a playtest.
- [ ] Scene looks for the other battlefields (the user wants different vibes per scenery). Arcane Overflow, What's On His Mind? and Crumbling Roof still use the night tower; ideas, not agreed: aurora and glowing rune joints, fog over the enemy half, an old broken roof for Crumbling Roof. Which battlefield gets which scene is the user's call.
- [ ] **Open question: moving water** (2026-10-31). The second version (flow with swirls at the rocks, whitecaps, clouds as separate layers) still "doesn't look that good" to the user. Parked; don't hand the user knobs next time, bring a finished look to judge. Options not tried: a Leonardo video loop of the painting, painted animated pieces (bobbing ships, surf at the rocks, rain), or a still sea with only the cloud layers moving.
- [ ] Platform built from pieces (painted slabs per tile plus edge and wall pieces) instead of one painting, so boards can be any size or shape. Later, when non-rectangular boards are planned.
- [ ] Path markers for moves (the glowing tile highlights are done).
- [ ] Tune the cloud layers' values once settled (live in the Inspector: Scenery → Scenery Clouds) and make them the defaults.
- [ ] Check in game (built, never seen moving): Elemental facing (mirrored in `UnitView`, 2026-11-01), the map screen's scrolling army list, the void, harvest and sea scenes, the falling-into-the-void animation, the selected statue waking.

## Gameplay (mechanics chat)

### Next up

- [ ] Playtest bundle: the night pass, the one-name-per-rule changes and the UI readability steps are all unplayed or played once. Summarise from the logs.
- [ ] Playtest the tension pieces of 2026-11-09 (Planted, Steady Aim+, Lone Wolf, Overchannel, Rigged Barricades, Big Mines; see the design note). Watch in the logs: Planted shots with Steady Aim+ (up to 7 damage that pins), Overchannelled Fireballs (6 per target), rigged barricades hurting your own units. Balance: Planted and Overchannel are actions the sim bots don't use yet.
- [ ] Playtest the two rule changes of 2026-11-08: slowed ("move or act, not both") and Battlecry's shield (enemies now avoid the shielded units, so one unshielded ally may draw every hit). Battle logs mark slowed moves and hits (`slowed: true`); a slowed move with no hit after it is an action given up.
- [ ] The Mage has no escape since the night pass (Blink was parked and Frost Nova took its slot). A 5 HP unit with no way out died in melee on turn 3. Gameplay decision: Blink back in the base kit, or a self-push on Frost Nova.
- [ ] Hidden state is hard to read (playtest 2026-11-08: the Rogue walked into two wolves believing it was still hidden). The Hidden chip and its tooltip now say "until your next turn"; check in play whether that is enough or the Vanish card and the statue need more.

### Unit kits and techniques

- [ ] Review the night pass's Archer, Rogue, Mage and Sapper pools with the user (Claude designed them alone; see `docs/night/`). The Warrior was designed with the user.
- [ ] Warrior, next, with the user: more skills (8 now, only 2 passives); offers that lean towards a unit's build; rarities; cooldowns; final numbers; a name for Placeholder; whether it gets a base passive after all. Enemies don't avoid Placeholder yet.
- [ ] Synergy holes to design with the user (see the [synergy map](https://claude.ai/artifact/BCBjEq3EFMuN1tZi1sLoyw)): bridges for the weak pairs Warrior-Archer and Rogue-Sapper; payoffs for Burning, Poison and Guard outside their own kits, and for being hidden outside the Rogue; fewer themes for the Mage; an in-kit payoff for the Warrior's pushes; Prey and Shatter still pay the same +2 on different statuses.
- [ ] From the synergy map's "Roles and judgment" tab (first pass 2026-11-09, to discuss with the user): fewer passive burning sources (Searing Bolt, Burning Ground, Incendiary scatter it) rather than a new burning payoff; give the number-only passives a decision (Dodge, Riposte; Steady Aim+, Big Mines and Spiked Barricades were done 2026-11-09); readability: name Throw Shield for what it does, say what Build Turret's turret does, Charge's random push side; Mighty and Arcane Overflow change numbers, not play.
- [ ] Balance tool: the sim bots only move and basic-attack, so the strongest-looking techniques (Opening Shot with Pierce, Momentum, Twincast, Quick Fuse, Third Wheel) can't be measured. Teach the bots to use actions.
- [ ] Keep the synergy map current when skills, items or banners change: edit the entity's makes / uses / hurts tags and its role and judgment (the `JUDGE` table) in [synergy-map.html](synergy-map.html) (JS tables at the bottom), run `tools/dev.sh synergy` so its hover cards get the new rule texts, and republish it to the same link. A new entity's name must match the game's (the command copies texts by owner and name). A fixed row or hole gets "(fixed date)" in its text and moves to the bottom by itself.
- [ ] Optional, from the duplicates list: a keyword for "takes 2 less from every source" (Shield Up, Armored); Hidden, the Night shroud and Behind Cover are three different "can't be hit" rules (fine if they stay clearly different).

### Enemies and AI

- [ ] Faction teamwork ([faction map](faction-map.html), "Gaps and ideas" tab): each weak faction has its fix (Bandits, Goblins, Elementals, 2026-11-09, see Done and the design note). Next: judge them in a playtest against the combo numbers (`Core/ComboTally.cs`, battle log `combos`, `tools/dev.sh sim N combos`). Watch the Bandit elite (it times out more often in the sim than before) and the Elementals (the bot wins these fights more often now, a balance question). Playtest the Demons before copying their shape.
- [ ] Faction map: its unit hover texts are copied by hand from the gamepedia; extend `tools/dev.sh synergy` (or a sibling) to refresh them like the synergy map's.
- [ ] Re-export the illustrated gamepedia (Tools → Export Gamepedia) for the Bodyguard Brute, bombs as ammo, Tremor and Rending Blow +1 (2026-11-09): the routines, Bomb and Tremor texts and intents are written in `GamepediaWriter`, but the editor was open in another session, so `docs/gamepedia.html` still shows the old brute and bomber.
- [ ] Slowed enemy AI plays some turns badly: it doesn't weigh moving against acting when slowed, which makes slow stronger against those enemies than intended. Elementals are fine since Tremor (2026-11-09: a slowed one tremors from where it stands).
- [ ] Enemies can't see mines, snares or the Archer's Overwatch tiles and walk into them. With more traps in the pools (Quick Fuse, Snare Trap) this may be "win without playing". Watch it in a playtest.
- [ ] What's On His Mind? hides every intent, including the demons' swap-then-hit and pushes into holes: maybe still show intents that move your units.
- [ ] Goblin Bomber hover intent only checks the nearest unit: it shows "attack" when it will bomb a farther unit, and ignores backing off. Decide whether the intent should match.
- [ ] Enemy intent doesn't show when an enemy will hit a barricade.
- [ ] The Giant's Slam can push its own units off the edge (the AI doesn't weigh that); the player gets no warning before their own push would kill an ally.
- [ ] Goblin Bombers feel weak (playtest 2026-11-01: bombs deal 2-3 per hit, several explosions hit nobody). Discuss before tuning. Since 2026-11-09 bombers also hand bombs to goblins as ammo (1.6 flung per fight in the sim); judge both in the next playtest.
- [ ] Cutpurse may be too hard to catch (playtest 2026-11-01: in both Bandit Camp fights one escaped with 5 gold by turn 2-3). Discuss before tuning.

### Run, banners and battlefields

- [ ] Battlefields: playtest a run (numbers are placeholders). Then decide whether every fight keeps one (the user's "every fight while testing") and build more from the unbuilt list in the design note.
- [ ] Banners: playtest a run with each (numbers are placeholders). Decide how banners are gained for good (the user's first take). Design Lady Death's reward, then more banners. Check who should be small or huge (Claude's placeholder pick). A hired mercenary looks like any other unit in battle; maybe mark it.
- [ ] The Loser banner with cracks: a death gives +1 HP and +1 damage for a crack that costs 10 to mend. User's call (Loser was left uncapped on purpose).
- [ ] After techniques (parked, suggested order): a first choice at run start then Commanders, unlocks between runs, a branching map, ? events with trade-offs.
- [ ] More map events and node types: the user wants to think about what else to add. Scenario ideas parked: assassinate the leader, rescue a captive, hold the ground, break through.
- [ ] Difficulty pools: only step 1 uses them (`Encounter.Pool`, 2026-11-01). Later steps, ambushes and elites could draw from pools too, with more fights per pool.
- [ ] Moon Shrine balance: 3 test battles with a tier-1 Warrior/Archer/Rogue were all lost; no blessing earned in play yet. Leave for the balance pass (user's call).
- [ ] Still missing for a "full" game: acts 2-3, difficulty levels, unlocks, onboarding, save/continue.

### UI and readability

- [ ] Too many statuses for a 6x6 board (Marked, Exposed, Pinned, Slowed, Hidden, Burning, rubble, snares, smoke, fractures, Fury, Contract, Champion...): the keyword glossary is in; judge readability in play.
- [ ] Which stats stay on the unit card's header line (user will decide); the action cards could become names only too; more keywords if texts keep explaining a rule (Taunt, Engaged, cracks).
- [ ] The damage preview only covers attacks from where the unit stands and walk-up attacks, not actions like Fireball; the kill skull shows for the basic attack, Pinning Shot and Fireball only.
- [ ] Check in game (built, only compiled or tested): the Giant's 42-piece HP bar, the start screen at 720p height, the hero detail screen, the Inter font sizes and bold.

### Logs and tools

- [ ] Automatic screenshots for the feedback chat (asked for 2026-11-01): in the editor and dev builds, while the user plays normally, save PNGs to the git-ignored `Playtests/screens/`, overwriting by name: one each of the start screen, map, technique pick, shop, victory and defeat; in battle, deployment, the first turn with a unit selected, a mid-fight board (round 3 or later), a unit card open and Alt held; and one battle per battlefield. Include the IMGUI screens, no extra input, no visible hitch, nothing in release builds. F9 markers also save a screenshot. Not started.
- [ ] A "new player" run tag (asked for 2026-11-01): add a third choice next to "serious run" and "just testing". Not started.
- [ ] Run tag and rating: stopping Play or closing the window ends a run with no prompt, so those logs stay untagged. Mid-battle there is no "Abandon run" button, only on the map.
- [ ] Logs from before 2026-11-01 keep their negative `durationSeconds` on abandoned battles (`endedAt` minus `startedAt` is a close stand-in) and have no credit for ticks.

## Balance (balance chat)

Targets and working rules: "Balance" in the design note. Report: `tools/dev.sh sim` writes `tools/out/balance-report.md`.

- [ ] Third Wheel may be far too strong: one playtest run (2026-11-04) rated 1, Archer dealing 82 damage in the Moon Shrine, Giant dead in 3 turns. Vampiric may be too costly at level 0: its Curse of the Sun was in both of its first-fight losses. Put both in the banner power test.
- [ ] Ambush can bring Elementals at step 2 (playtest 2026-11-04: won at 1 survivor after 11 turns). Watch it.
- [ ] Late game has no tension: test recruit price (15 gold for a whole unit; suspect), technique prices and level growth, and boss and elite strength, against whole runs. The night pass's cracks, champions and Stone Heart aim at this; compare with the next playtest before applying.
- [ ] Technique and banner power test: whole runs can't show it, because they're decided before most techniques and banners matter (all techniques and items within -4 to +1 points, all 5 banners 82-86%, 2026-11-01). Test each on its own in hard fights at a fixed level.
- [ ] Compare the next playtest with the bot: Moon Shrine (bot 95% at level 1, players 1 of 3 and all test battles lost), first fights. Bosses at fixed levels after the night: Giant 9 / 50 / 79 / 93%, Necromancer 0 / 0 / 5 / 17% at levels 0-3.
- [ ] Battlefields help the player more than the enemy (bot at level 1: Storm and Fortune's Favored mostly +8 to +15 points), partly because the bot sees the dice. Measure again with players before changing.
- [ ] Battering Ram with Edge of the World: a small enemy can be pushed off from any tile, a medium one from 32 of 36. Measure with the smart bot.
- [ ] Draft gaps (bot at level 2 before the night: 34-84%; every top draft has the Warrior; after the night, whole runs 77-97%). Treat as kit design as well as numbers.
- [ ] Get 3-5 outside playtesters on a dev build (user).

## Sound (sound chat)

- [ ] Volume sliders in the game itself: the volumes are saved, but only the F1 playground can change them (part of the UI pass; ask first).
- [ ] Sun scorch sizzle: the steaming one is better, but the user may want no sizzle at all. Decide after hearing it in play.
- [ ] Listen in the F1 playground after levelling: are `lightning_rumble` and `sun_sizzle` (both about -36 LUFS as played) and `moon_absorb` (-31 to -33) too quiet? Is `unit_hurt_5` still too small? Do all 12 `sun_charge` variants earn their place?
- [ ] Sound pass, still to do: ranged shots and projectiles landing, shoves, raising the dead, mines and bombs, the other effects, ambience (wind, braziers).
- [ ] Ambient sounds and music depending on the battlefield (user request, 2026-11-01). Ask the user how they should feel first.
- [ ] Void sounds (2026-10-31): a unit falling into the void still plays the crumble (`unit_die` in `UnitView.FallIntoVoid`), and the void itself is silent. Ask the user how they should feel first.
- [ ] Wake sounds (2026-10-30): the selected statue lifting is silent and its drop reuses `unit_step`; maybe a soft stone scrape on lifting. Ask the user first.
- [ ] Unused ElevenLabs generations the user liked, for later effects: "warm golden glow" #1, #3, #4 and all four "heavy stone rune lighting up with heat" (in the ElevenLabs history).
- [ ] Before release: ElevenLabs free-plan sounds (`sun_mark_1`, `_2`, `sun_charge_2`-`_12`, the `unit_*` sounds and any later ones) need "elevenlabs.io" in the game credits, or regenerate them on a paid plan. See `Assets/Audio/CREDITS.md`.

## Marketing

- [ ] Not started.

## Done

Newest first. Details are in the design note and in git history.

- [x] 2026-11-09 Balance: Rending Blow bonus +2 to +1 after three lost Demon fights (turn-1 kills after a Hellswap).
- [x] 2026-11-09 One fix per weak faction: the Bodyguard Brute (Bandits), bombs as ammo (Goblins), Tremor and Aftershock replace Upheaval (Elementals). Tests and faction map updated; illustrated gamepedia not re-exported (open task). Not playtested.
- [x] 2026-11-09 Faction combo counters (`ComboTally`, battle log `combos`, `tools/dev.sh sim N combos`), with sim numbers on the faction map; setup-before-payoff turn order tried in the sim and dropped.
- [x] 2026-11-09 Tension pieces built: Planted, Steady Aim+ (+3 / -1), Lone Wolf, Overchannel, Rigged Barricades (replaces Spiked Barricades), Big Mines (6 damage, 2 out). Enemies leave a rigged barricade alone when only they would be hurt; skeletons skip a planted Archer. Logged, gamepedia re-exported, synergy map tagged and republished, tests pass (370). Not playtested.
- [x] 2026-11-09 Enemy faction map published (one faction per page: plan, roles, combos, answers grid, teamwork ideas).
- [x] 2026-11-09 Synergy map: a role and a five-criteria judgment for every entry.
- [x] 2026-11-08 Docs cleanup: the design note describes only current rules (replaced ones in `design-history.md`), done tasks moved here.
- [x] 2026-11-08 One name per rule: slowed is "move or act, not both"; Battlecry gives a shield; Elusive, Thorns, Overwatch; Grasp pins; Marked only the Archer's +2; Wall and Blast keywords; push texts without tile counts; Night text says a plus. Tests pass (359), gamepedia re-exported. Not playtested.
- [x] 2026-11-08 Synergy map: every hero skill, item, banner, battlefield, omen and champion affix tagged makes / uses / hurts, with a pair matrix and the holes.
- [x] 2026-11-08 UI readability (UI chat): left-click chooses / right-click acts, the unit card with names only and hover meanings, the keyword glossary (`Core/Keywords.cs`, `View/KeywordText.cs`), HP bars in pieces, the "Prepare your army" start screen, the hero detail screen, short texts and bold numbers, the Inter font. Not playtested.
- [x] 2026-11-08 Night pass merged into `main` after one playtest: cracks, events with a price, champions, Stone Heart, Crumbling Roof, Hone, 59 techniques, Demons. Giant 36 to 42 HP. Illustrated gamepedia re-exported.
- [x] 2026-11-05 Warrior redesign, first version: no base passive, Shield Bash deals damage, Sweep, Taunt and Charge actions, Shield Up, Battlecry, Throw Shield, Vanguard, Placeholder; five skills parked.
- [x] 2026-11-04 Sound: variants within a sound cut to one loudness (`make_sfx.sh`); effects levelled to about -20 LUFS by a table in `Sfx.Play`. By measurement, not yet heard in game.
- [x] 2026-11-04 Map screen: "Abandon run" pinned to the bottom, army and help text in a scrolling list.
- [x] 2026-11-01 Playtest fixes: Momentum gives a move back; one bone pile per tile; enemies prefer unshielded targets; the Night shroud is a plus; clearer Storm marks with a bolt badge; summoning countdown panel; kill skull over a unit.
- [x] 2026-11-01 Balance support: no favored boss, relearning gives no level, Archer damage levels, git stamp on logs, positive durations, tick credit, run tag and rating, "Abandon run", `Battle.Clone()` with `SeededRng`.
- [x] 2026-11-01 Simulator v1, smart bot and run simulator (`tools/balance/`); first number pass (Elementals, Crossbowman, Goblins, Elite Goblins, Break the Ritual).
- [x] 2026-10-31 Banners and weight; battlefields (Edge of the World, Storm, Arcane Overflow, Fortune's Favored, What's On His Mind?), with tests, logs, gamepedia and the test-battle picker.
- [x] 2026-10-31 Battlefield scenes: the void (Edge of the World), the harvest (Fortune's Favored), the stormy sea (Storm); a unit pushed off falls into the void.
- [x] 2026-10-30 Techniques replace evolutions and Trainers (merged into `main`); playtested once as a full 12-step run (won, shops too cheap; fixes in).
- [x] 2026-10-30 Small board merged into `main` after a playtest ("felt ok", 2 wins, 1 loss, 5-6 turns each): 6x6, 1 move and 1 AP, move first, Swift, unit kits for every enemy and boss, base-line actions, cooldowns. Bundle playtest fixes: Upheaval leaves no rock, Shards lost Reform, mines hit only their own tile.
- [x] 2026-10-30 Every unit and objective has painted art (statues standing on nothing; shrine and totem); demon statues painted ahead of gameplay; the selected statue wakes instead of bobbing (idle creaks tried and dropped).
- [x] 2026-10-28 Act 1 is 12 steps with elites, after the 7-step act was won in under 7 minutes (playtest 2026-10-27). Statues stand on nothing, no base. `experiment/art-style` merged into `main`.
- [x] 2026-10-27 Break the Ritual scenario; zone of control shown while a unit is selected; lightning strike effect; sound setup, the first clips and the F1 effects playground with audition mode; unit sounds (attack, hurt, die, step).
- [x] 2026-10-26 Moon Shrine scenario with blessings and curses; test mode; gamepedia documents enemy AI (merged with `docs/enemy-ai-wiki`).
- [x] 2026-10-25 Visual direction (tower top at night), painted statues for the player's units and wolves, palette system, scenery motion, glowing tile highlights, feel pass (wind-up, hit-stop, projectiles, crumbling deaths, damage numbers).
