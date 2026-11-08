# ArmyRoguelike (working title)

A single-player roguelike army-builder. You lead a small squad of living statues across a Slay the Spire–style map of fights, and each fight is a turn-based tactical battle against an AI army on a small isometric 2D grid.

## Status

The game as of 2026-11-08 (details and reasons in the design note; most of it is played once or not at all):

- **Run:** "Prepare your army" (pick 1 of 3 banners, draft 3 of 5 base units), then a 12-step Act 1: at least 5 fights, shops, ? events, two elites, the two scenario fights at steps 4 and 8 (Moon Shrine: defend for a blessing; Break the Ritual: smash totems; losing either gives a curse) and a rotating boss (Necromancer or Giant, both with Stone Heart phases). Battles bring factions you haven't beaten (wolves, goblins, bandits, elementals, demons), then rematches; champions from step 6. Statues that fall keep a crack; 3 shatter them.
- **Battle:** 6x6 board; every unit has 1 move (2 with Swift) and 1 AP, moves first and then acts; every action but the basic attack has a cooldown; one battlefield rule per fight (`Core/Battlefield.cs`). Every enemy and boss has its own kit. Left-click chooses, right-click acts, Alt shows enemy intents; rule texts use keywords explained on hover (`Core/Keywords.cs`).
- **Growth:** units learn techniques (59, one pool per line, each line with a theme keyword) from a pick after won fights and from shops; a unit's level is the number it learned. One item slot. Banners (`Core/Banner.cs`) are army-wide rules, no limit on how many. Champion affixes: `Core/Champion.cs`.
- **Latest work:** night pass (2026-11-08, `docs/night-log.md`, `docs/night/`), synergy map and one name per rule (2026-11-08, [Hero Synergy Map](https://claude.ai/artifact/BCBjEq3EFMuN1tZi1sLoyw), source [docs/synergy-map.html](docs/synergy-map.html)), UI readability pass (2026-11-08).
- "Test battle" on the start screen (editor/dev builds) jumps into any fight with any techniques.

Where things are written down:

- [docs/design-note.md](docs/design-note.md): what has been decided and why, current rules only. Keep it updated when design decisions change. Replaced rules are in [docs/design-history.md](docs/design-history.md).
- [docs/tasks.md](docs/tasks.md): open tasks across all topics. Read it at the start of a session and update it when you finish or find work.
- [docs/gamepedia.html](docs/gamepedia.html): every unit, ability, item, enemy and rule with its numbers, generated from the game data. Numbers are right there, not in the design note.
- [docs/synergy-map.html](docs/synergy-map.html) ([published](https://claude.ai/artifact/BCBjEq3EFMuN1tZi1sLoyw)): every hero skill, item and rule tagged by the theme keywords it makes, uses or hurts; hero-pair synergies, theme health, rule duplicates and holes.

## Tech

- Unity **6000.3.11f1**, Universal 2D template (URP 2D renderer), Input System only.
- Target platform: Windows desktop.
- Add or remove packages through the PackageManager Client API, not by hand-editing `Packages/manifest.json`.

## Code layout

- `Assets/Scripts/Core`: rules in pure C#, with no Unity references (`ArmyRoguelike.Core` assembly).
  - `UnitStats.cs`: every unit type (and its weight). `Techniques.cs`: the techniques units learn and the per-line growth per level. `Equipment.cs`: items, plus names and rule text for abilities and actions. `Banner.cs`: banners, their numbers and rule text. `Battlefield.cs`: battlefields (one rule per fight), their numbers and rule text. `Keywords.cs`: the keywords rule texts name without explaining, their meanings, and finding them in a text (`View/KeywordText.cs` draws texts with them highlighted and explained on hover).
  - `Battle.cs`: board, AP, movement, attacks, special actions, statuses and tiles. All damage goes through one `Damage` method so abilities apply to every source. `Battle.Clone()` makes an exact copy for bots looking ahead (`SeededRng.cs` is its copyable random generator). Actions return the events they caused, which the view animates.
  - `EnemyAI.cs`: enemy behaviour and the intent shown on hover. `Run.cs`: draft, map, technique picks, shop and events. `Encounter.cs`: factions, bosses and their layout templates.
  - `AssemblyInfo.cs` lets the tests set internal state directly.
- `Assets/Scripts/View`: Unity side (`ArmyRoguelike.View` assembly). It shows the battle and handles input, and only reads state and calls into Core. Screens are IMGUI; buttons that change the screen run deferred via `GameBootstrap.Do` to avoid IMGUI layout errors.
- `Assets/Editor`: `PreviewCapture` (renders a battle to PNG headlessly), `GamepediaExporter` + `GamepediaWriter` (the gamepedia; the writer is plain C#), `ArtExperiment` (Tools → Art → Import Statues, plus the art-experiment renders).
- `Assets/Art`: painted art (see "Visual direction" in the design note). `Experiment/Source/` holds the AI-generated source images, named after the tool that made them (`<unit>_noground__<site>_<model>.png`, see "Art source" in the design note); Import Statues cuts them out into `Units/Resources/Statues/<UnitKind>_figure.png`. `Environment/Resources` holds the greyscale backdrops and the palette-ramp shader used by `View/BattleScenery.cs`; some battlefields swap the tower's sky for a backdrop of their own (Edge of the World: the void, moved by `View/SceneryVoid.cs`; Fortune's Favored: wheat fields and Storm: a stormy sea, both full-colour backdrops moved by `View/SceneryFields.cs`). Raw AI art also lives outside the repo in `Desktop/ai art/<unit>/`.
- `Assets/Audio`: sound (see "Sound" in the design note). `Resources/Sfx/<name>.wav` are the clips, numbered variants (`name_1`, `name_2`) picked at random; `CREDITS.md` lists every source, licence and prompt. `View/Sfx.cs` plays them (`Sfx.Play("name")` on the line where the visual moment happens) and `Editor/SfxImport.cs` sets their import settings. Raw downloads live outside the repo in `Desktop/sound sources/`; `tools/make_sfx.sh` cuts them into the clips.
- Banners on the roof (`View/BannerView.cs`): one painted pole and one grey cloth in `Assets/Art/Banners/Resources/Banners`, made from the blank painting by `tools/make_banner_layers.ps1`; `BannerCloth.shader` colours the cloth through each banner's ramp and moves it in the wind.
- Effects playground (`View/FxPlayground.cs`): F1 in a test battle plays any effect on the hovered tile or unit, plays each sound, sets the volumes and locks a sound to one variant. Try auditions a sound: its real effect on a unit with each variant in turn, Y / N verdicts saved to `Playtests/sound-review.txt`. `UnitView.Restore` stands a statue back up after the death demo.
- `Assets/Tests/EditMode`: NUnit tests for Core.
- `tools/dev.sh compile|test|wiki|synergy|sim`: compile, run the tests, write a text-only gamepedia, refresh the synergy map's texts and play bot battles for a balance report (`tools/balance/`, see "Balance" in the design note) with Unity's bundled compiler, with the Editor open or closed. See `tools/README.md`. The Pipeline package (`com.unity.pipeline`) lets `unity status` / `unity command` drive an open Editor.
- `Playtests/` (git-ignored): a JSON log per battle (`PlaytestLogger`) and per run (`RunLogger`), each stamped with its git commit and dirty flags; run logs also hold the player's `runTag` and `difficultyRating`. F9 in game saves a marker with a board snapshot. Read these to summarize playtests instead of asking the user for numbers. Battles skipped with the debug Win button are marked `skipped`.
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
- Keep the synergy map current the same way: after changing a hero skill, item, banner, battlefield, omen or champion affix, run `tools/dev.sh synergy` (it copies the game's rule texts in and lists anything new, renamed or removed), add or retag those entries in [docs/synergy-map.html](docs/synergy-map.html) (makes / uses / hurts), mark fixed rule duplicates or holes "(fixed date)", commit it, and republish it to its link with the Artifact tool.
- Update this file when the project's structure or status changes.

## Working across chats

The game is worked on in several chats, one per big topic (visuals and art, gameplay and mechanics, and later sound, marketing and others), so no chat gets overloaded. The user starts each chat and coordinates them; there is no master chat.

- Chats don't message each other. They can't read each other's conversations either; these files are what connects them:
  - `CLAUDE.md` (this file): how we work. `docs/design-note.md`: what has been decided. `docs/tasks.md`: what is left to do.
- At the start of a session, read all three. At the end of a piece of work, update `docs/tasks.md` (tick done items, add new ones) and record any decision in the design note, so the other chats know.
- Work found for another topic goes into that topic's section of `docs/tasks.md`.
- Gameplay chats add a row to the "Art needed for gameplay features" table in `docs/tasks.md` whenever a feature ships with placeholder visuals. The art chat takes work from that table and sets each row's Status when it starts and finishes it.
- Merges and decisions that affect more than one topic are the user's call. When asked for a status or a plan, answer from the three files.

### Roles

The user's first message in a chat names its role. Stay inside it; put work for other roles into their section of `docs/tasks.md`.

| Role | Owns | Hands off |
|---|---|---|
| Gameplay and mechanics | `Assets/Scripts/Core`, the tests, gameplay logic in `Assets/Scripts/View` (rules, HUD text, screens' flow), `PlaytestLogger` / `RunLogger`, `GamepediaWriter`, the design note's rules sections | Placeholder visuals → the "Art needed" table |
| Visuals and art | `Assets/Art`, `BattleScenery` and the scenery scripts, statues and `PlaceholderArt` look, highlight and effect styling, the UI look, the design note's "Visual direction" | Gameplay questions or bugs → the Gameplay section of `docs/tasks.md` |
| Balance | The numbers (changed freely, reported afterwards), `tools/balance/`, the design note's "Balance" section | Rules, kits and AI behaviour need the user's OK; fixes and features → a gameplay session |
| Docs quality | The quality of every doc (`CLAUDE.md`, `docs/`, `tools/README.md`, credits files): structure, stale or contradicting lines, duplicates, bloat, moving finished tasks to "Done", replaced rules to `docs/design-history.md`. A weekly scheduled task (`weekly-docs-cleanup`) does a pass and reports concerns to the user | Never changes a decision, rule or number; a doc that disagrees with the code is fixed in the doc only when the code is clearly the current state, otherwise flagged to the user |
| Sound, marketing (later) | Their own folders once started | Work for others → their sections of `docs/tasks.md` |

Some files are shared (`BattleController`, `UnitView`, `PlaceholderArt`): gameplay adds the placeholder, art restyles it. Don't edit one while another chat is working in it.

The chats share one project folder, so:

- Only one chat uses Unity at a time (batch renders and imports fail while the editor is open).
- Don't edit the same files as another chat at the same time; commit only your own files, and never switch branches, stash or reset. For bigger parallel work, a chat can get its own worktree on its own branch.
- This folder is checked out on `main`, which holds all work; `experiment/art-style` was merged and deleted on 2026-10-28; `feature/night-fun` was merged on 2026-11-08 (kept, not deleted).
