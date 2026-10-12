using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class OverdrawMovementHbrgoSaki : CardModel
{
    private const string _dpLossVar = "DpLoss";

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/overdraw_movement_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar(_dpLossVar, 15m),
            new EnergyVar(2)
        };

    public OverdrawMovementHbrgoSaki()
        : base(
            0,
            CardType.Skill,
            CardRarity.Uncommon,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        DeflectorProtection protection =
            Owner.GetRelic<DeflectorProtection>();
        protection?.LoseDp(
            (int)DynamicVars[_dpLossVar].BaseValue);
        if (protection != null)
        {
            await protection.ResolveMentalStrengthDpLossRewards(
                choiceContext,
                this);
        }

        await PlayerCmd.GainEnergy(
            DynamicVars.Energy.BaseValue,
            Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[_dpLossVar].UpgradeValueBy(-5m);
    }
}
