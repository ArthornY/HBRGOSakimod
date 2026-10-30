using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Models.Cards;

public sealed class StrongShotHbrgoSaki : CardModel
{
    private const int _hitCount = 3;
    private const string _fragileBonusVar = "FragileBonus";

    internal decimal FragileBonusPermille => DynamicVars[_fragileBonusVar].BaseValue * 6m;

    internal decimal GetDamageUnits()
    {
        decimal highestPercent = IsMutable && Owner != null && CombatState != null
            ? CombatState.GetOpponentsOf(Owner.Creature)
                .Where(target => target.IsHittable)
                .Select(HbrFragilePowerHelper.GetTotalEffectivePercent).DefaultIfEmpty(0m).Max()
            : 0m;
        decimal bonus = DamageAmplificationLimits.MultiplySaturated(highestPercent,
            HbrSkillPower.LegacyDamageAt100Percent * FragileBonusPermille / 1000m);
        return DamageAmplificationLimits.AddSaturated(DynamicVars.Damage.BaseValue, bonus);
    }

    public override string PortraitPath => ImageHelper.GetImagePath("packed/card_portraits/hbrgo_saki/strong_shot_hbrgo_saki.png");

    public override string BetaPortraitPath => PortraitPath;

    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        HbrSkillVars.Damage(50m, ValueProp.Move),
        new RepeatVar(_hitCount),
        HbrSkillVars.DamageBonus(_fragileBonusVar, 50m, divisor: 3m)
    };

    protected override bool IsPlayable =>
        ModeTransitionHelper.CanPlayBowRestrictedCard(Owner?.Creature);

    public StrongShotHbrgoSaki()
        : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
    {
    }

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("FragileBonusPermille", HbrSkillCardDescription.FormatPercent(FragileBonusPermille));
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        return card != this ||
               ModeTransitionHelper.CanPlayBowRestrictedCard(Owner?.Creature);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal units;
        if (HbrBattleStatsHelper.IsEnabled(Owner.Creature)) units = GetDamageUnits();
        else
        {
            int highestFragile = CombatState.GetOpponentsOf(Owner.Creature)
                .Where(target => target.IsHittable)
                .Select(target => ReptureBowPower.GetEnemyPowerAmount<FragilePower>(Owner.Creature, target))
                .DefaultIfEmpty(0).Max();
            units = DynamicVars.Damage.BaseValue + highestFragile * DynamicVars[_fragileBonusVar].BaseValue;
        }
        decimal damage = ModeTransitionHelper.GetAdjustedEffectAmount(this, units);
        if (_hitCount <= 0 || CombatManager.Instance.IsOverOrEnding) return;
        await DamageCmd.Attack(damage)
            .FromCardCompat(this, cardPlay)
            .TargetingAllOpponents(base.CombatState)
            .WithHitCount(_hitCount)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        base.DynamicVars.Damage.UpgradeDamageByPercent(50m, divisor: 3m);
        DynamicVars[_fragileBonusVar].UpgradeDamageByPercent(50m, divisor: 3m);
    }
}
