using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class StrengthenHbrgoSaki : CardModel
{
    internal bool HasEnhancedRollBonus =>
        IsUpgraded || DeckVersion?.IsUpgraded == true;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/strengthen_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DynamicVar("RollBonus", 10m),
        new DynamicVar("RollUses", 3m),
        new DynamicVar("AdvantageDice", 1m),
        new DynamicVar("AdvantageUses", 3m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<RollBonusPower>(),
        HoverTipFactory.FromPower<AdvantageDicePower>()
    };

    public StrengthenHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await RollBonusPower.Apply(
            choiceContext,
            Owner.Creature,
            (int)DynamicVars["RollBonus"].BaseValue,
            (int)DynamicVars["RollUses"].BaseValue,
            Owner.Creature,
            this);
        await AdvantageDicePower.Apply(
            choiceContext,
            Owner.Creature,
            (int)DynamicVars["AdvantageDice"].BaseValue,
            (int)DynamicVars["AdvantageUses"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["RollBonus"].UpgradeValueBy(10m);
        DynamicVars["RollUses"].UpgradeValueBy(1m);
        DynamicVars["AdvantageUses"].UpgradeValueBy(1m);
    }
}
