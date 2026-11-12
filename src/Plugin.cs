using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.ModCore.Tools;

namespace DiceModders.Extractor;

using Ui = DiceModders.UiKit.UiKit;

/// <summary>
/// Developer tool, not one of the normal mods: dumps of the game's UI and building types and a frame-time probe, all started from its own tab in the
/// game's Settings screen. Install it while you need it; it switches the BepInEx console window and log file on.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
[BepInDependency("DiceModders.ModCore")]
[BepInDependency("DiceModders.UiKit")]
[BepInProcess("Dice-Kingdoms.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "org.dicemodders.dicekingdoms.extractor";
    public const string Name = "DiceKingdoms Extractor";
    public const string Version = "3.0.0";

    internal static ManualLogSource L;

    public override void Load()
    {
        L = Log;
        EnableLogging();
        FrameProbe.Bind(Config, new Patcher(Guid, Log));
        Ui.CreateScope(Guid)?.Native.Settings.AddTab(Guid, "Extractor", tab =>
        {
            tab.AddInfo("Open the screen you want to capture first (for example Settings > Video, or the lobby). Each file is overwritten by the next dump.");
            tab.AddButton("UI hierarchy", "Write", UiProbe.Dump);
            tab.AddInfo("All canvases: object names, components, sizes, texts -> BepInEx\\DiceKingdoms_ui.txt");
            tab.AddButton("UI assets", "Write", UiAssetDump.Run);
            tab.AddInfo("Component types, sprites, fonts, button settings and every loaded sprite -> BepInEx\\DiceKingdoms_ui_assets.txt");
            tab.AddButton("Buildings", "Write", BuildingDumpFile.Run);
            tab.AddInfo("Building types and footprints -> BepInEx\\DiceKingdoms_buildings.txt (join a match first)");
            tab.AddToggle("Frame-time probe", false, FrameProbe.Set);
            tab.AddInfo("While on, the log shows the frame rate and the cost of the game's heaviest per-frame methods every few seconds (in a match).");
        });
    }

    /// <summary>A development tool: the BepInEx console window and log file are switched on from the next start.</summary>
    private static void EnableLogging()
    {
        try
        {
            var cfg = new BepInEx.Configuration.ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, "BepInEx.cfg"), false);
            var console = cfg.Bind("Logging.Console", "Enabled", true, "Enables showing a console for log output.");
            var disk = cfg.Bind("Logging.Disk", "Enabled", true, "Enables writing log messages to disk.");
            if (console.Value && disk.Value) return;
            console.Value = true; disk.Value = true;
            cfg.Save();
            L.LogMessage("BepInEx console window and log file switched on for the next start.");
        }
        catch (Exception e) { L.LogWarning("could not update BepInEx.cfg: " + e.Message); }
    }
}
