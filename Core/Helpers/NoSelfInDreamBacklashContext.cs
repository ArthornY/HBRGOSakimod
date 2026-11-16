using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Only the canonical command that claims this one-shot intent may consume its
// reserved limits. Nested damage commands install an independent disabled scope.
internal static class NoSelfInDreamBacklashContext
{
    private sealed class Intent
    {
        internal Creature[] Targets;
        internal decimal Amount;
        internal bool Active = true;
        internal bool Claimed;
    }
    internal sealed class Scope
    {
        internal Scope Previous;
        internal bool Active = true;
        internal Dictionary<Creature, decimal> Remaining;
    }
    private static readonly AsyncLocal<Intent> Pending = new();
    private static readonly AsyncLocal<Scope> Current = new();

    internal static async Task Damage(PlayerChoiceContext choiceContext,
        Creature[] targets, decimal amount)
    {
        Intent previous = Pending.Value;
        Intent intent = new() { Targets = targets, Amount = amount };
        Pending.Value = intent;
        try
        {
            await CreatureCommandCompatibility.Damage(choiceContext, targets,
                amount, ValueProp.Unpowered, null, null);
        }
        finally
        {
            intent.Active = false;
            if (ReferenceEquals(Pending.Value, intent)) Pending.Value = previous;
        }
    }

    internal static Scope Begin(IEnumerable<Creature> targets, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource)
    {
        Intent intent = Pending.Value;
        bool enabled = intent?.Active == true && !intent.Claimed &&
            amount == intent.Amount && props == ValueProp.Unpowered &&
            dealer == null && cardSource == null &&
            targets.SequenceEqual(intent.Targets);
        if (enabled) intent.Claimed = true;
        Scope scope = new()
        {
            Previous = Current.Value,
            Remaining = enabled ? intent.Targets.ToDictionary<Creature, Creature, decimal>(target => target,
                _ => amount, ReferenceEqualityComparer.Instance) : null
        };
        Current.Value = scope;
        return scope;
    }

    internal static decimal Limit(Creature target, decimal damage)
    {
        Scope scope = Current.Value;
        if (scope?.Active != true || scope.Remaining == null || target == null)
            return damage;
        lock (scope)
        {
            if (!scope.Remaining.TryGetValue(target, out decimal remaining)) return damage;
            decimal allowed = decimal.Floor(Math.Max(0m, Math.Min(damage, remaining)));
            scope.Remaining[target] = remaining - allowed;
            return allowed;
        }
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
