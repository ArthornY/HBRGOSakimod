using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

// Keep IsUpgradable intact: garden effect previews, selected upgrades and saved
// upgrade reconstruction still rely on that native property.
[HarmonyPatch]
internal static class HbrMemoryGardenUpgradeSelectionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo[] predicates = typeof(CardSelectCmd).GetNestedTypes(BindingFlags.NonPublic)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.ReturnType == typeof(bool) &&
                method.GetParameters() is [{ ParameterType: var parameterType }] &&
                parameterType == typeof(CardModel))
            .ToArray();

        foreach (string selection in new[]
                 { nameof(CardSelectCmd.FromDeckForUpgrade), nameof(CardSelectCmd.FromHandForUpgrade) })
        {
            MethodInfo[] matches = predicates.Where(method =>
                method.Name.StartsWith("<" + selection + ">", StringComparison.Ordinal)).ToArray();
            if (matches.Length == 0)
                throw new MissingMethodException(typeof(CardSelectCmd).FullName,
                    selection + " card-selection predicate");
            foreach (MethodInfo method in matches) yield return method;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardModel __0, ref bool __result)
    {
        // Filtering before the native count/automatic-choice/network branches
        // also prevents a sole garden card from being silently selected.
        if (__result && HbrMemoryGardenCardCatalog.IsGardenCard(__0)) __result = false;
    }
}

[HarmonyPatch(typeof(SmithRestSiteOption), nameof(SmithRestSiteOption.IsEnabled), MethodType.Getter)]
internal static class HbrMemoryGardenSmithAvailabilityPatch
{
    private static readonly Func<RestSiteOption, Player> GetOwner =
        AccessTools.MethodDelegate<Func<RestSiteOption, Player>>(
            AccessTools.PropertyGetter(typeof(RestSiteOption), "Owner"));

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(SmithRestSiteOption __instance, ref bool __result)
    {
        if (__result)
            __result = GetOwner(__instance).Deck.Cards.Any(HbrMemoryGardenCardCatalog.IsOrdinaryUpgradeCandidate);
    }
}
