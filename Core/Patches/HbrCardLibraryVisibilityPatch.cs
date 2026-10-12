using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Saves;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrCardLibraryVisibility
{
    internal static bool IsHbrCard(CardModel card) =>
        card != null &&
        card.GetType().Assembly == typeof(MagicSwordModeHbrgoSaki).Assembly;

    internal static bool CanBrowse(IReadOnlySet<ModelId> discoveredCards, ModelId id) =>
        discoveredCards.Contains(id) ||
        IsHbrCard(ModelDb.GetByIdOrNull<CardModel>(id));

    internal static void IncludeHbrCards(
        IEnumerable<CardModel> cards,
        HashSet<ModelId> seenCards,
        HashSet<CardModel> unlockedCards)
    {
        foreach (CardModel card in cards.Where(IsHbrCard))
        {
            seenCards.Add(card.Id);
            unlockedCards.Add(card);
        }
    }
}

[HarmonyPatch(typeof(NCardLibraryGrid), nameof(NCardLibraryGrid.RefreshVisibility))]
internal static class HbrCardLibraryRefreshVisibilityPatch
{
    private static void Postfix(
        HashSet<ModelId> ____seenCards,
        HashSet<CardModel> ____unlockedCards)
    {
        // These are the library's copies, not the progress save or acquisition pools.
        HbrCardLibraryVisibility.IncludeHbrCards(
            ModelDb.AllCards,
            ____seenCards,
            ____unlockedCards);
    }
}

[HarmonyPatch]
internal static class HbrCardLibraryDiscoveryChecksPatch
{
    private static readonly MethodInfo DiscoveredCardsGetter =
        AccessTools.PropertyGetter(typeof(ProgressState), nameof(ProgressState.DiscoveredCards));

    private static readonly MethodInfo Contains =
        AccessTools.Method(typeof(IReadOnlySet<ModelId>), nameof(IReadOnlySet<ModelId>.Contains));

    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Text search and the detail-screen navigation list use compiler-generated
        // local functions. Find those bodies without depending on generated names.
        return GetLibraryMethods(typeof(NCardLibrary))
            .Where(method => method.GetMethodBody() != null)
            .Where(method => PatchProcessor.GetOriginalInstructions(method)
                .Any(instruction => instruction.Calls(DiscoveredCardsGetter)));
    }

    private static IEnumerable<MethodInfo> GetLibraryMethods(Type type) =>
        AccessTools.GetDeclaredMethods(type)
            .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(GetLibraryMethods));

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        List<CodeInstruction> result = instructions.ToList();
        int replacements = 0;
        MethodInfo replacement = AccessTools.Method(
            typeof(HbrCardLibraryVisibility), nameof(HbrCardLibraryVisibility.CanBrowse));
        for (int index = 0; index < result.Count; index++)
        {
            if (!result[index].Calls(Contains))
            {
                continue;
            }

            result[index] = new CodeInstruction(result[index])
            {
                opcode = OpCodes.Call,
                operand = replacement
            };
            replacements++;
        }

        if (replacements == 0)
        {
            throw new InvalidOperationException(
                $"No card discovery check found in {__originalMethod.DeclaringType?.Name}." +
                __originalMethod.Name);
        }

        return result;
    }
}
