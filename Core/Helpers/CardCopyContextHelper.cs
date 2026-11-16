using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Replay-created cards retain their own X payment and copy provenance. Weapon
// forms no longer produce copies; Recall, Imitate and NotOverYet still do.
internal static class CardCopyContextHelper
{
    private static readonly Dictionary<CardModel, int> XEnergySpentByCopy = new();
    private static readonly ConditionalWeakTable<CardModel, CopyMarker>
        Copies = new();

    public static void RecordCopy(CardModel card)
    {
        if (card != null)
        {
            Copies.GetValue(
                card,
                static _ => new CopyMarker());
        }
    }

    public static bool IsMarkedCopy(CardModel card)
    {
        return card != null && Copies.TryGetValue(card, out _);
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

    internal static int PeekXEnergySpent(CardModel card, int fallback) =>
        card != null && XEnergySpentByCopy.TryGetValue(card, out int energySpent)
            ? energySpent : fallback;

    public static void Clear(CardModel card)
    {
        if (card == null)
        {
            return;
        }

        XEnergySpentByCopy.Remove(card);
        Copies.Remove(card);
    }

    private sealed class CopyMarker
    {
    }
}
