using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class FragileAutoPlayAnimationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return GetPowerCommands().Select(GetAsyncBody);
    }

    internal static IEnumerable<MethodInfo> GetPowerCommands()
    {
        yield return GetPowerCommand(nameof(PowerCmd.Apply), new[]
        {
            typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature),
            typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool)
        });
        yield return GetPowerCommand(nameof(PowerCmd.ModifyAmount), new[]
        {
            typeof(PlayerChoiceContext), typeof(PowerModel), typeof(decimal),
            typeof(Creature), typeof(CardModel), typeof(bool)
        });
    }

    private static MethodInfo GetPowerCommand(string name, Type[] arguments)
    {
        MethodInfo method = AccessTools.Method(typeof(PowerCmd), name, arguments);
        if (method == null || method.IsGenericMethod)
        {
            throw new MissingMethodException(typeof(PowerCmd).FullName,
                name + " non-generic power application overload");
        }

        return method;
    }

    private static MethodInfo GetAsyncBody(MethodInfo method)
    {
        AsyncStateMachineAttribute stateMachine =
            method.GetCustomAttribute<AsyncStateMachineAttribute>() ??
            throw new InvalidOperationException(
                $"PowerCmd.{method.Name} no longer exposes an async state machine.");
        return AccessTools.Method(stateMachine.StateMachineType, "MoveNext") ??
            throw new MissingMethodException(stateMachine.StateMachineType.FullName,
                "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        Type stateMachine = __originalMethod.DeclaringType ??
            throw new InvalidOperationException(
                "Power animation wait patch requires an async state-machine method.");
        FieldInfo power = GetStateField(stateMachine, "power", typeof(PowerModel));
        FieldInfo cardSource = GetStateField(stateMachine, "cardSource", typeof(CardModel));
        Type[] waitArguments =
        {
            typeof(float), typeof(float), typeof(bool), typeof(CancellationToken)
        };
        MethodInfo wait = AccessTools.Method(typeof(Cmd), nameof(Cmd.CustomScaledWait),
            waitArguments) ?? throw new MissingMethodException(typeof(Cmd).FullName,
                nameof(Cmd.CustomScaledWait));
        MethodInfo replacement = AccessTools.Method(typeof(FragileAutoPlayAnimationHelper),
            nameof(FragileAutoPlayAnimationHelper.WaitForPowerAnimation),
            waitArguments.Concat(new[] { typeof(PowerModel), typeof(CardModel) }).ToArray()) ??
            throw new MissingMethodException(typeof(FragileAutoPlayAnimationHelper).FullName,
                nameof(FragileAutoPlayAnimationHelper.WaitForPowerAnimation));
        if (wait.ReturnType != typeof(Task) || replacement.ReturnType != typeof(Task))
        {
            throw new InvalidOperationException(
                "Power animation wait and its replacement must return Task.");
        }

        List<CodeInstruction> result = instructions.ToList();
        int[] calls = result.Select((instruction, index) => (instruction, index))
            .Where(pair => pair.instruction.Calls(wait)).Select(pair => pair.index).ToArray();
        if (calls.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected one power animation wait in {stateMachine.FullName}; found {calls.Length}.");
        }

        CodeInstruction original = result[calls[0]];
        CodeInstruction loadState = new(OpCodes.Ldarg_0);
        loadState.labels.AddRange(original.labels);
        loadState.blocks.AddRange(original.blocks.Where(block =>
            block.blockType != ExceptionBlockType.EndExceptionBlock));
        CodeInstruction call = new(OpCodes.Call, replacement);
        call.blocks.AddRange(original.blocks.Where(block =>
            block.blockType == ExceptionBlockType.EndExceptionBlock));

        // Preserve the original wait arguments. Only this PowerCmd invocation's
        // power and card source are appended, so nested effects keep their waits.
        result.RemoveAt(calls[0]);
        result.InsertRange(calls[0], new[]
        {
            loadState,
            new CodeInstruction(OpCodes.Ldfld, power),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, cardSource),
            call
        });
        return result;
    }

    private static FieldInfo GetStateField(Type stateMachine, string name, Type fieldType)
    {
        FieldInfo field = AccessTools.DeclaredField(stateMachine, name);
        if (field == null || field.IsStatic || field.FieldType != fieldType)
        {
            throw new MissingFieldException(stateMachine.FullName,
                name + " : " + fieldType.FullName);
        }

        return field;
    }
}

[HarmonyPatch]
internal static class FragileAutoPlayPowerSilencePatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        FragileAutoPlayAnimationPatch.GetPowerCommands();

    private static void Prefix(CardModel cardSource, ref bool silent)
    {
        if (FragileAutoPlayAnimationHelper.IsSuppressed(cardSource))
        {
            silent = true;
        }
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.RemoveFromCurrentPile))]
internal static class FragileAutoPlayHandCleanupPatch
{
    private static void Prefix(CardModel __instance, bool silent, out bool __state)
    {
        __state = silent && __instance.Pile?.Type == PileType.Hand &&
            FragileAutoPlayAnimationHelper.IsSuppressed(__instance);
    }

    private static void Postfix(CardModel __instance, bool __state)
    {
        if (__state && __instance.Pile?.Type != PileType.Hand)
            FragileAutoPlayAnimationHelper.RemoveHandVisual(__instance);
    }
}
