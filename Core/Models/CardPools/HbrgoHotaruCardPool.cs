using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Unlocks;

namespace HBRGOSakiMod.Core.Models.CardPools;

public sealed class HbrgoHotaruCardPool : CustomCardPoolModel
{
    public override string Title => "hbrgo_hotaru";
    public override string EnergyColorName => "hbrgohotaru";
    public override string CardFrameMaterialPath => "card_frame_light_blue";
    public override Color DeckEntryCardColor => new("D4D6EEFF");
    public override Color EnergyOutlineColor => new("494959FF");
    public override bool IsColorless => false;

    protected override IEnumerable<CardModel> FilterThroughEpochs(
        UnlockState unlockState, IEnumerable<CardModel> cards)
    {
        CardModel[] hotaruCards = cards.Distinct().ToArray();
        bool hasCommon = false;
        bool hasUncommon = false;
        bool hasRare = false;
        foreach (CardModel card in hotaruCards)
        {
            switch (card.Rarity)
            {
                case CardRarity.Common: hasCommon = true; break;
                case CardRarity.Uncommon: hasUncommon = true; break;
                case CardRarity.Rare: hasRare = true; break;
            }
            if (hasCommon && hasUncommon && hasRare)
                return hotaruCards;
        }

        // Keep unlocked colorless rewards available while any normal reward
        // rarity is still missing from Hotaru's developing card pool.
        return hotaruCards.Concat(ModelDb.CardPool<ColorlessCardPool>()
            .GetUnlockedCards(unlockState, CardMultiplayerConstraint.None)).Distinct();
    }
}
