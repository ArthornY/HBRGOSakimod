using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class LargeHealthOstyCardMultiplierPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (Type card in new[] { typeof(Protector), typeof(Unleash), typeof(Sacrifice) })
        {
            MethodInfo[] multipliers = card.GetNestedTypes(BindingFlags.NonPublic)
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                .Where(method => method.Name.StartsWith("<get_CanonicalVars>b__") &&
                    method.ReturnType == typeof(decimal) && method.GetParameters().Length == 2 &&
                    method.GetParameters()[0].ParameterType == typeof(CardModel)).ToArray();
            if (multipliers.Length != 1)
                throw new InvalidOperationException("Expected one HP multiplier in " + card.FullName);
            yield return multipliers[0];
        }
    }

    private static bool Prefix(CardModel __0, MethodBase __originalMethod, ref decimal __result)
    {
        Creature osty = __0.Owner.Osty;
        if (!LargeCreatureHealthHelper.HasLargeHealth(osty)) return true;
        Type cardType = __originalMethod.DeclaringType.DeclaringType;
        __result = osty.IsAlive
            ? cardType == typeof(Unleash) ? LargeCreatureHealthHelper.GetCurrentHp(osty)
                : LargeCreatureHealthHelper.GetMaxHp(osty) * (cardType == typeof(Sacrifice) ? 2m : 1m)
            : 0m;
        // Damage modifiers run before the final per-hit cap; block keeps its
        // native cap in GainBlockInternal and its non-damage preview setter.
        return false;
    }
}

[HarmonyPatch]
internal static class LargeHealthPunchStartingHpPatch
{
    private static MethodBase TargetMethod() =>
        LargeCreatureHealthArithmeticPatch.AsyncBody(typeof(PunchConstruct), nameof(PunchConstruct.AfterAddedToRoom));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> result = instructions.ToList();
        MethodInfo current = AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.CurrentHp));
        MethodInfo reduction = AccessTools.PropertyGetter(typeof(PunchConstruct), nameof(PunchConstruct.StartingHpReduction));
        MethodInfo maximum = AccessTools.Method(typeof(Math), nameof(Math.Max), new[] { typeof(int), typeof(int) });
        MethodInfo conversion = AccessTools.Method(typeof(decimal), "op_Implicit", new[] { typeof(int) });
        int[] matches = result.Select((instruction, index) => (instruction, index))
            .Where(pair => pair.instruction.Calls(current)).Select(pair => pair.index).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("Expected one PunchConstruct starting HP read.");
        int i = matches[0];
        if (i < 3 || i + 5 >= result.Count || result[i - 3].opcode != OpCodes.Ldc_I4_1 ||
            !result[i + 2].Calls(reduction) || result[i + 3].opcode != OpCodes.Sub ||
            !result[i + 4].Calls(maximum) || !result[i + 5].Calls(conversion))
            throw new InvalidOperationException("PunchConstruct HP reduction expression changed.");
        foreach (int index in new[] { i - 3, i, i + 4, i + 5 })
        {
            result[index].opcode = OpCodes.Nop;
            result[index].operand = null;
        }
        result[i + 3].opcode = OpCodes.Call;
        result[i + 3].operand = AccessTools.Method(typeof(LargeCreatureHealthHelper),
            nameof(LargeCreatureHealthHelper.SubtractPunchStartingHp));
        return result;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyBlock))]
internal static class LargeHealthSacrificeBlockBoundaryPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(decimal block, CardModel cardSource, ref decimal __result)
    {
        // Sacrifice now derives its raw multiplier from exact Osty HP. Keep
        // the native block modifiers, then normalize to the existing block cap
        // before the command's history cast. Ordinary block sources are unchanged.
        if (cardSource is Sacrifice && block > int.MaxValue)
            __result = Math.Min(__result, BigCombatNumbers.NativeStatCap);
    }
}
