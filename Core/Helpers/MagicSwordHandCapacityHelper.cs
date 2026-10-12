using System;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MagicSwordHandCapacityHelper
{
    private static readonly AsyncLocal<int> ExtraCapacity = new();

    internal static int CurrentExtraCapacity =>
        Math.Max(0, ExtraCapacity.Value);

    internal static int GetEffectiveMaxCardsInHand()
    {
        return CardPile.MaxCardsInHand + CurrentExtraCapacity;
    }

    internal static int GetEffectiveMaxCardsInHandForCard(CardModel card)
    {
        int capacity = HalfBodyHandHelper.GetEffectiveMaxCardsInHandForCard(card) + CurrentExtraCapacity;
        // Under the memory's reduced cap, reordering a card already in hand
        // does not consume another slot. Other hands retain native behavior.
        return card?.Pile?.Type == PileType.Hand && HalfBodyHandHelper.CountInHand(card.Owner) > 0
            ? Math.Max(capacity, card.Owner.PlayerCombatState.Hand.Cards.Count + 1)
            : capacity;
    }

    internal static IDisposable AllowOneExtraSlot()
    {
        ExtraCapacity.Value++;
        return new CapacityScope();
    }

    private sealed class CapacityScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ExtraCapacity.Value = Math.Max(
                0,
                ExtraCapacity.Value - 1);
        }
    }
}
