using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.Shared;
using UnityEngine;

namespace DiceModders.Extractor;

/// <summary>
/// Developer tool, not part of the normal mods. Build and install it with scripts\build.ps1, press the hotkeys in the game,
/// send the written files, then remove it again with scripts\build.ps1 -Remove.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Dice-Kingdoms.exe")]
public sealed class ModPlugin : BasePlugin
{
    public const string Guid = "org.dicemodders.dicekingdoms.extractor";
    public const string Name = "DiceKingdoms Extractor";
    public const string Version = "2.1.0";

    internal static ManualLogSource L;
    internal static Hotkeys Keys;

    public override void Load()
    {
        L = Log;
        EnableLogging();
        Keys = new Hotkeys(Config, L, Name);
        Keys.Bind(Config, "UiHierarchy", HotkeyKey.U, "Write the game's UI hierarchy (all canvases) to BepInEx/DiceKingdoms_ui.txt", UiProbe.Dump, "UI hierarchy dump");
        Keys.Bind(Config, "UiAssets", HotkeyKey.I, "Deep UI dump: real component types, sprites, fonts, button settings, all loaded sprites -> BepInEx/DiceKingdoms_ui_assets.txt", UiAssetDump.Run, "UI asset dump");
        Keys.Bind(Config, "Buildings", HotkeyKey.F11, "Dump building types + footprints to BepInEx/DiceKingdoms_buildings.txt", BuildingDumpFile.Run, "Building dump");
        var patcher = new Patcher(Guid, L);
        FrameProbe.Load(Config, Keys, patcher);   // the probe's patches are installed on first use
        Keys.LogBindings();
        AddComponent<ExtractorTicker>();
        L.LogMessage("[extractor] Open the screen you want to capture (e.g. Settings > Video, or the lobby before a match starts), then press Right Ctrl + I.");
    }

    /// <summary>The Extractor is a development tool: make sure the BepInEx console window and log file are on from the next start.</summary>
    private static void EnableLogging()
    {
        try
        {
            var cfg = new BepInEx.Configuration.ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, "BepInEx.cfg"), false);
            bool changed = false;
            var console = cfg.Bind("Logging.Console", "Enabled", true, "Enables showing a console for log output.");
            if (!console.Value) { console.Value = true; changed = true; }
            var disk = cfg.Bind("Logging.Disk", "Enabled", true, "Enables writing log messages to disk.");
            if (!disk.Value) { disk.Value = true; changed = true; }
            if (changed) { cfg.Save(); L.LogMessage("[extractor] BepInEx console window and log file switched ON for the next start."); }
        }
        catch (Exception e) { L.LogWarning("[extractor] could not update BepInEx.cfg: " + e.Message); }
    }
}

public sealed class ExtractorTicker : MonoBehaviour
{
    public ExtractorTicker(IntPtr ptr) : base(ptr) { }
    private void Update()
    {
        ModPlugin.Keys?.Poll();
        FrameProbe.Tick();
    }
}
