using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

// The native generic Apply<T> takes this boundary for its first application,
// then ModifyAmount for an existing single instance. Both paths must implement
// the same max-and-refresh rule rather than native additive stacking.
[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply), new[] {
    typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal),
    typeof(Creature), typeof(CardModel), typeof(bool) })]
internal static class CalamityApplyPatch
{
    private static bool Prefix(PlayerChoiceContext choiceContext, PowerModel power,
        Creature target, decimal amount, Creature applier, CardModel cardSource, bool silent,
        ref Task __result)
    {
        if (power is not HbrCalamityPower calamity) return true;
        __result = CalamityApplication.Apply(choiceContext, calamity, target,
            amount, applier, cardSource, silent);
        return false;
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount))]
internal static class CalamityModifyPatch
{
    private static bool Prefix(PlayerChoiceContext choiceContext, PowerModel power,
        decimal offset, Creature applier, CardModel cardSource, bool silent, ref Task<int> __result)
    {
        if (power is not HbrCalamityPower calamity || offset <= 0m) return true;
        __result = CalamityApplication.Apply(choiceContext, calamity, power.Owner,
            offset, applier, cardSource, silent);
        return false;
    }
}
