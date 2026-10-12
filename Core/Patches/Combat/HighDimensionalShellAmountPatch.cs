using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.ApplyInternal))]
internal static class HighDimensionalShellInitialAmountPatch
{
    internal sealed record Preparation(HighDimensionalShellPower Shell, decimal? Previous);

    private static void Prefix(PowerModel __instance, ref decimal amount, out Preparation __state)
    {
        __state = null;
        if (__instance is not HighDimensionalShellPower shell || amount == 0m) return;
        __state = new(shell, shell.PrepareInitialNativeDp(amount));
        // Modify only the callee's storage argument. PowerCmd retains the real
        // modified amount for its history and amount-change hooks.
        decimal proxy = DpAmountCodec.ToNative(amount);
        if (proxy != 0m) amount = proxy;
    }

    private static void Finalizer(Preparation __state) =>
        __state?.Shell.RestorePreparedNativeDp(__state.Previous);
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.SetAmount))]
internal static class HighDimensionalShellNativeAmountPatch
{
    private static void Prefix(PowerModel __instance, ref int amount,
        out HighDimensionalShellPower.NativeDpWrite __state)
    {
        __state = __instance is HighDimensionalShellPower shell
            ? shell.BeginNativeDpWrite(ref amount) : null;
    }

    private static void Postfix(PowerModel __instance, HighDimensionalShellPower.NativeDpWrite __state)
    {
        if (__instance is HighDimensionalShellPower shell) shell.FinishNativeDpWrite(__state);
    }

    private static void Finalizer(PowerModel __instance, HighDimensionalShellPower.NativeDpWrite __state)
    {
        if (__instance is HighDimensionalShellPower shell) shell.FinishNativeDpWrite(__state);
    }
}

[HarmonyPatch]
internal static class HighDimensionalShellStackAmountPatch
{
    private static MethodBase TargetMethod() => MonsterCardRecipientPatch.GetAsyncBody(
        AccessTools.Method(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount)));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        var code = instructions.ToList();
        FieldInfo power = AccessTools.Field(__originalMethod.DeclaringType, "power");
        var conversions = code.Select((instruction, index) => (instruction, index)).Where(pair =>
            pair.instruction.operand is MethodInfo method && method.DeclaringType == typeof(decimal) &&
            method.IsStatic && method.ReturnType == typeof(int) &&
            method.GetParameters() is [{ ParameterType: var argument }] && argument == typeof(decimal) &&
            method.Name is "op_Explicit" or "ToInt32").ToArray();
        if (power?.FieldType != typeof(PowerModel) || conversions.Length != 2)
            throw new InvalidOperationException("Expected two decimal storage/check conversions in PowerCmd.ModifyAmount.");

        int storage = conversions[0].index;
        int check = conversions[1].index;
        if (storage < 1 || storage + 1 >= code.Count || check < 1 ||
            code[storage - 1].operand is not FieldInfo offset || offset.FieldType != typeof(decimal) ||
            !Equals(code[check - 1].operand, offset) || code[storage + 1].opcode != OpCodes.Add)
            throw new InvalidOperationException("Unexpected PowerCmd.ModifyAmount decimal-offset arithmetic.");

        for (int index = conversions.Length - 1; index >= 0; index--)
        {
            int position = conversions[index].index;
            CodeInstruction original = code[position];
            var first = new CodeInstruction(OpCodes.Ldarg_0);
            first.labels.AddRange(original.labels);
            first.blocks.AddRange(original.blocks.Where(block => block.blockType != ExceptionBlockType.EndExceptionBlock));
            var last = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HighDimensionalShellStackAmountPatch),
                index == 0 ? nameof(PrepareOffset) : nameof(OffsetForNotification)));
            last.blocks.AddRange(original.blocks.Where(block => block.blockType == ExceptionBlockType.EndExceptionBlock));
            code.RemoveAt(position);
            code.InsertRange(position, new[] { first, new CodeInstruction(OpCodes.Ldfld, power), last });
        }
        return code;
    }

    private static int PrepareOffset(decimal amount, PowerModel power) => power switch
    {
        HighDimensionalShellPower shell => shell.PrepareNativeDpOffset(amount),
        PhantomDamageInheritancePower inheritance => inheritance.PrepareNativePercentOffset(amount),
        HotaruDamageAmplificationPower amplification => amplification.PrepareNativePercentOffset(amount),
        _ => (int)amount
    };

    private static int OffsetForNotification(decimal amount, PowerModel power) =>
        power is HighDimensionalShellPower ? decimal.Sign(decimal.Truncate(amount))
            : power is PhantomDamageInheritancePower or HotaruDamageAmplificationPower
                ? decimal.Sign(amount) : (int)amount;
}
