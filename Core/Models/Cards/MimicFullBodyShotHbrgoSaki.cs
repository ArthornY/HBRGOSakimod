using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MimicFullBodyShotHbrgoSaki : CardModel
{
    private const string _destructionRateMultiplierVar =
        "DestructionRateMultiplier";

    public override string PortraitPath =>
        ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/mimic_full_body_shot_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayBowRestrictedCard(Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new PowerVar<FragilePower>(3m),
        new IntVar(_destructionRateMultiplierVar, 1m)
    };

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<FragilePower>(),
        HoverTipFactory.FromPower<BreakPower>(),
        HoverTipFactory.FromPower<DestructionRatePower>()
    };

    public MimicFullBodyShotHbrgoSaki()
        : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
    {
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card != this ||
               ModeTransitionHelper.CanPlayBowRestrictedCard(Owner?.Creature);
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, nameof(cardPlay.Target));
        await PowerCmd.Apply<FragilePower>(
            choiceContext,
            cardPlay.Target,
            ModeTransitionHelper.GetAdjustedEffectAmount(
                this,
                DynamicVars["FragilePower"].BaseValue),
            Owner.Creature,
            this);

        if (!ReptureBowPower.AnyEnemyHasPower<BreakPower>(
                Owner.Creature,
                cardPlay.Target) ||
            CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        int fragile = ReptureBowPower
            .GetEnemyPowerAmount<FragilePower>(
                Owner.Creature,
                cardPlay.Target);
        if (fragile <= 0)
        {
            return;
        }

        if (!cardPlay.Target.HasPower<DestructionRatePower>())
        {
            await PowerCmd.Apply<DestructionRatePower>(
                choiceContext,
                cardPlay.Target,
                DestructionRatePower.BaselineAmount,
                Owner.Creature,
                this);
        }

        decimal destructionRateGain =
            ModeTransitionHelper.GetAdjustedEffectAmount(
                this,
                fragile *
                DynamicVars[_destructionRateMultiplierVar].BaseValue);
        if (destructionRateGain > 0m)
        {
            await PowerCmd.Apply<DestructionRatePower>(
                choiceContext,
                cardPlay.Target,
                destructionRateGain,
                Owner.Creature,
                this);
        }

        int destructionRate = ReptureBowPower
            .GetEnemyPowerAmount<DestructionRatePower>(
                Owner.Creature,
                cardPlay.Target,
                useDisplayAmount: true);
        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            fragile * (destructionRate / 100m));
        if (damage <= 0m)
        {
            return;
        }

        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FragilePower"].UpgradeValueBy(3m);
        DynamicVars[_destructionRateMultiplierVar].UpgradeValueBy(1m);
    }
}
