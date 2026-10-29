using System.Collections.Generic;
using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Native placement must see battlefield geometry before it places allies and
// pets. Apply the corner layout only after all native camera/layout work ends.
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.PositionPlayersAndPets))]
internal static class SakiCornerNativePlacementPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(List<NCreature> creatureNodes) =>
        NSakiCornerCreatureLayout.PrepareForNativePlacement(creatureNodes);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(List<NCreature> creatureNodes) =>
        NSakiCornerCreatureLayout.FinishNativePlacement(creatureNodes);
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom._Ready))]
internal static class SakiCornerRoomReadyPatch
{
    private static void Postfix(NCombatRoom __instance)
    {
        ModNodeAttachmentRegistry.EnsureReadyAttachments(__instance);
        NSakiCornerCreatureLayout.RefreshAfterRoomReady(__instance);
    }
}

[HarmonyPatch(typeof(NCombatRoom), "AdjustCreatureScaleForAspectRatio")]
internal static class SakiCornerAspectRatioPatch
{
    private static void Prefix(NCombatRoom __instance) =>
        NSakiCornerCreatureLayout.PrepareForNativePlacement(__instance.CreatureNodes);

    private static void Postfix(NCombatRoom __instance) =>
        NSakiCornerCreatureLayout.FinishNativePlacement(__instance.CreatureNodes);
}

[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.UpdateLayoutForCreatureBounds))]
internal static class SakiCornerHealthBoundsPatch
{
    private static void Prefix(NHealthBar __instance) =>
        NSakiCornerStatusHud.PrepareForNativeBounds(__instance);
}
