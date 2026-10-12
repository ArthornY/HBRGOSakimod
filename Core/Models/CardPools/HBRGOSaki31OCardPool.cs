using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;
using STS2RitsuLib.Scaffolding.Characters;

namespace HBRGOSakiMod.Core.Models.CardPools;

public sealed class HBRGOSaki31OCardPool : CustomCardPoolModel, IModColorfulPhilosophersCardPool
{
    public override string Title => "hbrgo_saki_31o";

    public override string EnergyColorName => "hbrgosaki";

    public override string CardFrameMaterialPath => "card_frame_light_blue";

    public override Color DeckEntryCardColor => new("7799CCFF");

    public override Color EnergyOutlineColor => new("1C274AFF");

    public override bool IsColorless => false;

    protected override IEnumerable<CardModel> FilterThroughEpochs(
        UnlockState unlockState,
        IEnumerable<CardModel> cards)
    {
        return cards
            .Where(card => card is not MagicSwordLuckySevenHbrgoSaki
                and not MagicSwordModeHbrgoSaki
                and not FeintHbrgoSaki
                and not ChihayaClassmateHbrgoSaki
                and not AnokoHbrgoSaki
                and not ReptureBowChoiceHbrgoSaki
                and not ReptureDualSwordsChoiceHbrgoSaki
                and not RaptureScytheChoiceHbrgoSaki
                and not HighSongHbrgoSaki
                and not ShadowDanceSongHbrgoSaki
                and not BlueSkySongHbrgoSaki
                and not LostDaysSongHbrgoSaki)
            .Distinct();
    }
}
