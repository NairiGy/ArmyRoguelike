# Tasks

Open work across every part of the game. Any chat working on the game reads this at the start and
updates it when it finishes something or finds something new. Decisions themselves go in
[design-note.md](design-note.md); this file only tracks what is left to do.

Format: `- [ ] task (where it stands / what it needs)`. Tick it with `[x]` and the date when done,
and move done items to the "Done" section at the bottom now and then.

## Visuals (art chat, branch `experiment/art-style`)

- [ ] Painted aurora: ChatGPT's free image limit was hit on 2026-10-25; retry the prompt saved in the ChatGPT chat "Generate Crescent Moon Image". Then replace the code aurora with it (maybe for another setting).
- [ ] Decide how statues meet the ground, on the warrior first (needs ChatGPT for one image):
  1. Have ChatGPT redraw the warrior standing on nothing (no ground, no slab, feet at the bottom of the image).
  2. Render the warrior three ways side by side: painted thin slab only (no code base); painted slab plus code-drawn base (current); no slab plus code-drawn base.
  3. The user picks one.
- [ ] Then redraw the other units (archer, rogue, sapper, and the wolf if needed) the chosen way, save as `Assets/Art/Experiment/Source/<unit>_nopedestal.png` and run Tools → Art → Import Statues. Every future unit is drawn this way.
- [ ] Improve the player statues' colour (moon marble is better than before, not final).
- [ ] Painted statues for the other factions (goblins, bandits, elementals, bosses) and for evolved units; they are still grey placeholders.
- [ ] Labels and health bars over the statues: restyle (part of the UI pass).
- [ ] UI/HUD pass (IMGUI panels are still placeholders).
- [ ] Lightning at night (planned, later).
- [ ] Platform built from pieces (painted slabs per tile plus edge and wall pieces along the outline) instead of one painting, so boards can be any size or shape (16x16 already works by zooming out; odd shapes need this). Later, when non-rectangular boards are planned.
- [x] Highlights that fit the theme: smooth glowing tiles with a gentle pulse (2026-10-25). Not yet playtested; path markers not done.
- [x] Feel pass: attack wind-up, hit-stop, projectiles, heavy steps with dust; hit flash and chips, cracks as HP drops, crumbling deaths, popping damage numbers, explosions with shake (2026-10-25). Not yet playtested.
- [ ] Tune the cloud layers' values once settled (live in the Inspector: Scenery → Scenery Clouds) and make them the defaults.
- [ ] Merge `experiment/art-style` into `main` when the battle's look is settled.

## Gameplay (mechanics chat)

- [ ] Next (agreed 2026-10-27): zone of control is hard to notice: the user only notices it when a unit can still get away. Show enemies' reach (the tiles next to them) while a unit is selected. Design with the user before building.
- [ ] Then: playtest Prototype 5 (Act 1, now with both scenario fights) as a full run and summarise from the logs.
- [ ] Moon Shrine balance: 3 test battles with a tier-1 Warrior/Archer/Rogue were all lost (army wiped twice, shrine broken once); no blessing earned in play yet. Leave for the general balance pass (user's call).
- [ ] More map events and node types: the user wants to think about what else to add. Ideas parked from the scenario list: assassinate the leader, rescue a captive, hold the ground, break through, crumbling tower.
- [x] Moon Shrine scenario node with blessings and curses (2026-10-26), merged into `experiment/art-style`. Tested in test mode only.
- [x] Test mode: "Test battle" on the draft screen (editor and dev builds) starts any army against any encounter with any omen (2026-10-26).
- [x] Break the Ritual scenario node: smash 3 goblin totems before enemy turn 4 for an item pick, or take a curse (2026-10-27, branch `feature/break-ritual`). Not playtested; in test mode as "Break the Ritual".
- [ ] Later (parked ideas, see "Later / maybe" in the design note): Banners, more acts, terrain features, post-battle reward choice, elite nodes, difficulty scaling.
- [x] Gamepedia documents enemy AI: per-enemy actions and traits, shared AI rules, each behaviour's decision list, hover intent (2026-10-26, branch `docs/enemy-ai-wiki`, worktree `../ArmyRoguelike-enemy-ai-wiki`). Not merged yet; after merging, re-export with Tools → Export Gamepedia to get the statue images back (the committed page is text only).
- [ ] When merging `docs/enemy-ai-wiki`: the scenario targeting rules (Moon Shrine: go for the shrine unless a defender is adjacent; Break the Ritual: go for whoever is within 2 tiles of a totem; totems never act and have no intent) need documenting there. When adding enemy AI: add a value to `EnemyBehaviour` in `EnemyAI.cs` and its name and decision list in `GamepediaWriter.BehaviourSteps`; the export fails until the text exists.
- [ ] Crossbowman's role text says "Hides behind cover", but its AI just walks to the cheapest tile with a shot (only the Camp layout starts it behind rocks). Fix the text or give it cover-seeking AI. Ask first.
- [ ] Goblin Bomber hover intent only checks the nearest unit: it shows "attack" when it will bomb a farther unit, and ignores backing off. Decide whether the intent should match.

## Sound

- [ ] Sound pass: effects for moves, hits, deaths, mines and bombs, ambience (wind, braziers). Not started.

## Marketing

- [ ] Not started.

## Done

- [x] Visual direction, battlefield (tower top at night), painted statues for the player's units and wolves, palette system, scenery motion (2026-10-25).
