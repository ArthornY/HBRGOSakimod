using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(AttackIntent), "GetIntentDescription")]
internal static class PhantomAttackIntentDescriptionPatch
{
    private static bool Prefix(AttackIntent __instance, Creature owner, ref LocString __result)
    {
        if (!PhantomAttackIntentPreviewHelper.TryGetSegments(owner, __instance, out var segments))
            return true;

        // Native descriptions evaluate damage against the local player. Skip
        // that calculation for the same phantom attack shown above its model.
        __result = PhantomAttackIntentPreviewHelper.CreateDescription(segments);
        return false;
    }
}
