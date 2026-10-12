using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
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

public sealed class RestTimeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/rest_time_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new EnergyVar(2)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<NormalStatePower>(),
            HoverTipFactory.FromPower<EnergyNextTurnPower>()
        };

    public RestTimeHbrgoSaki()
        : base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        using (ModeTransitionHelper.SuppressDualSwordsOnModeExit())
        {
            await PowerCmd.Remove(
                Owner.Creature.GetPower<NormalStatePower>());
            await PowerCmd.Remove(
                Owner.Creature.GetPower<ReptureBowPower>());
            await PowerCmd.Remove(
                Owner.Creature.GetPower<ReptureDualSwordsPower>());
            await PowerCmd.Remove(
                Owner.Creature.GetPower<RaptureScythePower>());

            MagicSwordModePower magicSwordMode =
                Owner.Creature.GetPower<MagicSwordModePower>();
            if (magicSwordMode != null)
            {
                await magicSwordMode.RemoveForModeTransition();
            }

            await PowerCmd.Remove(
                Owner.Creature.GetPower<CrimsonBladeFlowPower>());
        }

        await PowerCmd.Apply<NormalStatePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);
        await PowerCmd.Apply<EnergyNextTurnPower>(
            choiceContext,
            Owner.Creature,
            DynamicVars.Energy.BaseValue,
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Energy.UpgradeValueBy(1m);
    }
}
