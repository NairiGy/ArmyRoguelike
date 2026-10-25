# Tasks

Open work across every part of the game. Any chat working on the game reads this at the start and
updates it when it finishes something or finds something new. Decisions themselves go in
[design-note.md](design-note.md); this file only tracks what is left to do.

Format: `- [ ] task (where it stands / what it needs)`. Tick it with `[x]` and the date when done,
and move done items to the "Done" section at the bottom now and then.

## Visuals (art chat, branch `experiment/art-style`)

- [ ] Painted aurora: ChatGPT's free image limit was hit on 2026-10-25; retry the prompt saved in the ChatGPT chat "Generate Crescent Moon Image". Then replace the code aurora with it (maybe for another setting).
- [ ] Decide on pedestals: keep, or redraw every statue on a thin slab (warrior and wolf versions exist in `Assets/Art/Experiment/Source/*_nopedestal.png`).
- [ ] Improve the player statues' colour (moon marble is better than before, not final).
- [ ] Painted statues for the other factions (goblins, bandits, elementals, bosses) and for evolved units; they are still grey placeholders.
- [ ] Labels and health bars over the statues: restyle (part of the UI pass).
- [ ] UI/HUD pass (IMGUI panels are still placeholders).
- [ ] Lightning at night (planned, later).
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
