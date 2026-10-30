using System;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Skill definitions use explicit coefficients. Native DynamicVars and commands
// retain their compatible units, so upgrades, enchants and old saves cross one
// conversion boundary here. These profiles are adaptation values, not original
// HBR character/monster skill data.
internal static class HbrDamageSkillAdapter
{
    internal const decimal OutgoingPowerUnit = HbrSkillPower.OutgoingDamageUnit;
    internal const decimal IncomingPowerUnit = 100m;
    internal const decimal PlayerBlockUnit = HbrSkillPower.PlayerBlockUnit;
    internal static readonly HbrStatWeights HpWeights = new(2, 1, 0, 0, 0, 0);
    internal static readonly HbrStatWeights DpWeights = new(1, 2, 0, 0, 0, 0);

    internal static decimal ConvertBaseDamage(Creature target, Creature dealer,
        decimal legacyPower, ValueProp props, CardModel cardSource)
    {
        if (legacyPower <= 0m || !props.IsPoweredAttack() ||
            !HbrBattleStatsHelper.IsEnabled(dealer))
            return legacyPower;

        // Resource-derived attacks already carry a world-space DP/HP/block
        // amount. They still receive normal attack modifiers, not another skill
        // coefficient conversion or stat-curve/natural-critical evaluation.
        if (HbrAbsoluteBaseDamageContext.Matches(target, dealer, cardSource, legacyPower))
            return legacyPower;

        // Ghost's Focused Breakthrough scales its skill coefficient, before
        // the shared attribute curve. Precalculated and absolute-base hits
        // bypass this conversion boundary, so they cannot receive it twice.
        if (dealer.GetPower<GhostStatePower>() is { } ghost)
            legacyPower = DamageAmplificationLimits.AddSaturated(legacyPower, ghost.GetBonusUnits(target, props));

        bool incoming = EnemyStatScalingHelper.IsEnemy(dealer);
        if (incoming)
        {
            if (!HbrBattleStatsHelper.TryGetStats(target, out HbrBattleStats defense))
                return legacyPower;
            defense = HbrStatPowerHelper.GetIncomingDefense(dealer, defense);
            HbrEnemySkillProfile skill = HbrEnemySkillProfiles.Get(dealer);
            return HbrOriginalNumericFormula.EvaluateIncomingAttackCurve(
                HbrEnemyProfileHelper.GetEnemyParameter(dealer),
                skill.EvaluateDefense(defense), skill.CurveFromUnits(legacyPower));
        }

        if (!HbrBattleStatsHelper.TryGetStats(dealer, out HbrBattleStats stats))
            return legacyPower;
        stats = HbrStatPowerHelper.GetOutgoingAttack(target, dealer, stats, cardSource, props);
        decimal parameter = HbrEnemyProfileHelper.GetEnemyParameter(target);
        bool critical = HbrCriticalDamageContext.IsCritical(target, dealer,
            cardSource, props, stats.Luck, parameter);
        HbrStatWeights weights = target?.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m
            ? DpWeights : HpWeights;
        decimal power = HbrSkillPower.EvaluateDamageFromUnits(legacyPower,
            HbrOriginalNumericFormula.EvaluateWeightedStat(stats, weights), parameter, critical);
        return critical ? DamageAmplificationLimits.MultiplySaturated(power,
            HbrOriginalNumericFormula.CriticalMultiplier()) : power;
    }

    internal static decimal ConvertFlatDamageBonus(AbstractModel model, Creature dealer,
        decimal amount, bool enabled)
    {
        // Strength now contributes attributes, never a second flat bonus.
        if (model is MegaCrit.Sts2.Core.Models.Powers.StrengthPower)
            return HbrBattleStatsHelper.IsEnabled(dealer) ? 0m : amount;
        // Vigor retains its separate one-shot additive damage behavior.
        if (!enabled || model is not MegaCrit.Sts2.Core.Models.Powers.VigorPower) return amount;
        return DamageAmplificationLimits.MultiplySaturated(amount,
            EnemyStatScalingHelper.IsEnemy(dealer) ? IncomingPowerUnit : OutgoingPowerUnit);
    }

    internal static decimal ConvertBlock(Creature target, decimal amount, ValueProp props)
    {
        if (amount <= 0m || !props.IsPoweredAttack() ||
            HotaruCannonBlockHelper.IsResolvedCurrentGain(target, props)) return amount;
        if (EnemyStatScalingHelper.IsEnemy(target))
        {
            // Block is a STS-only resource. Retain its explicit support bridge,
            // using E as an adaptation parameter without inventing enemy INT.
            return HbrBattleStatsHelper.IsEnabled(target)
                ? HbrSkillPower.EvaluateEnemyBlockFromUnits(amount,
                    HbrEnemyProfileHelper.GetEnemyParameter(target), OutgoingPowerUnit)
                : amount;
        }
        Creature statSource = HotaruCannonBlockHelper.GetStatSource(target);
        if (!HbrBattleStatsHelper.TryGetStats(statSource, out HbrBattleStats stats)) return amount;
        // The same single intelligence coefficient serves player cards,
        // phantoms and cannon grants. Already resolved gains bypass it above.
        return HbrSkillPower.EvaluateBlockFromUnits(amount, stats.Intelligence, PlayerBlockUnit);
    }
}
