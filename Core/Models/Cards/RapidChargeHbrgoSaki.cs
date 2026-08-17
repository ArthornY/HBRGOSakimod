using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class RapidChargeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/rapid_charge_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<ChargePower>(3m),
            new PowerVar<QuickSwitchPower>(2m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<ChargePower>(),
            HoverTipFactory.FromPower<QuickSwitchPower>()
        };

    public RapidChargeHbrgoSaki()
        : base(
            1,
            CardType.Skill,
            CardRarity.Uncommon,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<ChargePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ChargePower"].BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<QuickSwitchPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["QuickSwitchPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["ChargePower"].UpgradeValueBy(2m);
        DynamicVars["QuickSwitchPower"].UpgradeValueBy(1m);
    }
}
