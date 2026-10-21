using System;
using System.IO;
using System.Text;
using BepInEx;
using DiceModders.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.Extractor;

/// <summary>
/// Right Ctrl + U (Extractor mod): writes the game's whole UI hierarchy (every active-or-not canvas, object names, components, sizes, texts, button labels)
/// to BepInEx/DiceKingdoms_ui.txt. Used to find the game's own "?" / menu buttons and its look, so the editor can hook in exactly.
/// </summary>
internal static class UiProbe
{
    public static string OutputPath => Path.Combine(Paths.BepInExRootPath, "DiceKingdoms_ui.txt");

    public static void Dump()
    {
        try
        {
            var sb = new StringBuilder();
            int canvases = 0;
            sb.AppendLine($"# Dice Kingdoms UI dump, screen {Screen.width}x{Screen.height}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("# format: name [active] (components) rect | text");
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                if (c == null || !c.isRootCanvas) continue;
                canvases++;
                sb.AppendLine();
                sb.AppendLine($"=== CANVAS '{c.name}' mode={c.renderMode} sorting={c.sortingOrder} scale={c.scaleFactor:0.###} active={c.gameObject.activeInHierarchy} ===");
                Walk(c.transform, 0, sb);
            }
            File.WriteAllText(OutputPath, sb.ToString());
            ModPlugin.L.LogMessage($"[ui] UI hierarchy of {canvases} canvas(es) written to {OutputPath}");
        }
        catch (Exception e) { ModPlugin.L.LogError("[ui] dump failed: " + e); }
    }

    private static void Walk(Transform t, int depth, StringBuilder sb)
    {
        if (depth > 14) { sb.AppendLine(new string(' ', depth * 2) + "..."); return; }
        var go = t.gameObject;
        var sbl = new StringBuilder();
        sbl.Append(new string(' ', depth * 2)).Append(go.name).Append(go.activeSelf ? "" : " [inactive]");

        string text = null;
        var comps = new StringBuilder();
        try
        {
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                string n = UiAssetDump.ClassNameOf(comp);
                if (n == "Transform" || n == "CanvasRenderer") continue;
                if (comps.Length > 0) comps.Append(',');
                comps.Append(n);
                if (text == null && Reflect.TryGetMember(comp, "text", out object v) && v is string s && s.Length > 0) text = s;
            }
        }
        catch (Exception) { }
        if (comps.Length > 0) sbl.Append(" (").Append(comps).Append(')');

        var rt = t.TryCast<RectTransform>();
        if (rt != null)
        {
            var p = rt.anchoredPosition; var sz = rt.sizeDelta;
            sbl.Append($" pos={p.x:0},{p.y:0} size={sz.x:0}x{sz.y:0} anchors=({rt.anchorMin.x:0.##},{rt.anchorMin.y:0.##})-({rt.anchorMax.x:0.##},{rt.anchorMax.y:0.##})");
        }
        if (text != null) sbl.Append(" | ").Append(text.Replace("\n", "\\n").Replace("\r", ""));
        sb.AppendLine(sbl.ToString());

        for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), depth + 1, sb);
    }
}
