# Tasks

Open work across every part of the game. Any chat working on the game reads this at the start and
updates it when it finishes something or finds something new. Decisions themselves go in
[design-note.md](design-note.md); this file only tracks what is left to do.

Format: `- [ ] task (where it stands / what it needs)`. Tick it with `[x]` and the date when done,
and move done items to the "Done" section at the bottom now and then.

## Visuals (art chat, branch `experiment/art-style`)

### Art needed for gameplay features

Gameplay chats add a row when a feature ships with placeholder visuals. The art chat takes work from this
table and sets the Status (Needed / In progress / Done, with the date) when it starts and finishes a row.

| Feature | What needs art | Now (placeholder) | Status |
|---|---|---|---|
| Moon Shrine | Shrine statue | Code-drawn pillar with a crescent (`PlaceholderArt.MakeShrine`) | Needed |
| Break the Ritual | Totem statue | Code-drawn post with three faces (`PlaceholderArt.MakeTotem`) | Needed |
| Omens | Sun mark and the sun beam striking | Painted sigil on the tile under a faint shaft, charge, painted beam, gold light on the floor and statues, sparks, embers and a cooling scorch (`SunMark`, `Scorch`) | Done 2026-10-27, not playtested. Open: the "Sun beam!" popup is pink, clashing with the gold |
| Omens | Shroud tiles | Flat tile colour (`ShroudColor` in `BattleController`) | Needed |
| Omens | Moon shield on a unit | Moonlight touches the unit, a silver moon sigil on the floor and a veil of moonlight around the statue; flashes and fades as it absorbs, shatters when used up, fades at the next turn; its own silver status pip (`MoonShieldView`) | Done 2026-10-27, not playtested. Silver on both sides for now |
| Moon Shrine | Reinforcements arriving at the edge | The rise animation and a text popup | Needed |
| Scenarios | Blessing pick and item pick screens | Plain IMGUI panels (part of the UI pass) | Needed |
| Zone of control | Enemy reach tiles, orange "costs extra AP" move tiles, dotted link to the enemies holding the selected unit | Flat colours (`EnemyReachColor`, `CostlyMoveColor`) and a red dotted line (`EngagedLinkColor`) in `BattleController` | Needed |
| Unit card (branch `experiment/small-board`) | The hover card's look: panel, header, section colours | IMGUI box with rich-text colours (`DrawUnitCard` in `BattleController`) | Needed only if the experiment is kept |
| Howl (branch `experiment/small-board`) | The Pack Leader's howl, and the "+1 move" on each wolf | "Howl!" and "+1 move" popups and a small shake (`HowlEvent` in `BattleController`) | Needed only if the experiment is kept |
| Small board experiment (branch `experiment/small-board`) | Moves left on a statue (moves are now separate from AP); the tower-roof painting fitted under a 6x6 board | Moves only as text in the HUD ("Moves 1 / 1"); the scenery fits itself to the board size but hasn't been looked at on 6x6 | Needed only if the experiment is kept |

### Other visual work

