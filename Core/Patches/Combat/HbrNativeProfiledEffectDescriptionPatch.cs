using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrNativeProfiledEffectDescriptionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Description));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.SmartDescription));
        yield return AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.RemoteDescription));
        yield return AccessTools.PropertyGetter(typeof(OrbModel), nameof(OrbModel.Description));
        yield return AccessTools.PropertyGetter(typeof(OrbModel), nameof(OrbModel.SmartDescription));
    }

    private static void Postfix(AbstractModel __instance, LocString __result)
    {
        if (HbrNativeDamageProfiles.IsSkillSource(__instance) || __instance is SkittishPower)
            HbrNativeEffectSkillHelper.TagDescription(__instance, __result);
    }
}
