using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.FindExistingInstanceForStacking))]
internal static class HbrEffectInstanceStackingPatch
{
    private static bool Prefix(PowerModel basePower, Creature target, ref PowerModel __result)
    {
        if (basePower is not HbrEffectPower || !HbrBattleStatsHelper.IsEnabled(target)) return true;
        // Native generic callers must reach the common application boundary,
        // not merge an earlier copy from the same caster before slot selection.
        __result = null;
        return false;
    }
}
