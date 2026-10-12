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
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class FateHbrgoSaki : CardModel
{
    private const string MaxHpLossVar = "MaxHpLoss";

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/memory_corridor_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<StrengthPower>(3m),
            new PowerVar<DexterityPower>(3m),
            new PowerVar<FatePower>(1m),
            new IntVar(MaxHpLossVar, 3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<FatePower>(),
            HoverTipFactory.FromPower<StrengthPower>(),
            HoverTipFactory.FromPower<DexterityPower>()
        };

    public FateHbrgoSaki()
        : base(
            1,
            CardType.Power,
            CardRarity.Rare,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<FatePower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["FatePower"].BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<StrengthPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["StrengthPower"].BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<DexterityPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars["DexterityPower"].BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FatePower"].UpgradeValueBy(1m);
        DynamicVars[MaxHpLossVar].UpgradeValueBy(3m);
        AddKeyword(CardKeyword.Retain);
    }
}
