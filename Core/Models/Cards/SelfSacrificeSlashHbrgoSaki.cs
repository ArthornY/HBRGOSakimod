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

public sealed class SelfSacrificeSlashHbrgoSaki : CardModel
{
    private const string _dpLossVar = "DpLoss";

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/self_sacrifice_slash_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayDualSwordsRestrictedCard(
            Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new IntVar(_dpLossVar, 7m) };

    public SelfSacrificeSlashHbrgoSaki()
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
            ModeTransitionHelper.CanPlayDualSwordsRestrictedCard(
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
        protection?.LoseDp(
            HbrResourceValueHelper.FixedInt(Owner.Creature, DynamicVars[_dpLossVar].BaseValue));
        if (protection != null)
        {
            await protection.ResolveMentalStrengthDpLossRewards(
                choiceContext,
                this);
        }

        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            Owner.Creature.Block / 2m);
        using var absoluteDamage = HbrBattleStatsHelper.IsEnabled(Owner)
            ? HbrAbsoluteBaseDamageContext.Begin(Owner.Creature, this, cardPlay.Target, damage)
            : null;
        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

    }

    protected override void OnUpgrade()
    {
        DynamicVars[_dpLossVar].UpgradeValueBy(-2m);
    }
}
