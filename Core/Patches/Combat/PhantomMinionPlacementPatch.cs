using System;
using HBRGOSakiMod.Core.Compatibility;
using System.Linq;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
internal static class PhantomMinionPlacementPatch
{
    private readonly record struct Placement(
        NCreature Node, Creature Entity, Vector2 Position, bool IsInteractable);

    [HarmonyPriority(Priority.First)]
    private static void Prefix(NCombatRoom __instance, Creature creature,
        out Placement[] __state)
    {
        __state = Array.Empty<Placement>();
        if (creature?.PetOwner == null || creature.Side != CombatSide.Player) return;

        // AddCreature lays out every pet of this owner and disables their UI.
        // Preserve only already marked phantoms, including main and scene bosses.
        __state = __instance.CreatureNodes
            .Where(node => IsUsable(node) &&
                node.Entity?.PetOwner == creature.PetOwner &&
                node.Entity.Side == CombatSide.Player &&
                node.Entity.HasPower<PhantomPower>())
            .Select(node => new Placement(node, node.Entity,
                node.Position, node.IsInteractable))
            .ToArray();
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NCombatRoom __instance, Creature creature,
        Placement[] __state)
    {
        Restore(__instance, __state);
        // AddPet assigns the owner before PhantomPower is applied. The controller
        // will recognize the new minion once initialization has finished.
        if (creature?.PetOwner != null && creature.Side == CombatSide.Player)
            NPhantomMinionPlacement.NotifyCreatureAdded(__instance, creature);
    }

    private static Exception Finalizer(NCombatRoom __instance,
        Placement[] __state, Exception __exception)
    {
        if (__exception == null) return null;
        try
        {
            Restore(__instance, __state);
        }
        catch (Exception restoreException)
        {
            // Preserve the original failure if room teardown also prevents a
            // restoration. Keep the secondary error available for diagnostics.
            __exception.Data[nameof(PhantomMinionPlacementPatch)] = restoreException;
        }
        return __exception;
    }

    private static void Restore(NCombatRoom room, Placement[] placements)
    {
        if (placements == null || placements.Length == 0 || !IsUsable(room)) return;
        var present = room.CreatureNodes.ToHashSet();
        foreach (Placement placement in placements)
        {
            NCreature node = placement.Node;
            if (!IsUsable(node) || !present.Contains(node) ||
                !ReferenceEquals(node.Entity, placement.Entity)) continue;

            node.Position = placement.Position;
            if (node.IsInteractable != placement.IsInteractable)
                room.SetCreatureIsInteractableCompat(placement.Entity, placement.IsInteractable);
        }
    }

    private static bool IsUsable(Node node) =>
        node != null && GodotObject.IsInstanceValid(node) && node.IsInsideTree();
}
