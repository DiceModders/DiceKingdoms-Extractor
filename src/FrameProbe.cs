using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BepInEx.Configuration;
using DiceModders.ModCore.Tools;

namespace DiceModders.Extractor;

/// <summary>
/// While on, every few seconds the log shows the frame rate and, for the game's heaviest per-frame methods, how often they run per frame and how much
/// time they take. Use it to see where a battle spends its frames, or to check that an optimisation really changes the numbers.
///   UnitVisual.Update   104.0 calls/frame   3.20 ms/frame   (30.8 us/call)
/// The methods are patched the first time the probe is switched on. <c>GameState.Update</c> runs once per frame in a match: it counts the frames and
/// prints the report when the interval is over, so the probe needs no update loop of its own.
/// </summary>
internal static class FrameProbe
{
    private static readonly (string Type, string Method)[] Targets =
    {
        ("UnitVisual", "Update"), ("UnitVisual", "SpawnParticles"), ("ProjectileVisual", "Update"), ("CombatInterface", "Update"),
        ("CombatGrid", "PrepareSimulationStep"), ("CombatGrid", "SimulationStep"), ("PlayerSummary", "Update"),
        ("GameState", "Update"),   // once per frame: the frame counter and the report clock
    };

    private sealed class Data { public string Name; public long Ticks, Calls; }

    private static readonly Dictionary<MethodBase, Data> Stats = new();
    private static Patcher _patcher;
    private static bool _patched, _on;
    private static ConfigEntry<int> _intervalMs;
    private static readonly Stopwatch Clock = new();

    internal static void Bind(ConfigFile config, Patcher patcher)
    {
        _patcher = patcher;
        _intervalMs = config.Bind("Probe", "ReportIntervalMs", 2000, new ConfigDescription("How often the probe prints its report.", new AcceptableValueRange<int>(500, 30000)));
    }

    internal static void Set(bool on)
    {
        if (on && !_patched)
        {
            _patched = true;
            foreach (var (type, method) in Targets) _patcher.Patch(type, method, typeof(FrameProbe), prefix: nameof(Prefix), postfix: nameof(Postfix));
            _patcher.Summary();
        }
        _on = on;
        foreach (var d in Stats.Values) { d.Ticks = 0; d.Calls = 0; }
        Clock.Restart();
        Plugin.L.LogMessage("frame-time probe " + (on ? "on" : "off"));
    }

    internal static void Prefix(out long __state) => __state = _on ? Stopwatch.GetTimestamp() : 0;

    internal static void Postfix(MethodBase __originalMethod, long __state)
    {
        if (__state == 0) return;
        if (!Stats.TryGetValue(__originalMethod, out var d)) Stats[__originalMethod] = d = new Data { Name = __originalMethod.DeclaringType?.Name + "." + __originalMethod.Name };
        d.Ticks += Stopwatch.GetTimestamp() - __state;
        d.Calls++;
        if (d.Name == "GameState.Update" && Clock.ElapsedMilliseconds >= _intervalMs.Value) Report(d);
    }

    private static void Report(Data frames)
    {
        double seconds = Clock.Elapsed.TotalSeconds, n = frames.Calls, toMs = 1000.0 / Stopwatch.Frequency;
        Plugin.L.LogMessage($"[probe] {n / seconds:F1} fps  ({1000.0 * seconds / n:F1} ms/frame)");
        foreach (var d in Stats.Values)
        {
            if (d != frames && d.Calls > 0)
            {
                double total = d.Ticks * toMs;
                string line = $"        {d.Name,-28} {d.Calls / n,7:F1} calls/frame  {total / n,6:F2} ms/frame  ({1000.0 * total / d.Calls:F1} us/call)";
                Plugin.L.LogMessage(line);   // a plain string: BepInEx's log string handler does not support alignment
            }
            d.Ticks = 0; d.Calls = 0;
        }
        Clock.Restart();
    }
}
