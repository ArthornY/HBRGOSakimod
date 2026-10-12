using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Characters;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrgoHandSelectionLifetime
{
    private sealed record SelectionRun(IRunState Run);

    private static readonly ConditionalWeakTable<NPlayerHand, SelectionRun> Selections = new();

    internal static void Capture(NPlayerHand hand, CombatState combatState)
    {
        Selections.Remove(hand);
        // Capture the old hand's owner before SL can replace LocalContext and
        // RunManager. A HBRGO teammate must not affect another character's hand.
        if (LocalContext.NetId is ulong localId &&
            combatState?.RunState is { } run &&
            run.GetPlayer(localId)?.Character is HbrgoSaki)
        {
            Selections.Add(hand, new SelectionRun(run));
        }
    }

    internal static void CancelAbandonedSelection(
        NPlayerHand hand,
        TaskCompletionSource<IEnumerable<CardModel>> completion)
    {
        if (!Selections.TryGetValue(hand, out SelectionRun selection)) return;
        Selections.Remove(hand);
        if (completion == null || completion.Task.IsCompleted) return;

        RunManager manager = RunManager.Instance;
        if (manager.IsCleaningUp || !ReferenceEquals(manager.DebugOnlyGetState(), selection.Run))
        {
            // Vanilla _ExitTree completes this with an empty result, allowing
            // the old selection to restore detached UI and sync into the new
            // run. Cancellation stops that continuation at its existing await.
            completion.TrySetCanceled();
        }
    }
}

[HarmonyPatch(typeof(NPlayerHand), nameof(NPlayerHand.SelectCards))]
internal static class HbrgoHandSelectionCapturePatch
{
    private static void Prefix(NPlayerHand __instance, CombatState ____combatState) =>
        HbrgoHandSelectionLifetime.Capture(__instance, ____combatState);
}

[HarmonyPatch(typeof(NPlayerHand), nameof(NPlayerHand._ExitTree))]
internal static class HbrgoHandSelectionTeardownPatch
{
    private static void Prefix(
        NPlayerHand __instance,
        TaskCompletionSource<IEnumerable<CardModel>> ____selectionCompletionSource) =>
        HbrgoHandSelectionLifetime.CancelAbandonedSelection(__instance, ____selectionCompletionSource);
}
