using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class MoonlightReplayDisplayPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.HoverTips))
            ?? throw new MissingMethodException(typeof(CardModel).FullName, "get_HoverTips");
        yield return typeof(CardModel).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method => method.Name == nameof(CardModel.GetDescriptionForPile) &&
                method.ReturnType == typeof(string) && method.GetParameters() is var parameters &&
                parameters.Length == 3 && parameters[0].ParameterType == typeof(PileType));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        MethodInfo original = AccessTools.Method(typeof(CardModel), nameof(CardModel.GetEnchantedReplayCount));
        MethodInfo displayed = AccessTools.Method(typeof(MoonlightReplayDisplayHelper),
            nameof(MoonlightReplayDisplayHelper.GetDisplayedReplayCount));
        List<CodeInstruction> result = instructions.ToList();
        int[] calls = result.Select((instruction, index) => (instruction, index))
            .Where(item => item.instruction.Calls(original)).Select(item => item.index).ToArray();
        if (calls.Length != 1)
            throw new InvalidOperationException($"Expected one native replay-count read in {__originalMethod.Name}; found {calls.Length}.");
        // Keep the native REPLAY.extraText row and ReplayDynamic hover tip, only
        // supplying their displayed count. Do not patch the gameplay getter.
        int index = calls[0];
        result[index] = new CodeInstruction(result[index]) { opcode = OpCodes.Call, operand = displayed };
        return result;
    }
}
