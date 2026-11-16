using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class ThirtyOneACutinDescriptionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicDescription));
        yield return AccessTools.PropertyGetter(typeof(RelicModel), nameof(RelicModel.DynamicEventDescription));
    }
    private static void Postfix(RelicModel __instance, ref LocString __result)
    {
        if (__instance is not ThirtyOneACollection { IsMutable: true } relic || __result == null ||
            !HbrBattleStatsHelper.IsEnabled(relic.Owner)) return;
        int count = relic.DisplayAmount;
        __result.Add("Count", count);
        __result.Add("CutinCount", ThirtyOneACutinCombatHelper.CutinCount(count));
        __result.Add("NextTier", count < 2 ? "2种：2人" : count < 4 ? "4种：3人" : count < 6 ? "6种：6人和Live" : "已达到最高档");
        __result.Add("LivePending", relic.PendingLive ? "下一场开局士气+2。" : string.Empty);
    }
}
