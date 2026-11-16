using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Monsters;
using System;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

// The resource total uses STS encounter calibration. Table-monster profiles use
// original HBR DP/HP shares, not the original fixed total or invented six stats.
// Native HP selection, multiplayer and phase HP still supply the baseline.
internal readonly record struct HbrEnemyResourceProfile(
    decimal NativeReferenceHp,
    decimal ReferenceMaxHp,
    decimal ReferenceMaxDp)
{
    internal decimal NativeMaxHpScale => ReferenceMaxHp / NativeReferenceHp;

    internal decimal ScaleHealth(decimal nativeHp, decimal settingsMultiplier) => Math.Max(1m,
        EnemyResourceFraction.Floor(nativeHp, ReferenceMaxHp, settingsMultiplier, NativeReferenceHp));

    internal decimal ScaleDp(decimal adaptedMaxHp, decimal shellRatio) => Math.Max(1m,
        EnemyResourceFraction.Floor(adaptedMaxHp, ReferenceMaxDp, shellRatio,
            ReferenceMaxHp * (HBRGOSakiConfig.DefaultEnemyHighDimensionalShellPercent / 100m)));

    internal decimal GetDpReferenceMaximum(decimal adaptedMaxHp)
    {
        // ReferenceMaxDp is an independent resource value at the default shell
        // setting, not another HP coefficient. Existing shell calculators own
        // the configurable percentage, act growth and special full-layer rules.
        decimal defaultRatio = HBRGOSakiConfig.DefaultEnemyHighDimensionalShellPercent / 100m;
        decimal referenceDpBase = ReferenceMaxDp / defaultRatio;
        // Preserve the STS rule that phase replacements and later maximum-HP
        // changes resize fresh DP. Saved/current DP and frozen boss layers are
        // untouched. Keep the wider intermediate until the final resource clamp.
        // Multiply before dividing: a rounded repeating ratio (e.g. 1000/700)
        // can turn an exact 400 DP into 399 at the final floor. The wider
        // intermediate must not be clamped to the creature resource limit.
        return referenceDpBase == ReferenceMaxHp
            ? adaptedMaxHp
            : DamageAmplificationLimits.MultiplySaturated(adaptedMaxHp, referenceDpBase) / ReferenceMaxHp;
    }
}

internal static class HbrEnemyResourceAdapter
{
    private const decimal EnemyHpScale = 700m;
    private static readonly HbrEnemyResourceProfile LegacySts = new(1m, 1m, 0.2m);
    // The ordinary native midpoint (12..16) is the calibration reference only.
    // The actual native roll, difficulty, multiplayer and HP settings still
    // multiply both resource baselines; this is local encounter design data.
    private static readonly HbrEnemyResourceProfile SmallHopperOpening = new(14m, 8000m, 4800m);
    // Unlisted native monsters and the unchanged special bosses retain their
    // compatible resources. Table monsters use their original variant's share.
    private static readonly HbrEnemyResourceProfile HbrStsCompatibility = new(1m, EnemyHpScale, 200m);
    private static readonly HbrEnemyResourceProfile NararaShiba = new(1m, 1000m, 200m);

    internal static HbrEnemyResourceProfile GetProfile(Creature creature)
    {
        if (!EnemyStatScalingHelper.IsEnemy(creature) || !HbrBattleStatsHelper.IsEnabled(creature))
            return LegacySts;
        return creature.Monster switch
        {
            SmallHopper => SmallHopperOpening,
            NararaShibaBossMonster => NararaShiba,
            _ => HbrEnemyTableResourceProfiles.Get(creature.Monster) ?? HbrStsCompatibility
        };
    }

    internal static decimal GetMaxHpScale(Creature creature) =>
        GetProfile(creature).NativeMaxHpScale;

    internal static decimal ScaleHealth(Creature creature, decimal nativeHp) => GetProfile(creature).ScaleHealth(nativeHp,
        BigCombatNumbers.CalculateHealthMultiplier(HBRGOSakiConfig.EnemyMaxHpPercent,
            HBRGOSakiConfig.EnemyMaxHpGrowthPerActPercent,
            creature.CombatState?.RunState.CurrentActIndex ?? 0));

    internal static decimal CalculateMaximumDp(Creature creature, decimal shellRatio) =>
        GetProfile(creature).ScaleDp(LargeCreatureHealthHelper.GetMaxHp(creature), shellRatio);

    // Called only at fresh shell/resource calculation boundaries. Existing DP,
    // serialized amounts and Narara's frozen layer cap never enter this adapter.
    internal static decimal GetDpReferenceMaximum(Creature creature) =>
        GetProfile(creature).GetDpReferenceMaximum(LargeCreatureHealthHelper.GetMaxHp(creature));
}