- [ ] Painted aurora: ChatGPT's free image limit was hit on 2026-10-25; retry the prompt saved in the ChatGPT chat "Generate Crescent Moon Image". Then replace the code aurora with it (maybe for another setting).
- [x] Decide how statues meet the ground (2026-10-28): figures drawn standing on nothing, straight on the floor, no base (see "Bases and size" in the design note). The warrior is redrawn that way; not playtested.
- [ ] Paint the units that are still grey placeholders the new way: ChatGPT or Leonardo, figure only, standing on nothing, plain light grey background, in the same ChatGPT chat as the warrior so the set matches; enemies in dark basalt (skeletons stay pale bone). Save as `Assets/Art/Experiment/Source/<unit>_noground__<site>_<model>.png`, give crouching or towering units a height in `ArtExperiment.FigureHeights`, and run Tools → Art → Import Statues. Done: mage, skeleton (2026-10-28, ChatGPT), elemental, goblin (2026-10-29, ChatGPT), ogre, necromancer, cutpurse, crossbowman, goblin bomber, brute (2026-10-29, Leonardo GPT Image 2.5 Flare, Medium quality, the basalt elemental as the one reference the free plan allows); none playtested. Left: the shard (and the shrine and totem rows in the Art needed table). ChatGPT's free plan allows only about 3 images a day now.
- [ ] Archer, rogue, sapper and wolf keep their painted pedestals or slab for now (user's call); redraw them the new way later if the mix looks off.
- [ ] Improve the player statues' colour (moon marble is better than before, not final).
- [ ] Labels and health bars over the statues: restyle (part of the UI pass).
- [ ] UI/HUD pass (IMGUI panels are still placeholders).
- [ ] Decide whether effects change colour by team (e.g. a moon shield on an enemy in ember red instead of silver). User's call; everything is silver/gold for both sides for now.
- [x] Lightning strike effect onto a tile (2026-10-27, `LightningStrike`): sky flickers, a painted bolt flickering between three shapes, the board flashes white-blue, sparks and a scorch. No rule uses it yet; in test battles press L to strike the hovered tile. Not playtested.
- [ ] Lightning at night (ambient bolts around the tower, planned, later); can reuse the bolt art.
- [ ] Platform built from pieces (painted slabs per tile plus edge and wall pieces along the outline) instead of one painting, so boards can be any size or shape (16x16 already works by zooming out; odd shapes need this). Later, when non-rectangular boards are planned.
- [x] Highlights that fit the theme: smooth glowing tiles with a gentle pulse (2026-10-25). Not yet playtested; path markers not done.
- [x] Feel pass: attack wind-up, hit-stop, projectiles, heavy steps with dust; hit flash and chips, cracks as HP drops, crumbling deaths, popping damage numbers, explosions with shake (2026-10-25). Not yet playtested.
- [ ] Tune the cloud layers' values once settled (live in the Inspector: Scenery → Scenery Clouds) and make them the defaults.
- [ ] Merge `experiment/art-style` into `main` when the battle's look is settled.

## Gameplay (mechanics chat)

- [x] Zone of control made visible while a unit is selected (2026-10-27, branch `feature/zoc-visibility`): enemy reach tiles in faint red, move tiles that cost extra AP in orange, a dotted link to each enemy holding the selected unit, and "Engaged by ..." in the HUD and enemy hover. Not playtested; art row added above.
- [x] Full Act 1 playtest (2026-10-27, logs in `../ArmyRoguelike-sound/Playtests`): won in under 7 minutes with only 4 fights; one faction fight, Rogue at +10 Bloodthirst, Giant dead in 2 turns, Break the Ritual easy, Moon Shrine held at 4 HP. Led to the 12-step act below.
- [x] Act 1 is 12 steps with elites (harder rematches that pay an item pick); battles bring unbeaten factions, then rematches; Bloodthirst capped at +3; run log now gets each battle's log file even when Continue is clicked early (2026-10-28, branch `feature/act1-12-steps`). Not playtested.
- [x] Small-board idea test (2026-10-29, branch `experiment/small-board`, worktree `.claude/worktrees/small-board`): 6x6 board, 1 move per turn separate from AP, Swift keyword, scaled ranges, 1x1 bombs, plus-shaped fireballs, new layouts. See "Experiment: small board, one move" in the design note. Gamepedia on the branch is text only.
- [x] Small-board step 2, one AP (2026-10-29, same branch): every unit has 1 AP, so one move and one action a turn, no carry-over; Momentum gives the action back on a kill; Opening Shot doubles the first hit on each enemy; the Giant's slam deals damage and it picks the bigger hit; the Necromancer raises every pile in range at once.
- [x] Unit kits, wolves first (2026-10-29, same branch): attacks are named actions; Wolf (Blood Scent, Wolf Bite chain) and Pack Leader (Pack Bond HP, Howl); a hover card with Next / Passive / Actions / Now.
- [ ] Unit kits for the other enemy factions (goblins, bandits, elementals, bosses) and for the player's units: a passive and actions for each, designed one faction or line at a time with the user.
- [ ] Playtest the small-board experiment (open the worktree folder as its own project in Unity Hub), then decide: keep and merge, tune, or drop the branch. Watch for: fights now slower (one attack a turn each) and whether HP needs cutting, ranged units too strong, bombers too weak with 1x1 bombs, the bosses' new single actions, crowding in elites and the Necromancer fight.
- [ ] Next: playtest the 12-step Act 1 as a full run and summarise from the logs. Then decide on new node types and events, and the balance pass.
- [ ] Later: split fights into difficulty pools (and add more fights); each map step draws from a pool.
- [ ] Moon Shrine balance: 3 test battles with a tier-1 Warrior/Archer/Rogue were all lost (army wiped twice, shrine broken once); no blessing earned in play yet. Leave for the general balance pass (user's call).
- [ ] More map events and node types: the user wants to think about what else to add. Ideas parked from the scenario list: assassinate the leader, rescue a captive, hold the ground, break through, crumbling tower.
- [x] Moon Shrine scenario node with blessings and curses (2026-10-26), merged into `experiment/art-style`. Tested in test mode only.
- [x] Test mode: "Test battle" on the draft screen (editor and dev builds) starts any army against any encounter with any omen (2026-10-26).
- [x] Break the Ritual scenario node: smash 3 goblin totems before enemy turn 4 for an item pick, or take a curse (2026-10-27, branch `feature/break-ritual`). Not playtested; in test mode as "Break the Ritual".
- [ ] Later (parked ideas, see "Later / maybe" in the design note): Banners, more acts, terrain features, post-battle reward choice, difficulty scaling.
- [x] Gamepedia documents enemy AI: per-enemy actions and traits, shared AI rules, each behaviour's decision list, hover intent (2026-10-26, branch `docs/enemy-ai-wiki`, worktree `../ArmyRoguelike-enemy-ai-wiki`). Not merged yet; after merging, re-export with Tools → Export Gamepedia to get the statue images back (the committed page is text only).
- [ ] When merging `docs/enemy-ai-wiki`: the scenario targeting rules (Moon Shrine: go for the shrine unless a defender is adjacent; Break the Ritual: go for whoever is within 2 tiles of a totem; totems never act and have no intent) need documenting there. When adding enemy AI: add a value to `EnemyBehaviour` in `EnemyAI.cs` and its name and decision list in `GamepediaWriter.BehaviourSteps`; the export fails until the text exists.
- [ ] Crossbowman's role text says "Hides behind cover", but its AI just walks to the cheapest tile with a shot (only the Camp layout starts it behind rocks). Fix the text or give it cover-seeking AI. Ask first.
- [ ] Goblin Bomber hover intent only checks the nearest unit: it shows "attack" when it will bomb a farther unit, and ignores backing off. Decide whether the intent should match.

## Sound (sound chat, branch `feature/sound`, worktree `../ArmyRoguelike-sound`)

- [x] Sound setup (2026-10-27): `Sfx` (View) plays clips from `Assets/Audio/Resources/Sfx`, numbered variants picked at random, saved master and effects volume; `SfxImport` (Editor) sets import settings. Calls added to `SunMark`, `MoonShieldView` and `LightningStrike` (one line per moment). Compiles, tests pass; no clips yet, so it is silent.
- [x] Clips for the three effects (2026-10-27): 13 clips cut from 13 Freesound CC0 recordings by `tools/make_sfx.sh`, credited in `Assets/Audio/CREDITS.md`; raw files in `Desktop/sound sources/`. Sun: the hum loop was dropped after the first listen (didn't fit, not needed); a short `sun_mark` swell played when the mark appears instead, later replaced by two ElevenLabs-generated variants (`sun_mark_1`, `_2`). Then made hotter: a flame burst under the impact and a steaming scorch (`sun_sizzle`; a water-on-a-hot-stove variant was dropped). Charge: 13 variants approved, one later dropped in the audition; 12 played at random (`sun_charge_1` the reverse cymbal, `_2`-`_12` ElevenLabs). Lightning: the electric crackle before the bolt became a muffled far-off rumble (`lightning_rumble`); only the close strike is kept (the second variant sounded distant, 8 LU quieter); then, too aggressive and loud: highs cut at 6 kHz and brought down from -9.4 to -17.5 LUFS, just under the sun impact. Moon shield approved as is.
- [x] Effects playground (2026-10-27, `FxPlayground`): in test battles F1 opens a panel; keys 5-0 play the sun mark, sun strike, moon shield, a hit on it, its fade and lightning on the hovered tile or unit (visual and sound only), master/effects volume sliders, and the sounds grouped by effect in foldable sections (Sun omen, Moon shield, Lightning, Units), one row each: arrows step through and lock a variant (the effect then always plays it), « goes back to random, ▶ plays it alone. Redesigned 2026-10-27 after the first version (two long lists) got hard to use. Then an audition mode: Try on a row plays the real effect on a unit with each variant in turn, the number big on screen, Y keep / N drop / arrows / Space replay / Esc, verdicts in `Playtests/sound-review.txt`. Adds `UnitView.Restore` (and `UndoCrumble`, shared with `Raise`). Used for the first unit-sound review. Touches `BattleController` in two lines (created in `Init` for test battles; `IsOverHud` also checks the panel). Not tried in game yet.
- [ ] Volume sliders in the game itself: the volumes are saved, but only the test playground can change them (part of the UI pass; ask first).
- [x] Sun charge picked in the playground (2026-10-27): all 13 fit, all kept.
- [ ] Sun scorch sizzle: the steaming one is better, but the user may want no sizzle at all. Decide after hearing it in play.
- [ ] Unit sounds, shared by every unit (2026-10-27, ElevenLabs): `unit_attack` (melee blow lands, 9 variants), `unit_hurt` (stone crumbles on the unit taking damage, 12), `unit_die` (statue crumbles, 12), `unit_step` (each tile walked, 14; quietest). First listen: steps should sound like a heavy, solid stone object (no hollow ring) moving on stone, so only 2 of 11 were kept (now `unit_step_1`, `_2`) and 12 new ones added; every hurt sound was dropped (too crack-like) for 12 new crumbling ones. Step source A1 sounded like another ground and is kept in `Desktop/sound sources` for terrain later. First audition (2026-10-27) applied: attack 5 kept of 9, die 4 of 12, step 9 of 14, one sun charge dropped (12 left). Hurt, third round: the user wants a knock (not stone coming off), shorter and clearly smaller than dying, fast and simple; 9 knock variants replaced the crumbling ones (0.4 s, about 3 LU under dying); 7 kept in the audition. Hurt done. One `Sfx.Play` each in `UnitView` (`Lunge`, `Hit`, `Die`, `MoveAlong`). Levelled by loudness so variants match. Not covered yet: ranged shots and projectiles landing, shoves, raising the dead.
- [ ] Before release: ElevenLabs free-plan sounds (`sun_mark_1`, `_2`, `sun_charge_2`-`_12`, the `unit_*` sounds and any later ones) need "elevenlabs.io" in the game credits, or regenerate them on a paid plan. See `Assets/Audio/CREDITS.md`.
- [ ] Unused ElevenLabs generations the user liked, for later effects: "warm golden glow" #1, #3, #4 and all four "heavy stone rune lighting up with heat" (in the ElevenLabs history, sharing to Explore is off).
- [ ] Sound pass, still to do: ranged attacks, mines and bombs, the other effects, ambience (wind, braziers).

## Marketing

- [ ] Not started.

## Done

- [x] Visual direction, battlefield (tower top at night), painted statues for the player's units and wolves, palette system, scenery motion (2026-10-25).
