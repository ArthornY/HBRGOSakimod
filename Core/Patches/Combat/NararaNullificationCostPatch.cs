using System;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyCostInCombat))]
internal static class NararaNullificationCostPatch
{
    // Apply after native free/discount effects and the normal-priority Boost Up
    // postfix. This is a current-turn surcharge, including on a zero-cost card.
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardModel card, decimal originalCost, ref decimal __result)
    {
        if (originalCost < 0m || card?.EnergyCost.CostsX != false ||
            !NararaNullificationPenaltyHelper.IsPenalized(card))
            return;

        __result = Math.Min(int.MaxValue, Math.Max(0m, __result) + 1m);
    }
}
