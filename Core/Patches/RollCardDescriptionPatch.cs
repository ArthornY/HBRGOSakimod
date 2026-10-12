using System;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class RollCardDescriptionPatch
{
    private static MethodBase TargetMethod()
    {
        foreach (MethodInfo method in typeof(CardModel).GetMethods(
                     BindingFlags.Instance | BindingFlags.NonPublic))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name == nameof(CardModel.GetDescriptionForPile) &&
                method.ReturnType == typeof(string) &&
                parameters.Length == 3 &&
                parameters[0].ParameterType == typeof(PileType) &&
                parameters[2].ParameterType == typeof(Creature))
            {
                return method;
            }
        }

        throw new MissingMethodException(
            typeof(CardModel).FullName,
            nameof(CardModel.GetDescriptionForPile));
    }

    [HarmonyAfter("com.ritsukage.sts2-RitsuLib.framework-content-assets")]
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(
        CardModel __instance,
        ref string __result)
    {
        EnsureRollCardText(__instance, ref __result);
        if (HBRGOSakiContentPack.IsRegisteredCardType(__instance.GetType()) ||
            RollKeywordRules.IsRollEnabled(__instance))
        {
            __result = CardKeywordLayout.Compact(__result);
        }
    }

    internal static void EnsureRollCardText(
        CardModel card,
        ref string description)
    {
        if (!RollKeywordRules.IsRollEnabled(card))
        {
            return;
        }

        string rollText = RollGlossaryCapability.GetRollCardText();
        if (string.IsNullOrWhiteSpace(rollText) ||
            ContainsStandaloneLine(description, rollText))
        {
            return;
        }

        description = string.IsNullOrWhiteSpace(description)
            ? rollText
            : $"{rollText}\n{description}";
    }

    private static bool ContainsStandaloneLine(
        string description,
        string expectedLine)
    {
        if (string.IsNullOrEmpty(description))
        {
            return false;
        }

        foreach (string line in description.Split('\n'))
        {
            if (string.Equals(
                    line.Trim(),
                    expectedLine,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
