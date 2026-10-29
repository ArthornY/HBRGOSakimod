using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

// These are explicit STS resource adaptation coefficients, not an original HBR
// enemy's six attributes or fixed HP/DP data. Native encounter HP selection,
// multiplayer scaling and phase HP remain the source of the compatible baseline.
internal readonly record struct HbrEnemyResourceProfile(
    decimal NativeReferenceHp,
    decimal ReferenceMaxHp,
    decimal ReferenceMaxDp)
{
    internal decimal NativeMaxHpScale => ReferenceMaxHp / NativeReferenceHp;

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
    private static readonly HbrEnemyResourceProfile SmallHopperOpening = new(14m, 14m * EnemyHpScale, 3000m);
    // All enemies share the rounded opening calibration, except Narara.
    // Keep the existing independent compatibility DP reference (200 per
    // native HP); changing the HP calibration must not retune that resource.
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
            _ => HbrStsCompatibility
        };
    }

    internal static decimal GetMaxHpScale(Creature creature) =>
        GetProfile(creature).NativeMaxHpScale;

    // Called only at fresh shell/resource calculation boundaries. Existing DP,
    // serialized amounts and Narara's frozen layer cap never enter this adapter.
    internal static decimal GetDpReferenceMaximum(Creature creature) =>
        GetProfile(creature).GetDpReferenceMaximum(LargeCreatureHealthHelper.GetMaxHp(creature));
}
