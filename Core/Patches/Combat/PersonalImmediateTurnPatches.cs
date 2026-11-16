using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetReadyToEndTurn))]
internal static class PersonalImmediateTurnReadyPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Player player, bool canBackOut, out bool? __state)
    {
        __state = null;
        if (PersonalImmediateTurnCoordinator.TryHandleEndTurn(player, canBackOut)) return false;
        if (!CombatManager.Instance.IsPlayerReadyToEndTurn(player)) __state = canBackOut;
        return true;
    }

    private static void Postfix(Player player, bool? __state)
    {
        if (__state is { } canBackOut && CombatManager.Instance.IsPlayerReadyToEndTurn(player))
            PersonalTurnReadyGuard.RecordNativeVote(player, canBackOut);
    }
}

[HarmonyPatch]
internal static class PersonalImmediateTurnUndoReadyPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "UndoReadyToEndTurn", "TryUndoReadyToEndTurn" })
            if (AccessTools.Method(typeof(CombatManager), name, new[] { typeof(Player) }) is { } method)
                yield return method;
    }

    private static void Postfix(Player player)
    {
        // A rejected beta TryUndo must keep the original forced-vote rule.
        if (!CombatManager.Instance.IsPlayerReadyToEndTurn(player)) PersonalTurnReadyGuard.ForgetNativeVote(player);
    }
}

[HarmonyPatch(typeof(CombatManager), "AfterAllPlayersReadyToEndTurn")]
internal static class PersonalImmediateTurnEndGatePatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(ref Task __result)
    {
        if (!PersonalImmediateTurnCoordinator.ShouldDeferOrdinaryEnd()) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.IsPartOfPlayerTurn))]
internal static class PersonalImmediateTurnParticipantPatch
{
    private static void Postfix(Player player, ref bool __result) =>
        __result |= PersonalImmediateTurnCoordinator.IsActiveFor(player);
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
internal static class PersonalImmediateTurnResetPatch
{
    private static void Prefix()
    {
        PersonalTurnReadyGuard.ClearVotes(CombatManager.Instance.DebugOnlyGetState());
        PersonalImmediateTurnCoordinator.Clear();
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.HandlePlayerDeath))]
internal static class PersonalImmediateTurnDeathPatch
{
    private static void Prefix(object[] __args)
    {
        foreach (object argument in __args)
            if (argument is Player player) { PersonalImmediateTurnCoordinator.OnPlayerDeath(player); break; }
    }
}

// Native extra-turn membership also gates the local hand/end button. Do not
// change its shared list: recognize only the personal participant at these UI
// readers, leaving every teammate's ordinary/native membership untouched.
[HarmonyPatch]
internal static class PersonalImmediateTurnUiParticipantPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NPlayerHand), "AreCardActionsAllowed");
        yield return AccessTools.Method(typeof(NPlayerHand), "UpdateHandDisabledState");
        yield return AccessTools.Method(typeof(NEndTurnButton), "PlayerCanTakeAction");
    }

    internal static bool ContainsParticipant(IEnumerable<Player> players, Player player) =>
        players.Contains(player) || PersonalImmediateTurnCoordinator.IsActiveFor(player);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        int changed = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo method && method.Name == "Contains" &&
                method.IsGenericMethod && method.GetGenericArguments().SequenceEqual(new[] { typeof(Player) }))
            {
                changed++;
                yield return new CodeInstruction(instruction)
                {
                    opcode = OpCodes.Call,
                    operand = AccessTools.Method(typeof(PersonalImmediateTurnUiParticipantPatch), nameof(ContainsParticipant))
                };
            }
            else yield return instruction;
        }
        if (changed != 1) throw new InvalidOperationException("Native personal-turn UI membership reader changed.");
    }
}
