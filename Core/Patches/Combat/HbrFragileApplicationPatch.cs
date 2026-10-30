using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.FindExistingInstanceForStacking))]
internal static class HbrFragileStackingPatch
{
    private static bool Prefix(PowerModel basePower, Creature target, ref PowerModel __result)
    {
        if (!HbrPercentDebuffHelper.Supports(basePower)) return true;
        // Canonical lookup precedes owner assignment; the actual recipient
        // selects independent HBR instances versus legacy single-instance ticks.
        if (HbrBattleStatsHelper.IsEnabled(target)) __result = null;
        else if (basePower is FragilePower) __result = target.GetPower<FragilePower>();
        else return true;
        return false;
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply), new[] {
    typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal),
    typeof(Creature), typeof(CardModel), typeof(bool) })]
internal static class HbrFragileApplyPatch
{
    private static bool Prefix(PlayerChoiceContext choiceContext, PowerModel power,
        Creature target, decimal amount, Creature applier, CardModel cardSource, bool silent,
        ref Task __result)
    {
        if (!HbrPercentDebuffHelper.Supports(power) || !HbrBattleStatsHelper.IsEnabled(target)) return true;
        __result = HbrPercentDebuffApplication.Apply(choiceContext, power, target,
            amount, applier, cardSource, silent);
        return false;
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount))]
internal static class HbrFragileCopyPatch
{
    private static bool Prefix(PlayerChoiceContext choiceContext, PowerModel power,
        decimal offset, Creature applier, CardModel cardSource, bool silent, ref Task<int> __result)
    {
        if (!HbrPercentDebuffHelper.Supports(power) || offset <= 0m ||
            !HbrBattleStatsHelper.IsEnabled(power.Owner)) return true;
        __result = HbrPercentDebuffApplication.Copy(choiceContext, power, applier, cardSource, silent);
        return false;
    }
}
