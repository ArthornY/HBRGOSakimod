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
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class MimicThunderGaleHbrgoSaki : CardModel
{
    private const decimal BaseBreakMultiplier = 1.3m;
    private const decimal UpgradedBreakMultiplier = 1.5m;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/mimic_thunder_gale_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayDualSwordsRestrictedCard(
            Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Damage(150m, ValueProp.Move),
            new PowerVar<FragilePower>(2m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<FragilePower>(),
            HoverTipFactory.FromPower<BreakPower>()
        };

    private bool HasEnhancedBreakDamage =>
        IsUpgraded || DeckVersion?.IsUpgraded == true;

    public MimicThunderGaleHbrgoSaki()
        : base(
            2,
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
        decimal damage = DynamicVars.Damage.BaseValue;
        if (cardPlay.Target.HasPower<BreakPower>())
        {
            damage *= HasEnhancedBreakDamage
                ? UpgradedBreakMultiplier
                : BaseBreakMultiplier;
        }

        damage = ModeTransitionHelper.GetAdjustedEffectAmount(
            this,
            damage);
        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (cardPlay.Target.IsAlive &&
            !CombatManager.Instance.IsOverOrEnding)
        {
            await PowerCmd.Apply<FragilePower>(
                choiceContext,
                cardPlay.Target,
                ModeTransitionHelper.GetAdjustedEffectAmount(
                    this,
                    DynamicVars["FragilePower"].BaseValue),
                Owner.Creature,
                this);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(200m, divisor: 3m);
    }
}
