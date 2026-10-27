using System;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class TemporaryDamageBonusHelper
{
    internal static bool AppliesTo(Creature dealer) => dealer != null &&
        (dealer.Player != null || dealer.HasPower<PhantomPower>() || PhantomHelper.IsPhantomMinion(dealer));

    internal static bool IsTemporarySource(AbstractModel source, Creature dealer) =>
        AppliesTo(dealer) && source switch
        {
            PowerModel power => power.Owner == dealer,
            Overclock => true,
            _ => false
        };

    internal static bool IsModeSource(AbstractModel model) => HbrDamageMultiplierAccumulator.IsModeSource(model);

    internal static decimal ResolveMultiplier(decimal intrinsicMultiplier,
        AdditiveDamageResult group, decimal temporaryMultiplier, bool capped,
        bool positiveOnly = false)
    {
        if (!capped)
            return DamageAmplificationLimits.MultiplySaturated(Math.Max(0m,
                DamageAmplificationLimits.AddSaturated(intrinsicMultiplier,
                    positiveOnly ? group.PositiveMultiplierOffset : group.MultiplierOffset)), temporaryMultiplier);

        decimal permanent = positiveOnly ? Math.Max(0m, group.PermanentMultiplierOffset)
            : group.PermanentMultiplierOffset;
        // Weapon forms keep their basic reductions; their positive combat
        // enhancements compete with every other temporary source.
        decimal intrinsicTemporary = Math.Max(0m, intrinsicMultiplier - 1m);
        decimal baseline = DamageAmplificationLimits.AddSaturated(
            Math.Min(1m, intrinsicMultiplier),
            DamageAmplificationLimits.AddSaturated(permanent, group.OtherPositiveMultiplierOffset));
        decimal factor = Math.Max(1m, temporaryMultiplier);
        decimal temporary = Math.Min(DamageAmplificationLimits.MaximumTemporaryOffset,
            DamageAmplificationLimits.AddSaturated(
                MultiplyToBudget(DamageAmplificationLimits.AddSaturated(
                    group.TemporaryPositiveMultiplierOffset, intrinsicTemporary), factor),
                MultiplyToBudget(Math.Max(0m, baseline), factor - 1m)));
        decimal result = DamageAmplificationLimits.AddSaturated(baseline, temporary);
        // Debuffs and the target's reductions retain their effect after limiting
        // positive temporary amplification. Permanent growth spends no budget.
        if (!positiveOnly)
        {
            result = DamageAmplificationLimits.AddSaturated(result,
                DamageAmplificationLimits.MultiplySaturated(Math.Min(0m, baseline), factor - 1m));
            result = DamageAmplificationLimits.AddSaturated(result,
                DamageAmplificationLimits.MultiplySaturated(group.NegativeMultiplierOffset, factor));
        }
        return Math.Max(0m, result);
    }

    private static decimal MultiplyToBudget(decimal left, decimal right)
    {
        if (left <= 0m || right <= 0m) return 0m;
        decimal maximum = DamageAmplificationLimits.MaximumTemporaryOffset;
        return right >= 1m && left >= maximum / right ? maximum : Math.Min(maximum, left * right);
    }

    internal static decimal CalculateOutgoingMultiplier(Creature creature)
    {
        if (creature?.CombatState?.RunState is not { } runState) return 1m;
        const decimal reference = 100m;
        decimal runningDamage = reference;
        decimal unusedRawDamage = 0m;
        bool hbrNumerics = HbrBattleStatsHelper.IsEnabled(creature);
        HbrDamageMultiplierAccumulator multipliers = new(creature, 1m,
            HbrDamageMultiplierPolicy.OutgoingSummary, hbrNumerics);
        HbrgoSakiDamageMultiplierHelper.Begin(creature, null, hbrNumerics);
        bool completed = false;
        try
        {
            foreach (AbstractModel model in runState.IterateHookListeners(creature.CombatState))
            {
                if (PhantomHelper.IsSummonerOwnedDamageModifier(model, creature)) continue;
                decimal multiplier = DamageModifierCompatibility.ModifyDamageMultiplicative(model,
                    null, model.GetType().Assembly == typeof(TemporaryDamageBonusHelper).Assembly
                        ? reference : runningDamage, ValueProp.Move, creature, null, null);
                multipliers.Add(model, multiplier, ref runningDamage, ref unusedRawDamage);
            }
            AdditiveDamageResult group = HbrgoSakiDamageMultiplierHelper.Complete();
            completed = true;
            return DamageAmplificationLimits.MultiplySaturated(
                multipliers.Resolve(group, AppliesTo(creature)), multipliers.OtherMultiplier);
        }
        finally { if (!completed) HbrgoSakiDamageMultiplierHelper.Abort(); }
    }

    internal static decimal CalculateEffectiveBonusPercent(Creature creature)
    {
        if (creature == null) return 0m;
        if (creature.CombatState?.RunState == null)
        {
            var parts = PhantomHelper.GetDamageBonusBreakdown(creature);
            return Math.Max(0m, DamageAmplificationLimits.AddSaturated(parts.PermanentPercent,
                DamageAmplificationLimits.Clamp(parts.TemporaryPercent)));
        }
        return DamageAmplificationLimits.MultiplySaturated(
            Math.Max(0m, CalculateOutgoingMultiplier(creature) - 1m), 100m);
    }
}
