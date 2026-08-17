using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class DualSwordsCopyHelper
{
    private static readonly Dictionary<CardModel, int> XEnergySpentByCopy = new();
    private static readonly ConditionalWeakTable<CardModel, CopyMarker>
        DualSwordsCopies = new();

    public static void RecordCopy(CardModel card)
    {
        if (card != null)
        {
            DualSwordsCopies.GetValue(
                card,
                static _ => new CopyMarker());
        }
    }

    public static bool IsDualSwordsCopy(CardModel card)
    {
        return card != null && DualSwordsCopies.TryGetValue(card, out _);
    }

    public static void RecordXEnergySpent(CardModel card, int energySpent)
    {
        if (card == null || energySpent <= 0)
        {
            return;
        }

        XEnergySpentByCopy[card] = energySpent;
    }

    public static int TakeXEnergySpent(CardModel card, int fallback)
    {
        if (card == null || !XEnergySpentByCopy.Remove(card, out int energySpent))
        {
            return fallback;
        }

        return energySpent;
    }

    public static void Clear(CardModel card)
    {
        if (card == null)
        {
            return;
        }

        XEnergySpentByCopy.Remove(card);
        DualSwordsCopies.Remove(card);
    }

    private sealed class CopyMarker
    {
    }
}
