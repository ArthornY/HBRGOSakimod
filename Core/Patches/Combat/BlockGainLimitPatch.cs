using System;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

// CreatureCmd.GainBlock caps the creature at 999,999,999 Block in GainBlockInternal, then
// records the gain in combat history as (int)amount. Stack-scaled block (Intelligence from
// Dexterity, percentage block boosts) can pass int range once Double Powers repeats under a
// replay, and that cast then faults the whole card play. No single gain can add more than
// the cap, so every source keeps it; LargeHealthSacrificeBlockBoundaryPatch did this for
// Sacrifice alone.
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyBlock))]
internal static class BlockGainLimitPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref decimal __result) =>
        __result = Math.Min(__result, BigCombatNumbers.NativeStatCap);
}
