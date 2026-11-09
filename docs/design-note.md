# ArmyRoguelike — Design Note

What has been decided, and why. Working title "Army Roguelike" (a better name will come later).

**How to use this file**
- It describes the game **as it is now**. Replaced rules (8x8 board, AP pools, evolutions, Trainers, the 7-step act, the prototypes) moved to [design-history.md](design-history.md); read that for old reasons, not for rules.
- **Numbers live in the game data**, shown in [gamepedia.html](gamepedia.html). Numbers quoted here are placeholders that explain a decision; if one disagrees with the gamepedia, the gamepedia is right.
- "Not playtested" means no human has played it yet. Open work is in [tasks.md](tasks.md), not here.
- When a decision changes, edit the rule where it stands (one place per rule) and keep the why short. Add the date in brackets.

## Pitch

A roguelike army-builder: lead a small squad of living statues across a Slay the Spire–style map of fights, then fight turn-based tactical battles against an AI army on a small isometric 2D grid.

## Design goals

- Fun from the very first turn, with real choices.
- The board should be readable at a glance.
- Its own identity, not a copy of an existing game (see "Identity rule").

## Run

- **Start:** "Prepare your army" (2026-11-08): pick 1 of 3 banners (switchable until the run starts) and draft 3 of the 5 base units (Warrior, Archer, Rogue, Mage Apprentice, Sapper). Each unit card has "More info": every stat, level growth and every skill the line can have (starting kit blue, common white, rare purple).
- **Army:** 4 units at most. **Battle effects stay in the battle:** every unit starts every fight at full HP. Lasting harm comes only from cracks (see "Night pass").
- **Act 1, 12 steps** (one act for now). Pick 1 of 2 nodes per step:

| Step | Choice |
|---|---|
| 1 | Battle / Battle |
| 2 | Shop / ? |
| 3 | Battle / ? |
| 4 | Scenario / Battle |
| 5 | Shop / ? |
| 6 | Elite / Battle |
| 7 | Shop / ? |
| 8 | Scenario (the other one) / Battle |
| 9 | ? / Battle |
| 10 | Elite / Battle |
| 11 | Shop / ? |
| 12 | Boss: Necromancer or Giant (one per run) |

- At least 5 fights before the boss (steps 1, 4, 6, 8, 10), up to 7, more with ambushes. Goal: a 15–20 minute run that ramps up (the 7-step act was won in under 7 minutes).
- **Battle:** a faction you haven't beaten yet; once you've met them all, a normal-strength rematch. Each regular faction has a difficulty pool (`Encounter.Pool`): Wolves, Goblins and Bandits in pool 1; Elementals and Demons in pool 2. Step 1 draws only from pool 1; later steps, ambushes and elites ignore pools for now (2026-11-01, user's call). The map shows "Battle" / "Elite" until the run reaches the step; the two fights of a step never share a faction.
- **Elite:** a harder rematch of a faction you've beaten (more of the same enemies, no new types). High risk, high reward, always a choice: its reward pick holds a rare technique, or a banner instead.
- **Champions** from step 6 and **Stone Heart** bosses: see "Night pass".
- **Layouts:** every encounter has a rule-based template (`Core/Encounter.cs`); rock positions and start tiles vary within it. The player's back two rows are always clear and every enemy is reachable on foot. At most 2 rocks, never touching, except where the fight needs more (the goblins' broken ridge, the elite Bandit Camp's cover per crossbowman).
- **Rewards:** gold after a won fight scales with survivors (the cost of death), then pick 1 of 3 techniques or skip for gold. Offers only include techniques one of your units can learn; you choose who learns it. No pick after the boss or the Moon Shrine (it gives a blessing).
- **Shops** (map nodes only): 3 techniques (learnable ones), 3 items, a recruit (when the army has room), 1 banner you don't hold, and services: Forget, Mend (a crack), Hone. Gold goes only to shops.
- **? events:** Wandering Recruit (a base unit joins free, or gold if the army is full), Abandoned Cart (a technique or an item for one unit), Gambler (pay for a 50% chance at more), Ambush (see below), Shrine (one unit +2 max HP for the run); and the night pass's events with a price (Stonemason, Cracked Altar, Sculptor, Hooded Stranger, see "Night pass").
- **Ambush:** a fight against a faction you haven't met that isn't waiting later on the map, with extra gold; if none is left, the ambushers flee and drop gold. It rolls a battlefield too.
- **Loop:** draft → fight → pick a technique (the unit levels) or skip for gold → spend gold at shops → boss.
- **Run end:** asks "serious run / just testing" and a difficulty from 1 to 5, both optional, saved in the run log. The map has "Abandon run", which opens the same screen with a way back.

## Battle rules

Merged into `main` 2026-10-30 after the small-board test ("feels ok": tight fights of 5-6 turns). Why: positioning should matter, every step should count, and the board should read at a glance.

**Board and turns**
- **Board 6x6.** Each side deploys in its back 2 rows (you place freely), leaving 2 empty rows between the armies. Enemies start in rows 4–5.
- **Win:** kill every enemy (scenarios have their own goals). **Combat is deterministic:** no hit chances or damage rolls.
- **Side phases:** all your units act, then all enemies. Each unit finishes before the next starts.
- **1 move and 1 AP** for every unit, bosses included. The move is separate from AP; nothing carries over. Every attack or action costs 1 AP.
- **Move first:** once a unit spends AP it can't move again that turn, for both sides ("position, then commit"; no hit-and-run). Exceptions are named on the skill (Light Fingers, Momentum, Free actions).
- **Swift:** 2 moves. Rogue, Wolf, Pack Leader, Goblin, Cutpurse (and their raised versions).
- **Zone of control:** stepping out of a tile next to an enemy uses up all remaining moves, both sides. A 1-move unit can always back off one tile; a Swift unit can't slip past. Objectives (shrine, totems) have none.
- **Walking through allies** (2026-11-01): any unit can walk through its allies but not stop on them; never through enemies or objectives. Why: fewer traffic jams, while your front line still blocks Swift enemies. Mines, caltrops and overwatch only stop a unit on a tile it can stand on.
- **Cooldowns** (2026-10-30): basic attacks have none; every other action is ready again N of the unit's own turns after use (2 or 3, see the gamepedia). Skills start ready. Why: easier balancing, room for stronger actions, and with 1 AP a real choice each turn.
- **Free** actions (Vanish, Throw Shield and others) cost no AP and leave the unit able to move and attack, but only before it has acted, so "position, then commit" holds.

**Areas, pushes and tiles**
- **Areas:** bombs and mines hit only their own tile (one step dodges a bomb; only the enemy that steps on a mine is hurt). Fireballs and Volley hit a plus (the tile and its 4 neighbours). Larger blasts (barricades, Powder Keg, Corpse Blast) are 3x3. A 3x3 blast sets off mines and bombs inside it.
- **Ranges** fit the 6x6 board: most ranged attacks reach 3 tiles.
- **Weight:** every unit is small, medium or huge. A push moves a medium unit 1 tile, a small one 2, a huge one 0 (no move, no collision damage). Pushes move tile by tile and stop with collision damage at whatever blocks them. The Warrior is medium (huge would make it immune to Bull Rush, Upheaval and Slam). Raised and harvested copies keep their weight. Who is which: the gamepedia.
- **Walls** (rock, crystals, tombstones, boulders, ice, barricades) block walking and shots and give cover. **Rubble** blocks walking only and crushes what is pushed into it. **Holes** (Crumbling Roof) block walking; a unit in one falls. In code: `Board.IsObstacle` = can't stand there; `Board.IsRock` = blocks sight and gives cover.
- **Barricades** have HP; either side can attack them with a basic attack, and an enemy that spent no AP on its turn attacks a barricade in its reach. Rocks, cover, tombstones and ice stay solid.
- **Bone piles:** don't block movement. A statue standing on one stops it being raised. One tile holds one pile; the newer replaces the older (2026-11-01, so a raise can't put two units on a tile).
- **Mines:** the Sapper's; enemies don't see them and walk in. Player units walk over their own mines safely.

