# DiceKingdoms Extractor (`DiceModders.Extractor.dll`)

Developer tool: dumps the game's UI hierarchy, UI assets and building types, and measures where frames go. A mod for **Dice Kingdoms** (BepInEx 6 IL2CPP). It is one self-contained project: it shares no code with other mods.

## What it does

A developer tool - it has **no interface** and does not need UiKit. Install it only while you need the dumps; it also switches the BepInEx console window and `LogOutput.log` on.

In the game hold **Right Ctrl** and press:

| Key | Writes |
|---|---|
| U | the UI hierarchy of all canvases -> `BepInEx\DiceKingdoms_ui.txt` |
| I | deep UI dump: real component types, sprites, fonts, button settings and every loaded sprite -> `BepInEx\DiceKingdoms_ui_assets.txt` |
| F11 | building types and footprints -> `BepInEx\DiceKingdoms_buildings.txt` |
| P | switch the **frame-time probe** on / off: every 2 s the log shows the frame rate and, for the heaviest per-frame methods (unit visuals, projectiles, combat simulation, scoreboard), calls per frame and milliseconds per frame. Use it to see where a big battle spends its frames, or to check that an optimisation really changes the numbers |

Open the screen you want to capture first (e.g. Settings > Video, or the lobby), and rename each file before the next dump (they are overwritten).

Remove it again with `.\scripts\build.ps1 -Remove` (this also switches the console and log file off).

## Code map

`ModPlugin.cs`, `UiProbe.cs`, `UiAssetDump.cs`, `BuildingDumpFile.cs`, `FrameProbe.cs`, `Common/`.

## Requirements

* Dice Kingdoms with **BepInEx 6 IL2CPP (x64)** - see the wiki's [Mods Installation](https://dicemodders.github.io/DiceKingdoms-Modding-Wiki/guides/mod-installation/) guide. Start the game once so BepInEx creates its folders.

## Install (players)

Copy `DiceModders.Extractor.dll` into `BepInEx\plugins`. Settings are created in `BepInEx\config` (`org.dicemodders.dicekingdoms.extractor.cfg`).

## Build (developers)

Needs the .NET SDK (6 or newer) and PowerShell. Game files are never committed: the scripts link them from your game folder.

```powershell
.\scripts\link-interop.ps1 "D:\SteamLibrary\steamapps\common\Dice-Kingdoms"   # once: BepInEx\interop -> .\interop
.\scripts\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Dice-Kingdoms"   # build + install, console and log ON (path is remembered)
.\scripts\build.ps1 -Remove                                               # remove it again, console and log OFF
```

If PowerShell refuses to run scripts: `powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1`.

## License

MIT - see [LICENSE](LICENSE).
