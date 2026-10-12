using System;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PowerDuplicationBookkeepingHelper
{
    private static readonly Action<TenderPower, int> SetTenderCardsPlayed =
        AccessTools.MethodDelegate<Action<TenderPower, int>>(
            AccessTools.PropertySetter(typeof(TenderPower), "CardsPlayedThisTurn"));

    internal static Action SnapshotCounters(PowerModel power)
    {
        if (power is PhantomTenderBenefitPower benefit)
            return benefit.SnapshotCounterDuplication();

        if (power is not TenderPower tender)
            return null;

        // Tender's Amount is its status stack, while DisplayAmount stores the
        // temporary stat loss that must be refunded at the end of the turn.
        int cardsPlayed = tender.DisplayAmount;
        return () => SetTenderCardsPlayed(tender,
            (int)Math.Min(int.MaxValue, (long)tender.DisplayAmount + cardsPlayed));
    }
}
