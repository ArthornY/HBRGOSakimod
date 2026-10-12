using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.TickDownDuration))]
internal static class ShadowDanceExtraTurnDurationPatch
{
    private static bool Prefix(PowerModel power, ref Task __result)
    {
        if (!ExtraTurnDurationHelper.ShouldSuppressDurationTick(power))
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }
}
