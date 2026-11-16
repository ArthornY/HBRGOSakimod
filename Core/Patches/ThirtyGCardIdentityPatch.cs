using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(SavedProperties), nameof(SavedProperties.FromInternal))]
internal static class ThirtyGCardIdentityWritePatch
{
    private static void Postfix(object model, ref SavedProperties __result)
    {
        if (model is not CardModel card || ThirtyGProposalState.IdentityOf(card) is not { } id) return;
        __result ??= new(); __result.strings ??= new();
        __result.strings.RemoveAll(entry => entry.name == "ThirtyGPhysicalId");
        __result.strings.Add(new("ThirtyGPhysicalId", id));
    }
}
[HarmonyPatch(typeof(SavedProperties), nameof(SavedProperties.FillInternal))]
internal static class ThirtyGCardIdentityReadPatch
{
    private static void Postfix(SavedProperties __instance, object model)
    {
        if (model is CardModel card && __instance.strings?.FirstOrDefault(entry => entry.name == "ThirtyGPhysicalId").value is { Length: > 0 } id)
            ThirtyGProposalState.RestoreIdentity(card, id);
    }
}
