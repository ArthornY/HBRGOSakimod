using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyEnergyCostInCombat))]
internal static class BoostUpCostPatch
{
    private static void Postfix(CardModel card, ref decimal __result)
    {
        if (card?.Owner?.PlayerCombatState == null || __result <= 0m || card.EnergyCost.CostsX)
            return;

        // The native energy hook visits combat piles but not the permanent deck.
        // Apply each real deck card once, independently of its combat copy's pile
        // or generated duplicates, so exhausting Boost Up cannot disable Raiden.
        foreach (BoostUpHbrgoSaki source in card.Owner.Deck.Cards.OfType<BoostUpHbrgoSaki>().Distinct())
        {
            if (BoostUpFormHelper.TryModifyEnergyCost(source, card, __result, out decimal cost))
                __result = cost;
        }
    }
}
