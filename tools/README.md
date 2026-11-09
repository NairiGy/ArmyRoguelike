# tools

Helpers for working on the project without the Unity Editor (it can stay open).

```bash
tools/dev.sh compile   # compile Core, View and Editor scripts with Unity's compiler; prints errors only
tools/dev.sh test      # run the EditMode tests on Unity's bundled .NET (no Editor needed)
tools/dev.sh wiki      # write docs/gamepedia.html, text only
tools/dev.sh synergy   # copy the game's rule texts into docs/synergy-map.html (hover cards)
tools/dev.sh sim       # play battles with crude bots; writes tools/out/balance-report.md (optional: seeds per draft and fight, default 50)
tools/dev.sh sim 10 smart  # the same with the look-ahead bot, about 12 minutes; writes tools/out/balance-smart.md
tools/dev.sh sim 10 fights "Elementals,Giant"  # quick look-ahead check of chosen fights; set SIM_CORE to a patched copy of Core to try a number
tools/dev.sh sim 20 runs  # whole runs (seeds x 5 per draft without banners, seeds per draft for each banner), about an hour at 20; writes tools/out/balance-runs.md
tools/dev.sh sim 5 combos  # how often each enemy faction's combos fire per fight (ComboTally), smart bot, fights and elites at L0 and L2; writes tools/out/combos.md. Add factions to run only those: tools/dev.sh sim 15 combos bandits,demons
```

- `balance/`: the simulator behind `dev.sh sim` (`BalanceSim.cs`), its look-ahead bot (`SmartBot.cs`) and whole runs (`RunSim.cs`). Compare fights with each other rather than reading the results as real win rates.

- `TestRunner.cs`: a minimal NUnit-style runner used by `dev.sh test`.
- `GamepediaTextExport.cs`: entry point for `dev.sh wiki`.
- `SynergyTexts.cs`: entry point for `dev.sh synergy`; rewrites the GAME TEXTS block in `docs/synergy-map.html`.
- Build output goes to `tools/out/` (git-ignored).

Things that need Unity itself (with the Editor closed, or driven live through the Pipeline package):

```bash
unity test . --mode EditMode                     # the official test run
unity run . -- -executeMethod GamepediaExporter.ExportFromCommandLine   # gamepedia with statue images
unity run . -- -executeMethod PreviewCapture.CaptureFromCommandLine -previewOut out.png -previewPlaceArmy -previewEncounter 2 -previewUnits "Warrior,Archer"
```

`make_sfx.sh` cuts the sound clips (`Assets/Audio/Resources/Sfx`) out of the raw Freesound downloads with ffmpeg; the sources and cut points are in the script, the credits in `Assets/Audio/CREDITS.md`:

```bash
tools/make_sfx.sh "$HOME/Desktop/sound sources" Assets/Audio/Resources/Sfx
```
