# Grid Tactics Roguelike — Design Note

Working title: "Army Roguelike" (placeholder; a better name will come later). Separate project from Nardatro. Unity project: `Desktop/Unity projects/ArmyRoguelike`. This file (`docs/design-note.md`) is the canonical copy.
Status: Prototype 5 (Act 1) built, not yet playtested: 7-step Act 1 where each of four factions (wolves, goblins, bandits, elementals) appears at most once, rule-based layouts, a rotating boss (Necromancer raising your beaten factions, or the Giant), one evolution per unit per Trainer visit with paid extras, zone of control. Prototype 4: draft 3 of 5 base units, 9-step map with free Trainers at steps 4 and 7 (every unit +1 tier per visit), 5 evolution trees (38 unit types, 3 tiers), only rule-bending items. Full unit/ability/item reference: `docs/gamepedia.html` (generated from game data; re-export via Tools → Export Gamepedia). Earlier, Prototype 3: branching 6-step map (battles, shops, ? events, ogre boss), built-in unit abilities, ability items, the sapper recruit with mines, and a goblin bomber battle. Prototype 2 (fixed mini-run) was playtested once: won, about 5.5 minutes. Fixes from it: walk-up attacks, End turn confirmation, necromancer no longer idle. Prototype 1 (one battle): no bugs, felt OK, won 2/2, "not super easy". Numbers marked *(placeholder)* are only examples.

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
- **Run-persistent growth comes only from explicit abilities**, for example "this unit gets +1 damage after killing an enemy, persists for the run". There is no universal leveling.
- Identity direction: each unit carries its own rule, like Balatro's jokers. Abilities come from the unit type (one built-in each) and from items; there are no separately earned abilities.
- **Equipment:** units can wear equipment, possibly bought at shops.
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

- Intent icons: hovering over an enemy shows who it wants to attack.

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
| Rogue | **Bloodthirst:** +1 damage per enemy kill, kept for the run |
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

**Map (7 steps)** — the four factions are shuffled onto four battle nodes, so no faction appears twice in a run:

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

**Trainer economy** — one evolution per unit per visit. The first evolution of a visit is free, the second costs 20 gold, each after that 35. A won fight pays 15–25, so a typical act (2–3 fights) buys about one recruit (15), one item (7–10) and one paid evolution.

**Zone of control** — each step out of a tile next to an enemy costs +2 AP, both sides.

## Moon Shrine: the first non-"kill all" scenario

Built 2026-10-26 on branch `feature/moon-shrine`, not yet playtested. Other scenario ideas, ranked, for later: break the ritual (destroy totems before turn N), survive waves, assassinate the leader, rescue a captive, hold the ground, break through, crumbling tower; puzzles last (fixed puzzles clash with a random army; maybe as a ? event with a fixed squad).

- **Map:** a new node type (`NodeKind.Ritual`), once per run, as the other choice next to the battle at step 3 or step 5. May later merge into battle nodes as a variant.
- **Board** *(placeholders)*: the shrine (15 HP, can't act or move) stands at (3,3). You deploy within 2 steps of it, not in the back rows. 3 Elementals start on edge tiles at least 4 steps away; 2 more walk in from the edges after enemy turns 1 and 3. One mirrored pair of crystals.
- **Win:** survive 5 enemy turns; the ritual completes and the rest flee. Killing everyone wins early only once no reinforcements are left. Pays normal gold, then you pick a **blessing**.
- **Loss:** the shrine breaks: no gold, a random **curse**, and the run goes on. Losing the whole army still ends the run.
- **Enemy AI here:** go for the shrine unless one of your units is next to them (Taunt still wins). The shrine is your ally for Shield Wall and Guard; your own blasts and volleys hurt it.
- **Factions:** elementals only for now (slow and tough; a split next to the shrine drops two shards on it). Goblin bombers don't fit (bombs on something that can't move). Other factions would need their own behaviour in this mode.
- **Blessings and curses (omens):** one per run, lasting every battle after. A blessing helps you, the same omen as a curse helps the enemy. Omens never pick the shrine.

| Omen | Blessing | Curse |
|---|---|---|
| Sun | Start of your turn: the tile under a random enemy is marked; at the start of the enemy turn a sun beam hits it for 3 (tile-based: pushing the enemy off makes it miss) | The mark goes under one of your units; step off before the enemy turn |
| Night | Start of your turn: a random unit of yours and the 3x3 around it are shrouded until your next turn; enemies can't hit units there from range (point blank still works) | The shroud covers an enemy; your ranged attacks, fireballs and bombs can't reach it |
| Moon | Start of your turn: a random unit of yours gets a 4-point shield until your next turn | The shield goes to an enemy |

## Test mode (for testing only)

- Editor and dev builds only: a "Test battle" button on the draft screen opens a setup screen.
- Pick any 1–4 unit types (any tier, repeats allowed), any encounter (factions, bosses, Moon Shrine; for the Necromancer, which factions count as beaten) and optionally any blessing or curse. No items for now.
- The battle uses the normal rules; afterwards it returns to the setup screen. No run, gold or map, and no run log; battle logs are marked `test: true`.

## Later / maybe

- Drawn spell cards during battle.
- Other win conditions.
- Difficulty modifiers (for example, HP carrying over between fights).
- **Banners:** an army-wide rule layer (like Balatro jokers, but every banner bends a rule and has a cost, e.g. Lone Wolf: army limit 1 but big upside). Parked: "hard topic".
- **More acts** after Act 1; terrain features (river, bushes, crumbling floor) for layouts; post-battle reward choices; elite nodes.

## Open questions

- Is an unusual identity rule needed for the first prototype? Leaning towards: prototype the plain core first, then decide.
- Board size (playtest).
- Which node types exist besides battles.
- Unit roster for a first prototype (beyond warrior and archer).

## Unusual rule (identity hook)

Still exploring. Candidates are in the chat. Record the chosen one here.
