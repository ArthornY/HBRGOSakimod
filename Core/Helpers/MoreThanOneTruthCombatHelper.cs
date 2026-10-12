using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class MoreThanOneTruthCombatHelper
{
    internal sealed class PlayScope
    {
        internal PlayScope Previous;
        internal CardModel Card;
        internal bool Manual;
        internal int EnergySpent;
        internal bool HitBonusTaken;
        internal bool Active = true;
        internal readonly List<ContextState> Contexts = new();
    }
    private sealed class EffectScope
    {
        internal EffectScope Previous;
        internal PlayScope Play;
        internal Player Owner;
        internal bool Enabled;
        internal bool Active = true;
    }
    private sealed class TurnEnergy
    {
        internal ICombatState Combat;
        internal int NativeTurn;
        internal readonly HashSet<object> Used = new();
        internal readonly object Ordinary = new();
    }
    internal sealed class ContextState
    {
        internal AttackContext Context;
        internal AttackCommand Command;
        internal PlayScope Play;
        internal int Bonus;
        internal bool Active = true;
        internal Segment Last;
    }
    internal sealed record Segment(PlayerChoiceContext Context, Creature[] Targets,
        decimal Damage, ValueProp Props, Creature Dealer, CardModel Card);

    private static readonly AsyncLocal<PlayScope> Current = new();
    private static readonly AsyncLocal<EffectScope> Effect = new();
    private static readonly ConditionalWeakTable<Player, TurnEnergy> EnergyTurns = new();
    private static readonly ConditionalWeakTable<AttackCommand, Bonus> HitBonuses = new();
    private static readonly ConditionalWeakTable<AttackContext, ContextState> Contexts = new();
    private sealed record Bonus(int Amount);
    private static readonly MethodInfo CardEffect = typeof(CardModel).GetMethod("OnPlay",
        BindingFlags.Instance | BindingFlags.NonPublic, null,
        new[] { typeof(PlayerChoiceContext), typeof(CardPlay) }, null);
    private static readonly MethodInfo PotionEffect = typeof(PotionModel).GetMethod("OnUse",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
        new[] { typeof(PlayerChoiceContext), typeof(Creature) }, null);
    private static readonly FieldInfo ContextCommand = typeof(AttackContext).GetField("_attackCommand",
        BindingFlags.Instance | BindingFlags.NonPublic);

    internal static bool HasPermanentUpgrade(Player player, int id) =>
        player?.Deck?.Cards.OfType<MoreThanOneTruthHbrgoSaki>().Any(card => card.IsMutable &&
            card.Owner == player && !card.HasBeenRemovedFromState && card.Pile?.Type == PileType.Deck &&
            card.HasUpgradeEffect(id)) == true;
    internal static bool IsExtraTurn(Player player) => player?.Creature?.CombatState != null &&
        (OverclockImmediateTurnCoordinator.IsActiveFor(player) ||
         CombatManager.Instance.PlayersTakingExtraTurn.Contains(player));
    internal static bool IsVirtualMindsEye(Creature owner)
    {
        Player player = owner?.Player ?? owner?.PetOwner;
        return HasPermanentUpgrade(player, 3) && IsExtraTurn(player);
    }
    internal static bool CanReenterExtraTurnDeck(CardModel card) =>
        card is MoreThanOneTruthHbrgoSaki truth && truth.IsMutable &&
        !truth.HasBeenRemovedFromState && truth.Owner != null &&
        truth.Pile?.Type == PileType.Deck && truth.HasUpgradeEffect(1);
    internal static async Task ApplyRandomResistanceBreak(PlayerChoiceContext context, MoreThanOneTruthHbrgoSaki card)
    {
        if (card?.HasUpgradeEffect(5) != true || card.Owner?.Creature?.CombatState == null) return;
        foreach (Creature enemy in card.Owner.Creature.CombatState.Enemies.Where(SixSenseCombatHelper.IsEligibleEnemy).ToArray())
            await SixSenseCombatHelper.BreakRandomResistances(context, card.Owner, enemy, card, 1);
    }
    internal static void OnResistanceTransition(Player player, Creature enemy, int before, int after)
    {
        if (before < 6 && after == 6 && enemy?.Side != player?.Creature?.Side &&
            HasPermanentUpgrade(player, 2))
            OverclockImmediateTurnCoordinator.RequestResistanceReward(player);
    }
    internal static PlayScope BeginPlay(CardModel card, bool isAutoPlay, ResourceInfo resources)
    {
        PlayScope scope = new() { Previous = Current.Value, Card = card,
            Manual = !isAutoPlay, EnergySpent = Math.Max(0, resources.EnergySpent) };
        Current.Value = scope;
        return scope;
    }
    internal static void RestoreCaller(PlayScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }
    internal static async Task FinishPlay(Task original, PlayScope scope)
    {
        try { await original; }
        finally { ClosePlay(scope); }
    }
    internal static void ClosePlay(PlayScope scope)
    {
        if (scope != null)
        {
            scope.Active = false;
            foreach (ContextState state in scope.Contexts) { state.Active = false; Contexts.Remove(state.Context); }
        }
        RestoreCaller(scope);
    }
    private static Task Invoke(MethodInfo method, object receiver, object[] args)
    {
        try { return (Task)method.Invoke(receiver, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static Task Invoke(Delegate method, object[] args)
    {
        try { return (Task)method.DynamicInvoke(args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static EffectScope BeginActiveCardEffect(CardModel card, CardPlay play)
    {
        PlayScope current = Current.Value;
        EffectScope scope = new() { Previous = Effect.Value, Play = current, Owner = card.Owner,
            Enabled = current?.Active == true && current.Manual && play.IsFirstInSeries &&
                ReferenceEquals(current.Card, card) && !play.IsAutoPlay };
        Effect.Value = scope;
        return scope;
    }
    private static void CloseActiveEffect(EffectScope scope)
    {
        scope.Active = false;
        if (ReferenceEquals(Effect.Value, scope)) Effect.Value = scope.Previous;
    }
    internal static async Task InvokeActiveCardEffect(CardModel card, PlayerChoiceContext context, CardPlay play)
    {
        EffectScope scope = BeginActiveCardEffect(card, play);
        try { await Invoke(CardEffect, card, new object[] { context, play }); }
        finally { CloseActiveEffect(scope); }
    }
    internal static async Task InvokeActiveRitsuCardEffect(Delegate originalOnPlay, CardModel card,
        PlayerChoiceContext context, CardPlay play)
    {
        // The framework's before/after callbacks remain outside this window.
        // Invoke its supplied core delegate rather than bypassing the dispatcher.
        EffectScope scope = BeginActiveCardEffect(card, play);
        try { await Invoke(originalOnPlay, new object[] { card, context, play }); }
        finally { CloseActiveEffect(scope); }
    }
    internal static async Task InvokeActivePotionEffect(PotionModel potion, PlayerChoiceContext context, Creature target)
    {
        EffectScope scope = new() { Previous = Effect.Value, Play = Current.Value, Owner = potion.Owner,
            Enabled = Current.Value?.Active != true };
        Effect.Value = scope;
        try { await Invoke(PotionEffect, potion, new object[] { context, target }); }
        finally { scope.Active = false; if (ReferenceEquals(Effect.Value, scope)) Effect.Value = scope.Previous; }
    }
    internal static decimal ModifyActiveEnergyGain(Player player, decimal amount)
    {
        EffectScope effect = Effect.Value;
        if (amount <= 0m || effect?.Active != true || !effect.Enabled || effect.Owner != player ||
            !ReferenceEquals(effect.Play, Current.Value) || !HasPermanentUpgrade(player, 4)) return amount;
        TurnEnergy state = EnergyTurns.GetValue(player, static _ => new());
        int turn = player.PlayerCombatState?.TurnNumber ?? 0;
        ICombatState combat = player.Creature.CombatState;
        if (!ReferenceEquals(state.Combat, combat) || state.NativeTurn != turn)
        { state.Combat = combat; state.NativeTurn = turn; state.Used.Clear(); }
        object key = OverclockImmediateTurnCoordinator.GetTurnToken(player) ?? state.Ordinary;
        if (!state.Used.Add(key)) return amount;
        return amount > decimal.MaxValue / 2m ? decimal.MaxValue : amount * 2m;
    }
    private static int TakeHitBonus(AttackCommand command)
    {
        PlayScope scope = Current.Value;
        CardModel card = command.ModelSource as CardModel;
        if (scope?.Active != true || !ReferenceEquals(scope.Card, card) || scope.HitBonusTaken ||
            card?.Type != CardType.Attack || command.Attacker != card.Owner?.Creature ||
            scope.EnergySpent <= 0 || !HasPermanentUpgrade(card.Owner, 6) || !IsExtraTurn(card.Owner)) return 0;
        scope.HitBonusTaken = true;
        return scope.EnergySpent;
    }
    internal static decimal ModifyHitCount(AttackCommand command, decimal count)
    {
        if (HitBonuses.TryGetValue(command, out Bonus bonus)) return count + bonus.Amount;
        int amount = TakeHitBonus(command);
        if (amount > 0) HitBonuses.Add(command, new Bonus(amount));
        return count + amount;
    }
    internal static Task<AttackContext> TrackContext(Task<AttackContext> task) => TrackContext(task, Current.Value);
    private static async Task<AttackContext> TrackContext(Task<AttackContext> task, PlayScope play)
    {
        AttackContext context = await task;
        if (play?.Active != true || ContextCommand?.GetValue(context) is not AttackCommand command ||
            !ReferenceEquals(command.ModelSource, play.Card)) return context;
        ContextState state = new() { Context = context, Command = command, Play = play };
        Contexts.Add(context, state);
        play.Contexts.Add(state);
        return context;
    }
    internal static ContextState FindContext(Creature dealer, CardModel card, ValueProp props)
    {
        if (!props.IsPoweredAttack()) return null;
        PlayScope play = Current.Value;
        if (play?.Active != true || !ReferenceEquals(play.Card, card)) return null;
        ContextState state = play.Contexts.LastOrDefault(candidate => candidate.Active && candidate.Command.Attacker == dealer);
        if (state == null) return null;
        if (state.Last == null) state.Bonus = TakeHitBonus(state.Command);
        return state;
    }
    internal static async Task<IEnumerable<DamageResult>> RecordContextHit(Task<IEnumerable<DamageResult>> original,
        ContextState state, Segment segment)
    {
        IEnumerable<DamageResult> result = await original;
        if (state?.Active == true) state.Last = segment;
        return result;
    }
    internal static bool TryCompleteContext(AttackContext context, out ValueTask task)
    {
        task = default;
        if (!Contexts.TryGetValue(context, out ContextState state)) return false;
        Contexts.Remove(context);
        state.Active = false;
        task = new ValueTask(ReplayAndDispose(context, state));
        return true;
    }
    private static async Task ReplayAndDispose(AttackContext context, ContextState state)
    {
        try
        {
            Segment segment = state.Last;
            if (segment == null) return;
            for (int index = 0; index < state.Bonus && segment.Dealer.IsAlive; index++)
            {
                Creature[] targets = segment.Targets.Where(target => target.IsAlive).ToArray();
                if (targets.Length == 0) break;
                IEnumerable<DamageResult> results = await CreatureCommandCompatibility.Damage(segment.Context,
                    targets, segment.Damage, segment.Props, segment.Dealer, segment.Card);
                context.AddHit(results);
            }
        }
        finally { await context.DisposeAsync(); }
    }
}
