using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Rewards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Rewards;

namespace HBRGOSakiMod.Core.Patches;

// Native Reload connects RewardClaimed(NRewardButton) to a zero-argument
// callable. Repair only this mod's fragment groups; Ritsu still owns their
// synchronized ChooseOne resolution and all fragment claim receipts.
[HarmonyPatch(typeof(NLinkedRewardSet), "Reload")]
internal static class HbrFateFragmentRewardUiPatch
{
    internal sealed class CompletionReceipt
    {
        internal int Sent;
    }

    private static readonly ConditionalWeakTable<NLinkedRewardSet, CompletionReceipt> Receipts = new();

    internal static bool IsFateFragmentGroup(LinkedRewardSet group)
    {
        if (group == null || group.GetType() != typeof(LinkedRewardSet)) return false;
        IReadOnlyList<Reward> children = group.Rewards;
        return children.Count > 0 && children.All(reward => reward is HbrFateFragmentReward);
    }

    // This gate is independent of Godot presentation. The parent must already
    // have completed through real Reward.SelectUnsynchronized, not merely sent
    // a sidecar claim request. The three child callbacks share one receipt.
    internal static bool TryClaimCompletion(LinkedRewardSet group, CompletionReceipt receipt) =>
        group?.SuccessfullySelected == true && receipt != null &&
        Interlocked.CompareExchange(ref receipt.Sent, 1, 0) == 0;

    internal static Callable CreateClaimCallable(Action original)
    {
        if (original?.Target is not NLinkedRewardSet node || !IsFateFragmentGroup(node.LinkedRewardSet))
            return Callable.From(original!);

        CompletionReceipt receipt = Receipts.GetValue(node, static _ => new CompletionReceipt());
        return Callable.From<NRewardButton>(_ => NotifyClaimed(node, receipt));
    }

    private static void NotifyClaimed(NLinkedRewardSet node, CompletionReceipt receipt)
    {
        if (!GodotObject.IsInstanceValid(node) || !TryClaimCompletion(node.LinkedRewardSet, receipt)) return;
        try
        {
            // NRewardsScreen._Ready already connects this typed group signal
            // to RewardCollectedFrom. Do not also call that method directly.
            node.EmitSignal(NLinkedRewardSet.SignalName.RewardClaimed, node);
        }
        finally
        {
            node.QueueFreeSafely();
        }
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo original = AccessTools.Method(typeof(Callable), nameof(Callable.From), new[] { typeof(Action) });
        MethodInfo replacement = AccessTools.Method(typeof(HbrFateFragmentRewardUiPatch), nameof(CreateClaimCallable));
        int matches = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                matches++;
                yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = replacement };
            }
            else yield return instruction;
        }
        if (matches != 1)
            throw new InvalidOperationException("Expected one native linked-reward Action callable factory; found " + matches + ".");
    }
}
