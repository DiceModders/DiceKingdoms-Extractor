using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx;
using DiceModders.Shared;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceModders.Extractor;

/// <summary>
/// Right Ctrl + I (Extractor mod): writes BepInEx/DiceKingdoms_ui_assets.txt - a deep dump of the game's UI so native look-and-feel can be reused:
///  * every object under the game's canvases with its REAL component class names (the plain hierarchy dump only shows "Component"),
///  * Image sprites (name, 9-slice border, pixels per unit, type, colour, material), TMP fonts / sizes / colours, Button transitions and click targets,
///  * all fields of the game's own components (BaseButton, ButtonIcon, ButtonLabel, panels ...) via reflection,
///  * an inventory of every loaded Sprite and TMP font asset.
/// Everything is reflection based on purpose (no compile-time dependency on version-specific members) and wrapped in try/catch.
/// </summary>
internal static class UiAssetDump
{
    public static string OutputPath => Path.Combine(Paths.BepInExRootPath, "DiceKingdoms_ui_assets.txt");

    private static MethodInfo _getClass, _className, _classNs;
    private static bool _nativeInit;
    private static readonly Dictionary<string, Type> TypeCache = new();
    private const int MaxChildren = 40;

    private static readonly HashSet<string> Deny = new()
    {
        "gameObject", "transform", "parent", "root", "hideFlags", "tag", "name", "canvas", "rootCanvas", "mesh", "meshes", "sharedMesh",
        "materials", "sharedMaterials", "rectTransform", "canvasRenderer", "onClick", "isActiveAndEnabled", "runInEditMode", "useGUILayout",
        "enabled", "Pointer", "WasCollected", "ObjectClass", "isStatic",
    };

    private static readonly Dictionary<string, string[]> Curated = new()
    {
        ["Image"] = new[] { "sprite", "overrideSprite", "type", "color", "material", "preserveAspect", "fillCenter", "pixelsPerUnitMultiplier", "raycastTarget" },
        ["RawImage"] = new[] { "texture", "color", "uvRect", "material", "raycastTarget" },
        ["Button"] = new[] { "interactable", "transition", "colors", "spriteState", "targetGraphic" },
        ["Toggle"] = new[] { "interactable", "transition", "isOn", "targetGraphic", "graphic" },
        ["Slider"] = new[] { "interactable", "minValue", "maxValue", "value", "targetGraphic" },
        ["TextMeshProUGUI"] = new[] { "text", "font", "fontSize", "color", "alignment", "fontStyle", "fontSharedMaterial", "enableAutoSizing", "characterSpacing", "lineSpacing", "raycastTarget" },
        ["CanvasScaler"] = new[] { "uiScaleMode", "referenceResolution", "screenMatchMode", "matchWidthOrHeight", "scaleFactor" },
        ["Animator"] = new[] { "runtimeAnimatorController" },
        ["HorizontalLayoutGroup"] = new[] { "padding", "spacing", "childAlignment" },
        ["VerticalLayoutGroup"] = new[] { "padding", "spacing", "childAlignment" },
        ["GridLayoutGroup"] = new[] { "padding", "cellSize", "spacing" },
        ["LayoutElement"] = new[] { "minWidth", "minHeight", "preferredWidth", "preferredHeight" },
    };

    // ---- native class names -------------------------------------------------------------------------------------------

    private static void InitNative()
    {
        if (_nativeInit) return;
        _nativeInit = true;
        try
        {
            var t = typeof(IL2CPP);
            _getClass = t.GetMethod("il2cpp_object_get_class", new[] { typeof(IntPtr) });
            _className = t.GetMethod("il2cpp_class_get_name", new[] { typeof(IntPtr) });
            _classNs = t.GetMethod("il2cpp_class_get_namespace", new[] { typeof(IntPtr) });
        }
        catch (Exception e) { ModPlugin.L.LogWarning("[uidump] native class lookup unavailable: " + e.Message); }
    }

    /// <summary>The real il2cpp class name of an object ("Image", "ButtonIcon" ...), not the declared wrapper type.</summary>
    public static string ClassNameOf(Il2CppObjectBase o) => ClassNameOf(o, out _);

