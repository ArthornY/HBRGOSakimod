using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.addons.mega_text;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
internal static class ThirtyOneXPrankCombatStartPatch
{
    private static void Postfix(ICombatState combatState, ref Task __result) => __result = Finish(__result, combatState);
    private static async Task Finish(Task original, ICombatState combat)
    { await original; ThirtyOneXLanguagePrankHelper.TryBegin(combat); }
}

[HarmonyPatch]
internal static class ThirtyOneXPrankClearPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Hook), nameof(Hook.AfterCombatEnd));
        yield return AccessTools.Method(typeof(CombatManager), nameof(CombatManager.Reset));
        yield return AccessTools.Method(typeof(NGame), nameof(NGame.ReturnToMainMenu));
        yield return AccessTools.Method(typeof(NGame), nameof(NGame.Reset));
        yield return AccessTools.Method(typeof(NGame), nameof(NGame._ExitTree));
    }
    private static void Prefix(MethodBase __originalMethod) => ThirtyOneXLanguagePrankHelper.Clear(
        __originalMethod.DeclaringType == typeof(Hook));
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetFormattedText))]
internal static class ThirtyOneXPrankFormatScopePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(LocString __instance, out ThirtyOneXLanguagePrankHelper.FormatScope __state) =>
        __state = ThirtyOneXLanguagePrankHelper.BeginFormat(__instance);
    private static void Postfix(LocString __instance, string __result, ThirtyOneXLanguagePrankHelper.FormatScope __state)
    {
        try { ThirtyOneXLanguagePrankHelper.CaptureFormatted(__instance, __result, __state); }
        finally { ThirtyOneXLanguagePrankHelper.EndFormat(__state); }
    }
    private static Exception Finalizer(Exception __exception, ThirtyOneXLanguagePrankHelper.FormatScope __state)
    { ThirtyOneXLanguagePrankHelper.EndFormat(__state); return __exception; }
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class ThirtyOneXPrankChineseRawPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Postfix(LocString __instance, ref string __result) => ThirtyOneXLanguagePrankHelper.ChineseRaw(__instance, ref __result);
}

[HarmonyPatch]
internal static class ThirtyOneXPrankDisplayPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var entry in new[] { (typeof(Label), "Text"), (typeof(RichTextLabel), "Text"), (typeof(Button), "Text"),
                     (typeof(Control), "TooltipText"), (typeof(LineEdit), "PlaceholderText"), (typeof(Window), "Title") })
            yield return AccessTools.PropertySetter(entry.Item1, entry.Item2);
        yield return AccessTools.Method(typeof(RichTextLabel), nameof(RichTextLabel.AppendText));
        yield return AccessTools.Method(typeof(RichTextLabel), nameof(RichTextLabel.AddText));
        yield return AccessTools.Method(typeof(RichTextLabel), nameof(RichTextLabel.ParseBbcode));
        // Card descriptions call SetTextAutoSize directly; hover descriptions
        // use MegaRichTextLabel's hidden Text property. Cover the native game
        // boundaries as well as the small Godot binding setter (which callers
        // may inline), before layout and BBCode parsing consume the text.
        yield return typeof(MegaRichTextLabel).GetProperty(nameof(MegaRichTextLabel.Text),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).SetMethod;
        yield return AccessTools.Method(typeof(MegaRichTextLabel), nameof(MegaRichTextLabel.SetTextAutoSize));
    }
    private static void Prefix(GodotObject __instance, MethodBase __originalMethod, ref string __0)
    {
        if (__originalMethod.Name.StartsWith("set_", StringComparison.Ordinal))
            ThirtyOneXLanguagePrankHelper.PreparePropertyDisplay(__instance, ref __0, __originalMethod.Name[4..]);
        else if (__originalMethod.Name is nameof(RichTextLabel.AppendText) or nameof(RichTextLabel.AddText))
            ThirtyOneXLanguagePrankHelper.PrepareAppend((RichTextLabel)__instance, ref __0);
        else ThirtyOneXLanguagePrankHelper.PrepareDisplay(__instance, ref __0);
    }
    private static void Postfix(GodotObject __instance, MethodBase __originalMethod)
    {
        if (__originalMethod.Name is nameof(RichTextLabel.AppendText) or nameof(RichTextLabel.AddText))
            ThirtyOneXLanguagePrankHelper.CompleteAppend((RichTextLabel)__instance);
    }
}

[HarmonyPatch(typeof(RichTextLabel), nameof(RichTextLabel.Clear))]
internal static class ThirtyOneXPrankRichClearPatch
{
    private static void Prefix(RichTextLabel __instance) => ThirtyOneXLanguagePrankHelper.ForgetText(__instance);
}

[HarmonyPatch]
internal static class ThirtyOneXPrankDrawPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        string[] names = { "DrawString", "DrawStringOutline", "DrawMultilineString", "DrawMultilineStringOutline" };
        // Godot4.5 retains independent old terminals without oversampling.
        // Cover both signatures; they share font and text argument positions.
        foreach (MethodInfo method in typeof(CanvasItem).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(method => names.Contains(method.Name)))
            yield return method;
    }
    private static void Prefix(CanvasItem __instance, Font __0, ref string __2)
        => ThirtyOneXLanguagePrankHelper.PrepareDrawText(__instance, __0, ref __2);
}
