using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class RemoveDpHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/remove_dp_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<HighDimensionalShellPower>(),
            HoverTipFactory.FromPower<BreakPower>()
        };

    public RemoveDpHbrgoSaki()
        : base(
            2,
            CardType.Attack,
            CardRarity.Rare,
            TargetType.AnyEnemy)
    {
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        HighDimensionalShellPower shell =
            cardPlay.Target.GetPower<HighDimensionalShellPower>();
        if (shell != null)
        {
            await shell.BreakImmediately(
                choiceContext,
                IsUpgraded ? 1 : 0);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
