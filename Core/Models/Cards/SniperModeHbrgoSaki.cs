using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class SniperModeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/sniper_mode_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new[]
        {
            HoverTipFactory.FromPower<SniperModePower>(),
            HoverTipFactory.FromPower<ReptureBowPower>()
        };

    public SniperModeHbrgoSaki()
        : base(
            1,
            CardType.Power,
            CardRarity.Uncommon,
            TargetType.Self)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        await PowerCmd.Apply<SniperModePower>(
            choiceContext,
            Owner.Creature,
            1m,
            Owner.Creature,
            this);

        if (IsUpgraded)
        {
            await ModeTransitionHelper.DrawModeRestrictedCard(
                choiceContext,
                Owner,
                ReptureCardMode.Bow,
                this);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
