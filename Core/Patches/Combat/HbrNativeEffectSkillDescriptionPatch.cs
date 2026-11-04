using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrNativeEffectSkillDescriptionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Description));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicDescription));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicEventDescription));
        yield return AccessTools.PropertyGetter(typeof(PotionModel), nameof(PotionModel.DynamicDescription));
        yield return AccessTools.Method(typeof(EventModel), nameof(EventModel.GetOptionDescription));
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(AbstractModel __instance, LocString __result) =>
        HbrNativeEffectSkillHelper.TagDescription(__instance, __result);
}

[HarmonyPatch]
internal static class HbrNativeThornsSkillDescriptionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Description));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.SmartDescription));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.RemoteDescription));
    }

    private static void Postfix(PowerModel __instance, LocString __result)
    {
        if (__instance is ThornsPower && __instance.IsMutable &&
            HbrBattleStatsHelper.IsEnabled(__instance.Owner))
            HbrNativeEffectSkillHelper.TagDescription(__result, "Amount", __instance.Amount);
    }
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class HbrNativeEffectSkillTextPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(LocString __instance, ref string __result) =>
        __result = HbrNativeEffectSkillHelper.AdaptText(__instance, __result);
}

[HarmonyPatch(typeof(NMerchantRelic), "CreateHoverTip")]
internal static class HbrMerchantRelicSkillDescriptionPatch
{
    private static readonly AccessTools.FieldRef<NMerchantRelic, NRelic> RelicNode =
        AccessTools.FieldRefAccess<NMerchantRelic, NRelic>("_relicNode");
    private static readonly Func<NMerchantSlot, Player> Player =
        AccessTools.MethodDelegate<Func<NMerchantSlot, Player>>(
            AccessTools.PropertyGetter(typeof(NMerchantSlot), "Player"));

    private static void Prefix(NMerchantRelic __instance, out IDisposable __state) =>
        __state = HbrNativeEffectSkillHelper.BeginMerchantDisplay(RelicNode(__instance)?.Model, Player(__instance));

    private static void Postfix(IDisposable __state) => __state?.Dispose();

    private static Exception Finalizer(Exception __exception, IDisposable __state)
    {
        __state?.Dispose();
        return __exception;
    }
}
