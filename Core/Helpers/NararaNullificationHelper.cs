using System;
using System.Collections.Generic;
using System.Linq;
using HBRGOSakiMod.Core.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// Each wrapper owns a comparison result; nested automatic plays cannot inherit it.
internal static class NararaNullificationHelper
{
    internal sealed class PlayScope
    {
        internal CardModel Card;
        internal ICombatState Combat;
        internal PlayScope Previous;
        internal HashSet<Creature> Targets = new();
        internal bool Active = true;
        internal bool Resolved;
        internal readonly HashSet<Creature> SuccessfulTargets = new();
        internal readonly HashSet<Creature> HealedTargets = new();
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
        Current.Value = scope;
        return scope;
    }

    // Resolve before native BeforeCardPlayed dispatch, hence before the card's
    // separate RollGlossary roll. Autoplay and repeats never spend another slot.
    internal static async Task ResolvePlay(CardPlay play)
    {
        if (play?.Card?.Owner is not { } player) return;
        PlayScope scope = FindScope(play.Card);
        if (scope?.Active != true || scope.Resolved) return;
        scope.Resolved = true;
        if (play.IsAutoPlay || player.Creature?.IsAlive != true) return;
        var context = new ThrowingPlayerChoiceContext();
        foreach (Creature enemy in scope.Combat.GetOpponentsOf(player.Creature).ToArray())
        {
            if (enemy.GetPower<NararaNullificationPower>() is not { IsActive: true } power ||
                !power.TryClaimTurn(player)) continue;
            var roll = await RollGlossaryCapability.RollForContest(context, player, play.Card.Type);
            int bossRoll = player.RunState.Rng.Niche.NextInt(1, 101);
            bool success = roll.Winning.FinalRoll > bossRoll;
            if (success) scope.SuccessfulTargets.Add(enemy); else scope.Targets.Add(enemy);
            NararaContestPresentation.Show(player.Creature, enemy, roll.Winning.FinalRoll, bossRoll, success);
        }
        RefreshHands(scope.Combat);
    }

    internal static bool IsSuccessful(Creature target, Creature dealer, CardModel source)
    {
        PlayScope scope = FindScope(source);
        return IsCurrent(scope, target) && scope.SuccessfulTargets.Contains(target) &&
            dealer == scope.Card?.Owner?.Creature;
    }

    internal static bool ShouldHeal(PowerModel power, CardPlay play) =>
        WasNullified(power, play) && Plays.TryGetValue(play, out var recorded) &&
        recorded.Scope.HealedTargets.Add(power.Owner);

    internal static bool HasPendingContest(CardModel card) => card?.IsMutable == true &&
        card.Owner?.Creature is { IsAlive: true, CombatState: { } combat } owner &&
        ReferenceEquals(card.CombatState, combat) && combat.GetOpponentsOf(owner).Any(enemy =>
            enemy.GetPower<NararaNullificationPower>() is { IsActive: true } power && power.CanClaimTurn(card.Owner));

    internal static void RefreshHands(ICombatState combat)
    {
        foreach (Player player in combat?.Players ?? Array.Empty<Player>())
            foreach (CardModel card in player.PlayerCombatState?.Hand.Cards ?? Enumerable.Empty<CardModel>())
                card.InvokeEnergyCostChanged();
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
        return false;
    }

    internal static void RecordPlay(PowerModel power, CardPlay play)
    {
        if (play == null) return;
        PlayScope scope = FindScope(play.Card);
        if (!IsCurrent(scope, power?.Owner) || Plays.TryGetValue(play, out _)) return;
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
            NararaNullificationPower);
}
