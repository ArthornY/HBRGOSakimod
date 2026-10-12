using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyCostInCombat))]
internal static class LostMelodyCostPatch
{
    // Narara's surcharge uses Priority.Last (0). Harmony reserves -1 for an
    // unspecified priority, so use -2 to run strictly after its final surcharge.
    [HarmonyPriority(Priority.Last - 2)]
    private static void Postfix(CardModel card, decimal originalCost, ref decimal __result)
    {
        if (originalCost < 0m ||
            card?.Owner?.Creature?.GetPower<LostMelodyPower>() is not { } melody)
            return;

        if (melody.TryReplaceFinalEnergyCost(card, __result, out decimal cost))
            __result = cost;
    }
}
