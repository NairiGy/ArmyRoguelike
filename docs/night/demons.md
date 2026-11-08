# Demons (night pass, 2026-11-08, not playtested)

> Build notes from the night pass, kept as written. It was merged into `main` on 2026-11-08, so "main is unchanged" and branch names below are history. Some rules changed since (for example slowed is now "move or act, not both"); the design note and the gamepedia hold the current rules.

A fifth regular faction, built on branch `claude/demon-faction-ba3c9c` for the overnight fun pass. The art is already
painted (`Assets/Art/Experiment/Source/{imp,demon,devil,fiend}_noground__*.png`); until the art chat runs
Tools → Art → Import Statues, the four units use code-drawn placeholder figures with horns.

## The idea

The other factions each have one trick: wolves chain bites, goblins fling things, bandits hide and steal, elementals
are tough, split and throw you around. Demons are about **blood and bargains**: they spend their own HP, they get
stronger when things die around them, and they make you an offer you have to answer. Two new faction-wide ideas:

- **Fury**: a counter on a demon, +1 damage per point for the rest of the fight, 3 at most. Shown on the card ("Fury N") and as a
  dark red pip. It comes from deaths: imps feed it when they die, demons take it from any death next to them. So
  *where* you kill things matters: kill the imps away from the Demon, or fight the Demon away from the bodies.
- **Hellborn**: burning ground doesn't hurt demons (every demon has it). Their own Fire Breath leaves fire behind, and
  your Mage's Burning Ground does nothing to them.

Every new rule is shown before it happens: the intents name Hellswap, Rending Blow, Soul Contract and Fire Breath, and
a contract sits on your unit as a pip and a card line until it comes due.

## Units *(placeholder numbers)*

| Unit | HP | Damage | Range | Moves | Weight | Passive | Action (cooldown) |
|---|---|---|---|---|---|---|---|
| Imp | 5 | 2 | 1 | 2 (Swift) | small | Blood Offering, Hellborn | Hellswap (3) |
| Demon | 14 | 3 | 1 | 1 | huge | Bloodlust, Hellborn | Rending Blow (2) |
| Devil | 10 | 2 | 2 (trident) | 1 | medium | Long Reach (trident), Hellborn | Soul Contract (3) |
| Fiend | 7 | 2 | 1 | 2 (Swift) | small | Spines, Hellborn | Fire Breath (2) |

