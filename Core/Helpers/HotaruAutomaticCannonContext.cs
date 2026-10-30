using System;
using System.Threading;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Only the natural/OD turn boundary enters this scope. Nested extra actions
// retain that origin; a manually activated cannon has no automatic scope.
internal static class HotaruAutomaticCannonContext
{
    internal sealed class Scope : IDisposable
    {
        internal Scope Previous;
        internal Player Owner;
        internal HotaruCannonPhase Phase;
        internal bool Active = true;

        public void Dispose()
        {
            Active = false;
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }
    }

    private static readonly AsyncLocal<Scope> Current = new();

    internal static Scope Begin(Player owner, HotaruCannonPhase phase)
    {
        Scope scope = new() { Previous = Current.Value, Owner = owner, Phase = phase };
        Current.Value = scope;
        return scope;
    }

    internal static bool SuppressesPersonalHive(Creature target, Creature dealer, CardModel source, ValueProp props)
    {
        Scope scope = Current.Value;
        return scope is { Active: true, Owner: { } owner } &&
            ReferenceEquals(owner.Creature, dealer) &&
            HotaruCannonStatsHelper.TryGetCurrentCannon(target, dealer, source, props, out Creature cannon) &&
            ReferenceEquals(cannon.PetOwner, owner);
    }
}
