# Night log, 2026-11-08 (branch `feature/night-fun`)

The user asked for the game to be made more fun and interesting overnight, with Claude choosing what to change
(core rules included, on this branch only), placeholder visuals, and as much as possible. Helpers: builder sessions
in their own worktrees, reviewer subagents (player, exploit hunter, code), game research, bot sims.

Each entry: what was done or tried, and why. Scrapped and reverted ideas stay in the log.

## Timeline

- **02:10** Read the design note, tasks and run logs. Known problems going in: late game has no tension (a run
  rated 1, Giant dead in 3 turns), only the Warrior has a redesigned kit, no identity hook yet, many systems with
  no onboarding. Created branch `feature/night-fun` from `main` (ad60a93). Set up an hourly watchdog task
  (`night-fun-resume`) that nudges this session to continue if a usage limit stops it.
- **02:15** Ran a research subagent on how similar games handle our problems (summary in `docs/night/research.md`).
  Its top picks: boss phases, cracks (lasting harm from falling), one owned keyword per unit line, events that cost something,
  a crumbling-roof identity rule. Baseline: compile OK, 237 tests pass.
- **02:20** Started three builder sessions, each in its own worktree and branch: Archer + Rogue pools, Mage + Sapper
  pools, and a Demon faction from the already-painted art. Each one writes its design to `docs/night/<name>.md`. Why parallel: they
  touch mostly separate code, and the lead (this session) does run structure meanwhile. Sent the Archer/Rogue builder the
  "owned keyword" idea after the research came back (Archer: Marked, Rogue: Exposed).
- **02:40** **Cracks** (lead): a statue that falls in a fight you win (or a lost scenario) gets a crack, -1 max HP until
  mended; the 3rd crack shatters it and it leaves the army with its techniques. Shops mend a crack for 6 gold.
  Why: the bot and the playtests both say a run is decided by step 4, partly because units heal fully and death only
  costs 5 gold. Cracks make a costly win matter for the rest of the run (Into the Breach's grid, Darkest Dungeon's
  injuries) without dropping the full heal between fights.
- **02:40** **Four new ? events with real trade-offs**: Stonemason (mends every crack, or +1 max HP), Cracked Altar (a
  rare technique for a crack), Sculptor (give up a technique you choose for a random rare one of the line), Hooded
  Stranger (30 gold for a curse). Why: the old events were mostly free gifts or a coin flip; Slay the Spire's best events are clear
  trades. Run sim and test helpers handle them. 8 new tests (`NightRunTests`).
- **03:10** **Champions** (lead): from step 6, every battle and elite brings a champion. Its toughest enemy gets +3 max HP and
  one affix (Armored, Vengeful, Regenerating, Mighty, Fleet), shown on the map node, and winning pays +5 gold. Why: the late fights
  were rematches of factions already beaten, so nothing new asked anything of the build. Slay the Spire's later acts and
  elite modifiers do this job there. Picked by its own random source, so seeds keep their maps.
- **03:10** **Stone Heart boss phases** (lead): bosses fall in 3 phases, and damage past the next phase is lost until your next
  turn, so a boss takes at least 3 rounds. Each phase changes the fight: Giant phase 2 gets Wrath (it used to come at half HP), and phase 3 drops
  rocks on 2 of your units' tiles every round (marked a turn ahead, step off); the Necromancer raises 2 skeletons on the roof's
  edge at each new phase. Bosses also get +1 max HP per army level. Why: a playtest killed the Giant in 3 turns for 1
  damage taken, and the bot won 100% of boss fights. Tests that set boss HP directly were updated.
- **03:25** Run sim after cracks, champions and phases (200 runs): 83% before, **80%** now. Sapper drafts fell further (60-75%),
  and the boss and elite fights are still won about 100% of the time. So these changes add tension through cracks and early
  losses, but the bot still wins late fights. More pressure is needed on bosses; checking fight lengths next.
- **03:30** **Crumbling Roof battlefield** (lead): collisions and deaths crack the tile under the unit; a second crack opens a
  hole, and a unit standing there or pushed in falls and dies. Holes block walking, not shots or sight, and give no cover.
  Enemies push into holes the way they push off the edge. Why: the research's favourite "identity" rule for statues on a tower.
  It ties pushes, weight and the Edge of the World together, and the board changes during the fight. Built as a battlefield
  first (a 6th one), so it can be judged in play before it becomes a rule everywhere. One slip on the way: a perl
  substitution put text at the top of `Battle.cs` (`\|` inside a `|`-delimited pattern); caught at once and repaired before compiling.
