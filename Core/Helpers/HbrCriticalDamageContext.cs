using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Actual damage commands own a replayable critical sample. Previews have no
// sample and never advance RNG; recalculations of the same hit reuse it.
internal static class HbrCriticalDamageContext
{
    private static readonly AsyncLocal<Scope> Current = new();
    internal static int CurrentTargetCount => Current.Value?.TargetCount ?? 1;

    internal sealed class Scope
    {
        internal Creature Dealer;
        internal CardModel Card;
        internal ValueProp Props;
        internal int TargetCount;
        internal int? Sample;
        internal bool Active = true;
        internal Scope Previous;
    }

    internal static Scope Begin(Creature dealer, CardModel card, ValueProp props, int targets)
    {
        if (!props.IsPoweredAttack() || !HbrBattleStatsHelper.IsEnabled(dealer)) return null;
        Scope scope = new() { Dealer = dealer, Card = card, Props = props,
            TargetCount = Math.Max(1, targets), Previous = Current.Value };
        Current.Value = scope;
        return scope;
    }

    internal static bool IsCritical(Creature target, Creature dealer, CardModel card,
        ValueProp props, decimal luck, decimal enemyParameter)
    {
        Scope scope = Current.Value;
        if (scope?.Active != true || !ReferenceEquals(scope.Dealer, dealer) ||
            !ReferenceEquals(scope.Card, card) || scope.Props != props ||
            !StrongestShieldDamagePatch.IsResolvingActualDamage ||
            HotaruCannonStatsHelper.IsDamagePreview || HotaruCannonStatsHelper.IsReactionDamage)
            return false;
        // Sample lazily, after the pipeline excludes resolved HP reactions.
        var run = dealer?.CombatState?.RunState;
        if (run == null) return false;
        scope.Sample ??= run.Rng.Niche.NextInt(10000);
        return scope.Sample.Value < HbrOriginalNumericFormula.CriticalProbability(luck, enemyParameter) * 10000m;
    }

    internal static void RestoreCaller(Scope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }

    internal static void Close(Scope scope)
    {
        if (scope == null) return;
        scope.Active = false;
        RestoreCaller(scope);
    }

    internal static async Task<IEnumerable<DamageResult>> Finish(
        Task<IEnumerable<DamageResult>> original, Scope scope)
    {
        try { return await original; }
        finally { Close(scope); }
    }
}
