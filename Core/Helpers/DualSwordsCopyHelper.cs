using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class DualSwordsCopyHelper
{
    private static readonly Dictionary<CardModel, int> XEnergySpentByCopy = new();
    private static readonly HashSet<CardModel> DualSwordsCopies = new();

    public static void RecordCopy(CardModel card)
    {
        if (card != null)
        {
            DualSwordsCopies.Add(card);
        }
    }

    public static bool IsDualSwordsCopy(CardModel card)
    {
        return card != null && DualSwordsCopies.Contains(card);
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
}