### Imp: the trickster
- **Hellswap** (cooldown 3): trades places with one of your units 2-3 tiles away, no line of sight needed. Nothing on
  either tile goes off (it's a teleport, not a walk), so no mines, caltrops or overwatch.
- **Blood Offering**: when it dies, the nearest other demon within 3 tiles gains 1 Fury.
- **AI** (new behaviour "Imp"): imps act first in the enemy turn (with howlers), so the unit they swap in meets demons
  that still have their turn. If Hellswap is ready it looks at its own tile and every tile it can walk to that is next to
  a fighting ally, and swaps with one of your units that isn't already in an enemy's reach, so your unit lands next to
  that ally. It prefers ranged units (they least want to be in melee), then the tile next to the most allies, then the
  weakest unit, then the shortest walk. Otherwise it walks up and stabs. The intent shows the target and the tile it
  will land on.
- **Why**: the 6x6 board makes the back line safe by default; the imp breaks that in a readable way. It is the faction's
  answer to archers and mages standing back, and the swap also drops a 5 HP imp into your back line, where you can kill
  it, but its death feeds the Demon if the Demon is close. Weight small: shoves throw it 2 tiles, so a Warrior's
  Shield Bash or Edge of the World deals with it easily.

### Demon: the bruiser
- **Rending Blow** (cooldown 2): loses 2 of its own HP (nothing stops that, it never kills itself: it needs more than 2
  HP), then hits a unit next to it for its damage +2 (5 at no Fury).
- **Bloodlust**: gains 1 Fury whenever a unit next to it dies, friend or foe.
- **AI**: Walk up and hit, with Rending Blow instead of a strike whenever it is ready. The intent shows "Rending Blow".
- **Why**: sacrifice made visible: every other turn it hurts itself to hit hard, so whittling it costs it HP too, and a
  long fight against it gets dangerous through Fury. Huge weight (like the Brute and the Elemental) so your pushes don't
  move it and it can't be thrown off Edge of the World; you deal with it by placement instead.

### Devil: the dealmaker
- **Trident**: its basic attack is Long Reach (the Warrior technique): 2 tiles in a straight line, hitting both, and
  it's still melee (no line of sight, can be countered).
- **Soul Contract** (cooldown 3): marks one of your units within 3 tiles (no line of sight needed). At the start of the
  next enemy turn that unit takes 3 damage (shields, Tough and other reductions count), unless the Devil takes any damage before then (from anything: an attack, a
  mine, a fireball). A Devil that dies voids its contracts too.
- **AI** (new behaviour "Devil"): backs off from anyone next to it (comfort distance 2, like the Necromancer), signs a
  contract when it is ready (on a unit the contract would kill first, then the hardest hitter, then the weakest), else
  strikes with the trident, else walks to a tile from which it can strike or sign one.
- **Why**: the brief asked for a "deal". A pop-up choice would need new screens and a bot answer; this keeps the deal
  on the board: *pay 3 HP, or spend this turn getting to the Devil*. It pulls your army forward into the Demon and
  rewards ranged units, which the imps then go after. Medium weight: pushes move it 1 tile, so Edge of the World can
  kill it.

### Fiend: the firestarter
- **Fire Breath** (cooldown 2): at one of your units 1-2 tiles away in a straight line (a rock stops it): 2 damage
  (plus Fury) to each of your units on the 2 tiles of the line, and both tiles burn for 2 rounds (1 damage at the start
  of each of your turns, as Burning Ground). Demons are Hellborn, so the fire only ever hurts your side.
- **Spines**: a melee attacker takes 1 damage (the Thorns rule).
- **AI**: Walk up and hit, but Fire Breath beats a strike when it's ready: from its tile or one it can walk to, at
  whichever of your units puts the most of them in the line. The intent shows the target and the tile it breathes from.
- **Why**: punishes standing in a line (a Warrior with an Archer behind it is the natural shape) and leaves ground you
  don't want to stand on. Spines make melee units pay a little for killing it, so ranged units are the clean answer,
  which again plays into the imps. Small weight like the wolves and goblins.

## Encounter

- **Demons** ("Hellmouth"): a mirrored pair of rocks (rows 2-3); the Demon on the enemies' front row (x 1-4) with an
  imp on each side, the Devil behind the Demon on the back row, the Fiend on the free back-rows tile furthest from the
  Demon. 5 units, 41 HP.
- **Elite Demons**: the same pair of rocks; two Demons on the front row 2 apart with an imp between them and one beside
  them, the Devil behind the gap, the Fiend in a back corner. 6 units, 55 HP.
- **Pool 2** (like the Elementals): never in step 1, a regular battle from step 2 on. The Run needed no change: it
  draws from `Encounter.Regular`, which now has 5 factions, so rematches start after 5 beaten factions instead of 4
  (with at most 7 fights before the boss, a run sees most factions once).
- **Battlefield**: like any battle, from the run's battlefield roll; nothing special.
- **Necromancer**: raises a **Bone Imp** (all its passives and Hellswap; Blood Offering only reaches demons, so it
  usually feeds nobody). Note for the lead: with 5 factions the Necromancer can now field 5 raised minions instead of
  at most 4 (he raises one per beaten faction and tops up with skeletons to 4). Capping it is a boss decision, left
  to the lead.

## Battlefields and other rules

- **Edge of the World**: imps and fiends (small) fly 2 tiles from a push, so they fall easily; the Demon (huge) never
  moves; the Devil (medium) can be pushed off. Hellswap is not a push and never throws anyone off.
- **Storm**: imps step off marks like every enemy; Hellswap can drop your unit onto a storm-marked tile only if the imp
  stood on one, and enemies never stop on marks, so in practice it doesn't.
- **Arcane Overflow**: Rending Blow and Fire Breath every turn, Hellswap and Soul Contract every other turn. This is
  the battlefield to watch in playtests.
- **Fortune's Favored**: doubles Fire Breath and the trident; a contract is a tick, so it isn't doubled.
- **Zone of control**: Hellswap ignores it (teleport). Swapped-in units start next to a demon, so leaving costs their
  remaining moves, as usual.

## Logs

Battle logs need no new fields: Hellswap is an action `hellswap` (unit, target, from = the imp's tile where your unit
lands, to = your unit's old tile); Fury gains are `fury` effects; a signed contract is a `status` with note
`contract` (amount 3), a broken one `contractbroken`, a paid one a `tick` with note `contract` credited to the Devil;
Rending Blow is a `tick` "blood price" on the Demon followed by its hit; Fire Breath is an area attack `fire breath`
plus `tile added` burn entries.

## Sim numbers

`tools/dev.sh sim 10 fights` (look-ahead bot, every draft, random techniques per level; win rate, timeouts, average
turns). Compare rows with each other, not as real win rates; the bot is far stronger than a player at level 0.

| Fight | L0 | L1 | L2 | L3 |
|---|---|---|---|---|
| Elementals (reference) | 21% (11.3t) | 52% (11.8t) | 74% (9.5t) | 94% (7.5t) |
| Demons, first numbers (Imp 4, Demon 12, Devil 9, Fiend 6 HP) | 23% (8.2t) | 79% (6.8t) | 97% (5.3t) | 100% (3.7t) |
| **Demons, now** (Imp 5, Demon 14, Devil 10, Fiend 7 HP) | 7% (6.7t) | 61% (7.2t) | 98% (5.5t) | 100% (3.9t) |
| Elite Elementals (reference) | 0% (8.2t) | 22% (11.2t) | 39% (11.8t) | 75% (11.9t) |
| Elite Demons, first (7 units, 2 Fiends) | 1% | 12% | 60% | 89% |
| Elite Demons, still 2 Fiends after the HP change | 0% | 3% | 45% | 79% |
| **Elite Demons, now** (1 Fiend) | 0% (5.2t) | 16% (7.0t) | 65% (7.7t) | 90% (6.3t) |

Read: at level 1 the Demons are close to the Elementals (61% vs 52%), but the curve is steeper: harder at level 0,
much easier from level 2. Fights are short (about 7 turns against 11): the demons hit hard (Rending Blow 5, Fire Breath
on two units) and die fast. That suits a pool-2 fight (never step 1) but is worth a playtest look. If it's too swingy, the
next knob is Rending Blow's bonus (+2 to +1), not more HP.

## Art needed (for the art chat; rows to copy into the "Art needed for gameplay features" table)

| Feature | What needs art | Placeholder now |
|---|---|---|
| Demon statues | Run Tools → Art → Import Statues: the four source images exist, heights are in `ArtExperiment.FigureHeights` (Imp 0.7, Demon 1.6, Devil 1.3, Fiend 0.8) | Code-drawn figures with horns (`PlaceholderArt.MakeFigure`): wings on the imp, shoulders on the demon, a trident on the devil, spines on the fiend |
| Hellswap | A swap effect: both statues vanish in smoke/fire and reappear on each other's tile | Popups "Hellswap!" / "Swapped!", both statues jump |
| Rending Blow | A heavier hit, maybe blood on the demon | The normal lunge, a "-2 blood price" popup on the demon |
| Soul Contract | A mark on your unit (a burning sigil?) while the contract stands, and a "paid" effect | A black-red status pip, popups "Contract: -3 next turn" / "Contract broken" / "-3 contract" |
| Fire Breath | A breath of fire along the 2 tiles | The area flash of a volley plus the existing burning-tile look |
| Fury | A pip or glow that grows with Fury | A dark red status pip and a "+1 Fury" popup |
| Intent icons | Icons for Hellswap, Rending Blow, Soul Contract, Fire Breath | Reused: push, sword, eye, burst |

## Review fixes (2026-11-08, after the merge into `feature/night-fun`)

A reviewer went through the faction after the lead merged it. What changed:

1. **Devil intent**: the hover intent now plays the Devil's coming turn on a copy of the battle (`EnemyAI.PredictContract`), so it shows a contract it will sign after backing off or walking up. Before, it only checked from the tile it stood on.
2. **Falling Devils**: a Devil that falls off Edge of the World or through a Crumbling Roof hole now voids its contracts. `DemonDeath` (called from `Damage`, `Fall` and `DropIntoHole`) calls `BreakContracts`. Blood Offering and Bloodlust now work on falls through holes too.
3. **Dead units**: a unit's contract is cleared when it dies, and "Contract broken" only plays for living units.
4. **Fire Breath intent**: shows the real damage (2 + Fury, doubled when Favored), on the hover line and on the badge ("Fire Breath 4"). It lights up the whole line and, if the fiend walks first, the tile it breathes from.
5. **Rending Blow intent**: shows the total (its damage against that target + 2, Fury and Favored included), on the hover line and the badge. **Fury is capped at 3** (`Battle.MaxFury`); a gain past the cap shows nothing. The worst case, a Favored Rending Blow at Fury 3, is 16 and can still kill a Warrior. That is now always shown beforehand as a number on the badge.
6. **Hellswap combo warning**: the hover line names the units next to the landing tile ("...lands on the dark tile next to the Demon, which acts after the imps and can hit it this turn").
7. **Interrupted walk**: an imp whose walk to its swap tile is cut short replans from where it stopped and swaps only if that tile is still a good one.
8. **Slippery**: ChooseHellswap checks "already next to an enemy" by adjacency, not by zone of control (which ignores Slippery units).
9. **Soul Contract wording**: it is **3 damage** (a tick: shields, Tough, Shield Wall and the like reduce it; it is never doubled by Favored). All texts now say "takes 3 damage" instead of "loses 3 HP".
10. **Logs**: attack entries have the `type` of their attack: `rending blow` for a Rending Blow (`AttackEvent.Kind`), `attack` otherwise. Every hit entry records the attacker's `fury`.

Also: imp and fiend hover intents now see the walk-ups their turn will really have (`AsIfFreshTurn` clears last turn's "has acted" flags). The elite's HP sum above was wrong (48); it is 55. The sim numbers above are from before these fixes. The only rule change is the Fury cap, which rarely comes up in those fights.
