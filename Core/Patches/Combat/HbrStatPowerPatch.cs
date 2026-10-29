using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrStrengthAttributePatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(StrengthPower))
        .Where(method => method.Name == nameof(StrengthPower.ModifyDamageAdditive))
        .OrderByDescending(method => method.GetParameters().Length).First();

    private static bool Prefix(StrengthPower __instance, ref decimal __result)
    {
        if (!HbrBattleStatsHelper.IsEnabled(__instance.Owner)) return true;
        __result = 0m;
        return false;
    }
}

[HarmonyPatch]
internal static class HbrDexterityAttributePatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(DexterityPower))
        .Where(method => method.Name == nameof(DexterityPower.ModifyBlockAdditive))
        .OrderByDescending(method => method.GetParameters().Length).First();

    private static bool Prefix(DexterityPower __instance, ref decimal __result)
    {
        if (!HbrBattleStatsHelper.IsEnabled(__instance.Owner)) return true;
        __result = 0m;
        return false;
    }
}
