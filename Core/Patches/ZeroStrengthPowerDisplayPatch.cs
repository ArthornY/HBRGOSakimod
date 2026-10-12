using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches;

internal static class ZeroStrengthPowerDisplay
{
    private static readonly Action<NPowerContainer> UpdatePositions =
        AccessTools.MethodDelegate<Action<NPowerContainer>>(
            AccessTools.Method(typeof(NPowerContainer), "UpdatePositions"));

    internal static bool IsEmpty(PowerModel power) =>
        power is StrengthPower && power.Amount == 0;

    internal static List<NPower> GetLayoutNodes(List<NPower> nodes) =>
        nodes.Any(node => IsEmpty(node.Model))
            ? nodes.Where(node => !IsEmpty(node.Model)).ToList()
            : nodes;

    internal static void Refresh(NPower node, PowerModel power)
    {
        if (power is not StrengthPower)
            return;

        bool visible = power.IsVisible && !IsEmpty(power);
        if (node.Visible == visible)
            return;

        // Keep the model and its amount-change subscription intact, so both
        // positive and negative Strength can restore the badge after zero.
        node.Visible = visible;
        if (node.Container != null)
            UpdatePositions(node.Container);
    }
}

[HarmonyPatch(typeof(NPower), "RefreshAmount")]
internal static class ZeroStrengthPowerDisplayPatch
{
    private static void Postfix(NPower __instance, PowerModel ____model) =>
        ZeroStrengthPowerDisplay.Refresh(__instance, ____model);
}

[HarmonyPatch(typeof(NPowerContainer), "UpdatePositions")]
internal static class ZeroStrengthPowerLayoutPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        FieldInfo field = AccessTools.Field(typeof(NPowerContainer), "_powerNodes");
        MethodInfo filter = AccessTools.Method(typeof(ZeroStrengthPowerDisplay),
            nameof(ZeroStrengthPowerDisplay.GetLayoutNodes));
        List<CodeInstruction> body = instructions.ToList();
        if (field?.FieldType != typeof(List<NPower>) || filter == null ||
            !body.Any(instruction => instruction.LoadsField(field)))
            throw new InvalidOperationException("Power badge layout no longer exposes its node list.");

        // The native layout still controls spacing, wrapping and centering.
        // Use one filtered snapshot without changing its persistent node list.
        LocalBuilder nodes = generator.DeclareLocal(typeof(List<NPower>));
        yield return new CodeInstruction(OpCodes.Ldarg_0);
        yield return new CodeInstruction(OpCodes.Ldfld, field);
        yield return new CodeInstruction(OpCodes.Call, filter);
        yield return new CodeInstruction(OpCodes.Stloc, nodes);

        foreach (CodeInstruction instruction in body)
        {
            if (!instruction.LoadsField(field))
            {
                yield return instruction;
                continue;
            }

            CodeInstruction pop = new(OpCodes.Pop);
            pop.labels.AddRange(instruction.labels);
            pop.blocks.AddRange(instruction.blocks.Where(block =>
                block.blockType != ExceptionBlockType.EndExceptionBlock));
            CodeInstruction load = new(OpCodes.Ldloc, nodes);
            load.blocks.AddRange(instruction.blocks.Where(block =>
                block.blockType == ExceptionBlockType.EndExceptionBlock));
            yield return pop;
            yield return load;
        }
    }
}
