using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(ConditionalBranchState), nameof(ConditionalBranchState.GetNextState))]
internal static class PhantomInitialMovePatch
{
    private static bool Prefix(
        ConditionalBranchState __instance,
        Creature __0,
        ref string __result)
    {
        // Pet ownership is set before adding the creature to combat, whereas
        // PhantomPower is applied later. These native opening branches require
        // encounter slots that player-owned summons do not have.
        if (__instance.Id != "INIT_MOVE" || __0?.PetOwner == null)
        {
            return true;
        }

        string fallback = __0.Monster switch
        {
            Exoskeleton when __0.SlotName is not ("first" or "second" or "third" or "fourth")
                => "SKITTER_MOVE",
            Myte when __0.SlotName is not ("first" or "second")
                => "TOXIC_MOVE",
            PhantasmalGardener when __0.SlotName is not ("first" or "second" or "third" or "fourth")
                => "FLAIL_MOVE",
            Wriggler when __0.SlotName is not ("wriggler1" or "wriggler2" or "wriggler3" or "wriggler4")
                => "NASTY_BITE_MOVE",
            _ => null
        };
        if (fallback == null)
        {
            return true;
        }

        // Use the native first-slot opening; later transitions remain native.
        __result = fallback;
        return false;
    }
}
