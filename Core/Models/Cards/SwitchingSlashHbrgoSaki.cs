using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class SwitchingSlashHbrgoSaki : CardModel
{
    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/switching_slash_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DamageVar[]
        {
            HbrSkillVars.Damage(350m, ValueProp.Move, divisor: 3m)
        };

    protected override bool IsPlayable => CanUseDualSwordsCard();

    public SwitchingSlashHbrgoSaki()
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
        return card != this || CanUseDualSwordsCard();
    }

    protected override async Task OnPlay(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(
            cardPlay.Target,
            nameof(cardPlay.Target));
        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            DynamicVars.Damage.BaseValue);
        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    public override async Task AfterCardPlayed(
        PlayerChoiceContext choiceContext,
        CardPlay cardPlay)
    {
        Creature owner = Owner?.Creature;
        if (!ReferenceEquals(cardPlay?.Card, this) ||
            owner?.CombatState == null ||
            CombatManager.Instance.IsOverOrEnding ||
            owner.HasPower<ReptureBowPower>())
        {
            return;
        }

        await PowerCmd.Apply<ReptureBowPower>(
            choiceContext,
            owner,
            1m,
            owner,
            this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(100m, divisor: 3m);
    }

    private bool CanUseDualSwordsCard()
    {
        return ModeTransitionHelper.CanPlayDualSwordsRestrictedCard(
            Owner?.Creature);
    }
}
