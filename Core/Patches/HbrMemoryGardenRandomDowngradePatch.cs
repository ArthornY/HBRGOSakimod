using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class HbrMemoryGardenRandomDowngradePatch
{
    private static readonly MethodInfo UpgradedGetter =
        AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.IsUpgraded));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Filter before random selection so an excluded garden card cannot use
        // up a downgrade selection. Keep targeted and temporary downgrades, as
        // well as the inspect screen's base-card preview, on their own paths.
        foreach ((Type eventType, string selection) in new[]
                 {
                     (typeof(WelcomeToWongos), "Leave"),
                     (typeof(Reflections), "TouchAMirror")
                 })
        {
            MethodInfo[] predicates = GetDeclaredAndNestedMethods(eventType)
                .Where(method => method.Name.StartsWith("<" + selection + ">", StringComparison.Ordinal) &&
                    method.ReturnType == typeof(bool) &&
                    method.GetParameters() is [{ ParameterType: var parameterType }] &&
                    parameterType == typeof(CardModel) && method.GetMethodBody() != null)
                .Where(method => PatchProcessor.GetOriginalInstructions(method)
                    .Any(instruction => instruction.Calls(UpgradedGetter)))
                .ToArray();
            if (predicates.Length == 0)
                throw new MissingMethodException(eventType.FullName, selection + " downgrade candidate predicate");
            foreach (MethodInfo predicate in predicates) yield return predicate;
        }
    }

    private static IEnumerable<MethodInfo> GetDeclaredAndNestedMethods(Type type) =>
        AccessTools.GetDeclaredMethods(type)
            .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(GetDeclaredAndNestedMethods));

    private static bool IsRandomDowngradeCandidate(CardModel card) =>
        card != null && !HbrMemoryGardenCardCatalog.IsGardenCard(card) && card.IsUpgraded;

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        MethodInfo candidate = AccessTools.Method(typeof(HbrMemoryGardenRandomDowngradePatch),
            nameof(IsRandomDowngradeCandidate));
        bool replaced = false;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Calls(UpgradedGetter))
            {
                yield return instruction;
                continue;
            }
            replaced = true;
            yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = candidate };
        }
        if (!replaced)
            throw new InvalidOperationException("No random downgrade candidate check found in " + __originalMethod);
    }
}
