using System;
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

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class HangingShotHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/hanging_shot_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayBowRestrictedCard(
            Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<HangingShotPower>(15m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<HangingShotPower>()
        };

    public HangingShotHbrgoSaki()
        : base(
            1,
            CardType.Attack,
            CardRarity.Common,
            TargetType.AnyEnemy)
    {
    }

    public override bool ShouldPlay(
        CardModel card,
        AutoPlayType autoPlayType)
    {
        return card != this ||
            ModeTransitionHelper.CanPlayBowRestrictedCard(
                Owner?.Creature);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        await PowerCmd.Apply<HangingShotPower>(
            choiceContext,
            cardPlay.Target,
            ModeTransitionHelper.GetAdjustedEffectAmount(
                this,
                DynamicVars["HangingShotPower"].BaseValue),
            Owner.Creature,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HangingShotPower"].UpgradeValueBy(7m);
    }
}