    public static string ClassNameOf(Il2CppObjectBase o, out string ns)
    {
        ns = "";
        InitNative();
        try
        {
            if (_getClass != null && _className != null && o != null)
            {
                var klass = (IntPtr)_getClass.Invoke(null, new object[] { o.Pointer });
                if (klass != IntPtr.Zero)
                {
                    string name = Marshal.PtrToStringAnsi((IntPtr)_className.Invoke(null, new object[] { klass }));
                    if (_classNs != null) ns = Marshal.PtrToStringAnsi((IntPtr)_classNs.Invoke(null, new object[] { klass })) ?? "";
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
        }
        catch (Exception) { }
        return o?.GetType().Name ?? "null";
    }

    private static Type FindWrapperType(string ns, string name)
    {
        string full = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
        if (TypeCache.TryGetValue(full, out var cached)) return cached;
        Type found = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { found = asm.GetType(full, false); } catch (Exception) { }
            if (found != null) break;
        }
        TypeCache[full] = found;
        return found;
    }

    /// <summary>Re-wraps a component as its real (most derived) interop type so its fields can be read by reflection.</summary>
    private static object Upcast(Il2CppObjectBase o, out string className, out string ns)
    {
        className = ClassNameOf(o, out ns);
        try
        {
            var t = FindWrapperType(ns, className);
            if (t != null && t.IsInstanceOfType(o)) return o;
            if (t != null) return Activator.CreateInstance(t, new object[] { o.Pointer });
        }
        catch (Exception) { }
        return o;
    }

    // ---- formatting ------------------------------------------------------------------------------------------------------

    private static string Fmt(object v)
    {
        if (v == null) return "null";
        if (v is string s) return "\"" + (s.Length > 70 ? s.Substring(0, 70) + "..." : s).Replace("\n", "\\n").Replace("\r", "") + "\"";
        var t = v.GetType();
        if (t.IsPrimitive || t.IsEnum || v is decimal) return v.ToString();
        if (v is UnityEngine.Object uo)
        {
            string n; try { n = uo.name; } catch (Exception) { n = "?"; }
            return ClassNameOf(uo) + ":" + n;
        }
        if (t.IsValueType) return v.ToString();
        return t.Name;
    }

    private static bool IsGameType(Type t)
    {
        try { return t != null && t.Assembly.GetName().Name.StartsWith("Assembly-CSharp", StringComparison.Ordinal); }
        catch (Exception) { return false; }
    }

    private static void DumpProps(object o, string indent, int depth, StringBuilder sb, HashSet<string> seen)
    {
        if (o == null) return;
        Type t = o.GetType();
        var props = new List<PropertyInfo>();
        if (Curated.TryGetValue(t.Name, out var names))
        {
            foreach (var n in names) { var p = t.GetProperty(n); if (p != null) props.Add(p); }
        }
        else if (IsGameType(t))
        {
            for (var c = t; c != null && IsGameType(c); c = c.BaseType)
                props.AddRange(c.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }
        foreach (var p in props)
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0 || Deny.Contains(p.Name)) continue;
            object v;
            try { v = p.GetValue(o); } catch (Exception) { continue; }
            sb.Append(indent).Append(p.Name).Append(" = ").AppendLine(Fmt(v));
            if (depth > 0 && v is UnityEngine.Object uo && IsGameType(v.GetType()))
            {
                string key = uo.Pointer.ToString();
                if (seen.Add(key)) DumpProps(v, indent + "  ", depth - 1, sb, seen);
            }
        }
    }

    private static void DumpButtonEvents(object button, string indent, StringBuilder sb)
    {
        try
        {
            if (!Reflect.TryGetMember(button, "onClick", out object ev) || ev == null) return;
            var tt = ev.GetType();
            int n = (int)tt.GetMethod("GetPersistentEventCount").Invoke(ev, null);
            sb.Append(indent).Append("onClick persistent listeners = ").AppendLine(n.ToString());
            for (int i = 0; i < n; i++)
            {
                object target = tt.GetMethod("GetPersistentTarget").Invoke(ev, new object[] { i });
                object method = tt.GetMethod("GetPersistentMethodName").Invoke(ev, new object[] { i });
                sb.Append(indent).Append("  [").Append(i).Append("] ").Append(Fmt(target)).Append(" . ").AppendLine(Convert.ToString(method));
            }
        }
        catch (Exception) { }
    }

    // ---- hierarchy -------------------------------------------------------------------------------------------------------

    private static readonly HashSet<string> SkipComponents = new() { "Transform", "RectTransform", "CanvasRenderer" };

