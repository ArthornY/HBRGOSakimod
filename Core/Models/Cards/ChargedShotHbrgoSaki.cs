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

public sealed class ChargedShotHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/charged_shot_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayBowRestrictedCard(
            Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new PowerVar<ChargePower>(2m),
            new PowerVar<HangingShotPower>(12m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<ChargePower>(),
            HoverTipFactory.FromPower<HangingShotPower>()
        };

    public ChargedShotHbrgoSaki()
        : base(
            0,
            CardType.Attack,
            CardRarity.Uncommon,
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
        await PowerCmd.Apply<ChargePower>(
            choiceContext,
            Owner.Creature,
            ModeTransitionHelper.GetAdjustedEffectAmount(
                this,
                DynamicVars["ChargePower"].BaseValue),
            Owner.Creature,
            this);
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
        DynamicVars["ChargePower"].UpgradeValueBy(1m);
        DynamicVars["HangingShotPower"].UpgradeValueBy(4m);
    }
}
