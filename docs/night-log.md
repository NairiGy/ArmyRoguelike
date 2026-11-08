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
