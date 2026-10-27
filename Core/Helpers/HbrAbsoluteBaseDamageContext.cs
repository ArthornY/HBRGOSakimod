using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// Resource-derived base amounts are already in combat units. Claim their raw
// input at the canonical command/hook boundary, before native enchantments run;
// only the base-curve bridge is bypassed. Nested commands and hook previews get
// their own receipts, so matching owner/card/amount alone cannot inherit a hit.
internal static class HbrAbsoluteBaseDamageContext
{
    private static readonly AsyncLocal<Scope> Current = new();
    private static readonly AsyncLocal<CommandScope> CurrentCommand = new();
    private static readonly AsyncLocal<HookScope> CurrentHook = new();

    internal sealed class Scope : IDisposable
    {
        internal Scope Previous;
        internal Creature Dealer;
        internal CardModel Card;
        internal Creature Target;
        internal decimal Amount;
        internal bool Active = true;

        public void Dispose()
        {
            if (!Active) return;
            Active = false;
            if (ReferenceEquals(Current.Value, this)) Current.Value = Previous;
        }

        internal bool Matches(Creature target, Creature dealer, CardModel card, decimal amount) =>
            Active && ReferenceEquals(Target, target) && ReferenceEquals(Dealer, dealer) &&
            ReferenceEquals(Card, card) && Amount == amount;
    }

    internal sealed class CommandScope
    {
        internal CommandScope Previous;
        internal Scope Intent;
        internal bool Enabled;
        internal bool HookClaimed;
        internal bool Active = true;
    }

    internal sealed class HookScope : IDisposable
    {
        internal HookScope Previous;
        internal Scope Intent;
        internal bool Enabled;
        internal bool Active = true;

        public void Dispose()
        {
            if (!Active) return;
            Active = false;
            if (ReferenceEquals(CurrentHook.Value, this)) CurrentHook.Value = Previous;
        }
    }

    internal static Scope Begin(Creature dealer, CardModel cardSource, Creature target, decimal amount)
    {
        Scope scope = new()
        {
            Previous = Current.Value, Dealer = dealer, Card = cardSource,
            Target = target, Amount = amount
        };
        Current.Value = scope;
        return scope;
    }

    // No target enumeration is needed. The exact target is validated when the
    // native command first calls Hook.ModifyDamage with its raw amount.
    internal static CommandScope BeginCommand(Creature dealer, CardModel cardSource,
        decimal amount, ValueProp props)
    {
        Scope intent = Current.Value;
        CommandScope previous = CurrentCommand.Value;
        if (intent?.Active != true && previous?.Active != true) return null;
        CommandScope scope = new()
        {
            Previous = previous, Intent = intent,
            Enabled = intent?.Active == true && props.IsPoweredAttack() &&
                ReferenceEquals(intent.Dealer, dealer) && ReferenceEquals(intent.Card, cardSource) &&
                intent.Amount == amount && (previous?.Active != true || !ReferenceEquals(previous.Intent, intent))
        };
        CurrentCommand.Value = scope;
        return scope;
    }

    internal static HookScope BeginHook(Creature target, Creature dealer, CardModel cardSource,
        decimal damage, ValueProp props)
    {
        Scope intent = Current.Value;
        CommandScope command = CurrentCommand.Value;
        HookScope previous = CurrentHook.Value;
        if (intent?.Active != true && command?.Active != true && previous?.Active != true) return null;
        bool matches = intent?.Matches(target, dealer, cardSource, damage) == true && props.IsPoweredAttack();
        bool enabled = matches && (command?.Active == true
            ? command.Enabled && !command.HookClaimed && ReferenceEquals(command.Intent, intent)
            : previous?.Active != true || !ReferenceEquals(previous.Intent, intent));
        if (enabled && command != null) command.HookClaimed = true;
        HookScope scope = new() { Previous = previous, Intent = intent, Enabled = enabled };
        CurrentHook.Value = scope;
        return scope;
    }

    internal static bool Matches(Creature target, Creature dealer, CardModel cardSource, decimal amount)
    {
        HookScope hook = CurrentHook.Value;
        if (hook?.Active == true)
            return hook.Enabled && hook.Intent?.Active == true &&
                ReferenceEquals(hook.Intent.Target, target) && ReferenceEquals(hook.Intent.Dealer, dealer) &&
                ReferenceEquals(hook.Intent.Card, cardSource);
        // Exact fallback is useful for direct pure calculations; a live command
        // must have claimed its hook rather than broadening this fallback.
        return CurrentCommand.Value?.Active != true &&
            Current.Value?.Matches(target, dealer, cardSource, amount) == true;
    }

    internal static void RestoreCaller(CommandScope scope)
    {
        if (scope != null && ReferenceEquals(CurrentCommand.Value, scope))
            CurrentCommand.Value = scope.Previous;
    }

    internal static void Close(CommandScope scope)
    {
        if (scope == null) return;
        scope.Active = false;
        RestoreCaller(scope);
    }

    internal static async Task<IEnumerable<DamageResult>> Finish(
        Task<IEnumerable<DamageResult>> original, CommandScope scope)
    {
        try { return await original; }
        finally { Close(scope); }
    }
}