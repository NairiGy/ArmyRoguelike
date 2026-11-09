# Design history

Sections moved out of [design-note.md](design-note.md) on 2026-11-08, word for word, because the rules they describe have been replaced. Read them for the reasons behind old decisions; the design note holds the rules that apply now. Where the two disagree, the design note wins. The whole note as it stood before that cleanup: `git show ac9713e:docs/design-note.md`.

## Old status line (as of 2026-10-30)

Status (2026-10-30): techniques replace evolutions and Trainers, playtested once (see "Techniques instead of evolutions"); the sections before it are history where they mention evolutions, tiers, Trainers, item picks or Bloodthirst. Earlier: Prototype 5 (Act 1) built: 7-step Act 1 where each of four factions (wolves, goblins, bandits, elementals) appears at most once, rule-based layouts, a rotating boss (Necromancer raising your beaten factions, or the Giant), one evolution per unit per Trainer visit with paid extras, zone of control. Prototype 4: draft 3 of 5 base units, 9-step map with free Trainers at steps 4 and 7 (every unit +1 tier per visit), 5 evolution trees (38 unit types, 3 tiers), only rule-bending items. Full unit/ability/item reference: `docs/gamepedia.html` (generated from game data; re-export via Tools → Export Gamepedia). Earlier, Prototype 3: branching 6-step map (battles, shops, ? events, ogre boss), built-in unit abilities, ability items, the sapper recruit with mines, and a goblin bomber battle. Prototype 2 (fixed mini-run) was playtested once: won, about 5.5 minutes. Fixes from it: walk-up attacks, End turn confirmation, necromancer no longer idle. Prototype 1 (one battle): no bugs, felt OK, won 2/2, "not super easy". Numbers marked *(placeholder)* are only examples.

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


## Upheaval (elementals, 2026-10-30 to 2026-11-09)

The Elemental's action before Tremor: the ground burst up under one of your units within 2 tiles (no line of sight): 2 damage and a push straight away from the elemental (2 more if it slammed into something). It first left a rock behind; that was dropped after the 2026-10-30 playtest (too many rocks). Replaced by Tremor on 2026-11-09 because elementals never helped each other: in the sim an Upheaval slammed your unit into something only 0.3 times per fight, and the push went wherever the caster happened to be, never towards another elemental. The Shard had no passive; it got Aftershock at the same time.
