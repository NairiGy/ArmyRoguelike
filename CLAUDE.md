# ArmyRoguelike (working title)

A single-player roguelike army-builder. You lead a small squad of living statues across a Slay the Spire–style map of fights, and each fight is a turn-based tactical battle against an AI army on a small isometric 2D grid.

## Status

- **Prototype 5 (Act 1):** draft 3 of 5 base units, then a 7-step Act 1: each of four factions (wolves, goblins, bandits, elementals) appears at most once, each encounter uses a rule-based layout (`Core/Encounter.cs`), and the act ends with a rotating boss (Necromancer raising the factions you beat, or the Giant). Trainers allow one evolution per unit per visit (first free, then paid). 38 player unit types in 5 evolution trees; items only grant abilities. Not playtested yet.
- Design and placeholder numbers: [docs/design-note.md](docs/design-note.md). Keep it updated when design decisions change.
- Open tasks across all topics (visuals, gameplay, sound, marketing): [docs/tasks.md](docs/tasks.md). Read it at the start of a session and update it when you finish or find work.
- Reference of every unit, ability, item, enemy and rule: [docs/gamepedia.html](docs/gamepedia.html), generated from the game data.

## Tech

- Unity **6000.3.11f1**, Universal 2D template (URP 2D renderer), Input System only.
- Target platform: Windows desktop.
- Add or remove packages through the PackageManager Client API, not by hand-editing `Packages/manifest.json`.

## Code layout

- `Assets/Scripts/Core`: rules in pure C#, with no Unity references (`ArmyRoguelike.Core` assembly).
  - `UnitStats.cs`: every unit type and the evolution trees. `Equipment.cs`: items, plus names and rule text for abilities and actions.
  - `Battle.cs`: board, AP, movement, attacks, special actions, statuses and tiles. All damage goes through one `Damage` method so abilities apply to every source. Actions return the events they caused, which the view animates.
  - `EnemyAI.cs`: enemy behaviour and the intent shown on hover. `Run.cs`: draft, map, shop, trainer and events. `Encounter.cs`: factions, bosses and their layout templates.
  - `AssemblyInfo.cs` lets the tests set internal state directly.
- `Assets/Scripts/View`: Unity side (`ArmyRoguelike.View` assembly). It shows the battle and handles input, and only reads state and calls into Core. Screens are IMGUI; buttons that change the screen run deferred via `GameBootstrap.Do` to avoid IMGUI layout errors.
- `Assets/Editor`: `PreviewCapture` (renders a battle to PNG headlessly), `GamepediaExporter` + `GamepediaWriter` (the gamepedia; the writer is plain C#), `ArtExperiment` (Tools → Art → Import Statues, plus the art-experiment renders).
- `Assets/Art`: painted art (see "Visual direction" in the design note). `Experiment/Source/<unit>_iso.png` are the AI-generated source images; Import Statues cuts them out into `Units/Resources/Statues/<UnitKind>.png`. `Environment/Resources` holds the greyscale backdrop and the palette-ramp shader used by `View/BattleScenery.cs`. Raw AI art also lives outside the repo in `Desktop/ai art/<unit>/`.
- `Assets/Tests/EditMode`: NUnit tests for Core.
- `tools/dev.sh compile|test|wiki`: compile, run the tests and write a text-only gamepedia with Unity's bundled compiler, with the Editor open or closed. See `tools/README.md`. The Pipeline package (`com.unity.pipeline`) lets `unity status` / `unity command` drive an open Editor.
- `Playtests/` (git-ignored): a JSON log per battle (`PlaytestLogger`) and per run (`RunLogger`). F9 in game saves a marker with a board snapshot. Read these to summarize playtests instead of asking the user for numbers. Battles skipped with the debug Win button are marked `skipped`.
- The scene stays nearly empty. `GameBootstrap` builds the run at runtime, and placeholder art is generated in code (`PlaceholderArt`). Unit types with a painted statue sprite use it instead.

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

## Working across chats

The game is worked on in several chats, one per big topic (visuals and art, gameplay and mechanics, and later sound, marketing and others), so no chat gets overloaded. The user starts each chat and coordinates them; there is no master chat.

- Chats don't message each other. They can't read each other's conversations either; these files are what connects them:
  - `CLAUDE.md` (this file): how we work. `docs/design-note.md`: what has been decided. `docs/tasks.md`: what is left to do.
- At the start of a session, read all three. At the end of a piece of work, update `docs/tasks.md` (tick done items, add new ones) and record any decision in the design note, so the other chats know.
- Work found for another topic goes into that topic's section of `docs/tasks.md`.
- Merges and decisions that affect more than one topic are the user's call. When asked for a status or a plan, answer from the three files.

The chats share one project folder, so:

- Only one chat uses Unity at a time (batch renders and imports fail while the editor is open).
- Don't edit the same files as another chat at the same time; commit only your own files, and never switch branches, stash or reset. For bigger parallel work, a chat can get its own worktree on its own branch.
- Visual work currently lives on the branch `experiment/art-style`; it is merged into `main` when the battle's look is settled.
