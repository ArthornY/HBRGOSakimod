using System;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace HBRGOSakiMod.Core.Patches;

// Every localization string reads its raw text here before SmartFormat fills
// in the variables. Outside a compact hover scope this is a single
// thread-static read; inside, listed entries read their short text instead and
// are formatted with the full text's variables. (SmartFormat itself has an
// exception filter that Harmony cannot rebuild, so the swap happens here.)
[HarmonyPatch(typeof(LocTable), nameof(LocTable.GetRawText), new[] { typeof(string) })]
internal static class HbrCompactTextFormatPatch
{
    private static readonly FieldInfo NameField = AccessTools.Field(typeof(LocTable), "_name");

    private static bool Prefix(LocTable __instance, string key, ref string __result)
    {
        if (!HbrCompactText.IsActive || NameField?.GetValue(__instance) is not string table ||
            !HbrCompactText.TryGetCompactKey(table, key, __instance.IsLocalKey, out string compact))
            return true;
        // The short key is not listed, so this call reads it as written.
        __result = __instance.GetRawText(compact);
        return false;
    }
}

// The game and RitsuLib cache each keyword tooltip the first time it is built,
// and reuse it everywhere. Build it with the full text even inside a compact
// hover, ahead of RitsuLib's own prefix (priority 800) that builds mod keywords.
[HarmonyPatch(typeof(HoverTipFactory), nameof(HoverTipFactory.FromKeyword))]
[HarmonyPriority(900)]
internal static class HbrCompactKeywordCachePatch
{
    private static void Prefix(out bool __state) => __state = HbrCompactText.Suspend();

    private static Exception Finalizer(Exception __exception, bool __state)
    {
        if (__state) HbrCompactText.Resume();
        return __exception;
    }
}

// Current tier, charges and points for the short top-bar relic texts. Runs
// after the HBR description swap so the variables reach the final text.
[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.DynamicDescription), MethodType.Getter)]
[HarmonyPriority(Priority.Last)]
internal static class HbrCompactRelicDescriptionPatch
{
    private static void Postfix(RelicModel __instance, LocString __result) =>
        HbrCompactRelicHoverTips.AddVariables(__instance, __result);
}

// Tooltips only exist while something is hovered, so their frame tick is
// where the full-text key is watched.
[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet._Process))]
internal static class HbrHoverDetailPollPatch
{
    private static void Postfix() => HbrHoverDetail.Poll();
}
