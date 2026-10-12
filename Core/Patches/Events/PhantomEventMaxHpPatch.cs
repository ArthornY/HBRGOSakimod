using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models.Events;

namespace HBRGOSakiMod.Core.Patches.Events;

internal static class PhantomEventMaxHp
{
    internal static int GetEventMaxHp(Creature creature) =>
        (int)Math.Min(int.MaxValue,
            MomentOfReignMaxHpTarget.Get(creature)?.GetEventMaxHpReference() ?? creature.MaxHp);

    internal static decimal GetExactEventMaxHp(Creature creature) =>
        MomentOfReignMaxHpTarget.Get(creature)?.GetEventMaxHpReference() ?? creature.MaxHp;
}

[HarmonyPatch]
internal static class PhantomTabletOfTruthMaxHpPatch
{
    private static readonly MethodInfo MaxHpGetter =
        AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.MaxHp));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo loseMaxHp = AccessTools.Method(typeof(TabletOfTruth), "LoseMaxHpAndUpgrade");
        AsyncStateMachineAttribute stateMachine = loseMaxHp
            ?.GetCustomAttribute<AsyncStateMachineAttribute>() ??
            throw new MissingMethodException(typeof(TabletOfTruth).FullName,
                "LoseMaxHpAndUpgrade async state machine");

        // The option warnings are generated predicates; they can be emitted on
        // the event itself or on a nested compiler-generated type.
        IEnumerable<MethodInfo> predicates = GetDeclaredAndNestedMethods(typeof(TabletOfTruth))
            .Where(method => method.ReturnType == typeof(bool) &&
                method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == typeof(Player));
        MethodInfo[] candidates = new[]
        {
            AccessTools.Method(typeof(TabletOfTruth), nameof(TabletOfTruth.GetDecipherCost)),
            AccessTools.Method(stateMachine.StateMachineType, "MoveNext")
        }.Concat(predicates).Distinct().ToArray();

        return candidates.Where(method => method?.GetMethodBody() != null)
            .Where(method => PatchProcessor.GetOriginalInstructions(method)
                .Any(instruction => instruction.Calls(MaxHpGetter)));
    }

    private static IEnumerable<MethodInfo> GetDeclaredAndNestedMethods(Type type) =>
        AccessTools.GetDeclaredMethods(type)
            .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(GetDeclaredAndNestedMethods));

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        List<CodeInstruction> result = instructions.ToList();
        MethodInfo replacement = AccessTools.Method(
            typeof(PhantomEventMaxHp), nameof(PhantomEventMaxHp.GetEventMaxHp));
        int replacements = 0;
        for (int index = 0; index < result.Count; index++)
        {
            if (!result[index].Calls(MaxHpGetter)) continue;
            result[index] = new CodeInstruction(result[index])
            {
                opcode = OpCodes.Call,
                operand = replacement
            };
            replacements++;
        }

        if (replacements == 0)
            throw new InvalidOperationException(
                $"No maximum-HP check found in {__originalMethod.DeclaringType?.Name}." +
                __originalMethod.Name);
        return result;
    }
}

[HarmonyPatch(typeof(EventOption), nameof(EventOption.ThatDecreasesMaxHp))]
internal static class PhantomEventMaxHpWarningPatch
{
    private static bool Prefix(EventOption __instance, decimal value, ref EventOption __result)
    {
        // Drowning Beacon, Unrest Site and Vakuu's Distinguished Cape use this
        // shared warning. Ordinary HP damage and healing keep their own checks.
        __result = __instance.ThatWillKillPlayerIf(
            player => PhantomEventMaxHp.GetExactEventMaxHp(player.Creature) <= value);
        return false;
    }
}
