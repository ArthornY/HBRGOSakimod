using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class CrushYouLikeABugPlayScopePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(CardModel __instance, ResourceInfo resources,
        out CrushYouLikeABugCombatHelper.PlayScope __state) =>
        __state = CrushYouLikeABugCombatHelper.BeginPlay(__instance, resources);
    private static void Postfix(CrushYouLikeABugCombatHelper.PlayScope __state, ref Task __result)
    {
        if (__state == null) return;
        try { __result = CrushYouLikeABugCombatHelper.FinishPlay(__result, __state); }
        finally { CrushYouLikeABugCombatHelper.RestoreCaller(__state); }
    }
    private static Exception Finalizer(Exception __exception, CrushYouLikeABugCombatHelper.PlayScope __state)
    {
        if (__exception != null) CrushYouLikeABugCombatHelper.ClosePlay(__state);
        else CrushYouLikeABugCombatHelper.RestoreCaller(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(MimicEvilEyeTidalSlashHbrgoSaki), nameof(MimicEvilEyeTidalSlashHbrgoSaki.GetDestructionRateMultiplier))]
internal static class CrushYouLikeABugDestructionMultiplierPatch
{
    private static void Postfix(CardModel cardSource, ref decimal __result) =>
        __result *= CrushYouLikeABugCombatHelper.GetDestructionRateMultiplier(cardSource);
}
