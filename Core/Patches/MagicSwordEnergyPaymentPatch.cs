using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.HasEnoughResourcesFor))]
internal static class PlayerCombatStateHasEnoughResourcesForPatch
{
    private static void Postfix(CardModel card, ref UnplayableReason reason, ref bool __result)
    {
        if (!reason.HasFlag(UnplayableReason.EnergyCostTooHigh))
        {
            return;
        }

        if (!MagicSwordEnergyPaymentHelper.CanPayWithSwordheart(card, out _, out _, out _))
        {
            return;
        }

        reason &= ~UnplayableReason.EnergyCostTooHigh;
        __result = reason == UnplayableReason.None;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.SpendResources))]
internal static class CardModelSpendResourcesPatch
{
    private static bool Prefix(CardModel __instance, ref Task<ValueTuple<int, int>> __result)
    {
        // Keep the displayed half-price decision stable across energy-spent
        // hooks and the later mode switch/native replay series.
        DualSwordsChaseHelper.RecordPayment(__instance);
        if (!MagicSwordEnergyPaymentHelper.TryGetPaymentQuote(__instance, out SwordheartPaymentQuote quote) || !quote.CanAfford)
        {
            return true;
        }

        __result = SpendResourcesWithSwordheart(__instance, quote);
        return false;
    }

    private static void Postfix(CardModel __instance, ref Task<ValueTuple<int, int>> __result) =>
        __result = DualSwordsChaseHelper.KeepPaymentUnlessFailed(__instance, __result);

    private static async Task<ValueTuple<int, int>> SpendResourcesWithSwordheart(CardModel card, SwordheartPaymentQuote quote)
    {
        int energyToSpend = quote.EnergyToSpend;
        int starsToSpend = quote.StarsToSpend;
        if (card.Owner.Creature.HasPower<MagicSwordModePower>())
        {
            // The existing mode settles this once in BeforeCardPlayed, including
            // clarity's consumption multiplier and the mode's exit behaviour.
            MagicSwordEnergyPaymentHelper.RecordPendingSwordheartCost(card, quote.EnergyDeficit);
        }
        else
        {
            // Universality pays immediately: it must not depend on a mode hook
            // being present later. Clarity doubles this same quoted cost.
            MagicSwordEnergyPaymentHelper.RecordPendingSwordheartCost(card, 0);
            await PowerCmd.ModifyAmount(
                new ThrowingPlayerChoiceContext(),
                card.Owner.Creature.GetPower<SwordheartPower>(),
                -quote.SwordheartToSpend,
                card.Owner.Creature,
                card);
        }

        if (energyToSpend > 0)
        {
            CombatManager.Instance.History.EnergySpent(card.CombatState, energyToSpend, card.Owner);
            card.Owner.PlayerCombatState.LoseEnergy(energyToSpend);
        }

        await Hook.AfterEnergySpent(card.CombatState, card, energyToSpend);

        card.LastStarsSpent = starsToSpend;
        if (starsToSpend > 0)
        {
            card.Owner.PlayerCombatState.LoseStars(starsToSpend);
            await Hook.AfterStarsSpent(card.Owner.Creature.CombatState, starsToSpend, card.Owner);
        }

        return new ValueTuple<int, int>(energyToSpend, starsToSpend);
    }
}
