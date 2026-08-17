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
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ExtremeDodgeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/extreme_dodge_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new BlockVar(10m, ValueProp.Move),
            new PowerVar<ExtremeDodgeRollPenaltyPower>(2m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.Static(StaticHoverTip.Block),
            HoverTipFactory.FromPower<ExtremeDodgeRollPenaltyPower>()
        };

    public override bool GainsBlock => true;

    public ExtremeDodgeHbrgoSaki()
        : base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(
            Owner.Creature,
            DynamicVars.Block,
            cardPlay);
        await PowerCmd.Apply<ExtremeDodgeRollPenaltyPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["ExtremeDodgeRollPenaltyPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(5m);
    }
}
