using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.FindExistingInstanceForStacking))]
internal static class DamageAmplificationStackingPatch
{
    private static bool Prefix(PowerModel basePower, Creature target, ref PowerModel __result)
    {
        if (basePower is not HotaruDamageAmplificationPower) return true;

        // Legacy instances retain their IDs for saved data and combat snapshots,
        // but further applications belong to the same percentage bank.
        __result = null;
        foreach (PowerModel power in target.Powers)
        {
            if (power is not HotaruDamageAmplificationPower) continue;
            __result = power;
            break;
        }
        return false;
    }
}
