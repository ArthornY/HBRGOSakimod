using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
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

public sealed class ConsecutiveSlashHbrgoSaki : CardModel
{
    private const int _hitCount = 2;

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/consecutive_slash_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            HbrSkillVars.Damage(100m, ValueProp.Move, divisor: 3m),
            new RepeatVar(_hitCount),
            HbrSkillVars.DamageBonus("ResistanceDamage", 50m)
        };

    protected override bool IsPlayable => CanUseDualSwordsCard();

    public ConsecutiveSlashHbrgoSaki()
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
        for (int hit = 0;
             hit < _hitCount && !CombatManager.Instance.IsOverOrEnding;
             hit++)
        {
            decimal damage = GetDamageForTarget(cardPlay.Target);
            await DamageCmd.Attack(damage)
                .FromCardCompat(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
        }
    }

    internal decimal GetDamageForTarget(Creature target) =>
        ModeTransitionHelper.GetAdjustedEffectAmount(this,
            DynamicVars.Damage.BaseValue +
            SixSenseCombatHelper.TargetResistanceBreakCount(target) *
            DynamicVars["ResistanceDamage"].BaseValue);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeDamageByPercent(50m, divisor: 3m);
    }

    private bool CanUseDualSwordsCard()
    {
        return ModeTransitionHelper.CanPlayDualSwordsRestrictedCard(
            Owner?.Creature);
    }
}
