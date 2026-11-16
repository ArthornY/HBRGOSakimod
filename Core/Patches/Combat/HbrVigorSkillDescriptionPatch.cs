using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrVigorSkillDescriptionPatch
{
    [ThreadStatic] private static CardHoverScope _cardHover;

    private sealed class CardHoverScope : IDisposable
    {
        internal Player Owner;
        internal CardHoverScope Previous;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (ReferenceEquals(_cardHover, this)) _cardHover = Previous;
        }
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Description));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.SmartDescription));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.RemoteDescription));
    }

    internal static IDisposable BeginCardHoverTips(CardModel card)
    {
        // Even a legacy or canonical nested card gets a scope: it must shadow
        // an outer HBR card rather than inherit its interpretation of Vigor.
        CardHoverScope scope = new()
        {
            Owner = card?.IsMutable == true ? card.Owner : null,
            Previous = _cardHover
        };
        _cardHover = scope;
        return scope;
    }

    // Whether the card whose hover tips are being built belongs to an HBR player. A mod
    // power's canonical tip in those hover tips reads the same scope.
    internal static bool IsHbrCardHover => HbrBattleStatsHelper.IsEnabled(_cardHover?.Owner);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(PowerModel __instance, MethodBase __originalMethod, ref LocString __result)
    {
        if (__instance is not VigorPower) return;
        bool owned = __instance.IsMutable && HbrBattleStatsHelper.IsEnabled(__instance.Owner);
        bool canonicalCardTip = !__instance.IsMutable && HbrBattleStatsHelper.IsEnabled(_cardHover?.Owner);
        if (!owned && !canonicalCardTip) return;

        bool currentAmount = owned && __originalMethod.Name != "get_Description";
        LocString text = new("powers", currentAmount
            ? "VIGOR_POWER.hbrSmartDescription" : "VIGOR_POWER.hbrDescription");
        if (__result != null) text.AddVariablesFrom(__result);
        if (currentAmount)
            text.Add("SkillPercent", HbrNativeEffectSkillHelper.FormatPercent(__instance.Amount));
        __result = text;
    }
}

// The native getter materializes ExtraHoverTips synchronously. Keep the scope
// only around that getter, including failure paths; never infer a global player.
[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class HbrVigorCardHoverDescriptionPatch
{
    private static void Prefix(CardModel __instance, out IDisposable __state) =>
        __state = HbrVigorSkillDescriptionPatch.BeginCardHoverTips(__instance);

    private static void Postfix(IDisposable __state) => __state?.Dispose();

    private static Exception Finalizer(Exception __exception, IDisposable __state)
    {
        __state?.Dispose();
        return __exception;
    }
}
