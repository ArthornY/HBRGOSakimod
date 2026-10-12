using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(SteamEruptionPower))]
internal static class PhantomSteamEruptionPatch
{
    [HarmonyPatch(nameof(SteamEruptionPower.AfterDeath))]
    [HarmonyPrefix]
    private static bool BeforeAfterDeath(
        SteamEruptionPower __instance,
        PlayerChoiceContext choiceContext,
        Creature creature,
        bool wasRemovalPrevented,
        float deathAnimLength,
        ref Task __result)
    {
        if (__instance.Owner?.HasPower<PhantomPower>() != true)
        {
            return true;
        }

        __result = Task.CompletedTask;
        return false;
    }

    [HarmonyPatch(nameof(SteamEruptionPower.ShouldStopCombatFromEnding))]
    [HarmonyPostfix]
    private static void AfterShouldStopCombatFromEnding(
        SteamEruptionPower __instance,
        ref bool __result)
    {
        if (__instance.Owner?.HasPower<PhantomPower>() == true)
        {
            __result = false;
        }
    }

    [HarmonyPatch(nameof(SteamEruptionPower.ShouldCreatureBeRemovedFromCombatAfterDeath))]
    [HarmonyPostfix]
    private static void AfterShouldCreatureBeRemoved(
        SteamEruptionPower __instance,
        Creature creature,
        ref bool __result)
    {
        if (creature == __instance.Owner &&
            creature.HasPower<PhantomPower>())
        {
            __result = true;
        }
    }

    [HarmonyPatch(nameof(SteamEruptionPower.ShouldPowerBeRemovedAfterOwnerDeath))]
    [HarmonyPostfix]
    private static void AfterShouldPowerBeRemoved(
        SteamEruptionPower __instance,
        ref bool __result)
    {
        if (__instance.Owner?.HasPower<PhantomPower>() == true)
        {
            __result = true;
        }
    }
}
