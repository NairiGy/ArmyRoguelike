# Archer and Rogue redesign (night of 2026-11-08, not playtested)

Built on branch `claude/archer-rogue-pools-721626` while the user slept, following the Warrior redesign (commit `ad60a93`). Goals from the lead: each line ends with 10-12 offered techniques (far more than a run learns), a mix of passives and actions, about 2/3 common, a few `Requires` chains, and skills that pay off on statuses other lines also create (pinned, slowed, poisoned, burning, pushed, hidden). Every number, cooldown and rarity below is Claude's placeholder; nothing was played by a human.

For the lead to merge: the design-note section, the `docs/tasks.md` rows (art table below), CLAUDE.md's status line and the gamepedia re-export are left to you. `TechniqueTests.Catalog` now expects 49 techniques in `Technique.All`; the other pool sessions change that number too.

## Base kits

### Archer (unchanged)

6 HP, Shot 2 at range 3, Steady Aim (+1 if it hasn't moved), Pinning Shot (1 damage + pin, cooldown 2). It already had an identity (stand still, shoot, pin); what it lacked was choices. The kit stays and the pool around it grows.

- **Growth:** the second +1 damage moves from level 5 to level 6 (like the Mage), since the comment that set it at 5 said "the Archer has only 5 techniques". With 12 it would otherwise reach +2 too easily.

### Rogue (changed)

The problem: at 5 HP on a 1-move board, the Rogue walks in, hits and then eats the whole enemy turn. Vanish didn't help, because attacking revealed it, so it hid only while it did nothing.

- **HP 5 to 6.** One more hit survived against 2-damage wolves and goblins; still the most fragile line with the Mage.
- **Vanish reworked** (still free, before acting, cooldown 3): hidden **until your next turn** (one enemy turn, was "up to 2, until it attacks"). **Attacking doesn't reveal it.** Its first attack while hidden still deals +2 (the "ambush"; `Unit.AmbushUsed` tracks it). So the Rogue's turn is: vanish, walk 2, stab for +2, and the enemies can't answer that turn. With cooldown 3 that's one safe dive every third turn, about twice a fight; on the other turns it has to pick its spots.
- Why not a new base passive: Dodge as a base was tempting but it fixes only one hit, and it's a good rare already. Making Vanish do its job is the smallest change that fixes "dies first", and it's what the Rogue is about.
- Kept: Swift, small weight, Strike 4, growth +1 HP per level and +1 damage at levels 2, 4 and 6.
- The rule change is general: any hidden unit (Smoke Bomb, Into the Shadows) stays hidden when it attacks, and gets the +2 once.

## Archer pool (13: 7 common, 6 rare)

| Technique | Kind | Rarity | Rule (numbers) | Why / combos |
|---|---|---|---|---|
| Steady Aim+ | passive | common | +2 instead of +1 if it hasn't moved (kept) | The stand-still archer. Covering Fire uses its damage. |
| **Long Shot** | passive | common | +1 damage to targets 3+ tiles away | Positioning: rewards keeping distance, opposite of Kick. |
| **Vantage** | passive | common | next to a rock, ice or barricade: +1 range and shots ignore Behind Cover | Positioning on terrain. Combos with the Sapper's Barricade and the Mage's Ice Wall (build your own perch), and answers the bandit Crossbowman. |
| **Kick** | action, 1 AP, cd 2 | common | 1 damage (+ its damage bonuses, so 2 with Steady Aim) to an adjacent enemy and push it 1 tile (2 for small, 0 for huge); collision 2 if blocked | Self-defence for a fragile shooter: shove the wolf off and shoot next turn. Collisions, Battering Ram, Edge of the World, pushing onto mines and snares. |
| **Snare Trap** | action, 1 AP, cd 2 | common | hide a snare on a free tile within 3 (max 2); the first enemy to walk onto it stops, takes 1 and is pinned for its next turn. Enemies don't see snares | Board control before the enemy moves. Pinned = Prey (+2 for everyone), Cheap Shot (+2 Rogue), Fire Arrow (it stays on the fire). Next to the Sapper's mines it funnels. |
| **Fire Arrow** | action, 1 AP, cd 3 | common | a normal shot (attack damage, all bonuses), and the ground under the target burns for 2 rounds (1 at the start of each turn) | Shares the Mage's burning ground. Pin it first (Pinning Shot, Snare, Suppressing Fire) and it can't walk off the fire. |
| Pierce | passive | rare | shots along a row or column hit every enemy in line (kept) | Line play; root of Suppressing Fire. |
| Opening Shot | passive | rare | double damage the first time it hits each enemy (kept) | Burst. |
| Prey | passive | rare | pinned enemies take +2 from everyone (kept) | The army-wide payoff for every pin source (Pinning Shot, Snare, Suppressing Fire). Only your side's attacks get it. |
| **Covering Fire** | action, 1 AP, cd 2 | rare | aim instead of shooting: during the enemy turn, the first enemy that walks onto a tile it can shoot at is shot (its attack damage; Steady Aim counts if it didn't move that turn) and stops there. Ends at your next turn | The player's version of the Crossbowman's Overwatch. Stops a wolf rush short; the stopped enemy is out of position for the Rogue. |
| **Suppressing Fire** | action, 1 AP, cd 3 | rare, needs Pierce | shoot a row or column up to its range, stopped by rocks: every enemy on it takes 1 and is pinned | Lock a whole lane. With Prey every pinned enemy takes +2 from the army that turn. |
| **Mark Target** | action, free before acting, cd 2 | common | mark an enemy in shot range: the next hit on it from your side, of any kind except poison and burning ticks (attack, blast, collision, mine), deals +2, then the mark is gone | The Archer's owned keyword, **Marked**: the archer sets up, anyone cashes in (Warrior strike, Sapper mine, Mage fireball, a Kick into a rock). Free so it doesn't cost the archer its shot. |
| **Death Mark** | passive | rare, needs Mark Target | its marks spread: when a marked enemy dies, every enemy next to it is marked (and those marks spread too) | The build-defining rule-breaker: one mark can chain through a packed enemy line. |

Parked (rules kept, not in `Technique.All`):
- **Volley**: it replaced every shot with a friendly-fire plus area for 1 less damage; on a 6x6 board your melee units stand next to the targets, so it mostly hurt you, and it fought with Pierce. Fire Arrow and Suppressing Fire cover "area" more cleanly.
- **Spiked Snares** (passive, needs Snare Trap: snares deal 3 instead of 1): built and tested, parked to make room for Mark Target and Death Mark; the weakest chain card.

## Rogue pool (13: 8 common, 5 rare)

| Technique | Kind | Rarity | Rule (numbers) | Why / combos |
|---|---|---|---|---|
| Pincer | passive | common | +3 if an ally stands on the far side of the target (kept) | Positioning with the Warrior. Shadowstep sets it up. |
| Riposte | passive | common | counters the first melee hit each enemy turn (kept) | Duelist builds with Dodge. |
| Poison | passive | common (was rare) | hits poison: 2 at the start of the target's next 2 turns | Now common because it is the root of Rupture and shared with Throwing Knife. |
| **Cheap Shot** | passive | common | +2 against enemies that can't move (pinned or slowed) | The payoff for other lines' control: Archer pins and snares, Mage Chill. |
| **Elusive** | passive | common | ignores zone of control (like goblins' Slippery) | Swift + Elusive: through the front line to the bomber or crossbowman. |
| **Throwing Knife** | action, 1 AP, cd 2 | common | 2 damage (+ its level damage and bonuses) to an enemy 2-3 tiles away with line of sight; carries Poison, Cheap Shot and the ambush | A safe turn when diving would kill it; poisons from range. |
| **Isolate** | passive | common | +2 against **exposed** enemies (no unit of their own side next to them) | The Rogue's owned keyword, **Exposed**: picks off stragglers. Pushes create it (Kick, Shield Bash, Charge, Concussive), and kills next to a target expose it. |
| **Shadowstep** | action, free before acting, cd 3 | common | teleport to a free tile next to an enemy within 3, ignoring rocks and zone of control; only before it has walked, it can't walk afterwards, but it can still attack | A dive tool with a limit: not after walking and no walking after, so it never stacks with Swift for 5 tiles of reach. Sets up Pincer and flanks. |
| Momentum | passive | rare | a kill gives back the action and 1 move (kept) | Chains with Into the Shadows. |
| Dodge | passive | rare | first hit each enemy turn deals 0 (kept) | Survival. |
| **Smoke Bomb** | action, 1 AP, cd 3 | rare | smoke a tile within 3 and the 4 next to it: every ally there (itself too) is hidden until your next turn, like Vanish, with the +2 on their first attack | Team Vanish: hide the archer and mage after they shoot (they act first, then the Rogue smokes them), or hide a wounded Warrior. Combos with Covering Fire (a hidden archer still fires, with +2). |
| **Rupture** | action, 1 AP, cd 2 | rare, needs Poison | strike a poisoned adjacent enemy: its poison bursts first (2 per turn left, so 4 for a fresh poison), then a normal strike, which poisons again | The poison payoff: hit, then rupture next turn for about 8 on a level-0 Rogue. The burst is tick damage, so it doesn't trigger Momentum; the strike does. |
| **Into the Shadows** | passive | rare | a kill hides it until your next turn (no +2 from it) | The "kill and disappear" assassin. With Momentum: kill, hide, attack again. |

Parked: **Riposte+** (rule kept, not offered). A second Riposte with a requirement was the least interesting card and the Rogue shouldn't want to be hit; Riposte alone stays for the duelist.

## Owned keywords (for the lead to line up across lines)

- **Archer: Marked.** Set by Mark Target (and spread by Death Mark). `Unit.Marked`; the bonus is `Battle.MarkBonus` (+2), applied in `Battle.Damage` to the next hostile non-tick hit and then cleared, so any line's skill can read or create it. Hover and status show "marked"; log effect `mark`, status `mark`.
- **Rogue: Exposed.** Not a stored status but a board state: `Battle.IsExposed(unit)` = no living unit of its own side next to it. Read by Isolate (+2, `Battle.IsolateBonus`). Other lines create it by pushing or killing neighbours; another line's skill can read `IsExposed` too.
- **Pinned** stays shared (Archer's Pinning Shot, Snare Trap, Suppressing Fire; skeletons' Grasp on your side): Prey and Cheap Shot read it.
- **Hidden** is the Rogue's second state (Vanish, Smoke Bomb, Into the Shadows), with the +2 ambush on the first attack.

## Cross-line synergy map

- **Pinned** (Archer: Pinning Shot, Snare Trap, Suppressing Fire): Prey (+2 everyone), Cheap Shot (Rogue +2), Fire Arrow (stays on the fire).
- **Slowed** (Mage Chill): Cheap Shot, Shatter.
- **Poisoned** (Rogue Poison, Throwing Knife): Rupture.
- **Burning** (Mage Burning Ground, Archer Fire Arrow): pins keep enemies on it.
- **Pushed** (Kick, Shield Bash, Charge, Concussive): collisions, Battering Ram, Edge of the World, onto mines (snares trigger on walking only).
- **Hidden** (Vanish, Smoke Bomb, Into the Shadows): +2 ambush on the first attack; enemies ignore hidden units.
- **Obstacles** (Barricade, Ice Wall, rocks): Vantage.

## Rule details and edge cases

- Snares trigger only when an enemy **walks** onto one (not when pushed), like caltrops for your side. A Colossus takes the damage but isn't pinned. They count as a hazard (no mine on a snare tile). The AI can't see them, like mines.
- Covering Fire reuses the Crossbowman's overwatch table (`_overwatch`), checked for enemy movers in `DoMove`. `Battle.OverwatchTiles` now lists only enemy watchers; `Battle.CoverTiles` lists yours. Enemies don't avoid covered tiles yet (as they don't avoid mines). A 2x2 enemy triggers it (and snares) with any of its tiles.
- Vantage counts any obstacle next to the archer, its own Barricade or a Mage's Ice Wall included. It changes the attack range through `Battle.RangeFrom`, which `CanHitFrom`, Pierce's line, Covering Fire's sight and Suppressing Fire use.
- Hidden units are untargetable for the whole enemy turn even after attacking; area blasts (bombs, Slam, Corpse Blast, lightning) still hit them, as before.
- Kick, Fire Arrow, Throwing Knife and Rupture's strike count as attacks: Poison, Chill, Vampiric, Vampire Fang, Momentum and Into the Shadows all work with them. Their kill previews are in `ActionKills`.

## Logs

- Technique uses come from `TimesUsed` as before (every new action is counted).
- Marked: `status` with note `mark` when set or spread, and an effect `mark` when a hit uses it (amount = the bonus).
- New log lines without new fields: `status` with note `pin` from snares and Suppressing Fire, `status vanish` from Smoke Bomb and Into the Shadows; `tick` with note `snare` (credited to the archer) and `rupture` (credited to the rogue); `tile added`/`tile removed` for `snare` and `smoke`; `overwatch` for Covering Fire set-up and a new effect `covering fire` before each Covering Fire shot.
- Not logged: how much of a hit came from Cheap Shot, Long Shot, Vantage range or the ambush. If the playtest needs it, add a `bonus` note to `HitEntry`.

## Simulator (smart bot, 10 seeds per draft, levels 0-3 with random techniques)

`tools/dev.sh sim 10 fights "Wolf Pack,Bandit Camp,Elementals,Giant"`, with a new `SIM_DRAFTS="A,R"` filter (drafts with an Archer or a Rogue: 9 of the 10). "Before" is Core at `86093b3` (via `SIM_CORE`). Win rate, timeouts and average turns.

| Fight (A or R drafts) | L0 before → after | L1 | L2 | L3 |
|---|---|---|---|---|
| Wolf Pack | 89% → 94% | 100% → 100% | 100% → 100% | 100% → 100% |
| Bandit Camp | 57% (14 timeouts) → 51% (17) | 91% → 94% | 99% → 99% | 100% → 100% |
| Elementals | 22% → 27% | 54% → 54% | 77% → 80% | 97% → 90% |
| Giant | 53% → 54% | 81% → 81% | 93% → 90% | 97% → 98% |

All 10 drafts after: Wolf Pack 94 / 100 / 100 / 100, Bandit Camp 52 / 93 / 99 / 100, Elementals 25 / 52 / 77 / 88, Giant 55 / 79 / 90 / 98. Each run took about a minute.

The changes are within noise at 10 seeds (about ±5 points), except the Rogue's base change showing at level 0 (Wolf Pack +5, Elementals +5: 6 HP and a Vanish that protects). Higher levels didn't jump: the bot learns random techniques and uses actions only one round ahead, so it under-uses set-up skills (Mark Target, Snare Trap, Covering Fire, Smoke Bomb). Fights got slightly longer (Elementals L3 6.4 → 8.8 turns, more timeouts in Bandit Camp), likely the bot spending turns on low-value actions. Not a reason to change numbers yet; check against a playtest.

Reading: the bot picks techniques at random and plans one round ahead, so this compares before and after rather than predicting real win rates.

## Art needed (rows for the "Art needed for gameplay features" table)

| Feature | What needs art | Now (placeholder) |
|---|---|---|
| Snare Trap | a snare lying on the tile (only your side sees it), a snap when it catches | green tile highlight; "-1 snare" popup and "Pinned" |
| Covering Fire | the archer aiming, the covered tiles, the shot | pale blue tile highlight; "Overwatch!" and "Covering fire!" popups; the normal attack lunge |
| Fire Arrow | a burning arrow in flight | the normal shot lunge, then the existing burning-ground tiles |
| Suppressing Fire | arrows raining along the line | the line flashes like Pierce; "Pinned" popups |
| Kick | a kick | the normal attack lunge, then the push slide |
| Smoke Bomb | a smoke cloud on the plus area | pale blue preview while aiming; "Vanished" popups and the see-through statue |
| Shadowstep | a shadowy blink | a one-tile move (instant slide) |
| Throwing Knife | a knife in flight | the normal attack lunge from range |
| Rupture | the poison bursting | "-N rupture" popup |
| Mark Target / Death Mark | a mark over the enemy, and the mark jumping to its neighbours | "Marked" popup and a "marked" line on the card; "Mark +2" popup when a hit uses it |
| Into the Shadows | fade after the kill | "Vanished" popup and the see-through statue |
| Vanish (rework) | the hidden statue now stays hidden while it attacks | see-through statue (unchanged) |

## Not done / open

- No human playtest; numbers are placeholders. Watch in the logs: Vanish uptime (the Rogue may become hard to kill with Smoke Bomb + Into the Shadows + Dodge), how often Covering Fire fires, and whether Snare Trap is placed where enemies walk.
- The enemy AI doesn't account for Covering Fire tiles or the new Vanish (it just sees fewer targets). Intent lines don't predict an enemy being stopped by a snare or a covering shot.
- The gamepedia isn't re-exported (the lead does it); `GamepediaWriter`'s rules list mentions the new hidden rule, snares and Covering Fire.

## Review fixes (after the merge into `feature/night-fun`)

Reviewers' findings, fixed on this branch after merging `feature/night-fun` back in:

1. **Into the Shadows** hides the Rogue at most once every 3 turns (`Battle.IntoShadowsCooldown`, `Unit.ShadowsTurn`). Before, a kill every turn kept it untargetable for good.
2. **Smoke Bomb** is aimed at one ally within 3 (or itself) and hides only the Rogue and that ally, with no ambush +2 for either. Cooldown 4 (was 3, plus area, ambush for everyone).
3. **Pins and slows stop counting when the held unit's turn ends.** `RootedThisTurn` and `SlowedThisTurn` used to stay set through your next turn, so Prey, Cheap Shot and Shatter paid out on two of your turns. Now `BeginTurn` clears them for the side whose turn just ended. An enemy is "pinned" during your turn only if it will be held on its coming turn (`SnareTurns`/`SlowNext`), which is also what the hover card shows. `KitTests.Chill_...` was updated to match.
4. **Conditional flat bonuses are capped at +4 per hit** (`Battle.MaxConditionalBonus`, summed in `Battle.ConditionalBonus`): Pincer, Prey, Cheap Shot, Long Shot, Isolate, Shatter and the hidden ambush. Wolf Bite and the Mark are outside the cap.
5. **Rupture** has a kill preview in `ActionKills` (the burst alone, or burst plus strike).
6. **Death Mark** spreads whenever a marked enemy dies, whatever killed it (ticks and friendly fire too). A fall off the edge doesn't go through `Damage`, so it doesn't spread.
7. **Action damage order:** `ActionDamage` puts the action's base and the unit's own bonuses first, then the target bonuses, then Opening Shot's double (`DamageFrom`). A Kick with Opening Shot now doubles the kick (4 with Steady Aim) instead of the attack (5). This also applies to the Warrior's Shield Bash and Charge, which share `ActionDamage`.
8. **Blink and Shadowstep are blocked while pinned or slowed** (`Battle.IsHeld`), like walking.
9. **Logs:** `AttackEvent.Via` names the action behind a hit, and battle logs write it as the action's `type` (`kick`, `fire arrow`, `throwing knife`, `rupture`, `covering fire`) instead of `attack`. The summaries count them as damage like attacks.

New tests in `ArcherRogueKitTests` cover each of these. Not re-simulated after the fixes.