**Enemy behaviour**
- **Intents:** hovering an enemy shows what it will do; Alt shows every enemy's intent at once. Each shown intent puts a badge over the enemy's head (icon by kind, the action's name, coloured: orange harm, green buff, purple control, gold flee).
- **Shields** (2026-11-01): when an enemy's target has a shield, it goes for an unshielded unit it can reach this turn instead, and hits the shield only if there is none. Taunt overrides this. Why: attacking a shield wasted their turns, and never attacking one would make shields total protection.
- **Slowed** enemies never back off: one that can hit from where it stands does, one that can't walks up and loses its action.
- Per-enemy AI and each behaviour's decision list: the gamepedia (`GamepediaWriter.BehaviourSteps`; adding an `EnemyBehaviour` value needs its text there or the export fails).

## Keywords and one name per rule

- **Keywords** (`Core/Keywords.cs`, 17): Pinned, Slowed, Marked, Exposed, Hidden, Burning, Wall, Rubble, Blast, Poison, Push, Zone of control, Line of sight, Fury, Swift, Shield, Free. Rule texts name a keyword without explaining it; the game highlights it and shows its meaning on hover. A new rule word that several texts explain should become a keyword. Why: texts took too much space and repeated the same explanations.
- **One name per rule** (2026-11-08, not playtested):
  - **Slowed** = "move or act, not both" on its next turn (walking sets AP to 0; spending AP ends its moves). It used to be the same as pinned. **Pinned** = can't move on its next turn. Pinned is the Archer's theme, Slowed the Mage's.
  - **Shield** replaces "temporary HP"; each text says how long its shield lasts (Ward until used up). Battlecry gives a shield, so enemies avoid it like Ward and the Moon shield.
  - **Marked** is only the Archer's +2. Others say: a sun sigil appears (Sun omen), lightning gathers over a tile (Storm), the Devil signs a contract.
  - **Elusive** (goblins and others; was Slippery), **Thorns** (the Fiend and Thorned Mail; was Spines), **Overwatch** (the Crossbowman's, and the Archer's `CoveringFire` in code).
  - **Push** texts give no tile count (weight decides it). The Grappling Hook is a pull, not a push: a set distance that weight and Battering Ram don't change.
  - **Blast** = an explosion (mine, bomb, Demolish, Detonate, Powder Keg, Corpse Blast), what Blast Proof and Incendiary cover. Fortune's Favored says "hits"; Hidden and Behind Cover say "area damage".
  - Names inside action names aren't the keyword: Shield Up, Shield Bash, Shield Wall, Throw Shield, Ice Wall, Corpse Blast, Blast Proof.

## Enemies

Every enemy has its own passives and actions; the basic attack is a named action (Wolf Bite, Strike, Shot, Bolt, Smash, Stab). Numbers are in the gamepedia.

- **Wolf** (Swift). **Blood Scent:** goes for the lowest-HP unit it can reach and attack this turn. **Wolf Bite:** +1 for every wolf bite earlier in the same turn (any wolf, any target; resets every turn).
- **Pack Leader** (Swift, huge). **Pack Bond:** while it lives every wolf has +1 max HP; once it dies they have -1 and lose 2 HP on the spot. **Howl:** every wolf that hasn't acted gets +1 move. It acts first and howls when the extra move lets a wolf bite that otherwise couldn't.
- **Goblin** (Swift). **Elusive:** ignores zone of control. **Fling:** throws what is next to it at one of your units 2-4 tiles away, over obstacles. A bomb or mine next to it goes off there with its own blast (it always does this when it can); otherwise a goblin that hasn't acted lands next to the target, deals 2 and can still act. It flings a goblin only when it can't reach anyone to strike. Rocks are too heavy.
- **Goblin Bomber.** **Powder Keg:** explodes in the 3x3 when it dies, its own side included. **Bomb:** one tile, 2-3 away, goes off at the start of the next enemy turn. Keeps its distance.
- **Cutpurse** (Swift). Steals gold on its first hit and runs for the far edge; if it escapes, the gold is lost. **Light Fingers:** may move after attacking. **Caltrops:** a unit of yours stepping on them takes 1 and stops. Cornered with gold, it hits whoever it can.
- **Crossbowman.** **Behind Cover:** next to a wall it can't be hit by ranged attacks (area damage still reaches it); its AI picks covered tiles. **Overwatch:** with no shot it walks closer and aims; during your next turn the first of your units that walks onto a tile it sees in range is shot and stops there.
- **Bandit Brute** (huge). **Guard:** takes the hit when an adjacent ally is attacked. **Bull Rush:** hits and pushes.
- **Elemental** (huge). Splits into 2 Shards when it dies. **Upheaval:** the ground bursts under one of your units within 2 tiles (no line of sight): damage and a push away from the elemental. It used to leave a rock; too many rocks in play (2026-10-30 playtest), so not for now.
- **Shard:** no passive for now (Reform, merging back into an Elemental, was cut 2026-10-30: nearly every split reformed).
- **Skeleton.** **Grasp:** pins a unit next to it, no damage. Grasps ranged or Swift targets, strikes the rest. Why: a weak unit that holds yours in place for the Necromancer.
- **Demons** (5th faction, pool 2, 2026-11-08): Imp, Demon, Devil, Fiend; blood and bargains (Fury, Blood Offering, Hellswap, Rending Blow, Soul Contract, Thorns, Fire Breath; Hellborn: burning ground doesn't hurt them). Full design: [night/demons.md](night/demons.md).

**Bosses** (alone or with their army, one per run; both have Stone Heart, see "Night pass")
- **Giant** (2x2, huge). **Colossus:** can't be pushed, pinned, slowed or grasped. Why: one archer could lock it down. **Wrath:** two actions a turn from Stone Heart phase 2 (it used to come at half HP). Actions: **Smash** (every adjacent unit), **Slam** (damage and a shove to your units within 2 tiles; it picks smash or slam by whichever deals more), **Boulder** (lobbed; the rock stays, and it won't throw one that walls it off from its target). Phase 3: falling rocks on 2 of your units' tiles each round, marked a turn ahead.
- **Necromancer.** Raises **every** bone pile within range in one action; backs off before raising or shooting. His army is raised from the factions you beat this act (1 of each, topped up with skeletons to 4). **Soul Harvest:** while it lives your fallen units leave a bone pile; raised, they fight for him as "Bone <unit>" with HP, damage, range and Swift but no other kit (user's call over the milder Siphon). **Corpse Blast:** a bone pile explodes in the 3x3, friend or foe; used when it hits more of yours than his. Why: standing on a pile to block a raise now has a price. Each new phase: 2 skeletons climb onto the edge.

## Player units and techniques

Decided with the user 2026-10-30, replacing evolutions and Trainers (see the history file). Why: evolution added little to the run, and the run should feel like deckbuilding: a pick after every fight and builds that grow one card at a time.

- **The 5 base units** keep their base kits as a starter deck. Each line has a theme keyword other lines read:
  - **Warrior:** no base passive (user's call, for now); Strike, and **Shield Bash** (damage and a push; extra damage if it slams into something, and what it hits takes collision damage too). Themes: Push, Guard.
  - **Archer:** Steady Aim (+1 if it hasn't moved), **Pinning Shot** (1 damage and pin). Themes: Pinned, Marked.
  - **Rogue:** Swift, small; **Vanish** (free, before acting): hidden until your next turn; attacking doesn't reveal it, and its first attack while hidden deals +2. Themes: Exposed, Hidden, Poison.
  - **Mage Apprentice:** small, fragile; **Frost Nova** (slows) in the base kit since the night pass, Blink parked. Themes: Slowed, Burning.
  - **Sapper:** Mines and **Place Mine** (an enemy that steps on one sets it off; a few on the board at most). Themes: Wall, Blast (and Rubble).
- **Techniques:** passives or actions (actions use the normal 1 AP and cooldown rules), each belonging to one line, 62 in all (Warrior 8, Archer 14, Rogue 14, Mage 14, Sapper 12). Common or rare. Some need another first (`Requires`); these chains are the deckbuilding pull. Skills stay a deck (random offers), not a tree. Parked skills keep their rules in code but aren't in `Technique.All`, so no run offers them. Full lists: the gamepedia; designs and reasons: "Warrior redesign" below, [night/archer-rogue.md](night/archer-rogue.md), [night/mage-sapper.md](night/mage-sapper.md).
- **No slot limit.** With 1 AP, extra actions add choice more than power. Watch stacked passives in the logs; add a cap if one unit snowballs (flat stacking bonuses are capped at +4).
- **Levels:** a unit's level is the number of different techniques it has learned. Each level grows the unit by its line (mostly HP; damage only at a few levels, on purpose, since HP is 5–12 on a 6x6 board). Forgetting keeps the level; relearning a technique the unit ever knew gives no new level (2026-11-01: Forget plus relearn bought levels).
- **Items** (user's call, 2026-10-30): one slot per unit; a new item replaces the old. Vampire Fang, Thorned Mail, Spring Boots, Lucky Charm. Sold in shops; the Abandoned Cart holds a technique or an item. Items don't level a unit. A new item design (strong items with a downside, consumables) may come later.
- **Goals for every line** (2026-11-05): many more skills than a run can learn, skills that combine with each other and with other units, each unit unique, fun.

**Warrior redesign** (2026-11-05, not playtested; numbers are Claude's placeholders)
- **Sweep** (common): full damage to the target and the enemies on both sides.
- **Charge** (rare): up to 3 tiles in a straight line, damaging every enemy on the way and pushing each to a random side (not shown beforehand); the other side if blocked, collision damage if both are. An enemy still in the way stops it. Passes through allies. Needs an enemy on the way.
- **Shield Up** (common): 2 less damage from every source, at least 1, for 2 enemy turns.
- **Battlecry** (rare): allies within 2 tiles, itself included, get +1 move and a 2-point shield until your next turn. The extra move only helps units that haven't acted, so the Warrior should shout first.
- **Taunt** (rare, an action): until your next turn, enemies within 2 tiles can only attack or use offensive abilities on this unit. Area hits and Overwatch shots still work as usual.
- **Throw Shield** (common, free before acting, once per battle): +1 move and +2 damage for the rest of the battle, for 4 HP; needs more than 4 HP.
- **Vanguard** (common): deploys 1 row further forward (not in the Moon Shrine).
- **Placeholder** (rare, name to come): attacks any unit that steps out of its reach, once a round. Elusive ignores it, pushes don't trigger it, enemies don't avoid it yet.
- **Parked:** Shield Wall, Guard (the Brute keeps it), Long Reach, Double Strike, Tough.
- **Starting on cooldown:** rejected for now. Claude's concern if it returns: in 5-6 turn fights a cooldown 3 skill would be used about once.

## Scenarios

Both are in every run, next to battles at steps 4 and 8. Other ideas, ranked, for later: survive waves, assassinate the leader, rescue a captive, hold the ground, break through; puzzles last (fixed puzzles clash with a random army).

**Moon Shrine** (defend)
- The shrine (can't act or move) stands in the middle. You deploy within 2 steps of it, not in the back rows. Elementals start on edge tiles and more walk in from the edges, one at a time (2026-10-30: 7 Elementals and their Shards were too many on 6x6).
- **Win:** survive 5 enemy turns; the ritual completes and the rest flee. Killing everyone wins early only once no reinforcements are left. Normal gold, then pick a **blessing** (no technique pick).
- **Loss:** the shrine breaks: no gold, a random **curse**, the run goes on. Losing the whole army still ends the run.
- **AI:** enemies go for the shrine unless one of your units is next to them (Taunt still wins). The shrine is your ally for Guard; your own blasts hurt it.
- Elementals only for now; other factions would need their own behaviour in this mode.

**Break the Ritual** (attack, the mirror of the Moon Shrine)
- 3 totems (can't move or act) on rows 4–5, at least 3 steps apart, guarded by goblins; you deploy in your back two rows.
- **Win:** smash every totem before the summoning completes at the start of enemy turn 6 (4 until 2026-11-01; the 2026-11-04 playtest won on turns 5 and 6). The guards flee. Normal gold and a technique pick.
- **Loss:** no gold, a random curse, the run goes on.
- **Guard AI:** guards go for whichever of your units is within 2 tiles of a totem (nearest first), otherwise fight as usual. Their own bombs don't hurt the totems; your blasts do.

**Omens (blessings and curses):** one per scenario, lasting every battle after. A blessing helps you; the same omen as a curse helps the enemy. Losing both scenarios gives two different curses. Omens never pick the shrine.

| Omen | Blessing | Curse |
|---|---|---|
| Sun | Start of your turn: a sun sigil appears under a random enemy; at the start of the enemy turn a beam hits that tile (tile-based: push the enemy off and it misses) | The sigil goes under one of your units; step off before the enemy turn |
| Night | Start of your turn: a random unit of yours and the plus around it are shrouded until your next turn; enemies can't hit units there from range (point blank still works). 3x3 until 2026-11-01, too big | The shroud covers an enemy; your ranged attacks, fireballs and bombs can't reach it |
| Moon | Start of your turn: a random unit of yours gets a shield until your next turn | The shield goes to an enemy |

## Banners and weight

Built 2026-10-31, not playtested. A banner bends a rule for the whole army for the rest of the run (like Balatro jokers). The user wants builds and synergies from them, so there's no limit on how many you hold; each is held once.

- **Getting them** (the user's first take, not final): 1 of 3 at the start, one for sale in every shop, one on an elite's reward screen instead of its technique.
- **Vampiric:** every attack of yours that deals damage heals the attacker 1 (attacks only; not above max HP). Cost: the Curse of the Sun in every fight. The user's idea was +2; +1 because units have 4-12 HP.
- **Third Wheel:** your units get +1 AP and +1 move (a mercenary too). Cost: the army holds 2 units; taken later, you choose who stays. Move first still holds: move up to 2, then 2 actions.
- **Loser:** an army unit that dies in a fight gets +1 max HP and +1 damage for the rest of the run, stacking (also after a lost scenario). Uncapped on purpose (user's call, 2026-11-01).
- **Mercenaries:** before each fight, hire 1 of 2 random base units (level 0) for gold, or go without. It fights that battle only, doesn't count as a survivor, and can't keep a fight going alone.
- **Battering Ram:** your units' pushes go 1 tile further and deal 2 damage at the end. No cost for now (user's call). Enemy pushes are unchanged.
- **Ideas not built:** Lady Death (enemies get +1 unit; after X wins she gives powerful abilities; needs its reward designed), Run Rabbit Run (2 holes per fight that act as teleports); Claude's: Stone Skin, Glass Banner, Shieldwall, Zealots, Scholar, Last Stand.
- **Look:** see "Visual direction".

## Battlefields

Built 2026-10-31, not playtested. One rule per fight (`Core/Battlefield.cs`), the same for both sides.

- **Where:** every battle, elite, boss and ambush gets one when the run is made, shown on the map node (hover for the rule), so you can route towards or around it. Scenarios get none. A step's fights never share one or repeat one from the step before. Every fight has one while we test (user's call); later maybe only some.
- **Edge of the World:** a push off the board kills; nothing saves the unit, it leaves no bone pile and a falling bomber's Powder Keg doesn't go off. Huge units can't be pushed; bosses never fall. Enemies that can push your unit off do that before anything else, and the intent shows it.
- **Storm:** at the start of each round lightning gathers over the tile under one random unit of each side; it strikes at the end of the enemy turn (tile-based, like the sun beam). Enemies never stop on a mark and step off theirs. Replaced the user's "volcanoes": they don't fit the tower, and unmarked hits feel unfair.
- **Arcane Overflow:** every action's cooldown is 1 shorter, 1 at least. Both sides.
- **Fortune's Favored:** at the start of each round one random unit of each side that deals damage (never a boss) is favored: its hits deal double until the next round (not collisions, poison or burning ground). A side with nobody eligible has no favored unit (2026-11-01: the lone Giant was favored every round).
- **What's On His Mind?:** no enemy intents. View only.
- **Crumbling Roof** (2026-11-08): see "Night pass".
- **Not built:** Misty (needs real fog to matter on 6x6), Chaos (swap one unit with the enemy; open questions), Keeper of Balance (a third side), Portals; Claude's: Gale, Ice, High Ground, Ley Line.

## Night pass (2026-11-08, merged into `main`, playtested once)

The user asked Claude to make the game more fun overnight, with full design freedom on a branch (`feature/night-fun`, merged in full the same day after one playtest). Everything here is Claude's design with placeholder numbers and visuals. The timeline, including what was tried and dropped, is in [night-log.md](night-log.md); the per-line designs are in [night/](night/), and the research behind it in [night/research.md](night/research.md).

- **Cracks:** an army statue that falls in a fight you win (or a lost scenario) gets a crack: -1 max HP until mended. The 3rd crack shatters it, and it leaves the army with its techniques and item. Shops mend a crack. Scars (from the Cracked Altar) are cracks that can't be mended. Why: deaths cost only gold, so a run was decided by step 4 and the rest had no tension (Into the Breach's grid, Darkest Dungeon's injuries). Open: Loser plus cracks makes a death nearly free growth again (user's call).
- **Events with a price:** Stonemason (mends every crack, or +1 max HP with none), Cracked Altar (a rare technique for a scar; from step 5), Sculptor (give up a technique you choose for a random rare of the line, no level; from step 5), Hooded Stranger (gold for a curse). Why: the old events were mostly free gifts or a coin flip.
- **Champions:** from step 6, every battle and elite node has one, its affix shown on the node. The toughest enemy gets more max HP and Armored (-1 damage taken), Vengeful (damage to adjacent foes on death), Regenerating, Mighty (+1 damage) or Fleet (+1 move). Pays extra gold. `Core/Champion.cs`. Why: late fights were rematches that asked nothing new of the build.
- **Stone Heart (bosses):** HP falls in 3 phases; damage past the next phase is lost until your next turn, so a boss takes at least 3 rounds. Entering a phase makes the boss **Enraged** (one extra action next turn). Bosses get +1 max HP per army level and never fall (edge, holes). Why: a playtest killed the Giant in 3 turns for 1 damage taken. Each boss's phases: "Enemies".
- **Crumbling Roof** (6th battlefield): collisions and deaths fracture the tile under the unit; the 2nd fracture opens a hole. A unit on it, or pushed into one, falls and dies. Holes block walking, not shots, and give no cover. Enemies push into holes the way they push off the edge. Candidate for the game's identity rule if it plays well.
- **Hone** (shop service): one action's cooldown is 1 turn shorter for that unit for the run (actions with cooldown 2+, once each). Why: cheap depth that works with every action.
- **New pools and the Demons:** see "Player units and techniques" and "Enemies".

## Synergy map (2026-11-08)

Decided with the user. Goals for the five heroes: every hero has at least one synergy with each of the other four, synergies inside its own kit, and at least one theme that is a keyword.

- **The map:** [Hero Synergy Map](https://claude.ai/artifact/BCBjEq3EFMuN1tZi1sLoyw) (private claude.ai page; source [synergy-map.html](synergy-map.html), one HTML file with the data as a JS table at the bottom: edit the tags, open it in a browser, republish to the same link; the hover cards show the game's own rule texts, copied in by `tools/dev.sh synergy`). Every hero skill, item, banner, battlefield, omen and champion affix is tagged with the theme keywords it **makes**, **uses** (the payoff) or **hurts**; a tag marked `!` uses only the unit's own effect. A synergy is a maker plus a user; the page counts them per hero pair, plus a few links written by hand (Pincer needing an ally behind the target, Smoke Bomb on another hero, Taunt + Shield Up). Why tags: the counts stay correct as skills change and holes show up by themselves. The counts are a diagnostic, not a target: some anti-synergy is a real choice (Isolate wants lone enemies, Sweep wants groups).
- **Themes** (proposed, not final): Warrior Push (main), Guard; Archer Pinned, Marked; Rogue Exposed, Hidden, Poison; Mage Slowed, Burning; Sapper Wall, Blast. Guard (shields, taunts, damage cuts) and Kill (on-kill triggers) are design tags, not game keywords.
- **Pair links** (2026-11-08, in kit on the diagonal): Warrior 3 in kit; Warrior-Archer 1, Warrior-Rogue 2, Warrior-Mage 2, Warrior-Sapper 2; Archer 3 in kit, Archer-Rogue 3, Archer-Mage 3, Archer-Sapper 2; Rogue 3 in kit, Rogue-Mage 4, Rogue-Sapper 1; Mage 2 in kit, Mage-Sapper 3; Sapper 3 in kit.
- **Holes:** Warrior-Archer (only Taunt helping Steady Aim; Battlecry's move works against it) and Rogue-Sapper (only the Grappling Hook). Burning, Poison and Guard have no payoff outside their own kit; nothing but the Rogue gains from being hidden. The Mage spreads over too many themes. The Warrior's pushes pay off only outside its kit. Prey (pinned) and Shatter (slowed) still pay the same +2.
- **Roles and judgment** (2026-11-09): every entry on the map has a role (Standalone, Enabler, Payoff, Bridge, Tension; Condition for battlefields and omens, Counter for champion affixes) and is judged on Decision, Board, Answer, Readability and Power (in that order of weight; Power is a design estimate until the bots use actions). A standalone with no tags is doing its job, not a hole: not everything needs a synergy.
- **Tension pieces for every hero** (decided with the user 2026-11-09, built the same day, not playtested): a tension piece is strong with a real cost, so taking or using it is a choice. Only the Warrior had any (Sweep, Battlecry, Throw Shield), and they judged as the best-shaped part of the game. Where possible, a number-only passive is reworked into one instead of adding a new technique, so the pools don't grow. Numbers are placeholders; the details below are the defaults the user approved when they were built.
  - **Archer, Planted** (new, common action, free, cooldown 3): the Archer is pinned for the rest of this turn and on its next one; until then its shots deal +2 and pin every enemy they hit. Shots are the Shot, Pinning Shot, Fire Arrow, Suppressing Fire and Overwatch's shot; Kick is not one. The +2 counts inside the +4 cap on conditional bonuses and stacks with Steady Aim. Like other free actions it can be used after moving, before acting. Skeletons don't Grasp a planted Archer (it counts as pinned). Cost: it can't get away, and Battlecry's move is wasted on it.
  - **Archer, Steady Aim+** (rework): +3 if it hasn't moved this turn, -1 if it has (Kick included; never below 1).
  - **Rogue, Lone Wolf** (new, common passive): +2 damage while the Rogue itself is exposed, on every hit that carries its bonuses; inside the +4 cap like Isolate. Cost: alone means no Smoke Bomb, Ward or Taunt cover; the mirror of Isolate.
  - **Mage, Overchannel** (new, common action, free, cooldown 3): the next damaging spell this turn (its Bolt, Fireball, Firewall or Blizzard) deals double damage on every target; burning ground left after it is not doubled. The Mage is slowed on its next turn even if no spell followed (the cost is paid on use). Cost: the fragile Mage can't both cast and get away.
  - **Sapper, Rigged Barricades** (replaces Spiked Barricades, which is gone from the code): a barricade broken by a hit from either side explodes for 3 in the 3x3 around it (the Sapper's blast, so Blast Proof and Incendiary apply and it sets off mines and bombs), then leaves rubble as usual. Demolish and Detonate already blow a barricade up: one blast, not two. An enemy won't break a rigged barricade when the blast would cost its own side HP without costing yours more. Cost: the wall is gone for Vantage and pushes, and the blast hurts your units next to it unless you have Blast Proof.
  - **Sapper, Big Mines** (rework): mines deal 6, but it can have only 2 out (the level-3 mine still adds one).
  - Why: Planted and Overchannel turn a hero's own theme status against itself, the same shape as Steady Aim and Battlecry, and they need no new keyword.

## Faction map (2026-11-09)

Asked for by the user: the goal is factions that fight as one army, not random mobs, so faction fights are more interesting.

- **The map:** [Enemy Faction Map](https://claude.ai/artifact/AqboPtABMZ1qwcSG8CKrLJ) (private claude.ai page; source [faction-map.html](faction-map.html), data as JS tables at the bottom). A different format from the synergy map, because a faction is a fixed team with a plan, not pieces to combine. Per faction: its plan in one line, each unit's role (Anchor, Setup, Payoff, Disruptor, Clock, Filler), the combos between units with whether they fire (works, rare, missing, hurts, untested), the kill-order choice, the question it asks, a judgment (Cohesion, Question, Readability, Counterplay, Distinctness, in that order of weight) and a grid of which hero themes answer, resist or are punished by it.
- **First read** (Claude, from the code and logs, not checked in play): Wolves are the model (Howl and Blood Scent feed the bite chain; leader-or-pack choice). Demons are designed the same way but never played. Goblins' two halves don't meet (bombs go off before goblins could fling them). Bandits have three jobs that each work alone (the Brute never guards a shooter). Elementals have no link at all; they are hard through HP.
- **Ideas on the page, none agreed:** setup units act first for every faction, a shared prey per turn, layouts that put partners together, enemy banners, elites with a twist, combo counters in the logs, and a fix per faction. Each needs the user's OK.
- **Turn order stays as it is** (tried and dropped 2026-11-09): only howlers and imps act first. Letting units that move or hold your units (Brute, Elemental, Skeleton) act next changed nothing for four factions and made Bandits worse: the Brute charged ahead and guarded its shooters less (sim, same seeds). Why: turn order alone doesn't make teamwork; the gaps need each faction to act differently.

## UI and controls

- **Controls** (2026-11-08): left-click chooses, right-click acts. Left-click one of your units to select it (its card opens); left-click an enemy to open its card while your unit stays selected; left-click empty ground to drop the selection. Right-click a tile to move, an enemy to attack (or walk up and attack), a barricade or rubble to hit it, or a yellow tile to use the aimed action. Esc cancels an aimed action, or else closes the card. Placement and HUD buttons stay on left-click. Alt shows every enemy's intent.
- **Unit card** (2026-11-08, top right): name and level, HP bar in pieces (one per point), AP / moves / range / weight, statuses as chips (blue helps, red hurts, grey neither), **Next** for enemies, passives as green chips and one row per action (cost, cooldown, "ready in N"). Names only; hovering a name shows what it does. Plain hovering a unit shows no card. Which stats stay on the header line is open (user).
- **Attack preview:** on the target statue's own HP bar (a pale pulsing chunk and "-3", or who guards it), "walk 2, attack" by the pointer, and a red skull over a unit the action would kill.
- **Action cards:** the selected unit's special actions in a row at the bottom centre (key, name, cost, cooldown or "ready in N", short text). Click or press the key to aim, again to cancel. Done and End turn at the bottom right.
- **Zone of control shown** while one of your units is selected: enemy reach tiles tinted, move tiles that cost the rest of your moves in orange, a dotted link to each enemy holding the selected unit.
- Colours and icons are placeholders for the art chat (see the "Art needed" table in tasks).

## Visual direction

- **Look:** chunky, stylized, hand-painted **living statues** (like wizard-chess pieces) seen in 3/4 isometric view on the 2:1 board. Enemies are statues too. The board should not look like a chessboard. Generic animation shared by every unit, none per unit: attack lunge, hit flash and shake, crumbling death (skeletons turn into a bone pile), boulders arc, raised piles pull themselves back up.
- **Teams read by stone:** player statues pale (moon marble; torch gold and pale bone kept as alternatives), enemies dark basalt, so enemy art is generated in dark basalt. Exception: skeletons stay pale bone (2026-10-28). Rune glow optional.
- **Font** (2026-11-08, UI chat): Inter 4.1 (SIL OFL) on every IMGUI screen, `Assets/Art/Fonts`, set by `HudArt.ApplyFont`. Body text no smaller than 12 px. The statue labels (TextMesh) still use the built-in font.
- **Art source:** AI-generated 2D images from ChatGPT (free, about 3 images a day) and Leonardo.ai (Essential plan since 2026-10-31: 8,500 tokens a month, all models, Private Mode on). Every image is named after the tool that made it, `<name>__<site>_<model>.png` (e.g. `ogre_noground__leonardo_gpt-image-2.5-flare.png`, `ogre_noground__chatgpt_gpt-image.png`), in `Desktop/ai art` and `Experiment/Source`; the import tool finds files with or without the suffix. Every new unit uses the same prompt template on a plain light grey background, with an existing unit image attached as a style reference.
- **Generation rules** (2026-10-31, user): new images (backdrops, statues) with GPT Image (ChatGPT, or GPT Image 2.5 Flare in Leonardo); edits that keep the layout (colouring, removing clouds, masks) with Nano Banana. References are original GPT-made art (`iso_tower.png` for style and camera), never a Nano-made derivative. Backdrops seen from the tower: "looking steeply down, no horizon, no sky, fills the frame", no clouds (clouds are code layers), empty centre for the tower, landmarks near the corners. Make two variants, composite each under the tower layer and compare in context before wiring one in. Don't ask the user to tune sliders to judge a look; bring a finished one.
- **Pipeline:** Tools → Art → Import Statues cuts out the background. Figures drawn with no ground (`Source/<unit>_noground.png`) are scaled to their height and stand between their feet; older art with a pedestal (`_iso`) or slab (`_nopedestal`) is scaled by that pedestal's width. Left and right facing come from flipping the sprite.
- **Bases and size** (2026-10-28): unit art is the figure only, standing on nothing, straight on the floor with a dark contact shadow. A base can be added under any figure later without new art (`BattleScenery.DrawnBases`, off). Figures are sized by height (1.1 world units for a standing human, per-unit heights in `ArtExperiment.FigureHeights`) and drawn at 0.7 of the art's size (`BattleScenery.UnitScale`). Units whose art still has a painted pedestal or slab keep it until redrawn.
- **3D route** (image → Tripo/Meshy model → baked from the board's camera): tested; consistent but flatter and slimmer than the 2D images. Parked as a fallback, e.g. if units need more than two facings.
- **Battlefield scene:** the board is the roof of a tall tower above the clouds, at night. The scene is one greyscale isometric painting whose empty roof is fitted under the board, coloured through a palette ramp, so moods are cheap. Kept for later: a temple courtyard and a graveyard (same technique), a tower built from pieces, the amber-dusk and toxic-green palettes.
- **One palette for everything** (night scenes): each palette also has a ramp per team; statues are drawn by brightness through their team's ramp, so switching palette recolours units and scene together. Daylight scenes may use full-colour backdrops. A whole-screen snap to ~20 colours was tested and kept as an idea.
- **Each battlefield gets its own scene** (started 2026-10-31), so the rule reads before any text. Each scene is a backdrop behind the one tower layer, so a new scene needs only a background painting. `BattleScenery.Apply` picks it.
  - **Edge of the World = the void:** the roof hangs over a magic black hole (ancient magic, not sci-fi): a greyscale void painting replaces the sky, the walls dissolve into it, a painted ring of light turns over the hole, rocks spiral in, stones fall off the walls, pale specks peel off the edges; its own palette (black, indigo, violet, pale gold). A unit pushed off tumbles, is pulled into the hole spinning and shrinking, and the ring flares. `SceneryVoid`. Not yet: the ring's light on the walls.
  - **Fortune's Favored = the harvest:** the tower in daylight above ripe wheat fields with a river and windmills, walls sinking into warm haze, thin cloud, cloud shadows and gusts, chaff motes, no braziers. The land is in its own colours, not through the palette (user: one hue looked flat), coloured by Nano Banana with the layout kept; the Harvest palette is only sunlit stone. `SceneryFields`.
  - **Storm = the stormy sea:** the tower above dark teal waves with foam, rocks and small ships, ragged storm cloud, cold "Storm stone" palette, braziers lit. Shares the wheat scene's code (`FarBelow` scenes). The water flows (`Water` flow-map shader, whitecaps, a water mask keeping rocks and ships still; clouds removed from the painting and drifting as layers). The user isn't happy with the moving water; open question (see tasks).
  - Still to come: Arcane Overflow and What's On His Mind? (the night tower for now).
- **Faction colours** (tried 2026-10-30, goblins only): enemy factions lean their stone towards a hue of their own in code (`BattleScenery.FactionTints`, 45% towards the colour at each stop's brightness), so the teams read apart first, the faction second. Goblins, bombers and their totem lean mossy green. Ideas if it works: demons lava red, elementals cold blue-grey.
- **Board and facing:** the player deploys at the top of the screen, facing down-right; enemies face down-left, towards the camera, so their faces show. The roof has painted slabs and rocks and no grid.
- **A scene that breathes:** cloud shadows sweep across the roof and statues, and clouds, cloud bands and shadows drift with one wind. Dust motes, two braziers with flickering flames, soft contact shadows, a vignette. Stone only moves rigidly, in short sudden bursts (2026-10-30). Idle creaks were tried and dropped: in a still scene any lone movement grabs the eye. The selected statue wakes (lifts off the floor with dust falling, a thicker, brighter rim) and drops back with a thud when deselected. Team rims (pale blue player, ember red enemy) are 3 texels and barely show; 8 was too much; the user will decide. A warm light pool and a breathing squash were tried and dropped.
- **Effects:** painted where they sit still, code where they move. ChatGPT paints the textures (glows on black, drawn additively; burns on white, multiplied), imported at 512 px into `Art/Effects/Resources/Effects`; code animates them, adds particles and lights the floor and statues through `FxLight` (up to four lights). Floor effects draw above tiles and below anything standing. Each effect sprite gets its own material (shared ones batch with the wrong texture). Done: the sun omen, the moon shield, a lightning strike. For now effects look the same whichever side they help.
- **Banners on the roof** (2026-11-04): every banner shares one painted post and one cloth, so only the cloth's colour ramp and, later, a symbol differ (user's call). Painted from the game's camera with folds like the statues' capes. Crimson Vampiric, faded blue Third Wheel, grey Loser, green Mercenaries, rust orange Battering Ram. Held banners stand along the roof's two back edges behind your side; the cloth sways more towards the hem, each banner in its own time. Cards show the banner swaying beside its rule; the map shows a small row; hovering one on the map or roof shows its rule. Symbols parked (a black sun over fangs read as a spider; try plain bone-white fangs).
- **Still open:** placeholder visuals for many rules (the "Art needed" table in tasks), terrain, the UI/HUD look, the labels over the statues, lightning at night.

## Sound

- **Mood:** night on a tower roof above the clouds, living stone statues; magic sounds weighty and a bit ancient, not sci-fi.
- **Sources:** ElevenLabs Sound Effects is the main source (2026-10-27). Free libraries that allow commercial use (Freesound CC0 only, Kenney CC0, Sonniss GDC bundles) are the fallback, mostly for natural recordings. The free ElevenLabs plan needs an elevenlabs.io credit; a paid plan before release removes that. Raw files stay in `Desktop/sound sources/`; trimmed clips in `Assets/Audio/Resources/Sfx`, each listed with source, licence and prompt in `Assets/Audio/CREDITS.md`.
- **Workflow for a new sound:**
  0. Ask the user how it should feel (material and weight, what it must not sound like, length) and write the prompts from the answers. Guessed prompts missed badly on the first unit sounds.
  1. Write 3-4 prompts describing the moment and the mood ("warm, heavy, ancient magic, not sci-fi, no electronic sounds"), length set to the effect's timing. Generate 4 variations each (the user is signed in in the browser pane; sharing to Explore is off).
  2. Download every variation (standing OK) and cut them with `tools/make_sfx.sh`: hits start at 0 ms, a build-up ends on its loudest point; variants of one sound are cut to the same loudness (`loud`), checked in LUFS against the other sounds.
  3. Add them as numbered variants (`name_1`, `name_2`). The user auditions them in a test battle: F1, then Try plays the real effect on a unit with each variant in turn; Y keeps, N drops. Verdicts land in `Playtests/sound-review.txt`, which Claude reads; clear the lines once applied, since clip numbers change.
  4. Keep the winners (several can stay as random variants), drop the rest from the script, credit the prompts.
- **How it plays:** `Sfx.Play("name")` on the same line as the visual moment, so sound and animation stay in sync. Loops (`*_loop`) follow their object. 2D with a gentle left-right pan. A level table in `Sfx.Play` brings the effects to about -20 LUFS.
- **Volume:** master × effects, saved in PlayerPrefs. An AudioMixer comes later, with music and ambience.

## Balance

Decided with the user 2026-11-01. The balance chat owns the numbers: it changes them freely and reports afterwards, and asks before changing rules, kits or AI behaviour.

- **Win rate targets:** new player 10-20% per run, experienced player 50-60% on the base difficulty. Harder modes come later.
- **Power:** late builds may break the game in the last third of a run, but not by step 4, and no build should win without the player playing.
- **Luck:** it may decide what you face, but shouldn't kill a unit with no warning.
- **Drafts:** all 10 should be winnable in theory; win rates can differ. Draft gaps are a design question as much as a balance one.
- **Run length:** not a concern for now.
- **Simulator:** `tools/dev.sh sim` (see [tools/README.md](../tools/README.md)). The smart bot uses every action and sees the enemy's next turn and dice, but plans one round ahead and has no items, banners or omens. It wins more than a player, especially at level 0 (bot 93-99% where players won about half), so early fights are tuned from playtests and the bot is for comparing fights, not real win rates.
- **History of passes:**
  - 2026-11-01 (smart bot): Elementals and Shards less HP (hardest faction by far); Crossbowman less damage (two shots could kill a 5 HP unit on turn 1); Goblins tougher, Elite Goblins one more goblin; Break the Ritual 4 to 6 turns.
  - 2026-11-04 (5 played runs, 17 fights; 2 of 5 first fights won, both runs past the first fight won): Goblin damage back down (fling plus strike took 8 HP off a Warrior on turn 1, and a favored goblin killed an Archer, breaking the luck rule). The late game was easy as the bot predicted (a Third Wheel run rated 1).
  - 2026-11-08 (night pass, bot, 300 runs): 83% run win rate (same as 2026-11-01), drafts 77-97% (were 60-95%). Giant more HP (the easiest wall); the Necromancer is now the harder boss.

## Logs and tools

- **Logs** (`Playtests/`, git-ignored): `PlaytestLogger` writes one JSON per battle, `RunLogger` one per run. Every new rule logs what is needed to judge it. Logs are stamped with `gitCommit`, `gitDirty` and `gitDirtyCode` (a change under `Assets/Scripts`); a build writes "unknown". `tick` actions name who caused them; damage nobody caused (sun, lightning, a lost Pack Leader) is counted on the target. Durations use a stopwatch. F9 saves a marker with a board snapshot. Test battles are marked `test: true`, debug-skipped ones `skipped`.
- **Log fields by feature** (battle log unless it says run log):
  - Board: `boardSize`, each unit's `maxMoves`, `movesAfter` on moves and "done", `ranged` on hits from 2+ tiles, `throughAllies` per move, `slowed: true` on moves and hits by slowed units.
  - Techniques: uses per technique; run log: each pick's offers, choice or skip, who learned it, shop buys and forgets, levels. Warrior statuses: `shieldup`, `battlecry`, `taunt`, `throwshield`, `zoc strike` (Placeholder), `shield absorb`.
  - Tension pieces: statuses `planted` and `overchannel` (the uses); hits with `planted: true` (a Planted shot) or `overchanneled: true` (the doubled spell); the Mage's next turn shows `slowed`; explosions with note `rigged barricade`.
  - Banners: `banners`, each unit's `weight` and `mercenary`, ram damage as `tick` with note `ram`, Vampiric heals as `heal`. Run log: `banners` (source, start offers), `dismissals`, each battle's `mercenary` and `loserGains`, the shop's `bannerOffered`, each pick's `bannerOffered`.
  - Battlefields: `battlefield`, actions `fall`, `storm mark`, `favored`, explosions with note `lightning`. Run log: each battle's `battlefield`, each map choice's `chosenField` / `otherField`.
  - Credit: `damageDealt`, `kills`, `tickDamageDealt`; on the target `sunDamageTaken`, `lightningDamageTaken`, `tickDamageTaken`; `escaped` cutpurses. Run log: `runTag`, `difficultyRating`.
  - Faction combos (2026-11-09): `combos`, a list of `{name, count}` for every enemy combo that fired (`Core/ComboTally.cs`, keys like `wolves.howledBites`, zero counts left out). The simulator counts the same with `tools/dev.sh sim N combos`. The enemy Overwatch shot is logged as type `overwatch` (was `attack`).
- **Battle copy** (2026-11-01): `Battle.Clone()` is a deep copy for bots looking ahead; battles roll with `SeededRng` so the random state can be copied. New state on `Battle` or `Unit` must be added to `Clone`; `BattleCloneTests` compares every field and fails otherwise.
- **Test mode** (editor and dev builds): "Test battle" on the draft screen opens a setup: any 1–4 base units (repeats allowed) with any techniques (the level follows), any encounter (for the Necromancer, which factions count as beaten), any blessing or curse, and the battle banners (Vampiric, Third Wheel, Battering Ram). No items for now. Normal rules; afterwards it returns to the setup. No run, gold, map or run log.
- **Effects playground:** F1 in a test battle (see CLAUDE.md).

## Later / maybe

- More acts after Act 1; difficulty levels; unlocks between runs; onboarding; save and continue.
- A first choice at run start beyond the banner (a bonus technique, gold, a curse for a strong banner), then Commanders that set the draft pool, a starting banner and a rule twist.
- A branching map; more node types and events.
- Terrain features (river, bushes) for layouts; more battlefields (see "Battlefields").
- Drawn spell cards during battle; other win conditions; difficulty modifiers (for example, HP carrying over between fights).

## Identity rule

The game needs one unusual rule of its own. Current candidate: **Crumbling Roof** (pushes and falls break the tower's roof), built as a battlefield first so it can be judged in play. Record the decision here.
