using System.Collections.Generic;
using System.Linq;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;

namespace HBRGOSakiMod.Core.Models.CardPools;

public sealed class HBRGOSakiCardPool : CardPoolModel
{
    public override string Title => "hbrgo_saki";

    public override string EnergyColorName => "hbrgosaki";

    public override string CardFrameMaterialPath => "card_frame_light_blue";

    public override Color DeckEntryCardColor => new("7799CCFF");

    public override Color EnergyOutlineColor => new("1C274AFF");

    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards()
    {
        return GetHbrgoCards();
    }

    protected override IEnumerable<CardModel> FilterThroughEpochs(UnlockState unlockState, IEnumerable<CardModel> cards)
    {
        return GetHbrgoCards()
            .Where(card => cards.Any(candidate => candidate.Id == card.Id))
            .Where(card => card is not MagicSwordLuckySevenHbrgoSaki
                and not FeintHbrgoSaki
                and not ChihayaClassmateHbrgoSaki
                and not SwitchHbrgoSaki
                and not ReptureBowChoiceHbrgoSaki
                and not ReptureDualSwordsChoiceHbrgoSaki)
            .Distinct();
    }

    private static CardModel[] GetHbrgoCards()
    {
        return new CardModel[]
            {
                ModelDb.Card<StrikeHbrgoSaki>(),
                ModelDb.Card<DefendHbrgoSaki>(),
                ModelDb.Card<ArrowRainHbrgoSaki>(),
                ModelDb.Card<CrossSlashHbrgoSaki>(),
                ModelDb.Card<EhehePeroroHbrgoSaki>(),
                ModelDb.Card<AnonAidHbrgoSaki>(),
                ModelDb.Card<SwordheartClarityHbrgoSaki>(),
                ModelDb.Card<ImitateHbrgoSaki>(),
                ModelDb.Card<MagicSwordLuckySevenHbrgoSaki>(),
                ModelDb.Card<ChihayaClassmateHbrgoSaki>(),
                ModelDb.Card<SwitchHbrgoSaki>(),
                ModelDb.Card<ReptureBowChoiceHbrgoSaki>(),
                ModelDb.Card<ReptureDualSwordsChoiceHbrgoSaki>(),
                ModelDb.Card<FeintHbrgoSaki>()
            }
            .Distinct()
            .ToArray();
    }
}
