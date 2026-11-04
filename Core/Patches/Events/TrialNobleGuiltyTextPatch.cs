using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Events;

namespace HBRGOSakiMod.Core.Patches.Events;

// Trial's Noble Guilty heals a literal 10 HP, which HbrNativeFixedResourceCommandPatch
// scales, and its option text writes the 10 literally. Tag that option when Accept
// creates it so the literal-number text substitution shows the scaled heal.
[HarmonyPatch(typeof(Trial), "Accept")]
internal static class TrialNobleGuiltyTextPatch
{
    private const string OptionKey = "TRIAL.pages.NOBLE.options.GUILTY";
    private const int NativeHeal = 10;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> body = instructions.ToList();
        int key = body.FindIndex(instruction => instruction.operand as string == OptionKey);
        int created = key < 0 ? -1 : body.FindIndex(key, instruction => instruction.opcode == OpCodes.Newobj &&
            instruction.operand is ConstructorInfo constructor && constructor.DeclaringType == typeof(EventOption));
        if (created < 0) throw new InvalidOperationException("Trial Noble Guilty option not found.");
        body.InsertRange(created + 1, new[]
        {
            new CodeInstruction(OpCodes.Dup),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TrialNobleGuiltyTextPatch), nameof(Tag)))
        });
        return body;
    }

    private static void Tag(EventOption option, Trial trial) =>
        HbrNativeFixedModifierDescriptionPatch.Tag(option.Description, trial.Owner?.Creature, NativeHeal);
}
