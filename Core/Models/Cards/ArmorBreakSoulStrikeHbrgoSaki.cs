using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class ArmorBreakSoulStrikeHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/armor_break_soul_strike_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayScytheRestrictedCard(
            Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new DamageVar(9m, ValueProp.Move),
            new PowerVar<FragilePower>(0m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<FragilePower>()
        };

    public ArmorBreakSoulStrikeHbrgoSaki()
        : base(
            1,
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
            ModeTransitionHelper.CanPlayScytheRestrictedCard(
                Owner?.Creature);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        decimal fragileToApply =
            ModeTransitionHelper.GetAdjustedEffectAmount(
                this,
                DynamicVars["FragilePower"].BaseValue);
        if (fragileToApply > 0m)
        {
            await PowerCmd.Apply<FragilePower>(
                choiceContext,
                cardPlay.Target,
                fragileToApply,
                Owner.Creature,
                this);
        }

        int fragile =
            cardPlay.Target.GetPower<FragilePower>()?.Amount ?? 0;
        await DamageCmd.Attack(
                ModeTransitionHelper.GetAdjustedEffectAmount(
                    this,
                    DynamicVars.Damage.BaseValue))
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        int counterGain = Math.Max(
            0,
            (int)decimal.Round(
                ModeTransitionHelper.GetAdjustedEffectAmount(
                    this,
                    fragile),
                0,
                MidpointRounding.AwayFromZero));
        Owner.GetRelic<RestorationMoment>()?.GainCounter(
            counterGain);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FragilePower"].UpgradeValueBy(3m);
    }
}
