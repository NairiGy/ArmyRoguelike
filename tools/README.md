# tools

Helpers for working on the project without the Unity Editor (it can stay open).

```bash
tools/dev.sh compile   # compile Core, View and Editor scripts with Unity's compiler; prints errors only
tools/dev.sh test      # run the EditMode tests on Unity's bundled .NET (no Editor needed)
tools/dev.sh wiki      # write docs/gamepedia.html, text only
tools/dev.sh sim       # play battles with bots; writes tools/out/balance-report.md (optional: seeds per draft and fight, default 50)
```

- `balance/BalanceSim.cs`: the simulator behind `dev.sh sim`. Its bots only move and basic-attack, so compare fights with each other rather than reading the results as real win rates.

- `TestRunner.cs`: a minimal NUnit-style runner used by `dev.sh test`.
- `GamepediaTextExport.cs`: entry point for `dev.sh wiki`.
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
