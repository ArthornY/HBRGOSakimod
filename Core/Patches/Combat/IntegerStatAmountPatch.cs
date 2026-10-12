using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class IntegerStatAmountPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return ResolveStateMachine(nameof(PowerCmd.Apply),
            typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature),
            typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool));
        yield return ResolveStateMachine(nameof(PowerCmd.ModifyAmount),
            typeof(PlayerChoiceContext), typeof(PowerModel), typeof(decimal),
            typeof(Creature), typeof(CardModel), typeof(bool));
    }

    private static MethodInfo ResolveStateMachine(string name, params Type[] parameters)
    {
        MethodInfo command = AccessTools.DeclaredMethod(typeof(PowerCmd), name, parameters)
            ?? throw new MissingMethodException(typeof(PowerCmd).FullName, name);
        Type stateMachine = command.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new InvalidOperationException($"Expected async PowerCmd.{name}.");
        return AccessTools.DeclaredMethod(stateMachine, "MoveNext")
            ?? throw new MissingMethodException(stateMachine.FullName, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        List<CodeInstruction> code = instructions.ToList();
        FieldInfo power = AccessTools.DeclaredField(__originalMethod.DeclaringType, "power");
        if (power?.FieldType != typeof(PowerModel))
            throw new InvalidOperationException("Expected PowerCmd state machine power field.");

        List<int> initialApplication = FindCalls(code, typeof(PowerModel), nameof(PowerModel.BeforeApplied));
        FieldInfo amount;
        int insertion;
        if (initialApplication.Count == 1)
        {
            // The five arguments join after both receiving hooks and optional
            // multiplayer scaling. Normalize once here, keeping intermediate
            // precision until every multiplier has contributed.
            int call = initialApplication[0];
            insertion = call - 10;
            if (insertion < 0 || Enumerable.Range(0, 5).Any(index =>
                    code[insertion + index * 2].opcode != OpCodes.Ldarg_0 ||
                    code[insertion + index * 2 + 1].opcode != OpCodes.Ldfld) ||
                !Equals(code[insertion + 1].operand, power) ||
                code[insertion + 5].operand is not FieldInfo initialAmount ||
                initialAmount.FieldType != typeof(decimal))
                throw new InvalidOperationException("Unexpected PowerCmd.Apply BeforeApplied arguments.");
            amount = initialAmount;
        }
        else if (initialApplication.Count == 0)
        {
            // Existing stacks record the offset immediately after receiving
            // hooks. History, temporary-stat callbacks and expiry counters
            // must receive the same integer offset that native storage uses.
            List<int> received = FindCalls(code, typeof(Hook), nameof(Hook.ModifyPowerAmountReceived));
            if (received.Count != 1 || received[0] + 2 >= code.Count ||
                code[received[0] + 1].opcode != OpCodes.Stfld ||
                code[received[0] + 1].operand is not FieldInfo modifiedOffset ||
                modifiedOffset.FieldType != typeof(decimal))
                throw new InvalidOperationException("Unexpected PowerCmd.ModifyAmount receiving boundary.");
            amount = modifiedOffset;
            insertion = received[0] + 2;
        }
        else
            throw new InvalidOperationException("Expected one PowerCmd.Apply BeforeApplied call.");

        List<CodeInstruction> normalize = new()
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, amount),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, power),
            new(OpCodes.Call, AccessTools.DeclaredMethod(typeof(IntegralStatAmountHelper),
                nameof(IntegralStatAmountHelper.Normalize))),
            new(OpCodes.Stfld, amount)
        };
        // Every scaled/unscaled branch must pass through normalization.
        normalize[0].labels.AddRange(code[insertion].labels);
        normalize[0].blocks.AddRange(code[insertion].blocks);
        code[insertion].labels.Clear();
        code[insertion].blocks.Clear();
        code.InsertRange(insertion, normalize);
        return code;
    }

    private static List<int> FindCalls(List<CodeInstruction> code, Type type, string name) =>
        Enumerable.Range(0, code.Count).Where(index =>
            code[index].operand is MethodInfo method &&
            method.DeclaringType == type && method.Name == name &&
            (code[index].opcode == OpCodes.Call || code[index].opcode == OpCodes.Callvirt)).ToList();
}