- **03:40** Usage limit reached. Pausing here; the hourly watchdog resumes this session after the reset. State: lead work is
  committed on `feature/night-fun` (cracks, events, champions, Stone Heart, Crumbling Roof), compile OK, 259 tests pass. Not
  started yet: **Hone** (a shop service: -1 cooldown on one action for the run). Next when resumed: read the boss/elite sim
  (`tools/dev.sh sim 10 fights "Giant,Necromancer,Elite Elementals,Elite Wolf Pack"`) and give bosses more teeth if they are
  still won about 100% of the time; check the three builder branches (archer-rogue, mage-sapper, demons), review them with reviewer
  subagents, and merge them into `feature/night-fun`; then docs (design note, tasks, CLAUDE.md, gamepedia) and the visual timeline page.
- **09:23** Resumed after the usage limit. Boss sim at fixed levels (the bot): Giant 15 / 54 / 80 / 96% at levels 0-3, Necromancer
  0 / 4 / 17 / 42%. So bosses aren't trivial at a fixed level; whole runs win them because the army reaches them at about
  level 3 with 4 units.
- **09:23** The three builders finished: **Archer and Rogue** 13 techniques each (Archer owns *Marked*, Rogue owns *Exposed*;
  the Rogue has 6 HP and a Vanish that lasts the enemy turn), **Mage** 13 and **Sapper** 12 (Mage owns *Slowed*, Sapper owns
  *Rubble*: low heaps that block walking but not shots, and crush what's pushed into them), and the **Demons** (Imp, Demon, Devil,
  Fiend: Fury, Hellswap, Rending Blow, Soul Contract, Fire Breath; pool 2, with an elite). Their designs are in `docs/night/`.
  59 techniques in all, up from 34.
- **09:23** Four reviewer subagents in parallel: one code reviewer per branch and one player/exploit reviewer. Then merged the three
  branches into `feature/night-fun`, resolving the conflicts by hand (most were both sides adding enum values or cases). One real
  overlap: holes and rubble are both "obstacles that don't block sight", so `Board.IsRock` now means "blocks sight and gives cover".
  Tests: 282, then 303, then 322 after each merge.
- **09:23** Fixes from the player/exploit review (lead's systems): **bosses never fall** (one push could kill the Necromancer and
  skip all of Stone Heart); a boss that enters a new phase is **Enraged**, one extra action on its next turn, so a phase
  threatens and doesn't just soak damage (the reviewer: "being robbed for playing well"); the **Sculptor** gives no level (it
  was a swap plus a free level); **Altar cracks are scars** that can't be mended; **mending costs 10** (was 6); Altar and
  Sculptor only from step 5 (rares before step 4 broke the balance rule). Floor cracks are now called **fractures**, so "crack" means one
  thing. A test helper that teleported its attacker onto the goblins' rock ridge was fixed.
- **09:23** **Hone** (lead): a shop service, 12 gold, takes 1 turn off one action's cooldown for that unit for the run. Why:
  cheap depth that works with every action, old and new, and gives gold a use late in the act.
- **09:23** Sent each builder its code review to fix on its own branch, synced with the night branch: Archer/Rogue (Into the
  Shadows and Smoke Bomb near-permanent hiding, pins and slows lasting a turn too long, a +4 cap on stacking flat bonuses, a Rogue
  could reach 17-20 damage), Mage/Sapper (rubble walls melee enemies in for good because the AI can't path through it,
  Pierce stopping at rubble, raising inside rubble), Demons (intents that miss the Devil's walk-up contract and the Fire
  Breath line, an uncapped Fury that one-shots with Fortune's Favored).
- **Flag for the user:** the Loser banner with cracks. A death gives +1 HP and +1 damage, the crack takes 1 HP back and costs 10 to
  mend. Dying is now nearly free growth again. Left as is (the user chose an uncapped Loser); decide in the morning.
- **09:41** **Builders' review fixes merged** (Archer/Rogue d883a8c, Mage/Sapper e9ec7ea, Demons f549caa). One clash: both had added an
  action tag to attack events; kept one (`Via`). 343 tests pass.
- **09:41** **Run sim was broken since the Warrior redesign** (Nov 5): with a Vanguard Warrior the bot put other units on the Warrior's
  extra deploy row, so every layout failed ("no deployment"). Fixed in the bot (each unit only on tiles it may use).
- **09:41** **Run sim, night branch, 300 runs:** **83%** (Nov 1 baseline 83%, after the lead's first changes 80%). Drafts now **77-97%**
  (were 60-95%; Sapper drafts gained most). Necromancer 95% with 6 run-ending losses, Giant 99%, elites 90-100%, Demons 97% at
  step 5 (Elementals 92%). The bot sees the enemy's next turn, so players will win less.
- **09:41** **Giant 36 → 42 HP** (balance): it was the easiest wall. Fixed-level bot after: 9 / 50 / 79 / 93% at levels 0-3
  (before: 15 / 54 / 80 / 96). Necromancer at fixed levels: 0 / 0 / 5 / 17%, so the Necromancer is now the hard boss.
- **09:41** **Illustrated gamepedia re-exported** through Unity in batch mode, with cracks, champions, Hone and the new events added to the writer.
  The demon statues aren't imported yet (an art chat task), so demons show code placeholders.
