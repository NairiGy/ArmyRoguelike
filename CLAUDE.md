# ArmyRoguelike (working title)

A single-player roguelike army-builder. You lead a small squad of living statues across a Slay the Spire–style map of fights, and each fight is a turn-based tactical battle against an AI army on a small isometric 2D grid.

## Status

- **Prototype 5 (Act 1):** draft 3 of 5 base units, then a 12-step Act 1 with at least 5 fights before a rotating boss (Necromancer raising the factions you beat, or the Giant). Battles bring factions you haven't beaten (wolves, goblins, bandits, elementals), then rematches; elites are harder rematches; each encounter uses a rule-based layout (`Core/Encounter.cs`). Steps 4 and 8 offer the two scenario fights (Moon Shrine: defend for a blessing; Break the Ritual: smash totems; losing either gives a curse). The 7-step version was playtested once (2026-10-27, too short and easy). "Test battle" on the draft screen (editor/dev builds) jumps into any fight with any techniques.
- **Night pass (2026-11-08, branch `feature/night-fun`, not playtested, not merged into `main`):** cracks (statues that fall keep a lasting crack; 3 shatter them), events with a price, champions in later fights, Stone Heart boss phases, the Crumbling Roof battlefield, Hone; Archer, Rogue, Mage and Sapper pools (59 techniques in all) with one owned keyword per line; a Demons faction. See "Night pass" in the design note, `docs/night-log.md` and `docs/night/`. `Core/Champion.cs` holds champion affixes.
- **Warrior redesign (2026-11-05, not playtested):** no base passive; 8 new or changed skills, others parked. First line of a pass over all 5 units aiming for far more skills than a run can learn. See "Warrior redesign" in the design note.
- **Techniques (2026-10-30):** no evolutions or Trainers. The 5 base units grow by learning techniques (34, each tied to a line, some needing another first): every won fight except the boss and the Moon Shrine offers 3 to pick from (skip for gold; an elite's holds a rare one), shops sell them and a Forget service, carts hold one or an item. A unit's level is the number of techniques it learned and gives per-line HP and damage. Items stay in their one slot, sold in shops. Playtested once as a full 12-step run (won, shops too cheap; fixes in). See "Techniques instead of evolutions" in the design note.
- **Banners (2026-10-31, not playtested):** army-wide rules for the run, no limit on how many: Vampiric, Third Wheel, Loser, Mercenaries, Battering Ram (`Core/Banner.cs`). Pick 1 of 3 at the start, one for sale per shop, an elite's instead of its technique. Every unit also has a weight (small / medium / huge) that changes how far pushes move it. See "Banners and weight" in the design note.
- **Battlefields (2026-10-31, not playtested):** one rule per fight, named on its map node and the same for both sides: Edge of the World (pushes off the board kill), Storm, Arcane Overflow, Fortune's Favored, What's On His Mind? (`Core/Battlefield.cs`). Every battle, elite, boss and ambush has one for now; scenario fights don't. See "Battlefields" in the design note.
- **Battle rules (2026-10-30):** 6x6 board; every unit has 1 move (2 with Swift) and 1 AP, moves first and then acts; every action except the basic attack has a cooldown. Each unit is meant to be unique through its passives and actions: every enemy (bosses included) and your 5 base units have their kits; techniques add to them. Hovering a unit shows its card; Alt shows every enemy's intent. See "Small board, one move" in the design note.
- Design and placeholder numbers: [docs/design-note.md](docs/design-note.md). Keep it updated when design decisions change.
- Open tasks across all topics (visuals, gameplay, sound, marketing): [docs/tasks.md](docs/tasks.md). Read it at the start of a session and update it when you finish or find work.
- Reference of every unit, ability, item, enemy and rule: [docs/gamepedia.html](docs/gamepedia.html), generated from the game data.

## Tech

- Unity **6000.3.11f1**, Universal 2D template (URP 2D renderer), Input System only.
- Target platform: Windows desktop.
- Add or remove packages through the PackageManager Client API, not by hand-editing `Packages/manifest.json`.

## Code layout

- `Assets/Scripts/Core`: rules in pure C#, with no Unity references (`ArmyRoguelike.Core` assembly).
  - `UnitStats.cs`: every unit type (and its weight). `Techniques.cs`: the techniques units learn and the per-line growth per level. `Equipment.cs`: items, plus names and rule text for abilities and actions. `Banner.cs`: banners, their numbers and rule text. `Battlefield.cs`: battlefields (one rule per fight), their numbers and rule text.
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
- `tools/dev.sh compile|test|wiki|sim`: compile, run the tests, write a text-only gamepedia and play bot battles for a balance report (`tools/balance/`, see "Balance" in the design note) with Unity's bundled compiler, with the Editor open or closed. See `tools/README.md`. The Pipeline package (`com.unity.pipeline`) lets `unity status` / `unity command` drive an open Editor.
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
| Sound, marketing (later) | Their own folders once started | Work for others → their sections of `docs/tasks.md` |

Some files are shared (`BattleController`, `UnitView`, `PlaceholderArt`): gameplay adds the placeholder, art restyles it. Don't edit one while another chat is working in it.

The chats share one project folder, so:

- Only one chat uses Unity at a time (batch renders and imports fail while the editor is open).
- Don't edit the same files as another chat at the same time; commit only your own files, and never switch branches, stash or reset. For bigger parallel work, a chat can get its own worktree on its own branch.
- This folder is checked out on `main`, which holds all work; `experiment/art-style` was merged and deleted on 2026-10-28.
