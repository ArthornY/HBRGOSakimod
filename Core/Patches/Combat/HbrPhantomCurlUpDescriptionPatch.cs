using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.Description), MethodType.Getter)]
internal static class HbrPhantomCurlUpDescriptionPatch
{
    private static void Postfix(PowerModel __instance, ref LocString __result)
    {
        if (__instance is not CurlUpPower || !__instance.IsMutable ||
            __instance.Owner?.HasPower<PhantomPower>() != true ||
            !HbrBattleStatsHelper.IsEnabled(__instance.Owner)) return;
        __result = HbrSkillBlockDescription.ForPower(__instance, __result,
            "CURL_UP_POWER.hbrDescription", __instance.Amount);
    }
}
