# DiceKingdoms Extractor (`DiceModders.Extractor.dll`)

Developer tool for **Dice Kingdoms** (BepInEx 6 IL2CPP): dumps the game's UI hierarchy, UI assets and building types, and measures where frames go.

Install it only while you need it. It switches the BepInEx console window and `LogOutput.log` on (from the next game start).

## What it does

Everything is on its own tab, **Settings > Extractor**:

| Button | Writes |
|---|---|
| **UI hierarchy** | all canvases: object names, components, sizes, texts -> `BepInEx\DiceKingdoms_ui.txt` |
| **UI assets** | real component types, sprites, fonts, button settings and every loaded sprite -> `BepInEx\DiceKingdoms_ui_assets.txt` |
| **Buildings** | building types and footprints -> `BepInEx\DiceKingdoms_buildings.txt` (join a match first) |
| **Frame-time probe** (toggle) | while on, every few seconds the log shows the frame rate and, for the heaviest per-frame methods (unit visuals, projectiles, combat simulation, scoreboard), calls per frame and milliseconds per frame |

Open the screen you want to capture first (for example Settings > Video, or the lobby). Each file is overwritten by the next dump.

The probe's report interval is `[Probe] ReportIntervalMs` in `BepInEx\config\org.dicemodders.dicekingdoms.extractor.cfg`. The game methods are patched only the first time the probe is switched on, and the report is printed from the game's own once-per-frame update, so the probe has no update loop of its own.

## Code map

| File | Job |
|---|---|
| `Plugin.cs` | the plugin and the Settings tab |
| `UiProbe.cs` | UI hierarchy dump |
| `UiAssetDump.cs` | UI asset dump |
| `BuildingDumpFile.cs` | building dump |
| `FrameProbe.cs` | frame-time probe |

## Requirements

* Dice Kingdoms with **BepInEx 6 IL2CPP (x64)**: see the wiki's [Mods Installation](https://dicemodders.github.io/DiceKingdoms-Modding-Wiki/guides/mod-installation/) guide. Start the game once so BepInEx creates its folders.
* The DiceModders libraries in `BepInEx\plugins`: [ModCore](https://github.com/DiceModders/DiceKingdoms-ModCore) (`DiceModders.ModCore.dll`) and [UiKit](https://github.com/DiceModders/UiKit) (`DiceModders.UiKit.dll`). This mod does not contain copies of them.

## Install (players)

Copy `DiceModders.Extractor.dll` into `BepInEx\plugins`. Remove it again when you are done.

## Build (developers)

Needs the .NET SDK (6 or newer) and PowerShell. Game files are never committed: the script links `BepInEx\interop` from your game folder and copies the libraries from `BepInEx\plugins` into `libs`.

```powershell
.\scripts\build.ps1 -GameDir "<your Dice-Kingdoms folder>"   # first time: remembers the folder
.\scripts\build.ps1                                           # build + install (Release)
.\scripts\build.ps1 -Dev -Run                                 # Debug build, console and log ON, then start the game
```

Close the game before building: it locks the DLLs. If PowerShell refuses to run scripts: `powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1`.

## License

MIT, see [LICENSE](LICENSE).
