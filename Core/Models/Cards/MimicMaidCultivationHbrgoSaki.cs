using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MimicMaidCultivationHbrgoSaki : CardModel
{
    internal bool HasEnhancedOverclock =>
        IsUpgraded || DeckVersion?.IsUpgraded == true;

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/mimic_maid_cultivation_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<MaidCultivationPower>(2m),
        new DynamicVar("RollBonus", 20m),
        new DynamicVar("RollUses", 2m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<MaidCultivationPower>(),
        HoverTipFactory.FromPower<RollBonusPower>()
    };

    public MimicMaidCultivationHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add(
            "OverclockPercent",
            HasEnhancedOverclock ? 200m : 150m);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<MaidCultivationPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["MaidCultivationPower"].BaseValue,
            Owner.Creature,
            this);
        await RollBonusPower.Apply(
            choiceContext,
            Owner.Creature,
            (int)DynamicVars["RollBonus"].BaseValue,
            (int)DynamicVars["RollUses"].BaseValue,
            Owner.Creature,
            this);
    }
}
