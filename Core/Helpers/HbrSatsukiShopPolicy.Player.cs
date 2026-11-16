using System;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Equipment;

internal readonly partial record struct HbrSatsukiShopPolicy
{
    internal static HbrSatsukiShopPolicy Get(Player player)
    {
        if (player?.Deck == null) return default;
        bool enabled = false;
        int effects = 0;
        foreach (var entry in player.Deck.Cards)
        {
            if (entry is not AllOrdersConfirmedHbrgoSaki card ||
                !ReferenceEquals(card.Owner, player) || card.HasBeenRemovedFromState) continue;
            enabled = true;
            int count = Math.Min(card.CurrentUpgradeLevel, card.UpgradeEffectOrder.Length);
            for (int index = 0; index < count; index++)
                if (card.UpgradeEffectOrder[index] is >= 0 and < 7)
                    effects |= 1 << card.UpgradeEffectOrder[index];
        }
        if (!enabled) return default;
        bool Has(int effect) => (effects & (1 << effect)) != 0;
        return new(true, Has(AllOrdersConfirmedHbrgoSaki.Flash), Has(AllOrdersConfirmedHbrgoSaki.Saturation),
            Has(AllOrdersConfirmedHbrgoSaki.DestructionIllusion), Has(AllOrdersConfirmedHbrgoSaki.DiamondDust),
            Has(AllOrdersConfirmedHbrgoSaki.AmplifierCannon), Has(AllOrdersConfirmedHbrgoSaki.HappyDay),
            Has(AllOrdersConfirmedHbrgoSaki.Shopkeeper), ThirtyOneCKeywordRules.GetPermanentDistinctCount(player));
    }
}
