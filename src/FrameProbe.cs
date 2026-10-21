using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BepInEx.Configuration;
using DiceModders.Shared;

namespace DiceModders.Extractor;

/// <summary>
/// Right Ctrl + P: switches a frame-time probe on / off. While on, every few seconds it prints the frame rate and, for the game's heaviest
/// per-frame methods, how often they run per frame and how much time they take. Use it to find out where a battle spends its frames, and to check
/// that an optimisation (e.g. the Optimization mod's "No combat particles") really changes the numbers.
///
/// Example line:  UnitVisual.Update   104.0 calls/frame   3.20 ms/frame   (30.8 us/call)
/// The probe patches the methods only when it is switched on the first time.
/// </summary>
internal static class FrameProbe
{
    private static readonly (string Type, string Method)[] Targets =
    {
        ("UnitVisual", "Update"),
        ("UnitVisual", "SpawnParticles"),
        ("ProjectileVisual", "Update"),
        ("CombatInterface", "Update"),
        ("CombatGrid", "PrepareSimulationStep"),
        ("CombatGrid", "SimulationStep"),
        ("PlayerSummary", "Update"),
        ("GameState", "Update"),   // called once per frame: used as the frame counter
    };

    private sealed class Data { public string Name; public long Ticks; public long Calls; }

    private static readonly Dictionary<MethodBase, Data> Stats = new();
    private static Patcher _patcher;
    private static bool _patched;
    private static ConfigEntry<int> _intervalMs;
    private static long _last;

    internal static bool On { get; private set; }

    internal static void Load(ConfigFile config, Hotkeys keys, Patcher patcher)
    {
        _patcher = patcher;
        _intervalMs = config.Bind("Probe", "ReportIntervalMs", 2000,
            new ConfigDescription("How often the probe prints its report.", new AcceptableValueRange<int>(500, 30000)));
        keys.Bind(config, "Probe", HotkeyKey.P, "Toggle: frame-time probe (prints a report every few seconds)", Toggle, "Frame-time probe", () => On);
    }

    private static void Toggle()
    {
        if (!On && !_patched)
        {
            _patched = true;
            foreach (var (type, method) in Targets)
                _patcher.Patch(type, method, typeof(FrameProbe), prefix: nameof(Prefix), postfix: nameof(Postfix));
            _patcher.Summary();
        }
        On = !On;
        _last = Environment.TickCount64;
        ModPlugin.L.LogMessage("[probe] " + (On ? "ON" : "OFF"));
    }

    internal static void Prefix(out long __state) => __state = On ? Stopwatch.GetTimestamp() : 0;

    internal static void Postfix(MethodBase __originalMethod, long __state)
    {
        if (__state == 0) return;
        long elapsed = Stopwatch.GetTimestamp() - __state;
        if (!Stats.TryGetValue(__originalMethod, out var d))
        {
            d = new Data { Name = __originalMethod.DeclaringType?.Name + "." + __originalMethod.Name };
            Stats[__originalMethod] = d;
        }
        d.Ticks += elapsed;
        d.Calls++;
    }

    internal static void Tick()
    {
        if (!On) return;
        long now = Environment.TickCount64;
        if (now - _last < _intervalMs.Value) return;
        Report((now - _last) / 1000.0);
        _last = now;
    }

    private static void Report(double seconds)
    {
        Data frameCounter = null;
        foreach (var d in Stats.Values)
            if (d.Name == "GameState.Update") frameCounter = d;
        if (frameCounter == null || frameCounter.Calls < 1) return;   // not in a match

        double frames = frameCounter.Calls;
        double tickToMs = 1000.0 / Stopwatch.Frequency;
        ModPlugin.L.LogMessage($"[probe] {frames / seconds:F1} fps  ({1000.0 * seconds / frames:F1} ms/frame)");

        foreach (var d in Stats.Values)
        {
            if (d != frameCounter && d.Calls > 0)
            {
                double totalMs = d.Ticks * tickToMs;
                // Plain string first: BepInEx's log-message string handler does not support alignment (",-28").
                string line = $"        {d.Name,-28} {d.Calls / frames,7:F1} calls/frame  {totalMs / frames,6:F2} ms/frame  ({1000.0 * totalMs / d.Calls:F1} us/call)";
                ModPlugin.L.LogMessage(line);
            }
            d.Ticks = 0; d.Calls = 0;
        }
    }
}
