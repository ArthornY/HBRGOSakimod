using System.Collections.Generic;
using System.Linq;
using BaseLib.Abstracts;
using Godot;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;
using STS2RitsuLib.Scaffolding.Characters;

namespace HBRGOSakiMod.Core.Models.CardPools;

public sealed class HBRGOSaki30ACardPool : CustomCardPoolModel, IModColorfulPhilosophersCardPool
{
    public override string Title => "hbrgo_saki_30a";

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
            .Where(HbrgoSaki31OCardFilter.IsThirtyAAllowed)
            .Distinct();
    }
}
