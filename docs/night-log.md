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
