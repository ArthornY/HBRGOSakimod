using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class HbrMemoryGardenRandomUpgradeCandidatesPatch
{
    private static readonly MethodInfo UpgradableGetter =
        AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.IsUpgradable));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Random upgrades build their own candidate lists before shuffling or
        // taking a limited number. Filtering only CardCmd would waste those
        // rewards. Find native card predicates by their actual getter call,
        // without depending on compiler-generated lambda names or ordinals.
        return typeof(CardModel).Assembly.GetTypes()
            .Where(type => type.DeclaringType == null &&
                (typeof(CardModel).IsAssignableFrom(type) ||
                 typeof(RelicModel).IsAssignableFrom(type) ||
                 typeof(PowerModel).IsAssignableFrom(type) ||
                 typeof(EventModel).IsAssignableFrom(type)))
            // Pael's Tooth selects cards to remove/store, not to spend a random
            // upgrade reward. Preserve that selection and their saved effects;
            // its later return uses the command-level guard below.
            .Where(type => type != typeof(PaelsTooth))
            .SelectMany(GetDeclaredAndNestedMethods)
            .Where(method => method.ReturnType == typeof(bool) &&
                method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == typeof(CardModel) && method.GetMethodBody() != null)
            .Where(method => PatchProcessor.GetOriginalInstructions(method)
                .Any(instruction => instruction.Calls(UpgradableGetter)));
    }

    private static IEnumerable<MethodInfo> GetDeclaredAndNestedMethods(Type type) =>
        AccessTools.GetDeclaredMethods(type)
            .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(GetDeclaredAndNestedMethods));

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        MethodInfo candidate = AccessTools.Method(typeof(HbrMemoryGardenCardCatalog),
            nameof(HbrMemoryGardenCardCatalog.IsOrdinaryUpgradeCandidate));
        bool replaced = false;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Calls(UpgradableGetter))
            {
                yield return instruction;
                continue;
            }
            replaced = true;
            yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = candidate };
        }
        if (!replaced)
            throw new InvalidOperationException("No upgrade candidate check found in " + __originalMethod);
    }
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Upgrade),
    new[] { typeof(IEnumerable<CardModel>), typeof(CardPreviewStyle) })]
internal static class HbrMemoryGardenUpgradeCommandPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(ref IEnumerable<CardModel> cards)
    {
        // Both native overloads converge here, covering all-card upgrades and
        // direct mod calls as well. Leave UpgradeInternal untouched: save/load,
        // cloning and preview reconstruction must reproduce existing effects.
        cards = cards.Where(card => !HbrMemoryGardenCardCatalog.IsGardenCard(card) ||
            HbrMemoryGardenUpgradeCatalog.IsUpgradeAuthorized(card));
    }
}
