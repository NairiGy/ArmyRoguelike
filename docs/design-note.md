# Grid Tactics Roguelike — Design Note

Working title: "Army Roguelike" (placeholder; a better name will come later). Separate project from Nardatro. Unity project: `Desktop/Unity projects/ArmyRoguelike`. This file (`docs/design-note.md`) is the canonical copy.
Status (2026-10-30): techniques replace evolutions and Trainers, playtested once (see "Techniques instead of evolutions"); the sections before it are history where they mention evolutions, tiers, Trainers, item picks or Bloodthirst. Earlier: Prototype 5 (Act 1) built: 7-step Act 1 where each of four factions (wolves, goblins, bandits, elementals) appears at most once, rule-based layouts, a rotating boss (Necromancer raising your beaten factions, or the Giant), one evolution per unit per Trainer visit with paid extras, zone of control. Prototype 4: draft 3 of 5 base units, 9-step map with free Trainers at steps 4 and 7 (every unit +1 tier per visit), 5 evolution trees (38 unit types, 3 tiers), only rule-bending items. Full unit/ability/item reference: `docs/gamepedia.html` (generated from game data; re-export via Tools → Export Gamepedia). Earlier, Prototype 3: branching 6-step map (battles, shops, ? events, ogre boss), built-in unit abilities, ability items, the sapper recruit with mines, and a goblin bomber battle. Prototype 2 (fixed mini-run) was playtested once: won, about 5.5 minutes. Fixes from it: walk-up attacks, End turn confirmation, necromancer no longer idle. Prototype 1 (one battle): no bugs, felt OK, won 2/2, "not super easy". Numbers marked *(placeholder)* are only examples.

## Pitch

A roguelike army-builder: lead a small squad across a Slay the Spire–style map of fights, then fight turn-based tactical battles against an AI army on a small isometric 2D grid.

## Theme and look

- Units are **living statues**, like the wizard-chess pieces in the Harry Potter films.
- Generic animation shared by every unit, with no per-unit animation:
  - selected: bobs up and down
  - attack: quick lunge forward and back
  - hit: red flash and shake; death: sink and fade (skeletons turn into a bone pile)
  - boulder: arcs through the air; raise: the bone pile pulls itself back up
- Enemies are living statues too.
- The board should **not** look like a chessboard.
- Engine: Unity 6.

## Visual direction

