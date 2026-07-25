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
                and not ReptureDualSwordsChoiceHbrgoSaki
                and not HighSongHbrgoSaki
                and not ShadowDanceSongHbrgoSaki
                and not BlueSkySongHbrgoSaki
                and not LostDaysSongHbrgoSaki)
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
                ModelDb.Card<SwallowReversalHbrgoSaki>(),
                ModelDb.Card<SlideStepHbrgoSaki>(),
                ModelDb.Card<BladeDanceHbrgoSaki>(),
                ModelDb.Card<StrongShotHbrgoSaki>(),
                ModelDb.Card<HerSongIsMyScreamHbrgoSaki>(),
                ModelDb.Card<MimicDreamBubbleHbrgoSaki>(),
                ModelDb.Card<EhehePeroroHbrgoSaki>(),
                ModelDb.Card<NoSelfInDreamHbrgoSaki>(),
                ModelDb.Card<AnonAidHbrgoSaki>(),
                ModelDb.Card<SwordheartClarityHbrgoSaki>(),
                ModelDb.Card<CrimsonBladeFlowHbrgoSaki>(),
                ModelDb.Card<SoyoAidHbrgoSaki>(),
                ModelDb.Card<IOnlyKnowWhatIKnowHbrgoSaki>(),
                ModelDb.Card<ImitateHbrgoSaki>(),
                ModelDb.Card<SakuraRainHbrgoSaki>(),
                ModelDb.Card<MagicSwordEnhancementHbrgoSaki>(),
                ModelDb.Card<FragileHbrgoSaki>(),
                ModelDb.Card<LostStarSongHbrgoSaki>(),
                ModelDb.Card<MagicSwordLuckySevenHbrgoSaki>(),
                ModelDb.Card<ChihayaClassmateHbrgoSaki>(),
                ModelDb.Card<SwitchHbrgoSaki>(),
                ModelDb.Card<ReptureBowChoiceHbrgoSaki>(),
                ModelDb.Card<ReptureDualSwordsChoiceHbrgoSaki>(),
                ModelDb.Card<FeintHbrgoSaki>(),
                ModelDb.Card<HighSongHbrgoSaki>(),
                ModelDb.Card<ShadowDanceSongHbrgoSaki>(),
                ModelDb.Card<BlueSkySongHbrgoSaki>(),
                ModelDb.Card<LostDaysSongHbrgoSaki>()
            }
            .Distinct()
            .ToArray();
    }
}
