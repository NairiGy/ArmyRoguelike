# ArmyRoguelike (working title)

A single-player roguelike army-builder. You lead a small squad of living statues across a Slay the Spire–style map of fights, and each fight is a turn-based tactical battle against an AI army on a small isometric 2D grid.

## Status

- **Prototype 1:** one battle only. Warrior, archer and rogue against a wolf pack. There is no run layer yet.
- Full design and placeholder numbers: [docs/design-note.md](docs/design-note.md). Keep it updated when design decisions change.

## Tech

- Unity **6000.3.11f1**, Universal 2D template (URP 2D renderer), Input System only.
- Target platform: Windows desktop.
- Add or remove packages through the PackageManager Client API, not by hand-editing `Packages/manifest.json`.

## Code layout

- `Assets/Scripts/Core`: battle rules in pure C#, with no Unity references (`ArmyRoguelike.Core` assembly). The board, units, AP, movement, attacks, enemy AI and win/lose all live here.
- `Assets/Scripts/View`: Unity side (`ArmyRoguelike.View` assembly). It shows the battle and handles input, and only reads state and calls into Core.
- `Assets/Tests/EditMode`: NUnit tests for Core.
- `Playtests/` (git-ignored): one JSON log per battle, written by `PlaytestLogger`. F9 in game saves a marker with a board snapshot. Read these to summarize playtests instead of asking the user for numbers.
- The scene stays nearly empty. `GameBootstrap` builds the battle at runtime, and placeholder art is generated in code.

## Version control

- Local git on `main`, no remote.
- Commit every `.cs` and asset file together with its `.meta`. Never commit `Library/`, `Temp/`, `Logs/` or `UserSettings/`.

## Working preferences

- Always ask before doing actual work (editing files, building, committing). Design discussion is fine without asking.
- Don't add features, text or visuals that weren't asked for. Ask first.
- For tuning and game-feel issues, keep iterations small: one diagnosis and one fix, then let the user test.
- Ask about unknowns rather than guessing.
