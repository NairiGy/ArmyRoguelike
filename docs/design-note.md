# Grid Tactics Roguelike — Design Note

Working title: "Army Roguelike" (placeholder; a better name will come later). Separate project from Nardatro. Unity project: `Desktop/Unity projects/ArmyRoguelike`. This file (`docs/design-note.md`) is the canonical copy.
Status: Prototype 2 (mini-run) built, not yet playtested: 3 fixed battles (wolves → necromancer → ogre) with a shop after each of the first two. Prototype 1 (one battle) was playtested: no bugs, felt OK, won 2/2, "not super easy". Numbers marked *(placeholder)* are only examples.

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
- Identity direction: each unit carries its own rule, like Balatro's jokers.
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
| Necromancer | 6 | 2 (bolt, needs line of sight) | 3 | 3 | Raises a bone pile within 4 tiles to a full-HP skeleton (costs 3 AP). Stays at the back and backs away from units closer than 3 tiles. |

- A statue standing on a bone pile stops it being raised. Piles don't block movement.
- Kill the necromancer and the piles stay dead.
- A raised skeleton waits until the next enemy turn before acting.

**Battle 3: Ogre** *(placeholder numbers)* — alone, 2x2 tiles.

| Unit | HP | Smash | Boulder | AP |
|---|---|---|---|---|
| Ogre | 30 | 5 damage to **every** adjacent unit, 2 AP | 3 damage, range 2–5, 3 AP, once per turn, ignores line of sight | 4 |

- The boulder comes to rest on the free tile next to the target closest to the ogre and **stays there as a rock**.
- Hovering the ogre shows the smash ring, the boulder target and the tile the rock will land on.

## Later / maybe

- Drawn spell cards during battle.
- Other win conditions.
- Difficulty modifiers (for example, HP carrying over between fights).

## Open questions

- Is an unusual identity rule needed for the first prototype? Leaning towards: prototype the plain core first, then decide.
- Board size (playtest).
- Which node types exist besides battles.
- Unit roster for a first prototype (beyond warrior and archer).

## Unusual rule (identity hook)

Still exploring. Candidates are in the chat. Record the chosen one here.
