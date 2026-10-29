using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class DpSoulStrikeHbrgoSaki : CardModel
{
    private const string _dpLossVar = "DpLoss";
    private const string _damagePercentVar = "DamagePercent";
    private const string _counterGainPercentVar =
        "CounterGainPercent";

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/dp_soul_strike_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayScytheRestrictedCard(
            Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new IntVar(_dpLossVar, 10m),
            new IntVar(_damagePercentVar, 150m),
            new IntVar(_counterGainPercentVar, 100m)
        };

    public DpSoulStrikeHbrgoSaki()
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
        DeflectorProtection protection =
            Owner.GetRelic<DeflectorProtection>();
        int lostDp = protection?.LoseDp(
            HbrResourceValueHelper.FixedInt(Owner.Creature, DynamicVars[_dpLossVar].BaseValue)) ?? 0;
        if (protection != null)
        {
            await protection.ResolveMentalStrengthDpLossRewards(
                choiceContext,
                this);
        }

        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            lostDp *
            DynamicVars[_damagePercentVar].BaseValue /
            100m);
        using var absoluteDamage = HbrBattleStatsHelper.IsEnabled(Owner)
            ? HbrAbsoluteBaseDamageContext.Begin(Owner.Creature, this, cardPlay.Target, damage)
            : null;
        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        int counterGain = Math.Max(
            0,
            (int)decimal.Round(
                ModeTransitionHelper.GetAdjustedEffectAmount(
                    this,
                    lostDp *
                    DynamicVars[_counterGainPercentVar]
                        .BaseValue /
                    100m),
                0,
                MidpointRounding.AwayFromZero));
        Owner.GetRelic<RestorationMoment>()?.GainCounter(
            counterGain);
    }

    protected override void OnUpgrade()
    {
        DynamicVars[_damagePercentVar].UpgradeValueBy(50m);
        DynamicVars[_counterGainPercentVar]
            .UpgradeValueBy(50m);
    }
}
