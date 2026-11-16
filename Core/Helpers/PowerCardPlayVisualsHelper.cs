using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Helpers;

internal static class PowerCardPlayVisualsHelper
{
    private static readonly Func<CardModel, Task> PlayPowerCardFlyVfx =
        PrivateAccess.MethodDelegate<CardModel, Func<CardModel, Task>>("PlayPowerCardFlyVfx");

    internal static async Task FinishNullifiedPowerPlay(CardModel card)
    {
        try
        {
            // A zero-play power never enters the native loop that normally
            // starts its flight. Native Power removal relies on that flight
            // to free the existing card node; it does not free the node itself.
            // Never create a replacement when there is no visible card to end.
            if (GetUnclaimedTableCard(card) == null) return;
            await PlayPowerCardFlyVfx(card);
        }
        catch (Exception error)
        {
            // Presentation failure must not stop synchronized zero-play cleanup.
            Log.Warn($"[HBRGOSakiMod] Nullified power card exit failed: {error.Message}");
        }
    }

    internal static bool HasUnclaimedTableCard(CardModel card)
    {
        try { return GetUnclaimedTableCard(card) != null; }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Power card exit lookup failed: {error.Message}");
            return false;
        }
    }

    internal static NCard GetUnclaimedTableCard(CardModel card)
    {
        if (card?.Type != CardType.Power || card.Owner?.Creature?.IsAlive != true ||
            CombatManager.Instance.IsOverOrEnding) return null;
        NCombatRoom room = NCombatRoom.Instance;
        if (!IsUsable(room) || !IsUsable(room.CombatVfxContainer)) return null;
        NCard node = NCard.FindOnTable(card);
        if (!IsUsable(node)) return null;
        foreach (Node child in room.CombatVfxContainer.GetChildren())
        {
            // Native flight creates an additional node if this exact node is
            // already flying. A replay must leave the current owner's node alone.
            if (child is NCardFlyPowerVfx flight && IsUsable(flight) &&
                ReferenceEquals(flight.CardNode, node)) return null;
        }
        return node;
    }

    private static bool IsUsable(Node node) =>
        node != null && GodotObject.IsInstanceValid(node) &&
        node.IsInsideTree() && !node.IsQueuedForDeletion();
}
