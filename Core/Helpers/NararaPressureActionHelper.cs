using System;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class NararaPressureActionHelper
{
    internal sealed class Scope
    {
        internal Creature Dealer;
        internal Scope Previous;
        internal bool Active = true;
    }
    private static readonly AsyncLocal<Scope> Current = new();
    internal static Scope Begin(Creature dealer)
    {
        if (dealer?.PetOwner == null) return null;
        Scope scope = new() { Dealer = dealer, Previous = Current.Value };
        Current.Value = scope;
        return scope;
    }
    internal static object Token(Creature target, Creature dealer, CardModel card, ValueProp props)
    {
        // A triggered cannon still owns its own action inside a card wrapper.
        if (HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, card, props, out Creature cannon) &&
            HotaruCannonSupportUpgradeHelper.GetActionToken(cannon) is { } actualCannon) return actualCannon;
        // A new automatic card nested inside the cannon action is a separate
        // card, while the card that originally excited the cannon is not.
        object summon = HotaruCannonSupportUpgradeHelper.IsOriginalCardContext
            ? HotaruCannonSupportUpgradeHelper.GetActionToken(dealer) : null;
        summon ??= PhantomPlayerBenefitContext.GetActionToken(dealer);
        if (summon != null) return summon;
        if (dealer?.PetOwner != null && Current.Value is { Active: true } scope && scope.Dealer == dealer) return scope;
        return card != null && dealer == card.Owner?.Creature ? CardPlayContextHelper.GetCurrentPlayToken(card) : null;
    }
    internal static void Restore(Scope scope)
    { if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous; }
    internal static void Close(Scope scope)
    { if (scope != null) scope.Active = false; Restore(scope); }
    internal static async Task<T> Finish<T>(Task<T> task, Scope scope)
    { try { return await task; } finally { Close(scope); } }
}
