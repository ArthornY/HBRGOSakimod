using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyGoldGained))]
internal static class IchikoMilitaryPayPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(decimal amount, Player player, ref decimal __result)
    {
        // The manual applies military pay itself. Only a character without
        // that listener needs this fallback.
        if (amount > 0m && player?.GetRelic<ElectronicSoldierManual>() == null)
            __result = DamageAmplificationLimits.AddSaturated(__result,
                amount * IchikoEldestCombatHelper.GetGoldBonusPercent(player) / 100m);
    }
}
