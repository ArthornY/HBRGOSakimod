using System;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// HBR percentages belong to independent models, never native Weak/Vulnerable.
internal static class HbrPercentDebuffHelper
{
    private static readonly HbrStatWeights Weights = new(0, 0, 2, 0, 0, 1);

    internal static bool Supports(PowerModel power) => power is FragilePower or HbrPercentDebuffPower;
    internal static bool IsEnabled(PowerModel power) => Supports(power) && power.IsMutable && HbrBattleStatsHelper.IsEnabled(power.Owner);
    internal static decimal BasePercent(decimal modifiers) => HbrFragilePowerHelper.ScaleModifierPercent(modifiers, 15m);

    internal static decimal EvaluatePercent(decimal modifiers, decimal intelligence, decimal luck, decimal enemyParameter) =>
        EvaluateBasePercent(BasePercent(modifiers), intelligence, luck, enemyParameter);

    private static decimal EvaluateBasePercent(decimal basis, decimal intelligence, decimal luck, decimal enemyParameter)
    {
        if (basis <= 0m) return 0m;
        decimal reference = HbrOriginalNumericFormula.EvaluateWeightedStat(
            new HbrBattleStats(0, 0, intelligence, 0, 0, luck), Weights);
        decimal difference = Math.Max(0m, reference - Math.Max(0m, enemyParameter));
        return DamageAmplificationLimits.MultiplySaturated(basis,
            1m + difference * HbrOriginalNumericFormula.DebuffOverflowPerPoint);
    }

    // Resolve only when applying a new defense-down effect or constructing the
    // Contest cannon's virtual view. Saved effects, copies and Double retain
    // their own snapshot instead of receiving this bonus again at damage time.
    internal static decimal ApplyVulnerableBaseBoost(Creature target, decimal basis) =>
        HbrBattleStatsHelper.IsEnabled(target) && SaviorGrandEntranceCombatHelper.HasEnemyUpgradeEffect(target, 5)
            ? DamageAmplificationLimits.MultiplySaturated(basis, 2m) : basis;

    internal static decimal ResolvePercent(PowerModel power, decimal modifiers, Creature applier, Creature target)
    {
        if (power is FragilePower) return HbrFragilePowerHelper.ResolvePercent(modifiers, applier, target);
        decimal basis = BasePercent(modifiers);
        if (power is HbrDefenseDownPower) basis = ApplyVulnerableBaseBoost(target, basis);
        return HbrBattleStatsHelper.TryGetStats(applier, out HbrBattleStats stats)
            ? EvaluateBasePercent(basis, stats.Intelligence, stats.Luck, HbrEnemyProfileHelper.GetEnemyParameter(target))
            : basis;
    }

    internal static int GetModifierAmount(PowerModel power) => power is HbrPercentDebuffPower hbr
        ? hbr.ModifierAmount : power is FragilePower fragile
        ? fragile.ModifierAmount : Math.Max(0, power?.Amount ?? 0);

    internal static decimal GetEffectPercent(PowerModel power) => power?.IsMutable != true ? 0m : power is HbrPercentDebuffPower hbr
        ? hbr.EffectPercent : power is FragilePower fragile
        ? fragile.HbrEffectPercent : 0m;

    internal static void Configure(PowerModel power, decimal percent, int modifiers)
    {
        power.AssertMutable();
        if (power is HbrPercentDebuffPower hbr) { hbr.Configure(percent, modifiers); return; }
        if (power is FragilePower fragile) { fragile.ConfigureHbr(percent, modifiers); return; }
        throw new ArgumentException("Unsupported percentage debuff", nameof(power));
    }
}
