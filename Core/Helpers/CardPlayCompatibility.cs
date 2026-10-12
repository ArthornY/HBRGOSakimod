using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CardPlayCompatibility
{
    private static readonly PropertyInfo ResultPileProperty =
        typeof(CardPlay).GetProperty(
            nameof(CardPlay.ResultPile),
            BindingFlags.Instance | BindingFlags.Public) ??
        throw new MissingMemberException(
            typeof(CardPlay).FullName,
            nameof(CardPlay.ResultPile));

    internal static void SetResultPile(
        CardPlay cardPlay,
        PileType pileType)
    {
        ResultPileProperty.SetValue(cardPlay, pileType);
    }
}
