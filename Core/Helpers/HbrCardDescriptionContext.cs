using System;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Rendering state belongs to one card and one synchronous visual update.
// Inspecting a card must never change the card model or other visible cards.
internal static class HbrCardDescriptionContext
{
    internal sealed class Scope : IDisposable
    {
        internal CardModel Card;
        internal bool Detail;
        internal bool UseNativePreview;
        internal bool ForceUnpowered;
        internal Creature Target;
        internal Scope Previous;
        private bool _closed;

        public void Dispose()
        {
            if (_closed) return;
            _closed = true;
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }
    }

    private static readonly AsyncLocal<Scope> Current = new();

    internal static Scope Begin(CardModel card, bool detail, bool useNativePreview, Creature target)
        => Begin(card, detail, useNativePreview, target, false);

    internal static Scope Begin(CardModel card, bool detail, bool useNativePreview, Creature target,
        bool forceUnpowered)
    {
        Scope scope = new() { Card = card, Detail = detail, UseNativePreview = useNativePreview,
            Target = target, ForceUnpowered = forceUnpowered, Previous = Current.Value };
        Current.Value = scope;
        return scope;
    }

    internal static bool IsDetail(CardModel card) =>
        Current.Value is { } scope && ReferenceEquals(scope.Card, card) && scope.Detail;

    internal static bool UsesNativePreview(CardModel card) =>
        Current.Value is { } scope && ReferenceEquals(scope.Card, card) && scope.UseNativePreview;

    internal static bool IsUnpowered(CardModel card) =>
        Current.Value is { } scope && ReferenceEquals(scope.Card, card) && scope.ForceUnpowered;

    internal static Creature GetTarget(CardModel card) =>
        Current.Value is { } scope && ReferenceEquals(scope.Card, card) ? scope.Target : card?.CurrentTarget;
}
