using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class PiercingShotHbrgoSaki : CardModel
{
    private int DamagePerFragile =>
        IsUpgraded || DeckVersion?.IsUpgraded == true ? 2 : 1;

    internal decimal FragileBonusPermille => 6m * DamagePerFragile;

    internal decimal GetDamageUnits(Creature target)
    {
        decimal percent = HbrFragilePowerHelper.GetTotalEffectivePercent(target);
        decimal bonus = DamageAmplificationLimits.MultiplySaturated(percent,
            HbrSkillPower.LegacyDamageAt100Percent * FragileBonusPermille / 1000m);
        return DamageAmplificationLimits.AddSaturated(DynamicVars.Damage.BaseValue, bonus);
    }

    public override string PortraitPath => ImageHelper.GetImagePath(
        "packed/card_portraits/hbrgo_saki/piercing_shot_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayBowRestrictedCard(
            Owner?.Creature);

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DamageVar[]
        {
            HbrSkillVars.Damage(250m, ValueProp.Move, divisor: 3m)
        };

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        new IHoverTip[]
        {
            HoverTipFactory.FromPower<FragilePower>()
        };

    public PiercingShotHbrgoSaki()
        : base(
            1,
            CardType.Attack,
            CardRarity.Common,
            TargetType.AllEnemies)
    {
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("FragileBonusPermille", HbrSkillCardDescription.FormatPercent(FragileBonusPermille));
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
        Creature[] targets = CombatState
            .GetOpponentsOf(Owner.Creature)
            .Where(target => target.IsHittable)
            .ToArray();
        using var comboAttack = HbrComboHelper.BeginCardAttack(this);
        int hitCount = comboAttack?.HitCount(1) ?? 1;
        try
        {
            foreach (Creature target in targets)
            {
                decimal damage = HbrBattleStatsHelper.IsEnabled(Owner.Creature)
                    ? GetDamageUnits(target)
                    : DynamicVars.Damage.BaseValue +
                        ReptureBowPower.GetEnemyPowerAmount<FragilePower>(Owner.Creature, target) * DamagePerFragile;
                var attack = await DamageCmd.Attack(
                        ModeTransitionHelper.GetAdjustedEffectAmount(
                            this,
                            damage))
                    .FromCardCompat(this, cardPlay)
                    .Targeting(target)
                    .WithHitCount(hitCount)
                    .WithHitFx("vfx/vfx_attack_slash")
                    .Execute(choiceContext);
                if (attack.Results.Any(results => results.Count > 0)) comboAttack?.MarkHit();
            }
        }
        finally
        {
            if (comboAttack != null) await comboAttack.Complete(choiceContext);
        }
    }

    protected override void OnUpgrade()
    {
    }
}
