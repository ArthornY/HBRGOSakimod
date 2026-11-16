using System;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Characters;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrMatchaDpHelper
{
    internal static async Task Restore(PlayerChoiceContext choiceContext, Player player,
        decimal percent, bool convertOverflow, CardModel source = null)
        => await RestoreCore(choiceContext, player, percent, convertOverflow, source, false);

    internal static Task RestoreFromAbility(PlayerChoiceContext choiceContext, Player player, decimal percent, bool convertOverflow)
        => RestoreCore(choiceContext, player, percent, convertOverflow, null, true);

    private static async Task RestoreCore(PlayerChoiceContext choiceContext, Player player,
        decimal percent, bool convertOverflow, CardModel source, bool fromAbility)
    {
        percent = YanagiButlerCombatHelper.ScaleRecovery(source, percent);
        // Resolve at execution time so deferred recovery follows the current
        // main phantom, including one summoned after the card was played.
        if (player?.Character is HbrgoSaki30A &&
            PhantomHelper.GetLivingPhantom(player) is { } phantom)
        {
            await RestorePhantom(choiceContext, player, phantom, percent, convertOverflow, source, fromAbility);
            return;
        }

        if (player?.GetRelic<DeflectorProtection>() is not { } protection) return;
        int restored = protection.RestoreDpByMaxPercent(percent, out int overflow);
        MariaRepentanceCombatHelper.OnDpRestored(player.Creature, restored, source, fromAbility);
        await ConvertOverflow(player.Creature, overflow, convertOverflow);
    }

    private static async Task RestorePhantom(PlayerChoiceContext choiceContext, Player player,
        Creature phantom, decimal percent, bool convertOverflow, CardModel source, bool fromAbility)
    {
        if (percent <= 0m || phantom.HasPower<LastStandPower>()) return;

        HighDimensionalShellPower shell = phantom.GetPower<HighDimensionalShellPower>();
        if (shell != null && !shell.CanRestoreDp) return;
        decimal maxDp = shell?.MaxAmount ?? (phantom.Monster is NararaShibaBossMonster
            ? NararaLayeredDpPower.CalculateInitialAmount(phantom)
            : PhantomHighDimensionalShellPower.CalculateInitialAmount(phantom));
        if (maxDp <= 0) return;

        decimal recoveryMultiplier = DisabledPower.GetDpRecoveryMultiplier(phantom)
            * ElectronicSoldierManual.GetSimplicityDpRecoveryMultiplier(player);
        decimal recovery = BigCombatNumbers.ClampStat(BigCombatNumbers.MultiplyPositive(
            maxDp, percent * recoveryMultiplier));
        if (recovery <= 0) return;
        decimal capacity = Math.Max(0m, maxDp - Math.Max(0m, shell?.CurrentDp ?? 0m));
        decimal before = shell?.CurrentDp ?? 0m;
        decimal overflow = Math.Max(0m, recovery - capacity);

        if (shell != null)
        {
            decimal restored = shell.Restore(recovery);
            if (restored > 0 && shell is NararaLayeredDpPower layered &&
                phantom.Monster is NararaShibaBossMonster boss && boss.EnsureRecoveredActiveDpLayer())
                layered.RefreshLayerDisplay();
        }
        else if (phantom.Monster is NararaShibaBossMonster boss)
        {
            // Rebuild only the recovered part of the active layer. The boss's
            // full-layer recovery would also grant another reserve layer.
            NararaLayeredDpPower recovered = await PowerCmd.Apply<NararaLayeredDpPower>(
                choiceContext, phantom, Math.Min(recovery, maxDp), player.Creature, source);
            if (recovered?.CurrentDp > 0m && boss.EnsureRecoveredActiveDpLayer())
                recovered.RefreshLayerDisplay();
        }
        else
        {
            await PowerCmd.Apply<PhantomHighDimensionalShellPower>(
                choiceContext, phantom, Math.Min(recovery, maxDp), player.Creature, source);
        }

        MariaRepentanceCombatHelper.OnDpRestored(phantom,
            (phantom.GetPower<HighDimensionalShellPower>()?.CurrentDp ?? 0m) - before, source, fromAbility);
        await ConvertOverflow(phantom, overflow, convertOverflow);
    }

    private static async Task ConvertOverflow(Creature target, decimal overflow, bool convertOverflow)
    {
        if (convertOverflow && overflow > 0 && target.CombatState != null && target.IsAlive)
        {
            // Overflow already includes DP recovery modifiers. It is not a card
            // base BlockVar and must not receive Dexterity or a second dice roll.
            await CreatureCmd.GainBlock(target, overflow * 2m, ValueProp.Unpowered, null);
        }
    }
}
