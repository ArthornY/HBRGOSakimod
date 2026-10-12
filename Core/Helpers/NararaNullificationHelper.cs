using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Capture before the wrapper (including a required mode switch), not after the
// card has already reduced resistance. Each nested play owns its own snapshot.
internal static class NararaNullificationHelper
{
    internal sealed class PlayScope
    {
        internal CardModel Card;
        internal ICombatState Combat;
        internal PlayScope Previous;
        internal HashSet<Creature> Targets = new();
        internal bool Active = true;
    }

    private sealed class RecordedPlay
    {
        internal PlayScope Scope;
        internal HashSet<Creature> Targets;
    }

    private static readonly AsyncLocal<PlayScope> Current = new();
    private static readonly ConditionalWeakTable<CardPlay, RecordedPlay> Plays = new();

    internal static PlayScope Begin(CardModel card)
    {
        PlayScope scope = new()
        {
            Card = card,
            Combat = card?.CombatState,
            Previous = Current.Value
        };
        scope.Targets = CaptureTargets(card, scope.Combat);
        Current.Value = scope;
        return scope;
    }

    private static HashSet<Creature> CaptureTargets(CardModel card, ICombatState combat)
    {
        HashSet<Creature> targets = new();
        if (card?.Owner?.Creature != null && combat != null)
        {
            var attributes = SixSenseCardCapability.GetAttributes(card);
            if (attributes.Count > 0)
            {
                var primary = attributes[0];
                foreach (Creature target in combat.Enemies)
                {
                    if (!target.IsAlive || target.Side == card.Owner.Creature.Side ||
                        target.GetPower<NararaNullificationPower>()?.IsActive != true) continue;
                    SixSenseResistancePower resistance = target.GetPower<SixSenseResistancePower>();
                    if (resistance?.GetResistance(primary) > 0) targets.Add(target);
                }
            }
        }
        return targets;
    }

    // Harmony calls the original async method synchronously up to its first
    // await. Restore the caller immediately; the task has captured its scope.
    // Waiting until task completion would leak it into unrelated player actions.
    internal static void RestoreCaller(PlayScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope))
            Current.Value = scope.Previous;
    }

    internal static async Task Finish(Task original, PlayScope scope)
    {
        try { await original; }
        finally { Close(scope); }
    }

    internal static void Close(PlayScope scope)
    {
        if (scope != null) scope.Active = false;
        RestoreCaller(scope);
    }

    private static PlayScope FindScope(CardModel source)
    {
        // A source-less command belongs to the innermost play, even when that
        // play is not nullified. It must never inherit an outer card's immunity.
        if (source == null) return Current.Value;
        for (PlayScope scope = Current.Value; scope != null; scope = scope.Previous)
            if (ReferenceEquals(scope.Card, source)) return scope;
        return null;
    }

    private static bool IsCurrent(PlayScope scope, Creature target) =>
        scope?.Active == true && target != null &&
        ReferenceEquals(target.CombatState, scope.Combat) &&
        ReferenceEquals(scope.Card?.CombatState, scope.Combat);

    private static bool Contains(PlayScope scope, Creature target) =>
        IsCurrent(scope, target) && scope.Targets.Contains(target);

    internal static bool IsCardNullified(Creature target, CardModel source = null)
    {
        PlayScope scope = FindScope(source);
        if (scope != null) return Contains(scope, target);
        // Native damage previews have a card source but run outside a play.
        // Source-less poison, powers and ordinary phantom actions do not.
        if (source == null || Current.Value?.Active == true ||
            target?.GetPower<NararaNullificationPower>()?.IsActive != true ||
            source.Owner?.Creature == null || target.Side == source.Owner.Creature.Side ||
            !ReferenceEquals(source.CombatState, target.CombatState)) return false;
        var attributes = SixSenseCardCapability.GetAttributes(source);
        return attributes.Count > 0 &&
            target.GetPower<SixSenseResistancePower>()?.GetResistance(attributes[0]) > 0;
    }

    internal static void RecordPlay(PowerModel power, CardPlay play)
    {
        if (play == null) return;
        PlayScope scope = FindScope(play.Card);
        if (!IsCurrent(scope, power?.Owner) || Plays.TryGetValue(play, out _)) return;
        // Native repeats share a wrapper but create a new CardPlay. A resistance
        // broken by the first resolution must empower the next resolution.
        // Capture all bosses once, not once for every boss's BeforeCardPlayed.
        scope.Targets = CaptureTargets(play.Card, scope.Combat);
        Plays.Add(play, new RecordedPlay { Scope = scope, Targets = scope.Targets });
    }

    internal static bool WasNullified(PowerModel power, CardPlay play) =>
        play != null && Plays.TryGetValue(play, out RecordedPlay recorded) &&
        IsCurrent(recorded.Scope, power?.Owner) && recorded.Targets.Contains(power.Owner);

    internal static bool BlocksPowerChange(PowerModel power, Creature target,
        decimal amount, CardModel source)
    {
        // Unlike preview damage, status changes require an actual active play.
        if (power == null || amount == 0m || !Contains(FindScope(source), target)) return false;
        // Negative Strength/Dexterity are Buff models too. DP is deliberately
        // Type.None, as are the mod's two explicit damage-taken amplifiers.
        return amount > 0m
            ? power.Type == PowerType.Debuff ||
                power is DestructionRatePower or DestructionRateCapIncreasePower
            : power.Type == PowerType.Buff || power is HighDimensionalShellPower;
    }

    internal static bool BlocksPowerRemoval(PowerModel power) =>
        power != null && IsCardNullified(power.Owner) &&
        (power.Type == PowerType.Buff && power.Amount > 0 || power is HighDimensionalShellPower or
            NararaNullificationPower or SixSenseResistancePower);
}
