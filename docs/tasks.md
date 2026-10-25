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
- [ ] Highlights that fit the theme: soft glowing tiles instead of flat, pixelated diamonds (move = pale magic blue, attack = ember red, hover outline, path markers).
- [ ] Feel pass: attack wind-up, hit-stop and recoil, heavy steps with dust, projectiles; damage chips, cracks, crumbling deaths, better damage numbers, explosions.
- [ ] Tune the cloud layers' values once settled (live in the Inspector: Scenery → Scenery Clouds) and make them the defaults.
- [ ] Merge `experiment/art-style` into `main` when the battle's look is settled.

## Gameplay (mechanics chat)

- [ ] Playtest Prototype 5 (Act 1) and summarise from the logs.

## Sound

- [ ] Sound pass: effects for moves, hits, deaths, mines and bombs, ambience (wind, braziers). Not started.

## Marketing

- [ ] Not started.

## Done

- [x] Visual direction, battlefield (tower top at night), painted statues for the player's units and wolves, palette system, scenery motion (2026-10-25).
