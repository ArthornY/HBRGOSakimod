using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(ImbalancedPower), nameof(ImbalancedPower.AfterDamageGiven))]
internal static class PhantomOneShotImbalancedPatch
{
    [HarmonyPostfix]
    private static void AfterDamageGiven(
        ImbalancedPower __instance,
        Creature dealer,
        DamageResult result,
        ref Task __result)
    {
        if (__instance.Applier?.HasPower<PhantomPower>() != true ||
            dealer != __instance.Owner ||
            !result.WasFullyBlocked)
        {
            return;
        }

        __result = RemoveAfterTrigger(__instance, __result);
    }

    private static async Task RemoveAfterTrigger(
        ImbalancedPower power,
        Task originalTask)
    {
        await originalTask;
        if (power.Owner?.GetPower<ImbalancedPower>() == power)
        {
            await PowerCmd.Remove(power);
        }
    }
}
