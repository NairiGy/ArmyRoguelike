# ArmyRoguelike (working title)

A single-player roguelike army-builder. You lead a small squad of living statues across a Slay the Spire–style map of fights, and each fight is a turn-based tactical battle against an AI army on a small isometric 2D grid.

## Status

- **Prototype 4:** draft 3 of 5 base units, then a 9-step branching map (battles, shops, ? events, free Trainers at steps 4 and 7, ogre boss). Each base unit has a 3-tier evolution tree (38 unit types). Items only grant abilities. Not playtested yet.
- Design and placeholder numbers: [docs/design-note.md](docs/design-note.md). Keep it updated when design decisions change.
- Reference of every unit, ability, item, enemy and rule: [docs/gamepedia.html](docs/gamepedia.html), generated from the game data.

## Tech

- Unity **6000.3.11f1**, Universal 2D template (URP 2D renderer), Input System only.
- Target platform: Windows desktop.
- Add or remove packages through the PackageManager Client API, not by hand-editing `Packages/manifest.json`.

## Code layout

- `Assets/Scripts/Core`: rules in pure C#, with no Unity references (`ArmyRoguelike.Core` assembly).
  - `UnitStats.cs`: every unit type and the evolution trees. `Equipment.cs`: items, plus names and rule text for abilities and actions.
  - `Battle.cs`: board, AP, movement, attacks, special actions, statuses and tiles. All damage goes through one `Damage` method so abilities apply to every source. Actions return the events they caused, which the view animates.
  - `EnemyAI.cs`: enemy behaviour and the intent shown on hover. `Run.cs`: draft, map, shop, trainer, events and encounter layouts.
  - `AssemblyInfo.cs` lets the tests set internal state directly.
- `Assets/Scripts/View`: Unity side (`ArmyRoguelike.View` assembly). It shows the battle and handles input, and only reads state and calls into Core. Screens are IMGUI; buttons that change the screen run deferred via `GameBootstrap.Do` to avoid IMGUI layout errors.
- `Assets/Editor`: `PreviewCapture` (renders a battle to PNG headlessly), `GamepediaExporter` + `GamepediaWriter` (the gamepedia; the writer is plain C#).
- `Assets/Tests/EditMode`: NUnit tests for Core.
- `Playtests/` (git-ignored): a JSON log per battle (`PlaytestLogger`) and per run (`RunLogger`). F9 in game saves a marker with a board snapshot. Read these to summarize playtests instead of asking the user for numbers. Battles skipped with the debug Win button are marked `skipped`.
- The scene stays nearly empty. `GameBootstrap` builds the run at runtime, and placeholder art is generated in code (`PlaceholderArt`).

## Version control

- Local git on `main`, no remote.
- Commit every `.cs` and asset file together with its `.meta`. Never commit `Library/`, `Temp/`, `Logs/` or `UserSettings/`.

## Working preferences

- Always ask before doing actual work (editing files, building, committing). Design discussion is fine without asking.
- Don't add features, text or visuals that weren't asked for. Ask first.
- For tuning and game-feel issues, keep iterations small: one diagnosis and one fix.
- The user playtests in bundles, not after every small change. Don't ask them to test each tweak; say what's untested and suggest a playtest when a bundle is ready.
- Ask about unknowns rather than guessing.
- Keep `PlaytestLogger` and `RunLogger` up to date whenever gameplay changes: log whatever new data is needed to judge it. The user reports how it feels; the logs cover the numbers.
- Keep the gamepedia current: after changing units, abilities, items, enemies or rules, re-export it (Tools → Export Gamepedia, or the batch method when the editor is closed) and commit `docs/gamepedia.html`.
- Update this file when the project's structure or status changes.