    private static void Walk(Transform t, int depth, StringBuilder sb)
    {
        string pad = new string(' ', depth * 2);
        if (depth > 16) { sb.AppendLine(pad + "..."); return; }
        var go = t.gameObject;
        var header = new StringBuilder();
        header.Append(pad).Append("- ").Append(go.name).Append(go.activeSelf ? "" : " [inactive]");
        var rt = t.TryCast<RectTransform>();
        if (rt != null)
        {
            var p = rt.anchoredPosition; var sz = rt.sizeDelta;
            header.Append($"  pos={p.x:0.#},{p.y:0.#} size={sz.x:0.#}x{sz.y:0.#} anchors=({rt.anchorMin.x:0.##},{rt.anchorMin.y:0.##})-({rt.anchorMax.x:0.##},{rt.anchorMax.y:0.##}) pivot=({rt.pivot.x:0.##},{rt.pivot.y:0.##})");
        }
        sb.AppendLine(header.ToString());

        try
        {
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                object real = Upcast(comp, out string cn, out string ns);
                if (SkipComponents.Contains(cn)) continue;
                sb.Append(pad).Append("    * ").Append(string.IsNullOrEmpty(ns) ? cn : ns + "." + cn).AppendLine();
                DumpProps(real, pad + "        ", 1, sb, new HashSet<string>());
                if (cn == "Button") DumpButtonEvents(real, pad + "        ", sb);
            }
        }
        catch (Exception e) { sb.AppendLine(pad + "    (components failed: " + e.Message + ")"); }

        int count = t.childCount;
        for (int i = 0; i < count && i < MaxChildren; i++) Walk(t.GetChild(i), depth + 1, sb);
        if (count > MaxChildren) sb.AppendLine(pad + "  ... " + (count - MaxChildren) + " more children not listed");
    }

    // ---- inventories -----------------------------------------------------------------------------------------------------

    private static void DumpSprites(StringBuilder sb)
    {
        sb.AppendLine().AppendLine("##### ALL LOADED SPRITES: name | texture | rect w x h | border (l,b,r,t) | ppu");
        try
        {
            var all = Resources.FindObjectsOfTypeAll(Il2CppType.From(typeof(Sprite)));
            int n = 0;
            foreach (var o in all)
            {
                var s = o?.TryCast<Sprite>();
                if (s == null) continue;
                n++;
                string tex = "?", rect = "?", border = "?", ppu = "?";
                try { tex = s.texture != null ? s.texture.name : "null"; } catch (Exception) { }
                try { var r = s.rect; rect = $"{r.width:0}x{r.height:0}"; } catch (Exception) { }
                try { border = s.border.ToString(); } catch (Exception) { }
                try { ppu = s.pixelsPerUnit.ToString("0.#"); } catch (Exception) { }
                sb.AppendLine($"{s.name} | {tex} | {rect} | {border} | {ppu}");
            }
            sb.AppendLine("# " + n + " sprites");
        }
        catch (Exception e) { sb.AppendLine("(sprite inventory failed: " + e.Message + ")"); }

        sb.AppendLine().AppendLine("##### TMP FONT ASSETS");
        try
        {
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.From(typeof(TMP_FontAsset))))
            {
                var f = o?.TryCast<TMP_FontAsset>();
                if (f != null) sb.AppendLine(f.name);
            }
        }
        catch (Exception e) { sb.AppendLine("(font inventory failed: " + e.Message + ")"); }
    }

    public static void Run()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Dice Kingdoms UI asset dump, screen {Screen.width}x{Screen.height}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("# '* Class' lines list a component with its readable fields; sprites: name, 9-slice border, ppu.");
            int canvases = 0;
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                if (c == null || !c.isRootCanvas || c.name.Contains("Graphy") || c.name.Contains("DiceModders")) continue;
                canvases++;
                sb.AppendLine().AppendLine($"=== CANVAS '{c.name}' mode={c.renderMode} sorting={c.sortingOrder} scale={c.scaleFactor:0.###} active={c.gameObject.activeInHierarchy} ===");
                Walk(c.transform, 0, sb);
            }
            DumpSprites(sb);
            File.WriteAllText(OutputPath, sb.ToString());
            ModPlugin.L.LogMessage($"[uidump] {canvases} canvas(es) + sprite/font inventory written to {OutputPath} ({sb.Length / 1024} KB)");
        }
        catch (Exception e) { ModPlugin.L.LogError("[uidump] failed: " + e); }
    }
}
