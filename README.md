# ArmyRoguelike *(working title, in development)*

A single-player roguelike army-builder made in Unity. You lead a small squad of living statues across a branching map. Each fight is a turn-based tactical battle against an AI army on a small isometric grid.

![A battle turn against goblins on the Storm battlefield: shield bash, pinning shot, vanish](docs/media/battle-turn.gif)

| Sun beam against elementals at the Edge of the World | Moon shield, lightning and victory in the Storm |
|---|---|
| ![Sun beam](docs/media/sun-beam.gif) | ![Storm victory](docs/media/storm-victory.gif) |

## The game

- **Run:** pick a banner, draft your army, then cross a 12-step map of fights, shops, events, elites, two scenario fights and a boss.
- **Battle:** a 6x6 board. Every unit moves, then acts; every action except the basic attack has a cooldown, and every fight has its own battlefield rule. Enemies show what they plan to do next.
- **Growth:** units learn techniques from 62, sorted into one pool per unit line, each line built around a theme. Banners are army-wide rules. Statues that fall get a crack, and three cracks shatter them for good.
- **Enemies:** five factions (wolves, goblins, bandits, elementals, demons), each with its own units, teamwork and AI. Champions with extra traits appear later in the run. There are two bosses with phases.

## How it's built

- **Rules apart from the engine.** All game rules are plain C# with no Unity references (`Assets/Scripts/Core`), covered by NUnit tests (`Assets/Tests/EditMode`). The Unity side (`Assets/Scripts/View`) only shows the state and passes input in.
- **One path for damage.** All damage goes through a single method, so every ability works with every source of damage. Actions return the events they caused, and the view animates those events.
- **AI that looks ahead.** The battle can make an exact copy of itself, with its own copyable random generator, so bots can try moves before choosing one.
- **Balance from data.** A bot-vs-bot simulator plays whole battles and writes balance reports (`tools/balance/`). Every playtest battle and run is saved as a JSON log tagged with its git commit.
- **Tools.** A game wiki generated from the game's own data, headless battle renders, an in-game effects and sound test bench, and design maps of hero synergies and enemy factions.

## Design docs

Open in the browser at **[nairigy.github.io/ArmyRoguelike](https://nairigy.github.io/ArmyRoguelike/)**:

- [Gamepedia](https://nairigy.github.io/ArmyRoguelike/gamepedia.html): every unit, ability, item, enemy and rule with its numbers, generated from the game data.
- [Hero synergy map](https://nairigy.github.io/ArmyRoguelike/synergy-map.html): every hero skill, item and rule tagged by theme, with its role in a build and a judgment.
- [Enemy faction map](https://nairigy.github.io/ArmyRoguelike/faction-map.html): each faction's plan, unit roles, combos and the hero themes that answer it.
- [Design note](docs/design-note.md): what has been decided and why. [Design history](docs/design-history.md): replaced rules and why.

## Tech

Unity 6 (6000.3), C#, URP 2D, Input System. Windows desktop.

## How it was made

Solo project. Design, systems and balance are mine. The code was written with help from AI (Claude), and the art and some sounds are AI-generated. Sound credits: [Assets/Audio/CREDITS.md](Assets/Audio/CREDITS.md).
