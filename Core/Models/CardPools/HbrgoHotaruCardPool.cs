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
    public override string EnergyColorName => "hbrgosaki";
    public override string CardFrameMaterialPath => "card_frame_light_blue";
    public override Color DeckEntryCardColor => new("D4D6EEFF");
    public override Color EnergyOutlineColor => new("494959FF");
    public override bool IsColorless => false;

    protected override IEnumerable<CardModel> FilterThroughEpochs(
        UnlockState unlockState, IEnumerable<CardModel> cards)
    {
        CardModel[] hotaruCards = cards.Distinct().ToArray();
        if (hotaruCards.Any(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare))
            return hotaruCards;

        // The initial implementation has only Basic cards. Use the unlocked
        // colorless pool until a normal Hotaru reward pool has been created.
        return hotaruCards.Concat(ModelDb.CardPool<ColorlessCardPool>()
            .GetUnlockedCards(unlockState, CardMultiplayerConstraint.None)).Distinct();
    }
}
