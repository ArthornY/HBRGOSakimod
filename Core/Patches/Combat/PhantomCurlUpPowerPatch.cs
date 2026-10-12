using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CurlUpPower), nameof(CurlUpPower.AfterDamageReceived))]
internal static class PhantomCurlUpPowerPatch
{
    [HarmonyPrefix]
    private static bool BeforeAfterDamageReceived(
        CurlUpPower __instance,
        [HarmonyArgument(5)] CardModel cardSource,
        ref Task __result)
    {
        if (__instance.Owner?.HasPower<PhantomPower>() != true ||
            cardSource != null)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
