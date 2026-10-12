using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Helpers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(AscensionHelper))]
internal static class PhantomMagiKnightAscensionPatch
{
    [HarmonyPatch(
        nameof(AscensionHelper.GetValueIfAscension),
        typeof(AscensionLevel),
        typeof(int),
        typeof(int))]
    [HarmonyPrefix]
    private static bool BeforeIntValue(
        AscensionLevel level,
        int fallbackValue,
        ref int __result)
    {
        if (!PhantomAscensionHelper.ShouldSuppressEnemyAscension(level))
        {
            return true;
        }

        __result = fallbackValue;
        return false;
    }

    [HarmonyPatch(
        nameof(AscensionHelper.GetValueIfAscension),
        typeof(AscensionLevel),
        typeof(float),
        typeof(float))]
    [HarmonyPrefix]
    private static bool BeforeFloatValue(
        AscensionLevel level,
        float fallbackValue,
        ref float __result)
    {
        if (!PhantomAscensionHelper.ShouldSuppressEnemyAscension(level))
        {
            return true;
        }

        __result = fallbackValue;
        return false;
    }

    [HarmonyPatch(
        nameof(AscensionHelper.GetValueIfAscension),
        typeof(AscensionLevel),
        typeof(decimal),
        typeof(decimal))]
    [HarmonyPrefix]
    private static bool BeforeDecimalValue(
        AscensionLevel level,
        decimal fallbackValue,
        ref decimal __result)
    {
        if (!PhantomAscensionHelper.ShouldSuppressEnemyAscension(level))
        {
            return true;
        }

        __result = fallbackValue;
        return false;
    }
}
