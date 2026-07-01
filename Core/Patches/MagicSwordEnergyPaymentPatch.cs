using System;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;

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
        if (!MagicSwordEnergyPaymentHelper.CanPayWithSwordheart(__instance, out int deficit, out int energyToSpend, out int starsToSpend))
        {
            return true;
        }

        __result = SpendResourcesWithSwordheart(__instance, deficit, energyToSpend, starsToSpend);
        return false;
    }

    private static async Task<ValueTuple<int, int>> SpendResourcesWithSwordheart(CardModel card, int deficit, int energyToSpend, int starsToSpend)
    {
        MagicSwordEnergyPaymentHelper.RecordPendingSwordheartCost(card, deficit);

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

[HarmonyPatch(typeof(NHandCardHolder), nameof(NHandCardHolder.UpdateCard))]
internal static class NHandCardHolderUpdateCardPatch
{
    private static readonly Color SupplementGlowColor = new(1f, 0.86f, 0.32f, 0.98f);

    private static void Postfix(NHandCardHolder __instance)
    {
        CardModel card = __instance.CardNode?.Model;
        if (card == null)
        {
            return;
        }

        if (!MagicSwordEnergyPaymentHelper.CanPayWithSwordheart(card, out _, out _, out _))
        {
            return;
        }

        if (!CanPlayIgnoringSwordheartEnergyDeficit(card))
        {
            return;
        }

        __instance.CardNode.CardHighlight.AnimShow();
        __instance.CardNode.CardHighlight.Modulate = SupplementGlowColor;
    }

    private static bool CanPlayIgnoringSwordheartEnergyDeficit(CardModel card)
    {
        if (card.CanPlay(out UnplayableReason reason, out _))
        {
            return true;
        }

        return (reason & ~UnplayableReason.EnergyCostTooHigh) == UnplayableReason.None;
    }
}
