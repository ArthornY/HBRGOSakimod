using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Creature), nameof(Creature.AfterAddedToRoom))]
internal static class EnemyHealthScalingPatch
{
    private static void Postfix(Creature __instance, ref Task __result) =>
        __result = EnemyHealthScalingHelper.AfterAddedToRoom(__result, __instance);
}

[HarmonyPatch]
internal static class EnemyPhaseHealthScalingPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return GetAsyncBody(typeof(ToughEgg), "Hatch", Type.EmptyTypes);
        yield return GetAsyncBody(typeof(TestSubject), "Revive", new[] { typeof(int) });
    }

    private static MethodInfo GetAsyncBody(Type type, string name, Type[] arguments)
    {
        MethodInfo method = AccessTools.Method(type, name, arguments);
        AsyncStateMachineAttribute stateMachine = method?.GetCustomAttribute<AsyncStateMachineAttribute>()
            ?? throw new MissingMethodException(type.FullName, name + " async state machine");
        return AccessTools.Method(stateMachine.StateMachineType, "MoveNext")
            ?? throw new MissingMethodException(stateMachine.StateMachineType.FullName, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        MethodInfo scale = AccessTools.Method(typeof(Creature), nameof(Creature.ScaleHpForMultiplayer),
            new[] { typeof(decimal), typeof(EncounterModel), typeof(int), typeof(int) });
        MethodInfo replacement = AccessTools.Method(typeof(EnemyHealthScalingHelper),
            nameof(EnemyHealthScalingHelper.ScaleReplacementHealth));
        FieldInfo[] owners = __originalMethod.DeclaringType
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => typeof(MonsterModel).IsAssignableFrom(field.FieldType)).ToArray();
        List<CodeInstruction> result = instructions.ToList();
        int[] calls = result.Select((instruction, index) => (instruction, index))
            .Where(pair => pair.instruction.Calls(scale)).Select(pair => pair.index).ToArray();
        if (scale?.ReturnType != typeof(decimal) || owners.Length != 1 || calls.Length != 1)
            throw new InvalidOperationException(
                $"Expected one monster owner and one raw phase HP scaling call in {__originalMethod.DeclaringType?.FullName}.");

        // The freshly calculated value stays on the stack; inject only at this raw-base
        // source, leaving ordinary healing, max-HP changes and other monsters untouched.
        result.InsertRange(calls[0] + 1, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, owners[0]),
            new CodeInstruction(OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(MonsterModel), nameof(MonsterModel.Creature))),
            new CodeInstruction(OpCodes.Call, replacement)
        });
        return result;
    }
}
