using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

namespace HBRGOSakiMod.Core.Helpers;

internal static class FragileAutoPlayAnimationHelper
{
    private static readonly AsyncLocal<Scope> ActiveScope = new();

    internal static IDisposable Suppress(FragileHbrgoSaki card)
    {
        Scope scope = new(card, ActiveScope.Value);
        ActiveScope.Value = scope;
        return scope;
    }

    internal static bool IsSuppressed(CardModel cardSource)
    {
        Scope scope = ActiveScope.Value;
        return scope is { IsDisposed: false } &&
            ReferenceEquals(scope.Card, cardSource);
    }

    internal static void RemoveHandVisual(CardModel card)
    {
        // skipCardPileVisuals bypasses native hand.Remove along with its tweens.
        // Clear the holder only after the synchronized model left the hand.
        // UI failures must not interrupt card resolution on one multiplayer peer.
        try
        {
            if (TestMode.IsOn || !LocalContext.IsMe(card.Owner)) return;

            NCombatRoom room = NCombatRoom.Instance;
            if (room == null || !GodotObject.IsInstanceValid(room) || !room.IsInsideTree())
                return;

            NPlayerHand hand = room.Ui?.Hand;
            if (hand == null || !GodotObject.IsInstanceValid(hand) || !hand.IsInsideTree())
                return;

            // Older fast plays may already have left several holders for the
            // same model. Remove all of them before it can be drawn again.
            while (hand.GetCardHolder(card) is { } holder)
            {
                NCard node = holder.CardNode;
                hand.Remove(card);
                // Native holder.Clear detaches the card node without freeing it.
                if (node != null && GodotObject.IsInstanceValid(node))
                    node.QueueFreeSafely();
            }
        }
        catch (Exception error)
        {
            Log.Warn($"[HBRGOSakiMod] Fragile hand UI cleanup failed: {error.Message}");
        }
    }

    // Used only in the native power-application animation wait. Preserve the
    // command, modifiers, history and hooks, including nested unrelated effects.
    internal static Task WaitForPowerAnimation(
        float fastSeconds,
        float standardSeconds,
        bool ignoreCombatEnd,
        CancellationToken cancellationToken,
        PowerModel power,
        CardModel cardSource)
    {
        if (power != null && IsSuppressed(cardSource))
        {
            return Task.CompletedTask;
        }

        return Cmd.CustomScaledWait(
            fastSeconds, standardSeconds, ignoreCombatEnd, cancellationToken);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Scope _previous;

        internal FragileHbrgoSaki Card { get; }
        internal bool IsDisposed { get; private set; }

        internal Scope(FragileHbrgoSaki card, Scope previous)
        {
            Card = card;
            _previous = previous;
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            ActiveScope.Value = _previous;
        }
    }
}
