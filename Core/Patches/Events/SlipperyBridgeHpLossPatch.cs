using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Events;

namespace HBRGOSakiMod.Core.Patches.Events;

// Slippery Bridge's Hold On loses its CurrentHpLoss property, not its HpLoss
// variable, so the fixed-variable readers never see the amount. Scale only the
// reads that reach the HP loss command and the options' lethal checks. The read
// written back into HpLoss stays native; its text scales via the fixed registry.
// Beta changed the damage command's overload but not these call sites.
[HarmonyPatch]
internal static class SlipperyBridgeHpLossPatch
{
    private static readonly MethodInfo CurrentHpLoss =
        AccessTools.PropertyGetter(typeof(SlipperyBridge), "CurrentHpLoss");

    private static readonly Func<SlipperyBridge, int> ReadCurrentHpLoss =
        AccessTools.MethodDelegate<Func<SlipperyBridge, int>>(CurrentHpLoss);

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(SlipperyBridge), "GenerateInitialOptions");
        MethodInfo holdOn = AccessTools.Method(typeof(SlipperyBridge), "HoldOn");
        Type state = holdOn.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType ??
            throw new MissingMethodException("Missing native Slippery Bridge Hold On body.");
        yield return AccessTools.Method(state, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase original)
    {
        CodeInstruction[] body = instructions.ToArray();
        int scaled = 0;
        for (int index = 0; index < body.Length; index++)
        {
            if (!body[index].Calls(CurrentHpLoss) || !ReachesHpLoss(body, index)) continue;
            body[index].opcode = OpCodes.Call;
            body[index].operand = AccessTools.Method(typeof(SlipperyBridgeHpLossPatch), nameof(ScaledHpLoss));
            scaled++;
        }
        if (scaled == 0)
            throw new InvalidOperationException("Slippery Bridge HP loss read not found in " + original.Name);
        return body;
    }

    // The amount passes its decimal conversion and the command's props/null
    // arguments; any other consumer, such as HpLoss's setter, keeps it native.
    private static bool ReachesHpLoss(CodeInstruction[] body, int index)
    {
        for (int next = index + 1; next < body.Length; next++)
        {
            CodeInstruction instruction = body[next];
            if (instruction.opcode == OpCodes.Ldnull || instruction.LoadsConstant()) continue;
            if (instruction.operand is not MethodInfo method) return false;
            if (method.DeclaringType == typeof(decimal)) continue;
            return method.DeclaringType == typeof(CreatureCmd) && method.Name == nameof(CreatureCmd.Damage) ||
                method.DeclaringType == typeof(EventOption) && method.Name == nameof(EventOption.ThatDoesDamage);
        }
        return false;
    }

    private static int ScaledHpLoss(SlipperyBridge bridge) =>
        HbrResourceValueHelper.FixedInt(bridge.Owner?.Creature, ReadCurrentHpLoss(bridge));
}
