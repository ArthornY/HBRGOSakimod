using System;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Each caller selects the actual pool once (all-party effects do not redirect
// the 30A hero to a phantom that is already another target in the same list).
internal static class SeiraDpRecoveryHelper
{
    private static bool Valid(Creature target, CardModel source) => target?.IsAlive == true &&
        !EnemyStatScalingHelper.IsEnemy(target) && !target.HasPower<LastStandPower>() &&
        (source?.Owner?.Creature is not { } healer || target.Side == healer.Side);
    private static bool HasMissingShellPool(Creature target) => target.HasPower<PhantomPower>() || target.HasPower<KozueSpiritPower>();
    private static decimal Maximum(Creature target, HighDimensionalShellPower shell) => shell?.MaxAmount ??
        (target.Monster is NararaShibaBossMonster ? NararaLayeredDpPower.CalculateInitialAmount(target)
            : target.HasPower<KozueSpiritPower>() ? BigCombatNumbers.ScaleHealth(LargeCreatureHealthHelper.GetMaxHp(target), .2m)
            : PhantomHighDimensionalShellPower.CalculateInitialAmount(target));
    private static decimal RecoveryMultiplier(Creature target) => DisabledPower.GetDpRecoveryMultiplier(target) *
        ElectronicSoldierManual.GetSimplicityDpRecoveryMultiplier(target.PetOwner);
    private static decimal Adjust(Creature target, decimal raw) => BigCombatNumbers.ClampStat(
        BigCombatNumbers.MultiplyPositive(raw, RecoveryMultiplier(target)));
    private static decimal FromRatio(Creature target, decimal maximum, decimal ratio) => BigCombatNumbers.ClampStat(
        BigCombatNumbers.MultiplyPositive(maximum, DamageAmplificationLimits.MultiplySaturated(ratio, RecoveryMultiplier(target))));
    private static decimal RestoreExistingShell(Creature target, HighDimensionalShellPower shell, decimal recovery,
        bool allowOverflow, decimal ceilingRatio)
    {
        if (recovery <= 0m) return 0m;
        decimal restored = shell.RestoreWithCeiling(recovery, allowOverflow, ceilingRatio);
        if (shell.CurrentDp > 0m && shell is NararaLayeredDpPower layered &&
            target.Monster is NararaShibaBossMonster boss && boss.EnsureRecoveredActiveDpLayer()) layered.RefreshLayerDisplay();
        return restored;
    }

    // Synchronous resource-invariant path used by Faith at a real DP-zero
    // setter. Only a valid missing phantom/spirit shell needs the async path.
    internal static bool TryRestoreExisting(Creature target, decimal ratio, bool allowOverflow, CardModel source = null)
        => TryRestoreExistingCore(target, YanagiButlerCombatHelper.ScaleRecovery(source, ratio),
            allowOverflow, source, 1.2m, false);

    internal static bool TryRestoreExistingFromAbility(Creature target, decimal ratio, bool allowOverflow)
        => TryRestoreExistingCore(target, ratio, allowOverflow, null, 1.2m, true);

    private static bool TryRestoreExistingCore(Creature target, decimal ratio, bool allowOverflow,
        CardModel source, decimal ceilingRatio, bool fromAbility)
    {
        if (!Valid(target, source) || ratio <= 0m) return true;
        if (target.Player is { } player)
        {
            int restored = player.GetRelic<DeflectorProtection>()?.RestoreDpByMaxPercentWithCeiling(ratio, allowOverflow, ceilingRatio) ?? 0;
            MariaRepentanceCombatHelper.OnDpRestored(target, restored, source, fromAbility);
            return true;
        }
        var shell = target.GetPower<HighDimensionalShellPower>();
        if (shell == null) return !HasMissingShellPool(target);
        if (shell.CanRestoreDp)
            MariaRepentanceCombatHelper.OnDpRestored(target, RestoreExistingShell(target, shell,
                FromRatio(target, Maximum(target, shell), ratio), allowOverflow, ceilingRatio), source, fromAbility);
        return true;
    }

    internal static Task Restore(PlayerChoiceContext context, Creature target, decimal ratio, bool allowOverflow, CardModel source)
        => RestoreWithCeiling(context, target, ratio, allowOverflow, source, 1.2m);

    internal static Task RestoreFromAbility(PlayerChoiceContext context, Creature target, decimal ratio, bool allowOverflow)
        => RestoreWithCeilingCore(context, target, ratio, allowOverflow, null, 1.2m, true);

    internal static Task RestoreWithCeiling(PlayerChoiceContext context, Creature target, decimal ratio,
        bool allowOverflow, CardModel source, decimal ceilingRatio)
        => RestoreWithCeilingCore(context, target, ratio, allowOverflow, source, ceilingRatio, false);

    private static async Task RestoreWithCeilingCore(PlayerChoiceContext context, Creature target, decimal ratio,
        bool allowOverflow, CardModel source, decimal ceilingRatio, bool fromAbility)
    {
        ratio = YanagiButlerCombatHelper.ScaleRecovery(source, ratio);
        if (TryRestoreExistingCore(target, ratio, allowOverflow, source, ceilingRatio, fromAbility)) return;
        decimal maximum = Maximum(target, null);
        if (maximum > 0m) await RestoreMissingShell(context, target,
            FromRatio(target, maximum, ratio), maximum, allowOverflow, source, ceilingRatio, fromAbility);
    }

    // A fixed resource amount never round-trips through amount / maximum.
    // Recovery modifiers and the same pool/cap/overflow rules are applied once.
    internal static async Task RestoreAmount(PlayerChoiceContext context, Creature target, decimal amount, bool allowOverflow, CardModel source)
    {
        if (!Valid(target, source) || amount <= 0m) return;
        amount = YanagiButlerCombatHelper.ScaleRecovery(source, amount);
        if (target.Player is { } player)
        {
            int restored = player.GetRelic<DeflectorProtection>()?.RestoreDpAmountWithLimit(amount, allowOverflow) ?? 0;
            MariaRepentanceCombatHelper.OnDpRestored(target, restored, source);
            return;
        }
        var shell = target.GetPower<HighDimensionalShellPower>();
        if (shell == null && !HasMissingShellPool(target) || shell?.CanRestoreDp == false) return;
        decimal maximum = Maximum(target, shell);
        if (maximum <= 0m) return;
        decimal recovery = Adjust(target, amount);
        if (shell != null) MariaRepentanceCombatHelper.OnDpRestored(target,
            RestoreExistingShell(target, shell, recovery, allowOverflow, 1.2m), source);
        else await RestoreMissingShell(context, target, recovery, maximum, allowOverflow, source, 1.2m, false);
    }

    private static async Task RestoreMissingShell(PlayerChoiceContext context, Creature target, decimal recovery,
        decimal maximum, bool allowOverflow, CardModel source, decimal ceilingRatio, bool fromAbility)
    {
        if (recovery <= 0m) return;
        HighDimensionalShellPower shell = target.Monster is NararaShibaBossMonster
            ? (HighDimensionalShellPower)ModelDb.Power<NararaLayeredDpPower>().ToMutable()
            : target.HasPower<KozueSpiritPower>() ? (HighDimensionalShellPower)ModelDb.Power<KozueSpiritShellPower>().ToMutable()
            : (HighDimensionalShellPower)ModelDb.Power<PhantomHighDimensionalShellPower>().ToMutable();
        if (allowOverflow) shell.AllowOvercapRecoveryWithCeiling(ceilingRatio);
        decimal ceiling = allowOverflow ? BigCombatNumbers.ClampStat(BigCombatNumbers.MultiplyPositive(maximum,
            HbrDpRecoveryLimit.Ratio(HbrDpRecoveryLimit.ToPercent(ceilingRatio)))) : maximum;
        // Structural resource units bypass an ordinary card die a second time.
        await PowerCmd.Apply(context, shell, target, Math.Min(recovery, ceiling),
            source?.Owner?.Creature ?? target.PetOwner?.Creature, null);
        shell = target.GetPower<HighDimensionalShellPower>();
        if (shell?.CurrentDp > 0m && shell is NararaLayeredDpPower layered && target.Monster is NararaShibaBossMonster boss &&
            boss.EnsureRecoveredActiveDpLayer()) layered.RefreshLayerDisplay();
        MariaRepentanceCombatHelper.OnDpRestored(target, shell?.CurrentDp ?? 0m, source, fromAbility);
    }
}
