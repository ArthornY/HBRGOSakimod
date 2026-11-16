using System;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Presentation only. The native getter and GeneratePlayCount remain untouched;
// Moonlight's modifying hook still decides and spends the actual extra play.
internal static class MoonlightReplayDisplayHelper
{
    internal static int GetDisplayedReplayCount(CardModel card)
    {
        if (card == null) return 0;
        int original = card.GetEnchantedReplayCount();
        if (!card.IsMutable || card.Pile?.Type != PileType.Hand) return original;
        int bonus = MoonlightReplayPower.GetReplayCountBonus(card);
        if (bonus <= 0 || !card.CanPlay()) return original;
        return (int)Math.Min(int.MaxValue, (long)original + bonus);
    }
}
