using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Expose is the native card's sole explicit enemy buff-removal operation in
// both supported profiles. Attribute only that command, never natural expiry,
// attribute reductions, power consumption or a general PowerCmd.Remove call.
[HarmonyPatch]
internal static class IreneNativeExposeDispelPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(Expose), "OnPlay")
        .GetCustomAttribute<AsyncStateMachineAttribute>().StateMachineType.GetMethod("MoveNext",
            BindingFlags.NonPublic | BindingFlags.Instance);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        FieldInfo source = AccessTools.GetDeclaredFields(__originalMethod.DeclaringType)
            .Single(field => field.Name == "<>4__this" && field.FieldType == typeof(Expose));
        MethodInfo adapter = AccessTools.Method(typeof(IreneNativeExposeDispelPatch), nameof(RemoveArtifact));
        int count = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo called && called.DeclaringType == typeof(PowerCmd) &&
                called.Name == nameof(PowerCmd.Remove) && called.IsGenericMethod &&
                called.GetGenericArguments().SequenceEqual(new[] { typeof(ArtifactPower) }) &&
                called.GetParameters().Length == 1)
            {
                CodeInstruction load = new(OpCodes.Ldarg_0);
                load.labels.AddRange(instruction.labels); load.blocks.AddRange(instruction.blocks);
                yield return load;
                yield return new(OpCodes.Ldfld, source);
                yield return new(OpCodes.Call, adapter);
                count++;
            }
            else yield return instruction;
        }
        if (count != 1) throw new InvalidOperationException("Expected one native Expose Artifact removal; found " + count);
    }

    private static async Task RemoveArtifact(Creature target, CardModel source)
    {
        ArtifactPower original = target?.GetPower<ArtifactPower>();
        try { await PowerCmd.Remove<ArtifactPower>(target); }
        finally
        {
            if (original != null && !target.Powers.Contains(original))
                IreneExposeCombatHelper.OnBuffKindDispelled(source?.Owner, target, original.Id);
        }
    }
}
