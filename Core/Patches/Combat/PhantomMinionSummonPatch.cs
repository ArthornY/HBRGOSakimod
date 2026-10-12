using System;
using System.Collections.Generic;
using System.Threading;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Patches.Combat;

internal static class PhantomMinionSummonContext
{
    private static readonly AsyncLocal<Scope> CurrentScope = new();

    internal static Scope Begin(Creature leader)
    {
        Scope scope = new(leader, CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    internal static bool TryGetLeader(out Creature leader)
    {
        leader = CurrentScope.Value?.Leader;
        return leader != null;
    }

    internal static void Track(Creature creature)
    {
        CurrentScope.Value?.CreatedCreatures.Add(creature);
    }

    internal sealed class Scope : IDisposable
    {
        private readonly Scope _previous;
        private bool _disposed;

        internal Scope(Creature leader, Scope previous)
        {
            Leader = leader;
            _previous = previous;
        }

        internal Creature Leader { get; }

        internal List<Creature> CreatedCreatures { get; } = new();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (ReferenceEquals(CurrentScope.Value, this))
            {
                CurrentScope.Value = _previous;
            }
        }
    }
}

[HarmonyPatch(typeof(CombatState), nameof(CombatState.CreateCreature))]
internal static class PhantomMinionCreateCreaturePatch
{
    private static void Prefix(ref CombatSide side)
    {
        if (PhantomMinionSummonContext.TryGetLeader(
                out Creature leader))
        {
            side = leader.Side;
        }
    }

    private static void Postfix(Creature __result)
    {
        if (__result == null ||
            !PhantomMinionSummonContext.TryGetLeader(
                out Creature leader) ||
            leader.PetOwner is not { } owner ||
            __result.CombatState != leader.CombatState)
        {
            return;
        }

        if (__result.PetOwner == null)
        {
            owner.PlayerCombatState.AddPetInternal(__result);
        }

        PhantomHelper.ConfigureSummonedMinionHealth(__result, 1);
        PhantomMinionSummonContext.Track(__result);
    }
}