- **Look:** chunky, stylized, hand-painted statues on square pedestals, seen in 3/4 isometric view on the 2:1 board.
- **Teams read by stone:** player statues are pale sandstone, enemies dark basalt, so enemy art is generated in dark basalt (the enemy colouring expects dark art; light art comes out pale). The one exception is the undead: skeletons stay pale bone on purpose (decided 2026-10-28). Rune glow is an optional extra, not required.
- **Art source:** AI-generated 2D images: ChatGPT (free plan, about 3 images a day) and Leonardo.ai with GPT Image 2.5 Flare (free plan, 150 tokens, about 73 per High-quality image; free generations are public). From 2026-10-31 Flare asks for a paid plan; on the free plan only Auto Image works (it picked Lucid Origin, 8 tokens per image, JPEG). The user bought Leonardo's Essential plan the same day (8,500 tokens a month, all models, private generations: keep Private Mode on); Nano Banana (40 tokens) is good at editing an image while keeping its layout, e.g. colouring a greyscale painting. Every generated image is named after the tool that made it, `<name>__<site>_<model>.png` (e.g. `ogre_noground__leonardo_gpt-image-2.5-flare.png`, `ogre_noground__chatgpt_gpt-image.png`; ChatGPT doesn't name its image model), in `Desktop/ai art` and in `Experiment/Source`; the import tool finds files by their plain name with or without the suffix. Every new unit uses the same prompt template, on a plain light grey background, with an existing unit image attached as a style reference. Tested with warrior, archer and wolf: they match as one set.
- **Generation rules (2026-10-31, user):** new images (scene backdrops, statues) with GPT Image (ChatGPT, or GPT Image 2.5 Flare in Leonardo); edits that keep the layout (colouring, removing clouds, masks) with Nano Banana. References are original GPT-made art (`iso_tower.png` for style and camera), never a Nano-made derivative. Backdrops seen from the tower: "looking steeply down, no horizon, no sky, fills the frame", no clouds (clouds are code layers), empty centre for the tower, landmarks near the corners. Make two variants, composite each under the tower layer and compare in context before wiring one in. Don't ask the user to tune sliders to judge a look; bring a finished one.
- **Pipeline:** an editor tool (Tools → Art → Import Statues) cuts out the background. Figures drawn with no ground (`Source/<unit>_noground.png`) are scaled to their height and stand between their feet; older art with a pedestal (`_iso`) or slab (`_nopedestal`) is scaled by that pedestal's width (0.6 tile). Left and right facing come from flipping the sprite.
- **3D route** (image → Tripo/Meshy model → baked from the board's camera): tested. Consistent and controllable, but it looked flatter and slimmer than the 2D images. Parked as a fallback, for example if units ever need more than two facings.
- **Battlefield:** the board is the roof of a tall tower above the clouds, at night. The scene is one greyscale isometric painting (ChatGPT) whose empty roof is fitted under the board, coloured through a palette ramp: the same painting can take any palette, so moods are cheap. Kept for later as alternatives: a temple courtyard and a graveyard (same technique), a tower built from pieces in front of a flat sky, and the amber-dusk and toxic-green palettes.
- **Battlefields change the scene (started 2026-10-31):** each battlefield is meant to get its own look, so the rule reads before any text. First: **Edge of the World is the void**. The roof hangs over a magic black hole (ancient magic, not space sci-fi): a greyscale void painting (ChatGPT, framed like the tower painting) replaces the sky and cloud sea, the tower's cut-out walls dissolve into it, a painted ring of light (Leonardo) turns slowly over the painted hole, broken rocks spiral into it, now and then a stone falls off the walls, and pale specks peel off the roof's edges. It has its own palette (black, indigo, violet stone, pale gold light). `BattleScenery.Apply` swaps the tower for the void whenever the battle's field is Edge of the World; `SceneryVoid` moves it. A unit pushed off drops past the edge tumbling, then is pulled behind the tower into the hole, spinning and shrinking, and the ring flares (no crumble, no bone pile). Not yet: the ring's light on the walls. Second: **Fortune's Favored is the harvest** (2026-10-31): the same tower in daylight, high above a patchwork of ripe wheat fields with a river and windmills (a painting of the land alone, Leonardo Nano Banana, filling the screen behind the tower layer), the walls sinking into warm haze, thin cloud drifting below, cloud shadows and faint gusts over the fields (`SceneryFields`), chaff motes, no braziers. **The land is drawn in its own colours, not through the palette** (user, 2026-10-31: one hue looked flat; the river should be blue, clouds white): the greyscale painting was coloured in by Nano Banana with its layout kept. The Harvest palette is only sunlit stone (warm greys to near-white) for the tower, cloud and haze. So "one palette for everything" holds for night scenes; daylight scenes may use coloured backdrops. Each scene is a backdrop of its own behind the one tower layer, so new scenes need only a background painting. Third: **Storm is the stormy sea** (2026-10-31): the same tower high above dark teal waves with white foam, rocks and a couple of small ships, ragged storm cloud below (a full-colour painting, Leonardo Nano Banana, two made, the more dramatic one used, the other kept in `Experiment/Source`), cold "Storm stone" palette, braziers lit. It shares the wheat scene's code (`FarBelow` scenes: coloured backdrop, haze, `SceneryFields` cloud shadows and gusts). The sea moves (2026-10-31, second version): a first noise wobble looked unnatural (the painted clouds rippled with the water), so now the clouds are removed from the painting (Nano Banana) and drift over it as separate layers like the night sky's; the `Water` shader is a flow map, dragging the painting with the wind (down the screen, across the painted crests) and swirling it around the rocks, two copies half a cycle apart cross-fading; whitecaps (code sprites) bloom, drift and fade on open water; foam glints faintly. A water mask (Nano Banana, light = water) keeps rocks and ships still. Live knobs on "Land" in Play (`SceneryWater`: flow distance, flow speed, foam glint, whitecap count and strength). The user wasn't happy with it and left moving water as an open question (see tasks). Still to come: Arcane Overflow and What's On His Mind? (the night tower for now); maybe ships as bobbing sprites, surf bursts at the rocks, rain.
- **One palette for everything:** each palette also has a ramp per team; painted statues are drawn by brightness through their team's ramp (player warm and light, enemy cool and dark), so switching palette recolours units and scene together without new art. A whole-screen snap to ~20 colours was tested and kept as an idea.
- **Faction colours (tried 2026-10-30, goblins only):** enemy factions lean their stone towards a hue of their own, done in code, not in the art (the statue shader keeps only brightness, so colour in the images is lost anyway). Each faction's ramp is the enemy ramp moved 45% towards its colour at each stop's own brightness (`BattleScenery.FactionTints`, `FactionTintAmount`), so enemies stay dark and the teams read apart first, the faction second. Goblins, bombers and their totem lean mossy green. Ideas if it works: demons dark lava red (they may then need a different rim than ember red), elementals cold blue-grey. Colour on parts only (glowing crystals, burning eyes) would need a mask per image.
- **Board and facing:** the player deploys at the top of the screen, facing down-right towards the enemy; enemies face down-left, towards the camera, so their faces show. The tower roof has painted slabs and rocks and no grid; the clouds drift and swirl.
- **A scene that breathes:** crisp cloud shadows sweep across the roof and the statues, and the painted clouds, the cloud bands and the shadows all drift with one wind. Dust motes drift, two braziers on the side corners burn with flickering flames (a warm light pool on the floor was tried and dropped), units have soft contact shadows and a vignette frames the screen. Statues shed the odd speck of dust (a breathing squash was tried and dropped). Stone only moves rigidly, in short sudden bursts (2026-10-30). Idle creaks (one statue at a time jerking into a small tilt every few seconds) were tried and dropped: in a still scene any lone movement grabs the eye ("what was that?"), and making every statue creak would only make the board twitch. The selected statue wakes, lifting off the floor with dust falling off and a thicker, steady, brighter rim, and drops back with a thud and a puff of dust when deselected (this replaced the bob) and have a pulsing rim in team colours (3 texels, under a screen pixel, so it barely shows; 8 texels was tried 2026-10-30 and was too much; the user will decide later) (pale blue for the player, ember red for enemies). Player statues are moon marble (pale cool white); torch gold and pale bone are kept as alternatives.
- **Bases and size (decided 2026-10-28):** unit art is the figure only, drawn standing on nothing (no ground, no slab, no pedestal), and it stands straight on the floor with a darker, tighter contact shadow under its feet. This is the most flexible: a base (the square one drawn in code, `BattleScenery.DrawnBases`, now off, or others later) can be added under any figure without new art. Figures are sized by height, not by a pedestal: 1.1 world units for a standing human by default, with per-unit heights for units that crouch or tower (`ArtExperiment.FigureHeights`), and stand halfway between their feet. Units are drawn at 0.7 of the art's size (`BattleScenery.UnitScale`, also live in the Inspector). Compared on the warrior: painted slab only, slab on the code base, no slab on the code base (centred or not), no slab and no base; the last was chosen. Units whose art still has a painted pedestal or slab keep it until redrawn. Lightning at night is planned for later.
- **Effects:** painted where they sit still, code where they move. ChatGPT paints the textures (glowing parts on black, drawn additively; burns on white, multiplied onto the floor), imported with mipmaps at 512 px into `Art/Effects/Resources/Effects`; code animates them, adds particles, and lights the floor and statues around them through `FxLight` (up to four effect lights, read by the scenery and statue shaders). Floor effects draw above every tile and below anything standing. Each effect sprite gets a material of its own (sprites sharing one get batched and can be drawn with the wrong texture). Done this way: the sun omen (sigil, painted beam, scorch), the moon shield (moon sigil, veil of moonlight) and a lightning strike (three painted bolts on one sheet, flickered between; test key L, no rule uses it yet). For now effects look the same whichever side they help.
- **Still open:** placeholder statues for the other factions, terrain, the other effects, UI/HUD, how evolution tiers look, and the unit labels that currently sit over the statues.

## Sound

- **Mood:** night on a tower roof above the clouds, living stone statues; magic sounds weighty and a bit ancient, not sci-fi.
- **Sources:** ElevenLabs Sound Effects is the main source (decided 2026-10-27 after the sun mark and sun charge). Free libraries whose licence allows commercial use (Freesound CC0 only, Kenney CC0, Sonniss GDC bundles) are the fallback, mostly for natural recordings such as thunder and glass. The free ElevenLabs plan needs an elevenlabs.io credit; a paid plan before release removes that. Raw files stay outside the repo, in `Desktop/sound sources/`; the trimmed clips go in `Assets/Audio/Resources/Sfx`, each listed with source, licence and (for generated ones) the prompt in `Assets/Audio/CREDITS.md`.
- **Workflow for a new sound:**
  0. Ask the user first how the sound should feel (material and weight, what it must not sound like, length) and write the prompts from the answers. Guessed prompts missed badly on the first unit sounds.
  1. Write 3-4 prompts that describe the moment and the mood ("warm, heavy, ancient magic, not sci-fi, no electronic sounds"), with the length set to the effect's timing. Generate 4 variations each in ElevenLabs (the user is signed in in the browser pane; sharing to the Explore page is off).
  2. Download every variation (standing OK) and cut them with `tools/make_sfx.sh`: hits start at 0 ms, a build-up ends on its loudest point, levels set by peak and checked in LUFS against the other sounds; variants of one sound are cut to the same loudness (`loud`), not the same peak.
  3. Add them as numbered variants (`name_1`, `name_2`, ...). The user auditions them in a test battle: F1, then Try on the sound plays its real effect on a unit with each variant in turn, the number big on screen; Y keeps, N drops. Verdicts land in `Playtests/sound-review.txt` (one "variant verdict" line each), which Claude reads; clear the lines once applied, since clip numbers change when the set changes.
  4. The user picks; keep the winners (several can stay as random variants), drop the rest from the script, and credit the prompts.
- **How it plays:** `Sfx.Play("name")` on the same line as the visual moment it belongs to, so sound and animation stay in sync; clips are trimmed so the hit is at 0 ms. Loops (`*_loop`) follow the object that owns them. Sound is 2D with a gentle left-right pan from the screen position.
- **Volume:** master × effects, saved in PlayerPrefs. An AudioMixer comes later, with music and ambience.

## Design goals

- Fun from the very first turn, with real choices.
- The board should be readable at a glance.
- Its own identity, not a copy of an existing game (see "Unusual rule" below).

## Run structure

- New run = new army.
- A map of nodes, as in Slay the Spire. You go from objective to objective.
- The army carries over between fights.
- Node types beyond battles (camp, events…): not decided. Shops exist (see Prototype 2).

## Army

- 3–4 units maximum for now.
- Different unit types (warrior, archer, …). Each type differs in AP pool and costs.
- A unit is a "card" in the army-builder sense, but there is no drawing in battle.
- **Battle effects stay in the battle.** Every unit starts every fight at full HP. Damage, death and other effects reset afterwards.
- **Run-persistent growth comes only from explicit abilities**, for example "this unit gets +1 damage after killing an enemy, persists for the run". There is no universal leveling (once techniques are built, a unit levels only by learning techniques; see "Techniques instead of evolutions").
- Identity direction: each unit carries its own rule, like Balatro's jokers. Abilities come from the unit type (one built-in each) and from items; there are no separately earned abilities.
- **Equipment:** units can wear equipment, possibly bought at shops. (Items stay once techniques are built: their one slot, sold in shops; see "Techniques instead of evolutions".)
- **Rewards scale with how many units survived the fight.** This is the cost of death.
- Difficulty modifiers may change these rules later.

## Battle

- Grid: 8x8 to start. Might be too small; decide after playtesting.
- **Deployment:** the whole army fights. You place units freely in your back 2 rows.
- **Win condition:** kill all enemies. Other objectives later.
- **Terrain:** simple obstacles for now.
- **Combat:** deterministic. No hit chances or damage rolls.

## Turns and action points

- **Side phases:** all of your units act, then all enemies act. No initiative order.
- **Zone of control:** each step out of a tile next to an enemy costs +2 AP *(placeholder)*, for both sides. Stops cheap hit-and-run.
- Within your phase each unit finishes its turn before the next unit starts, like DOS1.
- Action points per unit, like DOS1. Example *(placeholder)*:
  - 4 AP per turn
  - moving 1 tile = 1 AP
  - basic attack = 2 AP
  - up to 2 unused AP carry over to the next turn
  - unit types change the pool and costs (for example, a warrior moves cheaply and an archer shoots cheaply)

## Enemy AI

- Intent icons: hovering over an enemy shows who it wants to attack. Holding Alt shows every enemy's intent at once (2026-10-29, small-board branch). Since 2026-10-30 each shown intent also puts a badge over the enemy's head: an icon (sword, arrow, burst, chevrons for buffs, eye, a "no move" ring, push arrows) and the action's name, coloured by kind (orange harm, green buff, purple control, gold flee). Icons are code-drawn placeholders (`HudArt`).

## Prototype 1: one battle

Goal: find out whether a single battle is fun. There's no map, rewards, shop or abilities yet. Units are split only by type: melee or ranged.

**Shared rules (placeholder)**

- Moving 1 tile = 1 AP. An attack = 2 AP. Up to 2 unused AP carry over.
- Units differ in AP pool, HP, damage and range.
- Movement and attacks use 4 directions only, with no diagonals (try this first).
- Obstacles block movement and the archer's line of sight.
- The board is 8x8 with a few obstacles. Each side deploys in its back 2 rows, leaving 4 empty rows between the armies.

**Player units (placeholder numbers)**

| Unit    | Type   | HP | Damage | Range | AP | Example turn |
|---------|--------|----|--------|-------|----|---|
| Warrior | melee  | 12 | 3      | 1     | 3  | move 1 + attack, or move 3 |
| Archer  | ranged | 6  | 2      | 4     | 4  | move 2 + shoot, or shoot twice |
| Rogue   | melee  | 5  | 4      | 1     | 5  | move 3 + attack, or move 1 + attack twice (8 damage) |

**Enemies: wolf pack (placeholder numbers)**

| Unit        | HP | Damage | Range | AP | Rule |
|-------------|----|--------|-------|----|---|
| Wolf (x3)   | 6  | 2      | 1     | 4  | — |
| Pack leader | 10 | 3      | 1     | 4  | Wolves within 2 tiles get +1 damage |

- If the pack leader dies, every wolf gets -1 damage for the rest of the battle.
- That gives three tiers: 3 damage near the leader, 2 away from it, 1 once it's dead.
- Intent: hovering over an enemy shows which unit it plans to attack.
- AI targeting: each enemy goes for the **nearest** player unit.

## Prototype 2: mini-run

Goal: find out whether building up the army across fights feels good.

**Run** — a fixed chain, no map yet: Wolf Pack → shop → Necromancer → shop → Ogre → "Run won!". A defeat ends the run.

**Gold** — after a won battle: 10 + 5 per surviving unit *(placeholder)*. More survivors = more gold.

**Shop** — 3 random items from the catalog. No rerolls, no selling. One slot per unit; buying replaces (and loses) the old item. Decisions come from limited gold, the single slot, and items with downsides.

| Item | Effect | Price *(placeholder)* |
|---|---|---|
| Iron Helm | +3 HP | 6 |
| Heavy Armor | +6 HP, -1 AP | 8 |
| Whetstone Blade | +1 damage | 10 |
| Longbow | +1 range (ranged units only) | 8 |
| Swift Boots | +1 AP | 12 |
| Berserker Axe | +2 damage, -3 HP | 9 |

**Battle 2: Necromancer** *(placeholder numbers)*

| Unit | HP | Damage | Range | AP | Rule |
|---|---|---|---|---|---|
| Skeleton (x4) | 4 | 2 | 1 | 3 | Leaves a **bone pile** when killed |
| Necromancer | 6 | 2 (bolt, needs line of sight) | 3 | 3 | Raises a bone pile within 4 tiles to a full-HP skeleton (costs 3 AP). Backs away from units closer than 3 tiles. With nothing to raise, walks up to bolt range (3) but never closer, and shoots. |

- A statue standing on a bone pile stops it being raised. Piles don't block movement.
- Kill the necromancer and the piles stay dead.
- A raised skeleton waits until the next enemy turn before acting.

**Battle 3: Ogre** *(placeholder numbers)* — alone, 2x2 tiles.

| Unit | HP | Smash | Boulder | AP |
|---|---|---|---|---|
| Ogre | 30 | 5 damage to **every** adjacent unit, 2 AP | 3 damage, range 2–5, 3 AP, once per turn, ignores line of sight | 4 |

- The boulder comes to rest on the free tile next to the target closest to the ogre and **stays there as a rock**.
- Hovering the ogre shows the smash ring, the boulder target and the tile the rock will land on.

## Prototype 3: abilities, recruit, map

**Map** — 6 steps; pick 1 of 2 nodes per step (Slay the Spire style). Step 1 is always two different battles; steps 2–5 mix battle (50%), shop (25%) and ? event (25%), never two identical nodes side by side; step 6 is always the ogre. Shops only appear as map nodes now (no automatic shop after battles). Gold after a won battle: 10 + 5 per survivor (+5 for an ambush).

**Built-in abilities** — one per unit type:

| Unit | Ability *(placeholder)* |
|---|---|
| Warrior | **Shield Wall:** allies next to it take 1 less damage (min 1), from any source |
| Archer | **Steady Aim:** +1 damage if it hasn't moved this turn |
| Rogue | **Bloodthirst:** +1 damage per enemy kill, kept for the run (capped at +3 since 2026-10-28) |
| Sapper | **Mines** (see below) |

**Items with abilities** (in the shop/cart pool next to the 6 stat items): Vampire Fang (heal 2 on a kill, 9g), Thorned Mail (melee attackers take 1, 8g), Spring Boots (first tile each turn is free, 7g), Lucky Charm (once per battle, survive a killing blow at 1 HP, 10g). There are no other "earned" abilities: abilities come from unit types and items only.

**Sapper (recruit)** — 6 HP, 2 damage melee, 4 AP. Army max is 4. Recruited for 15 gold in a shop (when the army has room) or free from the Wandering Sapper event.
- **Place mine:** 2 AP, on a free tile next to it; max 3 of its mines on the board.
- An **enemy** stepping onto a mine stops there and sets it off: 4 damage to every unit in the 3x3 around it, **friendly fire included**. Player units walk over their own mines safely.
- A blast sets off any other mine or bomb inside it: **chain reactions**. Kills by a mine count for the sapper (Bloodthirst, Vampire Fang).
- Enemies don't see mines and walk into them.

**Battle: Goblin Bombers** *(placeholder numbers)*

| Unit | HP | Damage | AP | Rule |
|---|---|---|---|---|
| Goblin Bomber (x3) | 4 | 1 (stab) | 3 | Lobs a bomb (2 AP, range 2–4, ignores line of sight) onto the nearest unit's tile, once per turn. Keeps 2+ tiles away. |
| Goblin (x2) | 5 | 2 | 4 | Melee runner |

- A bomb sits on its tile with its 3x3 blast area shown, and **explodes at the start of the next enemy turn** for 3 damage to everyone in the area. You get one turn to step out.
- Bomb blasts set off mines and vice versa.

**? events** (one at random):
- **Wandering Sapper:** joins for free (15 gold instead if the army is full).
- **Abandoned Cart:** a random catalog item for any unit, or leave it.
- **Gambler:** pay 10 gold, 50% to get 25 back.
- **Ambush:** a wolf pack fight that pays +5 gold.
- **Shrine:** one unit gets +2 max HP for the run.

## Prototype 5: Act 1

Goal: battles that don't repeat and a boss that checks the build. The run is a single Act 1 for now; later acts come after this one feels good.

**Map (7 steps, replaced on 2026-10-28 by the 12-step act below)** — the four factions are shuffled onto four battle nodes, so no faction appears twice in a run:

| Step | Choice |
|---|---|
| 1 | Battle (faction A) or Battle (faction B) |
| 2 | Shop or ? |
| 3 | Battle (faction C) or ? |
| 4 | Trainer or Shop |
| 5 | Battle (faction D) or ? |
| 6 | Trainer or ? |
| 7 | Boss: Necromancer or Giant (one per run) |

- An Ambush event fights a faction you haven't met that isn't waiting later on the map; if none is left, the ambushers flee and drop 10 gold.

**Layouts** — every encounter has a template with fixed rules; rock positions and start tiles vary within them. The player's back two rows are always clear and every enemy is reachable on foot.

| Encounter | Layout | Rule |
|---|---|---|
| Wolf Pack | Open Field | 1–2 mirrored pairs of rocks; wolves start within 2 tiles of the leader |
| Goblin Bombers | Ridge | A rock ridge across row 5 with 3 gaps; bombers on the back row |
| Bandit Camp | Camp | 3 cover rocks in the middle; crossbowmen behind them, cutpurses on the flanks, brute in the centre |
| Elementals | Crystal Cave | 2 mirrored pairs of crystals; 3 elementals spread out |
| Necromancer (boss) | Graveyard | 4–6 tombstones in the middle |
| Giant (boss) | Quarry | 1 mirrored pair of boulders |

**New factions** *(placeholder numbers)*

| Unit | HP | Dmg | Rng | AP | Rule |
|---|---|---|---|---|---|
| Cutpurse (x2) | 5 | 2 | 1 | 5 | On a landed hit it steals 5 gold and runs for the far edge; if it reaches it, it escapes and the gold is lost. Kill it first and nothing is lost. |
| Crossbowman (x2) | 6 | 3 | 4 | 3 | Ranged, needs line of sight. |
| Bandit Brute | 12 | 3 | 1 | 3 | Tough (takes 1 less damage). |
| Elemental (x3) | 10 | 3 | 1 | 3 | When it dies it splits into 2 Shards (its tile + a free neighbour). |
| Shard | 5 | 2 | 1 | 3 | — |

**Bosses** *(placeholder numbers)*

- **Necromancer** (14 HP, 3 dmg bolt, range 3): his army is raised from the factions beaten this act — 2 of each faction's representative (Bone Wolf, Bone Goblin Bomber, Bone Crossbowman, Bone Shard), topped up with Skeletons to at least 4. All of them leave bone piles and can be raised again (within 5 tiles, 3 AP).
- **Giant** (the ogre, 36 HP): Slam (2 AP) shoves everyone within 2 tiles 1 tile away when 2+ of your units are that close; plus the club smash and boulders that stay as rocks.

**Trainer economy** — one evolution per unit per visit. The first evolution of a visit is free, the second costs 20 gold, each after that 35. A won fight pays 15–25, so a typical act (2–3 fights) buys about one recruit (15), one item (7–10) and one paid evolution. (Trainers and evolutions go once techniques are built; see "Techniques instead of evolutions".)

**Zone of control** — each step out of a tile next to an enemy costs +2 AP, both sides.

**Showing zone of control** (2026-10-27, not playtested) — only while one of your units is selected: tiles next to enemies (their reach) get a faint red tint, move tiles whose cheapest path pays zone-of-control AP are orange instead of blue, and a dotted red link runs to each enemy holding the selected unit. The HUD and the enemy hover say "Engaged by ...: each step out of their reach costs +2 AP". A step-by-step path preview is parked for later if this isn't clear enough.

## Moon Shrine: the first non-"kill all" scenario

Built 2026-10-26 on branch `feature/moon-shrine`, not yet playtested. Other scenario ideas, ranked, for later: break the ritual (built next, see below), survive waves, assassinate the leader, rescue a captive, hold the ground, break through, crumbling tower; puzzles last (fixed puzzles clash with a random army; maybe as a ? event with a fixed squad).

- **Map:** a new node type (`NodeKind.Ritual`), once per run, as the other choice next to a battle (now at step 4 or 8 of the 12-step act). May later merge into battle nodes as a variant.
- **Board** *(placeholders)*: the shrine (15 HP, can't act or move) stands at (3,3). You deploy within 2 steps of it, not in the back rows. 3 Elementals start on edge tiles at least 4 steps away; 2 more walk in from the edges after enemy turns 1 and 3 (one after each since the techniques playtest, 2026-10-30: 7 Elementals and their Shards were too many on 6x6). One mirrored pair of crystals.
- **Win:** survive 5 enemy turns; the ritual completes and the rest flee. Killing everyone wins early only once no reinforcements are left. Pays normal gold, then you pick a **blessing**.
- **Loss:** the shrine breaks: no gold, a random **curse**, and the run goes on. Losing the whole army still ends the run.
- **Enemy AI here:** go for the shrine unless one of your units is next to them (Taunt still wins). The shrine is your ally for Shield Wall and Guard; your own blasts and volleys hurt it.
- **Factions:** elementals only for now (slow and tough; a split next to the shrine drops two shards on it). Goblin bombers don't fit (bombs on something that can't move). Other factions would need their own behaviour in this mode.
- **Blessings and curses (omens):** one per run, lasting every battle after. A blessing helps you, the same omen as a curse helps the enemy. Omens never pick the shrine.

| Omen | Blessing | Curse |
|---|---|---|
| Sun | Start of your turn: the tile under a random enemy is marked; at the start of the enemy turn a sun beam hits it for 3 (tile-based: pushing the enemy off makes it miss) | The mark goes under one of your units; step off before the enemy turn |
| Night | Start of your turn: a random unit of yours and the plus around it (5 tiles; 3x3 until 2026-11-01, too big on a 6x6 board) are shrouded until your next turn; enemies can't hit units there from range (point blank still works) | The shroud covers an enemy; your ranged attacks, fireballs and bombs can't reach it |
| Moon | Start of your turn: a random unit of yours gets a 4-point shield until your next turn | The shield goes to an enemy |

## Break the Ritual: the second scenario

Built 2026-10-27 on branch `feature/break-ritual`, not yet playtested. The mirror of the Moon Shrine: attack instead of defend.

- **Map:** every run has both scenario nodes, next to battles (now steps 4 and 8 of the 12-step act): the Moon Shrine at one, Break the Ritual at the other.
- **Board** *(placeholders)*: 3 totems (6 HP, can't move or act) on rows 5–6, at least 3 steps apart; a mirrored pair of rocks on row 3. Goblin guards: 3 bombers on the back row, 2 runners on rows 4–5. You deploy in your back two rows.
- **Win:** smash every totem before the summoning completes at the start of enemy turn 6 (you get 6 turns; 4 until 2026-11-01). The guards flee. Normal gold, then pick 1 of 3 random items for one unit (it replaces that unit's item), or leave them.
- **Loss:** the summoning completes with a totem standing: no gold and a random curse, and the run goes on. Losing the whole army still ends the run.
- **Guard AI:** guards go for whichever of your units is within 2 tiles of a totem (nearest first); otherwise they fight as usual. Taunt still wins. Their own bombs don't hurt the totems; your blasts do.
- **Curses:** losing both scenarios gives two curses, always different omens.
- **Zone of control:** the shrine and totems can't attack, so they have none (this also changed the Moon Shrine: enemies next to it can leave freely).

## Act 1, 12 steps, with elites

Decided and built 2026-10-28 (branch `feature/act1-12-steps`), not yet playtested. Why: the first full playtest of the 7-step act was won in under 7 minutes with only 4 fights (the scenarios had replaced faction battles), and a Rogue snowballed to +10 damage. Goal: about 15–20 minutes, a run that ramps up.

| Step | Choice |
|---|---|
| 1 | Battle / Battle |
| 2 | Shop / ? |
| 3 | Battle / ? |
| 4 | Scenario / Battle |
| 5 | Trainer / Shop (now Shop / ?) |
| 6 | Elite / Battle |
| 7 | Shop / ? |
| 8 | Scenario (the other one) / Battle |
| 9 | Trainer / ? (now ? / Battle) |
| 10 | Elite / Battle |
| 11 | Shop / ? |
| 12 | Boss |

- **At least 5 fights before the boss** (steps 1, 4, 6, 8, 10 are two fights), up to 7. The two Trainers stay (enough for tier 3). (Trainers went with techniques, 2026-10-30.) (Trainers went with techniques, 2026-10-30.)
- **Battle:** a faction you haven't beaten yet; once you've met all four, a normal-strength rematch. Step 1 draws only from the first difficulty pool: each regular faction has a pool (`Encounter.Pool`), Wolves, Goblins and Bandits in pool 1, Elementals in pool 2 (2026-11-01, the user's call). Elementals can come from the next battle on; later steps, ambushes and elites ignore pools for now. **Elite:** a harder rematch of a faction you've beaten. Both get their faction when the run reaches their step (the map shows "Battle" / "Elite" until then); the two fights of a step never share a faction.
- **Elites are high risk, high reward**, always a choice: normal gold plus a pick of 1 of 3 items (now a technique pick holding a rare one). First version is more of the same enemies, no new types *(placeholders)*: Wolves 2 leaders + 4 wolves; Goblins 4 bombers + 3 runners behind a ridge with only 2 gaps; Bandits 3 crossbowmen behind cover, 2 cutpurses, 2 brutes; Elementals 4.
- **Bloodthirst capped at +3 for the run.** (Removed with the evolutions, 2026-10-30.)
- **Later:** fights get split into difficulty pools (and more fights added); each step then draws from a pool. New node types and events after a playtest of this act.

## Small board, one move (merged into `main` 2026-10-30)

Built 2026-10-29 as an idea test on `experiment/small-board`, playtested 2026-10-30 ("feels ok": tight fights of 5-6 turns, one loss in three) and merged into `main` the same day. These are now the game's rules; the 8x8 numbers above are history. Why: positioning should matter more, every step should count, and the board should read at a glance.

- **Board 6x6.** Each side still deploys in its back 2 rows, so only 2 empty rows lie between the armies.
- **Moves are separate from AP.** Every unit walks **1 tile per turn**, before or after acting; unused moves don't carry over. AP now only pays for attacks and actions. AP pools are unchanged for the first test, so units attack more often (rogues and archers get 2 attacks a turn).
- **Swift** (new keyword): 2 moves. Rogue line, Wolf, Pack Leader, Goblin, Cutpurse (and their raised versions).
- **Zone of control:** stepping out of an enemy's reach uses up all remaining moves. A 1-move unit can always back off one tile; a Swift unit can't slip past.
- **Ranges** scaled to the board: Archer and the Trapper line 4 → 3, Marksman/Sniper/Deadeye 5 → 4, Crossbowman 4 → 3, Turret 3 → 2, Mage bolt stays 3. Fireball and player bomb 4 → 3, Charge 4 → 3, Taunt 3 → 2, goblin bomb 2–4 → 2–3, Giant boulder 2–5 → 2–4, Necromancer raise 5 → 4 and keeps 2 tiles away instead of 3.
- **Areas:** bombs (goblin and Bombardier) hit only their own tile, so one step dodges them; fireballs and Volley hit a plus (the tile and its 4 neighbours) instead of a 3x3. Barricade blasts stay 3x3 (the Night shroud too, until it became a plus on 2026-11-01). **Mines hit only their own tile** too (2026-10-30, after the bundle playtest; were 3x3): only the enemy that steps on one is hurt, and a mine no longer sets off mines next to it (only a 3x3 blast does).
- **Abilities:** Spring Boots give +1 move (were "first tile free"). Chill: the target can't move on its next turn (was −2 AP). Big Mines max 5 → 4.
- **Enemy AI:** no longer keeps AP back for walking. The Giant won't throw a boulder that would wall it off from its target (on 6x6 its rocks sealed it in).
- **Layouts:** enemies start in rows 4–5, the shrine at (2,2) with spawns at least 3 steps from it, totems on rows 4–5. The Necromancer raises **1** of each beaten faction (was 2), topped up with skeletons to 4, so his army fits.
- **Fewer rocks** (2026-10-29): at most 2 per layout (a mirrored pair, two tombstones, two cover rocks), and no two rocks touch, not even corner to corner. Exceptions where the fight needs more: the goblins' broken ridge (3 rocks on row 3, one every other column, elite too) and the elite Bandit Camp (one cover rock per crossbowman, 3). A cutpurse that is cornered while carrying gold now hits whoever it can instead of standing still (it used to jam the Bandit Camp).
- **Logs:** battle logs record `boardSize`, each unit's `maxMoves`, `movesAfter` on moves and "done", and `ranged` on hits (a shot from 2+ tiles), for the ranged vs melee damage split.

**Step 2: one AP** (2026-10-29, same branch, not playtested). Every unit, bosses included, has **1 AP**: one move and one attack or action per turn. Every attack and action costs 1, and unused AP doesn't carry over. Units no longer differ by AP pool (the old pools are gone from the data).

- **Momentum** (Shadow): a kill gives the action back and, since 2026-11-01, 1 move, and lets the unit move again. Without that it only helped with another enemy already in range: the unit had acted, so it couldn't walk to one, and its turn finished with the AP unused.
- **Opening Shot** (Deadeye): double damage the first time it hits each enemy in a battle (was "first attack each turn").
- **Bosses get strong single actions** *(placeholders, "for now")*: the Giant's slam also deals 3 damage to each of your units within 2 tiles, and it picks smash or slam by whichever deals more in total (ties go to smash), else a boulder (now 4 damage); the Necromancer raises **every** bone pile within range in one action, and its bolt does 4.

**Move first** (2026-10-29, same branch, not playtested): once a unit spends AP (any attack or action) it can't move again that turn, for both sides. Each turn is "position, then commit"; no more hit-and-run. Swift units can still move 2 and then attack, and Blink (an action) is the mage's escape. The Necromancer now backs off before raising or shooting.

**Enemies and shields** (2026-11-01, not playtested): when an enemy's target has a shield (Ward or moon), it goes for an unshielded unit it can reach this turn instead, and hits the shield only if there is none. Shielded units also come last among adjacent blockers, totem threats and Blood Scent prey. Taunt still overrides this. Why: attacking a shield wasted their turns, and never attacking one would make a Ward total protection.

**One tile, one bone pile** (2026-11-01): piles don't block, so a unit could die on one. The newer pile now replaces the older one, so a raise can't put two units on a tile.

**Walking through allies** (2026-11-01, not playtested): any unit, on both sides, can walk through its allies but not stop on them; never through enemies or objectives. It only matters with 2 or more moves. Why allies only: fewer traffic jams on the small board, while your front line still protects your back line from Swift enemies. Mines, caltrops and overwatch only stop a unit on a tile it can stand on, so it never ends up on an ally's tile. Battle logs count the allies passed per move (`throughAllies`).

**Step 3: unit kits** (2026-10-29, same branch, not playtested). Every unit should feel unique through its **passives** and its **actions**. The basic attack counts as an action too and has its own name (Wolf Bite, Strike, Shot, Bolt, Smash, Long Strike, Stab). Wolves are the first faction designed this way; the rest of the enemies and the player's units follow.

- **Wolf** (6 HP, Swift). Passive **Blood Scent**: goes for the lowest-HP unit it can reach and attack this turn, instead of the nearest. Action **Wolf Bite**: 2 damage, +1 for every wolf bite earlier in the same turn (any wolf, any target, the Pack Leader's included; resets every turn).
- **Pack Leader** (10 HP, Swift, Wolf Bite 3). Passive **Pack Bond**: while it lives every wolf has +1 max HP (7); once it dies they have -1 (5) and lose 2 HP on the spot, which can kill wounded wolves. Replaces the old +1 damage aura. Action **Howl**: every wolf that hasn't acted gets +1 move this turn. It acts before the other enemies and howls when the extra move lets a wolf bite that otherwise couldn't (two wolves if it could bite itself).
- **Goblin** (6 HP, Swift, Strike 2; 5 HP until 2026-11-01, briefly Strike 3). Passive **Slippery**: ignores zone of control. Action **Fling**: throws what is next to it at one of your units 2-4 tiles away, over obstacles. A bomb or mine on an empty tile next to it goes off there with its own blast (so your mines and dodged bombs come back at you; it always does this when it can). Otherwise a goblin that hasn't acted lands on the free tile next to the target nearest the thrower, deals 2, takes no damage itself, and can still act (it usually strikes right away). It flings a goblin only when it can't reach anyone to strike this turn. Rocks are too heavy to throw.
- **Goblin Bomber** (4 HP). Passive **Powder Keg**: when it dies it explodes for 2 on everyone in the 3x3 around it, its own side included. Actions: Stab (1) and Bomb (unchanged: one tile, 2-3 away, goes off next enemy turn).
- **Cutpurse** (5 HP, Swift, Strike 2, steals 5 gold on its first hit). Passive **Light Fingers**: the only unit that may still move after attacking, so it stabs, grabs the gold and runs in the same turn (zone of control still applies). Action **Caltrops**: on its tile or a free tile next to it; a unit of yours stepping on them takes 1 and its movement ends. It drops them behind it after running, or when it can't do anything else.
- **Crossbowman** (6 HP, Shot 2, range 3; Shot 3 until 2026-11-01). Passive **Behind Cover**: while next to a rock (or ice, or a barricade) it can't be hit by ranged attacks; melee, bombs and blasts still reach it. Action **Overwatch**: with no shot this turn it walks closer (into cover if it can) and aims; during your next turn the first of your units that walks onto a tile it can see in range is shot and stops there. One shot, and it ends at the crossbowman's next turn. Fixes the old "hides behind cover" card text: its AI now picks covered tiles.
- **Bandit Brute** (12 HP, Strike 3). Passive **Guard** (replaces Tough; same rule as your Shielder): takes the hit when an adjacent ally is attacked. Action **Bull Rush**: hits for its damage and pushes the target 1 tile; if it slams into a unit, both take 2.
- **Elemental** (8 HP, 10 until 2026-11-01; Strike 3, splits into 2 Shards when it dies; 2026-10-30, not playtested). Action **Upheaval**: the ground bursts up under one of your units within 2 tiles (no line of sight needed): 2 damage, and it's thrown 1 tile straight away from the elemental (2 more if blocked). AI: strike when it can reach; otherwise walk as close as it can and then upheave its target, or the nearest unit in range. First version left a rock on the unit's old tile; the bundle playtest (2026-10-30) found the rocks too much (7 upheavals, 5 rocks in two fights), so the rock is gone for now (user: rethink later).
- **Shard** (4 HP, 5 until 2026-11-01; Strike 2): no passive for now. The first version had **Reform** (two Shards next to each other at the start of the enemy phase merged back into an Elemental); in the bundle playtest every split but one reformed, and it was cut (2026-10-30). A new Shard passive is to be designed later with the user.
- **Skeleton** (4 HP, Strike 2, bone pile). Action **Grasp**: grabs a unit next to it; no damage, but it can't move on its next turn (the same hold as a pin). AI: grasps ranged or Swift targets that aren't held already (they're the ones that would get away), strikes everyone else. Why: a weak unit that holds your units in place for the Necromancer.
- **Giant** (boss, 36 HP, 2x2; 2026-10-30, not playtested). Passive **Colossus**: can't be pushed, pinned, slowed or grasped (Pinning Shot still deals its damage; Shield Bash can't target it). Why: one archer could lock the boss down every other turn. Passive **Wrath**: at half HP or less it gets 2 AP a turn (two actions, each still on its cooldown, e.g. slam then smash), with a "Wrath!" popup when it triggers. Why: a second phase; burst it from half to dead, or get out of reach. Actions unchanged: Smash, Slam, Boulder.
- **Necromancer** (boss, 14 HP). Passive **Soul Harvest**: while it lives, your units that die leave a bone pile; raising one brings it back on its side as "Bone <unit>" with the unit's HP, damage, range and Swift but no other passives, actions or item (user's call: keep Harvest over the milder Siphon). Its pile is used up; the Bone unit leaves a pile of its own. Action **Corpse Blast** (cooldown 2): a bone pile within raise range (4) explodes for 2 in the 3x3, friend or foe, and is used up. AI: blast first when the 3x3 holds more of your units than of its side (at least one), else raise, else bolt. Why: standing on a pile to block a raise now has a price.
- **Cooldowns** (2026-10-30): basic attacks have none; every other action, yours and the enemies', is ready again N of the unit's own turns after use *(placeholders)*: 3 for Fireball, Ice Wall, Mend, Build Turret, Detonate, Vanish, Raise and Caltrops; 2 for everything else (Shield Bash, Pinning Shot, Blink, Place Mine, Throw Bomb, Ward, Barricade, Charge, Slam, Boulder, Bomb, Howl, Fling, Overwatch, Bull Rush, Upheaval, Grasp, Corpse Blast). Why: easier balancing, room for stronger actions, and with 1 AP a real choice each turn. The card and the action buttons show the cooldown and "ready in N".
- **Your base lines** (each action is inherited by every evolution of the line; 1 AP, so it's a choice against attacking):
  - **Warrior line, Shield Bash** (changed, see "Warrior redesign")**:** push an adjacent enemy 1 tile; no damage, only the 2 collision damage if it's blocked, and a unit it slams into takes the same 2 (other pushes still only hurt the pushed unit). The attack (Strike) is for damage.
  - **Archer line, Pinning Shot:** 1 damage in shot range (line of sight), and the target can't move on its next turn. The attack (Shot) is for damage. The Trapper line lost Snare to it (Prey now works on pinned enemies); the Trapper gets a new action later.
  - **Rogue line, Vanish:** the base Rogue's passive is Swift only (Bloodthirst removed 2026-10-29, and from the game with the evolutions on 2026-10-30). Vanish goes off as soon as you press it: enemies can't target or attack it (area blasts still hit) until it attacks again, for 2 enemy turns at most; the attack that reveals it deals +2. Free since 2026-11-01, like Blink: no AP and the unit can still move and attack, but only before it has acted (attacking and then escaping stays impossible, so "position, then commit" holds). Not playtested.
  - **Mage line, Blink:** teleport to a free tile within 3 (2 until 2026-11-01), past rocks and zone of control. Free since 2026-11-01, see Vanish.
  - **Sapper line:** unchanged (Strike, Place Mine).
- **Controls** (2026-11-08): left-click chooses, right-click acts. Left-click one of your units to select it (its card opens); left-click an enemy to open its card while your unit stays selected; left-click empty ground to drop the selection and close the card. Right-click a tile to move, an enemy to attack (or walk up and attack), a barricade or rubble to hit it, or a yellow tile to use the aimed action. Esc cancels an aimed action, or else closes the card. Placement and every HUD button stay on left-click. The top-left panel keeps only the turn and the selected unit's AP and moves.
- **Unit card** (2026-11-08, replaces the hover card): the card of the selected unit, or of the last unit you left-clicked, at the top right. It shows the name and level, an HP bar with "HP / max" on it, AP / moves / range / weight, statuses as coloured chips (blue helps, red hurts, grey neither), **Next** for enemies, passives as green chips and one row per action (gold chip, cost, cooldown, "ready in N"). Every passive, action and status is its name only; hovering the name shows what it does in a box beside the card. Plain hovering a unit shows no card. The attack preview moved onto the target statue's own HP bar (a pale pulsing chunk and "-3" to its left, or who guards it), and "walk 2, attack" shows by the pointer. Which stats stay on the header line is still open (user). Colours are placeholders for the art chat.
- **Keywords** (2026-11-08): rule texts name a keyword without explaining it, and the game shows its meaning on hover (highlighted, underlined words; listed under a tooltip where the pointer can't reach). 15 for now, in `Core/Keywords.cs`: Pinned, Slowed, Marked, Exposed, Hidden, Burning, Rubble, Poison, Push, Zone of control, Line of sight, Fury, Swift, Temporary HP, Free (no AP, before the unit acts, it can still move and attack). New texts should name these instead of re-explaining them; a new rule word that several texts explain should become a keyword. Why: texts took too much space and repeated the same explanations.
- **Action cards:** the selected unit's special actions are cards in a row at the bottom centre (key, name, cost and cooldown or "ready in N", description). Click or press the key to aim, again to cancel; the aimed card turns gold, unusable ones are faded. Done and End turn stay at the bottom right (2026-10-30).

## Techniques instead of evolutions (built 2026-10-30)

Decided with the user and built 2026-10-30 (branch `feature/techniques`, merged into `main` with it); playtested once, fixes below. Why: evolution added little to the run (every unit evolved at every Trainer, the only real choice was the tier-2 branch), 33 evolution kits were still to design, and the run should feel more like deckbuilding: a pick after every fight and builds that grow one card at a time. Replaces evolutions and Trainers (items stay).

- **No evolutions.** The 5 base units stay (Warrior, Archer, Rogue, Mage Apprentice, Sapper) with their base kits, which work as a starter deck; the 33 evolved types go. Statues are unaffected (evolved units already shared their line's statue).
- **Techniques:** passives or actions (actions use the normal 1 AP and cooldown rules). Today's tier-2 and tier-3 additions become techniques, so almost the whole pool is already built:
  - Core (from tier 2): Sweep, Guard (Warrior); Steady Aim+ (Archer); Pincer, Counter (Rogue); Fireball, Chill, Ward (Mage); Big Mines, Barricade (Sapper).
  - Follow-ups (from tier 3): Long Reach, Double Strike, Charge, Taunt, Tough (Warrior); Pierce, Opening Shot, Prey, Volley (Archer); Momentum, Poison, Counter All, Dodge (Rogue); Burning, Concussive, Ice Wall, Shatter, Wide Ward, Mend (Mage); Throw Bomb, Blast Proof, Turret, Detonate (Sapper).
- **Line pools:** every technique belongs to one line (Fireball stays a mage's): 33 techniques (34 since the Warrior redesign, see below). Effects for any unit stay items.
- **Items stay** (user's call, 2026-10-30): the one-item slot and the 4 items (Vampire Fang, Thorned Mail, Spring Boots, Lucky Charm) remain; a new item replaces the old, and items don't level a unit. Shops sell 3 items next to the techniques; the Abandoned Cart holds a technique or an item (half and half *(placeholder)*). Reward picks are always techniques. A new item design (strong items with a downside, consumables) may come later.
- **Requirements only where the rule needs them:** Shatter needs Chill; Burning and Concussive need Fireball; Wide Ward and Mend need Ward; Turret and Detonate need Barricade; Counter All needs Counter. These chains are the deckbuilding pull.
- **No slot limit** for the first version. About 7 fights means about 7 picks across 3–4 units, and with 1 AP extra actions add choice, not power. Watch stacked passives in the logs; add a cap if one unit snowballs.
- **Levels:** a unit's level is the number of different techniques it has learned. Each level grows the unit by its line *(placeholders)*: Warrior +2 HP; Archer +1 HP, +1 damage at levels 3 and 5 (it has only 5 techniques; 2026-11-01); Rogue +1 HP, +1 damage at levels 2, 4 and 6; Mage +1 HP, +1 damage at levels 3 and 6; Sapper +1 HP, +1 max mine at level 3. Damage stays rare on purpose (HP is 5–12 on a 6x6 board). Forgetting a technique keeps the level, and it stays learnable, but relearning a technique the unit ever knew gives no new level (2026-11-01: Forget plus relearn used to buy levels). The level shows on the hover card.
- **Reward pick:** after every won fight, normal gold, then pick 1 of 3 techniques or skip for gold (+8 *(placeholder)*). Offers only include techniques at least one of your units can learn (line and requirements); you choose which unit learns it. Elites: at least one rare offer (replaces the elite item pick). Break the Ritual's item pick becomes a technique pick.
- **No Trainers; the shop does it.** The shop sells 3 techniques (only learnable ones), 3 items, a recruit, and a Forget service for a small fee. No "+" upgrades in the first version (levels already give growth).
- **Map** (12-step act): step 5 Trainer / Shop becomes Shop / ?; step 9 Trainer / ? becomes ? / Battle (one more optional fight, one more pick).
- **Economy:** gold now goes only to shops (plus skipped picks); prices need a pass after the first run.
- **Logs:** the run log records each pick's offers, the choice or skip, which unit learned it, shop buys and forgets, and levels; battle logs record how often each technique is used.
- **Loop:** draft 3 base units → fight → pick 1 of 3 techniques (the unit levels up) or skip for gold → spend gold at shops on techniques, items or Forget → boss.
- **First playtest of the techniques branch (2026-10-30, one run, won; every shop emptied: 10 purchases on top of 4 picks)**, fixes on `feature/techniques` *(placeholders)*: prices up ~50% (techniques 15 / 24, items 11-15, Forget 8); Moon Shrine reinforcements one at a time (5 Elementals, was 7); Goblin Bombers 2 bombers and 3 goblins (was 3 and 2; elite and Break the Ritual unchanged); **barricades have 4 HP**, either side can attack them (a basic attack, no target bonuses) and they break at 0, and an enemy that spent no AP on its turn attacks a barricade in its reach (rocks, cover, tombstones and ice stay solid); hovering an enemy you can attack shows the HP you would take off as "-3" (or "kills", or who guards it) and a pale chunk on the card's HP bar; the hover card moves to the top left when the pointer is under it and shrinks its text (then widens) to fit.

Ideas from the same discussion, parked for after techniques (suggested order): battlefield modifiers shown on the map node (built 2026-10-31 as "Battlefields", see below; still unbuilt: Ice Floor, Fog, Mud, High Ground, Crumbling Floor, Braziers, Ley Line); Banners as army-wide rules (see "Later / maybe"); a first choice at run start (pick 1 of 3: a bonus technique, gold, a curse for a strong banner), then Commanders that set the draft pool, a starting banner and a rule twist; unlocks between runs; a branching map; ? events with trade-offs.

## Warrior redesign (built 2026-11-05, not playtested)

Decided with the user. Goals for every unit, to be applied one line at a time: many more skills to choose from than a run can learn, skills that combine with each other and with other units, each unit feeling unique, and fun. Direction agreed in discussion: skills stay a deck (random offers), not a tree. Pool size, more passives, rarities, cooldowns and every number below are still open; the numbers are Claude's placeholders so the kit can run.

- **Base kit:** 12 HP, no passive (user's call, for now). **Strike:** 3 damage. **Shield Bash** (cooldown 2): 2 damage and push 1 tile; if blocked, 2 more, and what it slams into takes 2. Huge units and the Giant take the 2 but don't move, with no collision.
- **Sweep** (active, common, cooldown 2): full attack damage to the target and the enemies on both sides.
- **Charge** (active, rare, cooldown 2): up to 3 tiles in a straight line. 2 damage to every enemy on the way, each pushed to a random side (not shown beforehand). If that side is blocked it goes the other way; both blocked: 2 collision damage. An enemy still in the way (huge, or both sides blocked) stops the charge in front of it. Passes through allies without pushing them and can't end on one. Needs an enemy on the way.
- **Shield Up** (active, common, cooldown 3): 2 less damage from every source, at least 1, for 2 enemy turns.
- **Battlecry** (active, rare, cooldown 3): allies within 2 tiles, itself included, get +1 move and 2 temporary HP until your next turn. Temporary HP is lost first and fades. The extra move only helps units that haven't acted, so the Warrior should shout first.
- **Taunt** (active, rare, cooldown 3; replaces the passive): until your next turn, enemies within 2 tiles can only attack or use offensive abilities on this unit. Area hits (Smash, Slam, Corpse Blast) and Overwatch shots aren't aimed at one unit, so they still work as usual.
- **Throw Shield** (active, common, free before acting, once per battle): for the rest of the battle +1 move and +2 damage, for 4 HP; needs more than 4 HP. A plain buff (user's call). Free because units can't move after acting, so the move would otherwise be lost that turn.
- **Vanguard** (passive, common): can deploy 1 row further forward (not in the Moon Shrine, which has its own zone).
- **Placeholder** (passive, rare; name to come): attacks any unit that steps out of its reach, at most once a round. Slippery ignores it, pushes don't trigger it, and enemies don't avoid it yet.
- **Starting on cooldown:** rejected for now; skills start ready. Claude's concern if it returns: in 5-6 turn fights a cooldown 3 skill would be used about once.
- **Parked, rules still in the code but never offered:** Shield Wall, Guard (the Brute keeps it), Long Reach, Double Strike, Tough. The passive Taunt and passive Sweep are gone.
- **Logs:** technique uses and status events (`shieldup`, `battlecry`, `taunt`, `throwshield`) as before; a `zoc strike` action marks each Placeholder attack; temporary HP soaked shows as `shield absorb`.

## Banners and weight (built 2026-10-31, not playtested)

A banner bends a rule for the whole army for the rest of the run (like Balatro jokers). The user wants
builds and synergies from them, so there's no limit on how many you hold; each is held once.

- **Getting them** (not final, the user's first take): pick 1 of 3 random banners at the start of a run, on the same screen as the draft ("Prepare your army", 2026-11-08: banner row on top, unit cards below, the banner can be switched until the run starts; Third Wheel lowers the units to pick). Every shop sells one you don't hold (20 gold). An elite's reward screen offers one you don't hold, taken instead of its technique.
- **Vampiric:** every attack of yours that deals damage heals the attacker 1 (attacks only, not blasts or collisions; not above max HP). Cost: the Curse of the Sun in every fight (once, even if you also carry that curse). The user's idea was +2; +1 because units have 4-12 HP.
- **Third Wheel:** your units get +1 AP and +1 move (a mercenary too). Cost: the army holds 2 units. Taken at the start, you draft 2. Taken later with more units, you choose who stays and the rest leave with their techniques and items. Move first still holds: move up to 2, then 2 actions.
- **Loser:** an army unit that dies in a fight gets +1 max HP and +1 damage for the rest of the run, stacking. Also after a lost scenario fight. Dying still costs gold (rewards scale with survivors).
- **Mercenaries:** before each fight, hire 1 of 2 random base units (level 0) for 8 gold, or go without. It fights that battle only, doesn't count as a survivor for gold, and can't keep a fight going alone: the fight is lost once the real army is down.
- **Battering Ram:** your units' pushes go 1 tile further and deal 2 damage at the end (whether the unit moved or not). No cost for now (user's call). Enemy pushes are unchanged.
- **Weight** (every unit, not a banner): a push moves a medium unit 1 tile, a small one 2, a huge one 0 (no move and no collision damage). Pushes now move tile by tile and stop with collision damage at whatever blocks them. Small: Rogue, Mage Apprentice, Wolf, Skeleton, Goblin, Goblin Bomber, Cutpurse, Shard. Huge: Pack Leader, Bandit Brute, Elemental, Giant (Colossus stays unpushable). Everyone else medium, the Warrior included (huge would have made it immune to Bull Rush, Upheaval and Slam). Raised and harvested copies keep their weight.
- **Ideas not built:** Lady Death (enemies get +1 unit; after X wins she gives powerful abilities; needs its reward designed), Run Rabbit Run (2 holes per fight that act as teleports), and Claude's suggestions: Stone Skin, Glass Banner, Shieldwall, Zealots, Scholar, Last Stand.
- **Look (2026-11-04, seen only in a still render, the sway not yet in Unity):** every banner shares one painted post and one cloth, so only the cloth's colour and, later, a symbol differ (user's call). Painted from the game's isometric camera with folds like the statues' capes, after a front-on, soft-cloth version looked wrong (user). Crimson Vampiric, faded blue Third Wheel, grey Loser, green Mercenaries, rust orange Battering Ram; each colour is a dark-to-light ramp, like the statues', not a flat tint. The ones you hold stand along the roof's two back edges, behind your side, taking turns between the edges, with the crossbar along the edge (mirrored on the right). The cloth is pinned at the crossbar and sways more towards the hem and away from the post, with light moving across its folds, each banner in its own time. The start pick shows three tall cards with the banner large; the shop and elite rewards use wide cards with the banner beside its rule. The banners sway on the cards too. The map shows the banners held as a small row beside the title, and hovering one there, or on the roof, shows its name and rule; the battle HUD no longer lists them. Symbols are parked: a first Vampiric symbol (black sun over fangs) looked like a spider at small size, so try plain bone-white fangs next. Symbols get painted into the cloth layer, so they move with it.
- **Logs:** battle logs list `banners` and each placed unit's `weight` and `mercenary`; ram damage is a `tick` with note `ram`, Vampiric heals are `heal` effects. Run logs have `banners` (source start / shop / elite, start offers), `dismissals`, each battle's `mercenary` (offered, hired) and `loserGains`, the shop's `bannerOffered` and banner purchases, and each technique pick's `bannerOffered` (technique "banner" when it was taken).

## Battlefields (built 2026-10-31, not playtested)

A battlefield is a rule for one fight (`Core/Battlefield.cs`), the "battlefield modifiers" parked above. It works the
same for both sides. The user listed nine ideas; five were picked for the first bundle.

- **Where:** every battle, elite and boss node gets one when the run is made, shown on the node (hover for the rule), so you can route towards or around it. An ambush rolls one too. The two scenario fights get none (they have their own rules). One per fight. A step's fights never share one or repeat one from the step before. Every fight has one for now because we're testing (user's call); later maybe only some fights.
- **Edge of the World:** a push off the board kills: the unit falls from the tower. Nothing saves it (shields, Guard, Lucky Charm), it leaves no bone pile and a falling bomber's Powder Keg doesn't go off; a Pack Leader's bond still breaks. Huge units can't be pushed, so they're safe; the Giant never moves. Enemies use it: one that can push your unit off with Bull Rush or Upheaval does that before anything else, and the intent shows it. (The user's first idea was "units under 10 XP fall"; there's no XP, so weight does that job.)
- **Storm:** at the start of each round the tile under one random unit of each side is marked; lightning strikes the marks at the end of the enemy turn for 3 (tile-based, like the sun beam). Both sides get a turn to step off. Enemies never stop on a mark and step off theirs before acting. Pushing a unit onto a mark works. Uses the painted lightning effect. (Replaced the user's "volcanoes erupting randomly": volcanoes don't fit the tower, and unmarked hits feel unfair.)
- **Arcane Overflow:** every action's cooldown is 1 shorter, 1 at least (a cooldown 2 action works every turn). Both sides. The action cards show the shorter cooldown.
- **Fortune's Favored:** at the start of each round one random unit of each side (one that deals damage, never a boss) is favored: its attacks and blasts deal double damage until the next round (collisions don't). A side with nobody else eligible, like the Giant alone, has no favored unit that round; the Necromancer's minions stay eligible (2026-11-01: the lone Giant was favored every round, Smash for 10). An orange status pip and a hover line show it; the attack preview counts it.
- **What's On His Mind?:** no enemy intents: no hover lines, no Alt, no "Next turn" on the card. View only.
- **Not built, from the same list:** Misty (hide the enemy's start; on 6x6 it does little unless it's real fog, which is big view work), Chaos (swap one unit with the enemy at the start; open questions: does your unit come back, what if it dies), Keeper of Balance (a third side attacking whoever has more units; simpler as a fixed statue in the middle), Portals (two linked tiles rather than a split board). Claude's ideas: Gale (wind pushes small units each round), Crumbling Roof (marked tiles become holes), Ice, High Ground, Ley Line.
- **Logs:** battle logs have `battlefield`, actions `fall` (pusher, unit, killed), `storm mark` and `favored` (the unit picked), explosions with note `lightning`, and `favored` on hits a favored unit dealt. Run logs have each battle's `battlefield` and each map choice's `chosenField` / `otherField`.
- **Logs, version and credit (2026-11-01):** every battle and run log has `gitCommit`, `gitDirty` (any uncommitted change) and `gitDirtyCode` (one under `Assets/Scripts`, since project settings are often dirty), read from git once when Play starts; a build writes "unknown". `tick` actions now name who caused them in `unit` (poisoner, the fireball's caster, whoever dropped the caltrops, the pusher), counted in `damageDealt`, `kills` and a new `tickDamageDealt`. Sun beams, lightning and a lost Pack Leader have no unit behind them and stay uncredited; the target takes them as `sunDamageTaken`, `lightningDamageTaken` or `tickDamageTaken`. Summaries now include escaped cutpurses (`escaped`) and Upheaval's damage. Durations use a stopwatch: `Time.realtimeSinceStartup` resets as Play mode stops, which made every abandoned battle negative.
- **Run tag and rating (2026-11-01):** the run-end screen asks "serious run / just testing" and a difficulty from 1 to 5, both optional, saved as `runTag` and `difficultyRating` in the run log ("" and 0 when skipped). The map has an "Abandon run" button that opens the same screen, with a way back; a run abandoned by stopping Play gets no prompt.
- **Battle copy (2026-11-01):** `Battle.Clone()` is a deep copy for bots looking ahead, random generator included. Battles roll with `SeededRng` (`Core/SeededRng.cs`) instead of `System.Random`, whose state can't be copied; this changes which picks a given seed rolls inside a battle, not their odds, and layouts stay the same. Army members and unit types are shared, since a battle doesn't change them. New state on `Battle` or `Unit` must be added to `Clone`; `BattleCloneTests` compares every field and fails otherwise.

## Balance

Decided with the user 2026-11-01. A balance chat owns the numbers: it changes them freely and reports afterwards, and asks before changing rules, kits or AI behaviour. Gameplay fixes and game features it needs go to a gameplay session.

- **Win rate targets:** new player 10-20% per run, experienced player 50-60% on the base difficulty. Harder modes come later.
- **Power:** late builds may break the game in the last third of a run, but not by step 4, and no build should win without the player playing.
- **Luck:** it may decide what you face, but shouldn't kill a unit with no warning.
- **Drafts:** all 10 should be winnable in theory; win rates can differ. Kits are far from final, so draft gaps are a design question as much as a balance one.
- **Run length:** not a concern for now.
- **Loser banner stays uncapped** (user's call, 2026-11-01), even though a death's +1 HP and +1 damage costs only 5 gold.
- **Simulator:** `tools/dev.sh sim` plays battles with crude bots and writes `tools/out/balance-report.md`; `tools/dev.sh sim 10 smart` uses the look-ahead bot (`tools/out/balance-smart.md`), and `tools/dev.sh sim 10 fights "Elementals,Giant"` prints a quick check. Set `SIM_CORE` to a patched copy of Core to try a number without touching the game. The smart bot uses every action and sees the enemy's next turn and dice, but plans only one round ahead and has no items, banners or omens. It wins more than a player in most fights and may stall in long ones, so compare fights rather than reading real win rates.
- **First number pass (2026-11-01, from the smart bot, not playtested):** Elemental 10 to 8 HP and Shard 5 to 4 HP (hardest faction by far); Crossbowman 3 to 2 damage (two shots could kill a 5 HP unit on turn 1); Goblin 5 HP and 2 damage to 6 and 3, Elite Goblins 3 to 4 goblins (even the elite was a 100% win); Break the Ritual 4 to 6 turns (bot 11% at level 1). Bot win rate at levels 0 / 1 / 2 before and after: Elementals 11 / 30 / 64 to 22 / 61 / 80, Bandit Camp 30 / 82 / 94 to 59 / 88 / 98, Goblin Bombers 100 / 100 / 100 to 99 / 100 / 100, Elite Goblins 100 / 100 / 100 to 48 / 94 / 100, Break the Ritual 2 / 11 / 34 to 26 / 50 / 75.
- **Playtest 2026-11-04 (5 played runs, 17 fights; 2 of 5 first fights won, both runs past the first fight won):** Goblin back to 2 damage, keeping 6 HP. At 3 damage, flinging and striking took 8 HP off a Warrior on turn 1, and a favored goblin killed an Archer on turn 1, which breaks the luck rule. The bot had this fight at 99%: at level 0 it is far stronger than a player (bot 93-99% where the player won about half), so early fights are tuned from playtests, not from the bot. Break the Ritual at 6 turns landed well (won on turns 5 and 6). The late game was easy as the bot predicted: a Third Wheel run rated 1, killing the Giant in 3 turns for 1 damage taken.

## Night pass (2026-11-08, branch `feature/night-fun`, not playtested)

The user asked Claude to make the game more fun and interesting overnight, with full design freedom on a branch. Everything here is Claude's design with placeholder numbers and visuals. The timeline (including what was tried and dropped) is in `docs/night-log.md`; the per-line designs are in `docs/night/`. `main` is unchanged.

**Run structure (lead)**
- **Cracks:** an army statue that falls in a fight you win (or a lost scenario) gets a crack: -1 max HP until mended. The 3rd crack shatters it, and it leaves the army with its techniques and item. Shops mend a crack for 10 gold. Scars (from the Cracked Altar) are cracks that can't be mended. Why: deaths cost only gold, so a run was decided by step 4 and the rest had no tension (Into the Breach's grid, Darkest Dungeon's injuries).
- **Events with a price:** Stonemason (mends every crack, or +1 max HP with none), Cracked Altar (a rare technique for a scar; from step 5), Sculptor (give up a technique you choose for a random rare of the line, no level; from step 5), Hooded Stranger (30 gold for a curse).
- **Champions:** from step 6, every battle and elite node has one, its affix shown on the node. The toughest enemy gets +3 max HP and Armored (-1 damage taken), Vengeful (2 to adjacent foes on death), Regenerating (+2 HP a turn), Mighty (+1 damage) or Fleet (+1 move). +5 gold. `Core/Champion.cs`.
- **Stone Heart (bosses):** HP falls in 3 phases; damage past the next phase is lost until your next turn, so a boss takes at least 3 rounds. Entering a phase makes the boss **Enraged** (one extra action next turn). Giant: Wrath (2 actions) from phase 2, falling rocks on 2 of your units' tiles each round in phase 3 (marked a turn ahead, like storm marks). Necromancer: 2 skeletons climb onto the edge at each new phase. Bosses get +1 max HP per army level and **never fall** (Edge of the World, holes).
- **Crumbling Roof** (6th battlefield): collisions and deaths fracture the tile under the unit; the 2nd fracture opens a hole. A unit on it, or pushed into one, falls and dies. Holes block walking, not shots, and give no cover. Candidate for the game's identity rule if it plays well.
- **Hone** (shop service, 12 gold): one action's cooldown is 1 turn shorter for that unit for the run (actions with cooldown 2+, once each).

**Unit lines (builders, see `docs/night/archer-rogue.md` and `docs/night/mage-sapper.md`)**
- 59 techniques (was 34): Warrior 8, Archer 13, Rogue 13, Mage 13, Sapper 12. Each line owns a keyword other lines read: Archer **Marked** (next hit from anyone deals more), Rogue **Exposed** (an enemy with no ally next to it), Mage **Slowed** (Frost Nova in the base kit, Chill, Blizzard), Sapper **Rubble** (low heaps that block walking but not shots, and crush what's pushed into them). Rogue base kit: 6 HP, and Vanish lasts through the enemy turn.
- **Board:** `IsObstacle` = can't stand there (rock, rubble, hole); `IsRock` = blocks sight and gives cover (not rubble or holes).

**Demons (5th faction, pool 2, see `docs/night/demons.md`):** Imp, Demon, Devil and Fiend, with Fury (+1 damage per point), Blood Offering, Hellswap, Rending Blow, Soul Contract, Spines and Fire Breath. Each has its own AI and intents, plus an elite and a raised version for the Necromancer. Statue art exists but isn't imported yet.

**Balance after the night (bot, 300 runs):** 83% run win rate (same as Nov 1), drafts 77-97% (were 60-95%). Giant 36 to 42 HP. The Necromancer is now the harder boss (fixed-level bot 17% at level 3).

**Open after the night:** playtest everything; the Loser banner now combines with cracks (a death is +1 HP and +1 damage for a crack that costs 10 to mend; the user's call); review fixes still landing from the builders (see the log).

## Test mode (for testing only)

- Editor and dev builds only: a "Test battle" button on the draft screen opens a setup screen.
- Pick any 1–4 base units (repeats allowed) and tick each one's techniques (its level follows), any encounter (factions, bosses, Moon Shrine, Break the Ritual; for the Necromancer, which factions count as beaten) and optionally any blessing or curse. No items for now. Battle banners (Vampiric, Third Wheel, Battering Ram) can be ticked too; Loser and Mercenaries work between fights, so they aren't there.
- The battle uses the normal rules; afterwards it returns to the setup screen. No run, gold or map, and no run log; battle logs are marked `test: true`.

## Later / maybe

- Drawn spell cards during battle.
- Other win conditions.
- Difficulty modifiers (for example, HP carrying over between fights).
- **Banners:** built 2026-10-31, see "Banners and weight". More to come (Lady Death and others are listed there).
- **More acts** after Act 1; terrain features (river, bushes, crumbling floor) for layouts; post-battle reward choices; elite nodes.

## Open questions

- Is an unusual identity rule needed for the first prototype? Leaning towards: prototype the plain core first, then decide.
- Board size (playtest).
- Which node types exist besides battles.
- Unit roster for a first prototype (beyond warrior and archer).

## Unusual rule (identity hook)

Still exploring. Candidates are in the chat. Record the chosen one here.
