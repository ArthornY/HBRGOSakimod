using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HBRGOSakiMod.Core.Models.Capabilities;

internal static class RollKeywordCombatState
{
    private static readonly ConditionalWeakTable<Player, PlayerState> States =
        new();

    internal static bool RollsAreFixedToOneHundred(Player player)
    {
        return player != null && Get(player).RollsAreFixedToOneHundred;
    }

    internal static void SetRollsFixedToOneHundred(
        Player player,
        bool enabled)
    {
        if (player != null)
        {
            Get(player).RollsAreFixedToOneHundred = enabled;
        }
    }

    internal static void BeginCombat(Player player)
    {
        if (player == null)
        {
            return;
        }

        PlayerState state = Get(player);
        ICombatState combatState = player.Creature?.CombatState;
        if (ReferenceEquals(state.CombatState, combatState))
        {
            return;
        }

        state.CombatState = combatState;
        state.CriticalSuccessCount = 0;
    }

    internal static void EndCombat(Player player)
    {
        if (player == null)
        {
            return;
        }

        PlayerState state = Get(player);
        state.CombatState = null;
        state.CriticalSuccessCount = 0;
    }

    internal static void RecordCriticalSuccess(Player player)
    {
        if (player != null)
        {
            Get(player).CriticalSuccessCount++;
        }
    }

    internal static int GetCriticalSuccessCount(Player player)
    {
        return player == null ? 0 : Get(player).CriticalSuccessCount;
    }

    private static PlayerState Get(Player player)
    {
        return States.GetValue(player, static _ => new PlayerState());
    }

    private sealed class PlayerState
    {
        internal ICombatState CombatState { get; set; }

        internal bool RollsAreFixedToOneHundred { get; set; }

        internal int CriticalSuccessCount { get; set; }
    }
}
