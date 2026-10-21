using System;
using System.IO;
using System.Text;
using BepInEx;
using DiceModders.Shared;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace DiceModders.Extractor;

/// <summary>
/// Writes "id: name | tier=T hp=H | [[dx,dy],...]" per building type to BepInEx/DiceKingdoms_buildings.txt (load it in tools/island-editor.html).
/// BuildingType layout: localPositions int2[] @+0x18, tier @+0x78, maxHealth @+0x80. The in-game island editor reads the same data itself.
/// </summary>
internal static class BuildingDumpFile
{
    private static string OutputPath => Path.Combine(Paths.BepInExRootPath, "DiceKingdoms_buildings.txt");

    public static void Run()
    {
        try
        {
            object types = FindBuildingTypes();
            if (types == null) { ModPlugin.L.LogWarning("[buildings] BuildingTypes not found - join a match (bottom-bar building panels visible) and try again."); return; }
            IntPtr arr = Mem.PtrOf(Reflect.Call(types, "get_Types"));
            if (arr == IntPtr.Zero) { ModPlugin.L.LogWarning("[buildings] get_Types() returned null."); return; }
            int len = Mem.ArrayLength(arr);
            var file = new StringBuilder();
            for (int i = 0; i < len; i++)
            {
                IntPtr bt = Mem.ArrayElem(arr, i);
                file.Append(bt == IntPtr.Zero ? $"{i}: (null)" : Describe(i, bt)).Append("\r\n");
            }
            File.WriteAllText(OutputPath, file.ToString());
            ModPlugin.L.LogMessage($"[buildings] {len} building types written to {OutputPath}");
        }
        catch (Exception e) { ModPlugin.L.LogError("[buildings] dump failed: " + e); }
    }

    private static object FindBuildingTypes()
    {
        var typesType = GameTypes.Find("BuildingTypes");
        if (typesType != null)
        {
            var found = Resources.FindObjectsOfTypeAll(Il2CppType.From(typesType));
            if (found != null && found.Length > 0) return Reflect.Wrap(typesType, Mem.PtrOf(found[0]));
        }
        var rulesType = GameTypes.Find("RulesBuilding");
        if (rulesType != null && typesType != null)
        {
            var found = Resources.FindObjectsOfTypeAll(Il2CppType.From(rulesType));
            if (found != null && found.Length > 0)
            {
                IntPtr bt = Mem.ReadPtr(Mem.PtrOf(found[0]), 0x18);
                if (bt != IntPtr.Zero) return Reflect.Wrap(typesType, bt);
            }
        }
        return null;
    }

    private static string Describe(int index, IntPtr bt)
    {
        var wrapper = Reflect.Wrap(GameTypes.Find("BuildingType"), bt);
        string name = Reflect.Call(wrapper, "get_DisplayName")?.ToString() ?? "?";
        IntPtr shapeArr = Mem.ReadPtr(bt, 0x18);
        var shape = new StringBuilder("[");
        int slen = Mem.ArrayLength(shapeArr);
        for (int j = 0; j < slen; j++)
        {
            shape.Append('[').Append(Mem.ReadInt(shapeArr, 0x20 + j * 8)).Append(',').Append(Mem.ReadInt(shapeArr, 0x20 + j * 8 + 4)).Append(']');
            if (j + 1 < slen) shape.Append(',');
        }
        shape.Append(']');
        return $"{index}: {name} | tier={Mem.ReadInt(bt, 0x78)} hp={Mem.ReadInt(bt, 0x80)} | {shape}";
    }
}
