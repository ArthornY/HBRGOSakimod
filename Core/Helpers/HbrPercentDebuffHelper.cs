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
        EvaluateBasePotency(BasePercent(modifiers), intelligence, luck, enemyParameter);

    // Share the attribute curve with counter debuffs without importing the
    // percentage effects' modifier growth, independent slots or duration.
    internal static decimal EvaluateBasePotency(decimal basis, decimal intelligence, decimal luck, decimal enemyParameter)
    {
        if (basis <= 0m) return 0m;
        try
        {
            // Average last: an exact integer counter must not become 399.999...
            // because a recurring weighted-stat fraction was rounded earlier.
            decimal numerator = intelligence * 2m + luck - enemyParameter * 3m;
            decimal step = basis * HbrOriginalNumericFormula.DebuffOverflowPerPoint;
            if (step != 0m)
                return DamageAmplificationLimits.AddSaturated(basis, step * numerator / 3m);
        }
        catch (OverflowException)
        {
            // The normalized path below retains saturation for extreme inputs.
        }
        decimal reference = HbrOriginalNumericFormula.EvaluateWeightedStat(
            new HbrBattleStats(0, 0, intelligence, 0, 0, luck), Weights);
        return HbrOriginalNumericFormula.EvaluateLinearDebuffPotency(basis, reference, enemyParameter);
    }

    // Resolve only when applying a new defense-down effect or constructing the
    // Contest cannon's virtual view. Saved effects, copies and Double retain
    // their own snapshot instead of receiving this bonus again at damage time.
    internal static decimal ApplyVulnerableBaseBoost(Creature target, decimal basis) =>
        HbrBattleStatsHelper.IsEnabled(target) && SaviorGrandEntranceCombatHelper.HasEnemyUpgradeEffect(target, 5)
            ? DamageAmplificationLimits.MultiplySaturated(basis, 2m) : basis;

    internal static decimal ResolvePercent(PowerModel power, decimal modifiers, Creature applier, Creature target)
    {
        return ResolveBasis(power, power is FragilePower ? HbrFragilePowerHelper.BasePercent(modifiers)
            : BasePercent(modifiers), applier, target);
    }

    internal static decimal ResolveBasis(PowerModel power, decimal basis, Creature applier, Creature target)
    {
        if (power is FragilePower) return HbrEffectFormula.Debuff(basis, applier, target, HbrEffectFormula.Fragile);
        if (power is HbrDefenseDownPower or HbrDpDefenseDownPower)
            basis = ApplyVulnerableBaseBoost(target, ApplyDefenseDownSourceBoost(applier, basis));
        return HbrBattleStatsHelper.TryGetStats(applier, out HbrBattleStats stats)
            ? EvaluateBasePotency(basis, stats.Intelligence, stats.Luck, HbrEnemyProfileHelper.GetEnemyParameter(target))
            : HbrEffectFormula.Debuff(basis, applier, target, Weights);
    }

    // Snapshot the source passive only for a new application. Saved effects,
    // copied percentages and Double bypass ResolveBasis and cannot add it twice.
    internal static decimal ApplyDefenseDownSourceBoost(Creature applier, decimal basis) =>
        basis > 0m && KiryuExorcisingStringCombatHelper.HasPermanentWeakening(applier?.Player ?? applier?.PetOwner)
            ? DamageAmplificationLimits.AddSaturated(basis, 10m) : basis;

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
    internal static void SetUnlimited(PowerModel power, bool unlimited)
    {
        if (power is HbrPercentDebuffPower hbr) hbr.SetUnlimited(unlimited);
        else if (power is FragilePower fragile) fragile.SetUnlimited(unlimited);
    }
    internal static void ConfigureProfile(PowerModel power, HbrEffectProfile profile)
    {
        if (power is HbrPercentDebuffPower hbr) hbr.ConfigureProfile(profile);
        else if (power is FragilePower fragile) fragile.ConfigureProfile(profile);
    }
}
