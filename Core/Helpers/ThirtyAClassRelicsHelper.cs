using System;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ThirtyAClassRelicsHelper
{
    internal sealed class AttackScope : IDisposable
    {
        internal AttackScope Previous;
        internal Creature Phantom;
        internal HighHitDamageVisualHelper.DirectDamageBatchScope Batch;
        internal bool Active = true;
        public void Dispose() { Active = false; if (ReferenceEquals(Current.Value, this)) Current.Value = Previous; }
    }
    private static readonly AsyncLocal<AttackScope> Current = new();
    internal static AttackScope BeginAttack(Creature phantom, HighHitDamageVisualHelper.DirectDamageBatchScope batch)
    {
        Player owner = phantom?.PetOwner;
        if (!PhantomHelper.IsOwnedMainPhantom(phantom, owner) || owner.GetRelic<CheckmateThirtyA>()?.TryConsumeAttack() != true) return null;
        AttackScope scope = new() { Previous = Current.Value, Phantom = phantom, Batch = batch };
        Current.Value = scope; return scope;
    }
    internal static decimal GetDamageAmplificationPercent(CardModel card, Creature dealer, Creature target, ValueProp props) =>
        Current.Value is { Active: true } scope && dealer == scope.Phantom && target?.Side != dealer.Side && props.IsPoweredAttack()
        && HighHitDamageVisualHelper.IsDirectBatchDamage(scope.Batch) ? 25m : 0m;
    internal static Task AfterAction(Creature phantom) => phantom?.PetOwner?.GetRelic<LetterArrivesThirtyA>()?.AfterPhantomActed(phantom) ?? Task.CompletedTask;
    internal static Task AfterRevival(Creature phantom) => phantom?.PetOwner?.GetRelic<PhantomsOfBygoneDaysThirtyA>()?.AfterPhantomRevived(phantom) ?? Task.CompletedTask;
    internal static int TicketExperience(Player owner, long amount)
    {
        amount = Math.Max(0L, amount);
        if (owner?.GetRelic<TicketStubThirtyA>()?.IsActive == true) amount = Math.Min(PhantomRosterEntry.MaximumStoredExperience * 2L, amount) * 3L / 2L;
        return (int)Math.Min(PhantomRosterEntry.MaximumStoredExperience, amount);
    }
}
