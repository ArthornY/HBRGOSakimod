using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class MagicSwordHandCapacityPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo addMethod = AccessTools
            .GetDeclaredMethods(typeof(CardPileCmd))
            .Where(method => method.Name == nameof(CardPileCmd.Add))
            .Where(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length is 5 or 6 &&
                    parameters[0].ParameterType ==
                        typeof(IEnumerable<CardModel>) &&
                    parameters[1].ParameterType == typeof(CardPile) &&
                    parameters[2].ParameterType ==
                        typeof(CardPilePosition) &&
                    parameters[3].ParameterType ==
                        typeof(AbstractModel) &&
                    parameters[4].ParameterType == typeof(bool);
            })
            .OrderBy(method => method.GetParameters().Length)
            .FirstOrDefault();
        if (addMethod == null)
        {
            throw new MissingMethodException(
                typeof(CardPileCmd).FullName,
                nameof(CardPileCmd.Add));
        }

        AsyncStateMachineAttribute attribute = addMethod
            .GetCustomAttribute<AsyncStateMachineAttribute>();
        if (attribute == null)
        {
            return addMethod;
        }

        return AccessTools.Method(
            attribute.StateMachineType,
            "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        MethodInfo originalGetter = AccessTools.PropertyGetter(
            typeof(CardPile),
            nameof(CardPile.MaxCardsInHand));
        MethodInfo replacementGetter = AccessTools.Method(
            typeof(MagicSwordHandCapacityHelper),
            nameof(MagicSwordHandCapacityHelper
                .GetEffectiveMaxCardsInHandForCard));
        IReadOnlyList<CodeInstruction> loadPlayer = HalfBodyHandCapacityPatch.LoadCurrentCard(__originalMethod);

        int replacementCount = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(originalGetter))
            {
                replacementCount++;
                for (int index = 0; index < loadPlayer.Count; index++)
                {
                    CodeInstruction load = new(loadPlayer[index]);
                    if (index == 0)
                    {
                        load.labels.AddRange(instruction.labels);
                        load.blocks.AddRange(instruction.blocks);
                    }
                    yield return load;
                }
                yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, replacementGetter);
                continue;
            }

            yield return instruction;
        }

        if (replacementCount == 0)
        {
            throw new InvalidOperationException(
                "Expected to replace CardPile.MaxCardsInHand in CardPileCmd.Add.");
        }
    }
}
