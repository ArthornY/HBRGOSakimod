using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Hook dispatch itself must follow the comparison: card capability listeners
// roll their normal dice in BeforeCardPlayed. This ordinary MoveNext call site
// is a safe boundary and leaves native generic commands untouched.
[HarmonyPatch]
internal static class NararaContestOrderPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(
        AccessTools.Method(typeof(CardModel), nameof(CardModel.OnPlayWrapper))
            .GetCustomAttribute<AsyncStateMachineAttribute>().StateMachineType, "MoveNext");

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo hook = AccessTools.Method(typeof(Hook), nameof(Hook.BeforeCardPlayed));
        MethodInfo adapter = AccessTools.Method(typeof(NararaContestOrderPatch), nameof(BeforeCardPlayed));
        int changed = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(hook)) { instruction.operand = adapter; changed++; }
            yield return instruction;
        }
        if (changed != 1) throw new InvalidOperationException("Expected one native BeforeCardPlayed dispatch in OnPlayWrapper.");
    }

    internal static async Task BeforeCardPlayed(ICombatState combat, CardPlay play)
    {
        await NararaNullificationHelper.ResolvePlay(play);
        await Hook.BeforeCardPlayed(combat, play);
    }
}
