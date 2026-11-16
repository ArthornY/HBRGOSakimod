using System;
using System.Linq;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
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
    internal const decimal EnvironmentalPowerUnit = HbrBattleStatsHelper.PlayerHealthScale;
    internal const decimal PlayerBlockUnit = HbrSkillPower.PlayerBlockUnit;
    internal static readonly HbrStatWeights HpWeights = new(1, 1, 0, 0, 0, 0);
    internal static readonly HbrStatWeights DpWeights = HpWeights;

    internal static decimal ConvertBaseDamage(Creature target, Creature dealer,
        decimal legacyPower, ValueProp props, CardModel cardSource) =>
        ConvertBaseDamageWithSkillBonus(target, dealer, legacyPower, props, cardSource, 0m);

    internal static decimal ConvertBaseDamageWithSkillBonus(Creature target, Creature dealer,
        decimal legacyPower, ValueProp props, CardModel cardSource, decimal bonusUnits)
    {
        bool effectSkill = HbrEffectSkillDamageContext.TryClaimBaseConversion(
            target, dealer, cardSource, legacyPower, props, out Creature effectSource);
        Creature statSource = effectSkill ? effectSource : dealer;
        decimal sourceUnit = EnemyStatScalingHelper.IsEnemy(statSource) ? IncomingPowerUnit : OutgoingPowerUnit;
        if (!props.IsPoweredAttack() && !effectSkill ||
            !effectSkill && !HbrBattleStatsHelper.IsEnabled(dealer))
            return legacyPower;

        // Resource-derived attacks already carry world-space DP/HP/block units.
        // Normalize that basis before the same attribute/critical formula; never
        // mistake a combat-world value for a raw legacy coefficient.
        if (!effectSkill && HbrAbsoluteBaseDamageContext.Matches(target, dealer, cardSource, legacyPower))
            return EvaluateSkillDamage(target, statSource, dealer,
                DamageAmplificationLimits.AddSaturated(DamageAmplificationLimits.AddSaturated(
                    legacyPower / sourceUnit, bonusUnits),
                    NiinaMakeupCombatHelper.GetBonusUnits(dealer, cardSource, props)),
                props, cardSource, effectSkill: false, allowCritical: true);

        if (effectSkill && HbrEffectSkillDamageContext.IsCurrentWorldBasis)
            legacyPower /= sourceUnit;

        if (!effectSkill)
        {
            legacyPower = DamageAmplificationLimits.AddSaturated(legacyPower,
                NiinaMakeupCombatHelper.GetBonusUnits(dealer, cardSource, props));
            legacyPower = ThirtyOneCMachinaHelper.ScaleSkillUnits(dealer, cardSource, props, legacyPower);
        }

        // Keep the original command amount for source-scope matching above.
        // Vigor is a base coefficient: six units add 100 percentage points.
        // Strike Dummy adds skill percentage points the same way, rather than
        // flat damage after the curve; the native hook is suppressed below.
        // Both join before one check, so a zero-base Strike still gets them.
        // Effect skills may have no dealer, so only attacks look for the relic.
        decimal skillBonus = effectSkill ? bonusUnits : DamageAmplificationLimits.AddSaturated(
            bonusUnits, DamageAmplificationLimits.AddSaturated(GetStrikeDummyBonus(dealer, cardSource),
                HBRGOSakiMod.Core.Models.Relics.TigersMouthSnackBond.GetDamageBonusUnits(dealer, cardSource)));
        legacyPower = DamageAmplificationLimits.AddSaturated(legacyPower, skillBonus);
        if (legacyPower <= 0m) return legacyPower;

        // Audited environmental effects have no attacker to invent attributes
        // for. Like fixed HP losses, their units keep the native share of the
        // hundredfold player HP, then enter the normal target defenses/caps.
        if (effectSkill && statSource == null && HbrEffectSkillDamageContext.IsCurrentWorldBasis)
            return legacyPower * sourceUnit;
        if (effectSkill && statSource == null)
            return HbrSkillPower.DamageBaseFromUnits(legacyPower, EnvironmentalPowerUnit);

        ValueProp numericalProps = effectSkill
            ? HbrEffectSkillDamageContext.GetPersistentEffectProps(props) : props;

        // Ghost's Focused Breakthrough scales its skill coefficient, before
        // the shared attribute curve. Precalculated and absolute-base hits
        // bypass this conversion boundary, so they cannot receive it twice.
        if (statSource.GetPower<GhostStatePower>() is { } ghost)
            legacyPower = DamageAmplificationLimits.AddSaturated(legacyPower, ghost.GetBonusUnits(target, numericalProps));

        return EvaluateSkillDamage(target, statSource, dealer, legacyPower, props, cardSource,
            effectSkill, allowCritical: true);
    }

    // A native additive coefficient uses the same attributes as the attack,
    // without claiming its base conversion or adding Ghost's base units again.
    private static decimal EvaluateSkillDamage(Creature target, Creature statSource, Creature dealer,
        decimal units, ValueProp props, CardModel cardSource, bool effectSkill, bool allowCritical)
    {
        bool incoming = EnemyStatScalingHelper.IsEnemy(statSource);
        if (incoming)
        {
            decimal defensiveParameter;
            if (HbrBattleStatsHelper.TryGetStats(target, out HbrBattleStats defense))
            {
                defense = HbrStatPowerHelper.GetIncomingDefense(statSource, defense);
                defensiveParameter = HbrEnemySkillProfiles.Get(statSource).EvaluateDefense(defense);
            }
            else if (EnemyStatScalingHelper.IsEnemy(target) && HbrBattleStatsHelper.IsEnabled(target))
            {
                // Confusion and enemy-owned reflections can hit another enemy.
                // Its one E is the defensive parameter; do not invent or save
                // six player attributes, or return unscaled raw skill units.
                defensiveParameter = HbrEnemyProfileHelper.GetEnemyParameter(target);
            }
            else return units;
            return HbrSkillPower.EvaluateIncomingDamageFromUnits(units,
                HbrEnemyProfileHelper.GetEnemyParameter(statSource), defensiveParameter, IncomingPowerUnit);
        }

        if (!HbrBattleStatsHelper.TryGetStats(statSource, out HbrBattleStats stats))
            return units;
        ValueProp numericalProps = effectSkill
            ? HbrEffectSkillDamageContext.GetPersistentEffectProps(props) : props;
        stats = HbrStatPowerHelper.GetOutgoingAttack(target, statSource, stats, cardSource, numericalProps);
        decimal parameter = HbrEnemyProfileHelper.GetEnemyParameter(target);
        bool critical = allowCritical && (effectSkill
            ? HbrEffectSkillDamageContext.IsCritical(stats.Luck, parameter)
            : !HbrEffectSkillDamageContext.HasEffectCommand && HbrCriticalDamageContext.IsCritical(target, dealer,
                cardSource, props, stats.Luck, parameter));
        bool hpOnlyEffect = effectSkill && HpOnlyDamageContext.IsCurrentHpDamage(target, dealer, cardSource, props);
        HbrStatWeights weights = HbrSkillAttributeProfiles.GetDamageWeights(cardSource,
            !hpOnlyEffect && target?.GetPower<HighDimensionalShellPower>()?.CurrentDp > 0m);
        decimal power = HbrSkillPower.EvaluateDamageFromUnits(units,
            HbrOriginalNumericFormula.EvaluateWeightedStat(stats, weights), parameter, critical);
        return critical ? DamageAmplificationLimits.MultiplySaturated(power,
            HbrOriginalNumericFormula.CriticalMultiplier()) : power;
    }

    internal static decimal ConvertFlatDamageBonus(AbstractModel model, Creature target, Creature dealer,
        CardModel cardSource, ValueProp props, decimal amount, bool enabled, bool allowCritical)
    {
        // Strength now contributes attributes, never a second flat bonus.
        if (model is MegaCrit.Sts2.Core.Models.Powers.StrengthPower)
            return HbrBattleStatsHelper.IsEnabled(dealer) ? 0m : amount;
        if (model is StrikeDummy dummy && HbrBattleStatsHelper.IsEnabled(dummy.Owner) &&
            HbrBattleStatsHelper.IsEnabled(dealer)) return 0m;
        // Audited raw additive units use six to 100% skill power, applied
        // at the native additive stage before the attack's multipliers/caps.
        if (enabled && amount > 0m && HbrAdditiveSkillDamageSources.Supports(model) &&
            HbrBattleStatsHelper.IsEnabled(dealer))
            return EvaluateSkillDamage(target, dealer, dealer, amount, props, cardSource,
                HbrEffectSkillDamageContext.IsCurrentEffectCalculation, allowCritical);
        // HBR Vigor has already joined the base skill coefficient. Legacy
        // modifiers keep their native additive units here.
        return amount;
    }

    private static decimal GetStrikeDummyBonus(Creature dealer, CardModel cardSource)
    {
        if (cardSource == null || !cardSource.Tags.Contains(CardTag.Strike)) return 0m;
        StrikeDummy dealerRelic = dealer.Player?.GetRelic<StrikeDummy>();
        StrikeDummy cardRelic = cardSource.Owner?.GetRelic<StrikeDummy>();
        decimal Bonus(StrikeDummy relic) => relic != null &&
            HbrBattleStatsHelper.IsEnabled(relic.Owner) &&
            !PhantomHelper.IsSummonerOwnedDamageModifier(relic, dealer)
                ? HbrSkillPower.DamageUnits(50m) : 0m;
        // A borrowed card can qualify its owner's relic as well as the dealer's.
        return DamageAmplificationLimits.AddSaturated(Bonus(dealerRelic),
            ReferenceEquals(cardRelic, dealerRelic) ? 0m : Bonus(cardRelic));
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
