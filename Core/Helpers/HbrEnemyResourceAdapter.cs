using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

// These are explicit STS resource adaptation coefficients, not an original HBR
// enemy's six attributes or fixed HP/DP data. Native encounter HP selection,
// multiplayer scaling and phase HP remain the source of the compatible baseline.
internal readonly record struct HbrEnemyResourceProfile(
    decimal NativeMaxHpScale,
    decimal NativeDpScale)
{
    internal decimal GetDpReferenceMaximum(decimal adaptedMaxHp)
    {
        // The current adapter preserves HP changes affecting the old STS shell
        // baseline. Separate resource coefficients let a future enemy resource
        // table replace that source without inventing VIT/SPR-to-HP/DP formulas.
        // Avoid dividing already-rounded HP on the compatibility path.
        return NativeDpScale == NativeMaxHpScale
            ? adaptedMaxHp
            : BigCombatNumbers.MultiplyPositive(adaptedMaxHp,
                NativeDpScale / NativeMaxHpScale);
    }
}

internal static class HbrEnemyResourceAdapter
{
    private static readonly HbrEnemyResourceProfile LegacySts = new(1m, 1m);
    private static readonly HbrEnemyResourceProfile HbrStsCompatibility = new(1000m, 1000m);

    internal static HbrEnemyResourceProfile GetProfile(Creature creature) =>
        EnemyStatScalingHelper.IsEnemy(creature) && HbrBattleStatsHelper.IsEnabled(creature)
            ? HbrStsCompatibility : LegacySts;

    internal static decimal GetMaxHpScale(Creature creature) =>
        GetProfile(creature).NativeMaxHpScale;

    // Called only at fresh shell/resource calculation boundaries. Existing DP,
    // serialized amounts and Narara's frozen layer cap never enter this adapter.
    internal static decimal GetDpReferenceMaximum(Creature creature) =>
        GetProfile(creature).GetDpReferenceMaximum(LargeCreatureHealthHelper.GetMaxHp(creature));
}
