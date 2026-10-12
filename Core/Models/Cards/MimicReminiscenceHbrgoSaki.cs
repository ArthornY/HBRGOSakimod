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

public sealed class MimicReminiscenceHbrgoSaki : CardModel
{
    public override string PortraitPath =>
        ImageHelper.GetImagePath(
            "packed/card_portraits/hbrgo_saki/mimic_reminiscence_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<MimicReminiscenceTemporaryStrengthPower>(3m),
            new PowerVar<MimicReminiscenceTemporaryDexterityPower>(1m),
            new PowerVar<ChargePower>(0m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<
                MimicReminiscenceTemporaryStrengthPower>(),
            HoverTipFactory.FromPower<
                MimicReminiscenceTemporaryDexterityPower>(),
            HoverTipFactory.FromPower<ChargePower>()
        };

    public MimicReminiscenceHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<MimicReminiscenceTemporaryStrengthPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[
                "MimicReminiscenceTemporaryStrengthPower"].BaseValue,
            Owner.Creature,
            this);
        await PowerCmd.Apply<MimicReminiscenceTemporaryDexterityPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars[
                "MimicReminiscenceTemporaryDexterityPower"].BaseValue,
            Owner.Creature,
            this);
        decimal chargeAmount = DynamicVars["ChargePower"].BaseValue;
        if (chargeAmount > 0m)
        {
            await PowerCmd.Apply<ChargePower>(
                choiceContext,
                Owner.Creature,
                chargeAmount,
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["MimicReminiscenceTemporaryStrengthPower"]
            .UpgradeValueBy(1m);
        DynamicVars["MimicReminiscenceTemporaryDexterityPower"]
            .UpgradeValueBy(1m);
        DynamicVars["ChargePower"].UpgradeValueBy(1m);
    }
}
