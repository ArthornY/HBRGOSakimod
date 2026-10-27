using System;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// STS cards have no original-game skill rows. These explicit bridge profiles
// keep their existing base values/upgrade effects while giving the shared HBR
// formula real stat-dependent power. They are adaptation values, not data
// claimed to have been extracted from HBR's character/monster catalog.
internal static class HbrDamageSkillAdapter
{
    internal const decimal OutgoingPowerUnit = 1000m;
    internal const decimal IncomingPowerUnit = 100m;
    internal const decimal PlayerBlockUnit = 100m;
    internal const decimal DefaultParameterLimit = 140m;
    internal static readonly HbrStatWeights HpWeights = new(2, 1, 0, 0, 0, 0);
    internal static readonly HbrStatWeights DpWeights = new(1, 2, 0, 0, 0, 0);
    internal static readonly HbrStatWeights SingleDefense = new(0, 0, 0, 2, 1, 0);
    internal static readonly HbrStatWeights AreaDefense = new(0, 0, 0, 1, 2, 0);

    internal static decimal ConvertBaseDamage(Creature target, Creature dealer,
        decimal legacyPower, ValueProp props, CardModel cardSource)
    {
        if (legacyPower <= 0m || !props.IsPoweredAttack() ||
            !HbrBattleStatsHelper.TryGetStats(dealer, out HbrBattleStats stats))
            return legacyPower;

        bool incoming = EnemyStatScalingHelper.IsEnemy(dealer);
        if (incoming)
        {
            if (!HbrBattleStatsHelper.TryGetStats(target, out HbrBattleStats defense))
                return legacyPower;
            bool area = HbrCriticalDamageContext.CurrentTargetCount > 1;
            decimal defensiveParameter = HbrOriginalNumericFormula.EvaluateWeightedStat(
                defense, area ? AreaDefense : SingleDefense);
            // Enemy skill rows and their exact incoming formula are not yet
            // verified. This named profile is the test adaptation boundary.
            return HbrOriginalNumericFormula.EvaluateAttackCurve(stats.Strength,
                defensiveParameter, Curve(legacyPower, IncomingPowerUnit));
        }

        decimal parameter = HbrBattleStatsHelper.GetEnemyParameter(target);
        bool critical = HbrCriticalDamageContext.IsCritical(target, dealer,
            cardSource, props, stats.Luck, parameter);
        HbrStatWeights weights = target?.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m
            ? DpWeights : HpWeights;
        decimal power = HbrOriginalNumericFormula.EvaluateSkillPower(stats, weights,
            parameter, Curve(legacyPower, OutgoingPowerUnit), critical);
        return critical ? DamageAmplificationLimits.MultiplySaturated(power,
            HbrOriginalNumericFormula.CriticalMultiplier()) : power;
    }

    private static HbrSkillPowerCurve Curve(decimal legacyPower, decimal unit)
    {
        decimal minimum = DamageAmplificationLimits.MultiplySaturated(legacyPower, unit);
        return new(minimum, DamageAmplificationLimits.MultiplySaturated(minimum, 2m),
            DefaultParameterLimit);
    }

    internal static decimal ConvertFlatDamageBonus(AbstractModel model, Creature dealer,
        decimal amount, bool enabled)
    {
        // Only known legacy point bonuses are converted. An effect based on
        // already-resolved damage or actual max HP must not be scaled again.
        if (!enabled || model is not (MegaCrit.Sts2.Core.Models.Powers.StrengthPower or
                MegaCrit.Sts2.Core.Models.Powers.VigorPower)) return amount;
        return DamageAmplificationLimits.MultiplySaturated(amount,
            EnemyStatScalingHelper.IsEnemy(dealer) ? IncomingPowerUnit : OutgoingPowerUnit);
    }

    internal static decimal ConvertBlock(Creature target, decimal amount, ValueProp props)
    {
        if (amount <= 0m || !props.IsPoweredAttack() ||
            HotaruCannonBlockHelper.IsResolvedCurrentGain(target, props) ||
            !HbrBattleStatsHelper.TryGetStats(target, out HbrBattleStats stats)) return amount;
        decimal unit = EnemyStatScalingHelper.IsEnemy(target) ? OutgoingPowerUnit : PlayerBlockUnit;
        // Block is a STS resource. The HBR intelligence buff curve supplies its
        // stat-dependent support coefficient without treating Dexterity as DEX.
        decimal support = HbrOriginalNumericFormula.EvaluateBuffPotency(stats.Intelligence,
            new HbrSkillPowerCurve(0.8m, 1.2m, 300m));
        return decimal.Floor(DamageAmplificationLimits.MultiplySaturated(
            DamageAmplificationLimits.MultiplySaturated(amount, unit), support));
    }
}
