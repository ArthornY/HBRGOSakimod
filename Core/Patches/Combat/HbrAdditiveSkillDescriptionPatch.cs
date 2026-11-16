using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrAdditiveSkillDescriptionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Description));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicDescription));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicEventDescription));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Description));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.SmartDescription));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.RemoteDescription));
        yield return AccessTools.PropertyGetter(typeof(AfflictionModel), nameof(AfflictionModel.DynamicDescription));
        yield return AccessTools.PropertyGetter(typeof(AfflictionModel), nameof(AfflictionModel.DynamicExtraCardText));
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(AbstractModel __instance, MethodBase __originalMethod, ref LocString __result) =>
        __result = HbrAdditiveSkillDescription.Adapt(__instance, __result,
            __originalMethod.Name == "get_DynamicExtraCardText");
}

[HarmonyPatch]
internal static class HbrAdditiveSkillHoverScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.HoverTips));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.HoverTips));
        yield return AccessTools.PropertyGetter(typeof(AfflictionModel), nameof(AfflictionModel.HoverTips));
    }

    private static void Prefix(AbstractModel __instance, out IDisposable __state) =>
        __state = HbrAdditiveSkillDescription.BeginHover(__instance);

    private static void Postfix(IDisposable __state) => __state?.Dispose();

    private static Exception Finalizer(Exception __exception, IDisposable __state)
    {
        __state?.Dispose();
        return __exception;
    }
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class HbrAdditiveSkillTextPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(LocString __instance, ref string __result) =>
        __result = HbrAdditiveSkillDescription.AdaptText(__instance, __result);
}
