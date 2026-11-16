using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Cards;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Patches.Combat;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// A monster move is one enemy action, even when it issues several attack commands.
// Standalone enemy attacks/damage commands get an equivalent fallback scope.
internal static class ThirtyOneBEnemyActionHelper
{
    internal sealed class Receipt
    {
        internal bool Invalidated;
        internal bool Miracle;
        internal bool GrantRoll;
        internal bool Applied;
        internal AoiInvincibleShieldPower Shield;
    }
    internal sealed record Deferred(Creature Target, Func<Task> Apply);
    internal sealed class Scope
    {
        internal Scope Previous;
        internal Creature Attacker;
        internal bool Active = true;
        internal readonly Dictionary<Creature, Receipt> Targets = new();
        internal readonly List<Deferred> Pending = new();
        internal HashSet<MuuaIWillNotFallPower> MuuaHits;
        internal HashSet<Player> ParryOwners;
        internal HashSet<Player> ButlerOwners;
    }
    private sealed class ParryLedger { internal readonly HashSet<Player> Used = new(); }
    private static readonly ConditionalWeakTable<ICombatState, ParryLedger> ParryBattles = new();
    private sealed class RetaliationLedger { internal readonly HashSet<(int, CombatSide, object)> Used = new(); }
    private static readonly ConditionalWeakTable<FrozenMistMeowHbrgoSaki, RetaliationLedger> Retaliations = new();
    private static readonly object OrdinaryTurn = new();
    private static readonly AsyncLocal<Scope> Current = new();
    private static readonly AsyncLocal<int> OwnEffects = new();
    private static readonly AsyncLocal<int> Replaying = new();
    internal static Scope CurrentAction => Current.Value is { Active: true } scope ? scope : null;
    internal static bool HasPending => Current.Value is { Active: true } scope && scope.Pending.Count > 0;
    internal static bool TryDeferPower(PowerModel power, Creature target, decimal amount, Creature applier, Func<Task> apply) =>
        (amount > 0m && (power.Type == PowerType.Debuff || power is DestructionRatePower) || amount < 0m && power.Type == PowerType.Buff) &&
        TryDeferAction(target, applier, apply);
    internal static bool TryDeferAction(Creature target, Creature applier, Func<Task> apply)
    {
        if (!CanDefer(target, applier)) return false;
        Current.Value.Pending.Add(new(target, apply)); return true;
    }
    private static bool CanDefer(Creature target, Creature applier) => Replaying.Value == 0 && OwnEffects.Value == 0 &&
        Current.Value is { Active: true } scope && target?.Player != null && target.Side != scope.Attacker.Side &&
        applier != target && (applier == null || applier == scope.Attacker) && target.HasPower<AoiInvincibleShieldPower>() &&
        !scope.Targets.ContainsKey(target);
    internal static bool TryDeferCommand(Creature target, Creature applier, MethodBase method, object instance, object[] arguments)
    {
        if (!CanDefer(target, applier)) return false;
        object[] snapshot = (object[])arguments.Clone();
        return TryDeferAction(target, applier, () => ReplayCommand(method, instance, snapshot));
    }
    private static async Task ReplayCommand(MethodBase method, object instance, object[] arguments)
    {
        object result;
        try { result = ((MethodInfo)method).Invoke(instance, arguments); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
        if (result is Task task) await task;
    }
    // The safe callsite adapter invokes this BEFORE entering the native command
    // and its ordinary scopes. Do not wrap/detour the native async factory.
    internal static async Task PrepareDamage(object[] arguments)
    {
        if (Current.Value is not { Active: true } scope || scope.Pending.Count == 0 ||
            arguments[4] is not Creature dealer || dealer != scope.Attacker || !((ValueProp)arguments[3]).IsPoweredAttack()) return;
        IEnumerable<Creature> projection = NativeDamageApiCompatibility.ReadTargets(arguments);
        IReadOnlyList<Creature> actual = PhantomDamageTargetPatch.RedirectTargets(projection);
        NativeDamageApiCompatibility.WriteTargets(arguments, projection, actual,
            target => PhantomHelper.GetDamageDefender(target?.Player) ?? target);
        Capture(actual, (Creature)arguments[4], (ValueProp)arguments[3]);
        await Flush(scope, actual);
    }
    internal static async Task PrepareDamageTargets(IEnumerable<Creature> targets, Creature dealer, ValueProp props)
    {
        if (Current.Value is not { Active: true } scope || scope.Pending.Count == 0 ||
            dealer != scope.Attacker || !props.IsPoweredAttack()) return;
        IReadOnlyList<Creature> actual = PhantomDamageTargetPatch.RedirectTargets(targets);
        Capture(actual, dealer, props);
        await Flush(scope, actual);
    }
    private static async Task Flush(Scope scope, IEnumerable<Creature> targets)
    {
        HashSet<Creature> selected = targets.ToHashSet();
        Deferred[] pending = scope.Pending.Where(item => selected.Contains(item.Target)).ToArray();
        foreach (Deferred item in pending)
        {
            scope.Pending.Remove(item);
            if (IsNullified(item.Target)) continue;
            Replaying.Value++;
            try { await item.Apply(); }
            finally { Replaying.Value--; }
        }
    }
    internal static Scope Begin(Creature attacker)
    {
        if (attacker?.Monster == null || attacker.Side != CombatSide.Enemy || attacker.PetOwner != null) return null;
        if (Current.Value is { Active: true } current && current.Attacker == attacker) return null;
        if (attacker.CombatState?.Players.Any(player => player.Creature.HasPower<AoiInvincibleShieldPower>() ||
            player.Creature.HasPower<AoiMiracleShieldPower>() || player.Creature.HasPower<MuuaIWillNotFallPower>() || KuraSatomiCombatHelper.HasParry(player) || ThirtyOneFBondHelper.HasBulwark(player) || player.PlayerCombatState?.Hand?.Cards
                .OfType<FrozenMistMeowHbrgoSaki>().Any(card => !ThirtyGProposalState.IsDisabled(card) && card.HasUpgradeEffect(FrozenMistMeowHbrgoSaki.SenseHostility)) == true) != true) return null;
        Scope scope = new() { Previous = Current.Value, Attacker = attacker };
        Current.Value = scope; return scope;
    }
    internal static void ObserveMuuaHits(IReadOnlyList<DamageResult> results)
    {
        if (Current.Value is not { Active: true } scope) return;
        foreach (DamageResult result in results)
        {
            if (!result.Props.IsPoweredAttack() || !CombatAttackHitHelper.IsConnected(result)) continue;
            Player player = result.Receiver?.Player ?? result.Receiver?.PetOwner;
            if (player != null && scope.ParryOwners?.Contains(player) == true && IsPlayerDamageReceiver(result.Receiver, player))
                ParryBattles.GetOrCreateValue(scope.Attacker.CombatState).Used.Add(player);
            if (player?.Creature.GetPower<MuuaIWillNotFallPower>() is not { } power ||
                result.Receiver.Side == scope.Attacker.Side ||
                !(ReferenceEquals(result.Receiver, player.Creature) ||
                  ReferenceEquals(result.Receiver, FubukiChangeCombatHelper.DpTarget(player)))) continue;
            (scope.MuuaHits ??= new()).Add(power);
        }
    }
    private static bool IsPlayerDamageReceiver(Creature target, Player player) =>
        ReferenceEquals(target, player.Creature) || ReferenceEquals(target, FubukiChangeCombatHelper.DpTarget(player));
    internal static decimal GetKuraParryMultiplier(Creature target, Creature dealer, ValueProp props)
    {
        if (!props.IsPoweredAttack() || dealer?.Monster == null || dealer.PetOwner != null ||
            dealer.Side != CombatSide.Enemy || target?.Side == dealer.Side) return 1m;
        Player player = target?.Player ?? target?.PetOwner;
        if (player == null || !IsPlayerDamageReceiver(target, player) || !KuraSatomiCombatHelper.HasParry(player)) return 1m;
        Scope scope = Current.Value;
        if (scope?.Active == true && ReferenceEquals(scope.Attacker, dealer) && scope.ParryOwners?.Contains(player) == true) return .5m;
        if (player.Creature.CombatState is not { } combat ||
            ParryBattles.TryGetValue(combat, out var ledger) && ledger.Used.Contains(player)) return 1m;
        // Preview remains read-only. During a real action reserve the entire
        // move; commit only on a connected physical result, including block/DP.
        if (scope?.Active == true && ReferenceEquals(scope.Attacker, dealer)) (scope.ParryOwners ??= new()).Add(player);
        return .5m;
    }
    internal static void RestoreCaller(Scope scope)
    { if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous; }
    internal static void Close(Scope scope)
    { if (scope != null) { scope.Active = false; scope.Pending.Clear(); RestoreCaller(scope); } }
    internal static void Capture(IEnumerable<Creature> targets, Creature dealer, ValueProp props)
    {
        if (!props.IsPoweredAttack() || Current.Value is not { Active: true } scope || scope.Attacker != dealer) return;
        foreach (Creature target in targets)
        {
            if (target?.Player == null || target.Side == dealer.Side || scope.Targets.ContainsKey(target)) continue;
            Receipt receipt = new(); scope.Targets.Add(target, receipt);
            AoiInvincibleShieldPower shield = target.GetPower<AoiInvincibleShieldPower>();
            if (shield?.TryTakeDecision() == true)
            {
                receipt.Shield = shield;
                // This deliberately bypasses every SoulRoll/roll modifier hook.
                int raw = target.Player.RunState.Rng.Niche.NextInt(100) + 1;
                receipt.Invalidated = raw <= 50;
                receipt.GrantRoll = raw > 50;
            }
            if (!receipt.Invalidated && target.GetPowerAmount<AoiMiracleShieldPower>() > 0) receipt.Miracle = true;
        }
    }
    internal static bool IsNullified(Creature target) => target != null && OwnEffects.Value == 0 && Current.Value is { Active: true } scope &&
        scope.Targets.TryGetValue(target, out Receipt receipt) && receipt.Invalidated;
    internal static bool IsDamageNullified(Creature target, Creature dealer) => target != null && Current.Value is { Active: true } scope &&
        scope.Attacker == dealer && scope.Targets.TryGetValue(target, out Receipt receipt) && (receipt.Invalidated || receipt.Miracle);
    internal static async Task ApplyPending(PlayerChoiceContext context, Creature target)
    {
        if (Current.Value is not { Active: true } scope || !scope.Targets.TryGetValue(target, out Receipt receipt) || receipt.Applied) return;
        receipt.Applied = true;
        OwnEffects.Value++;
        try
        {
        if (receipt.GrantRoll)
            await HbrRollPowerHelper.Apply<RollBonusPower>(context, target, 50, 1, target, null, false);
        if (receipt.Invalidated && receipt.Shield?.GrantsAttackUp == true)
            await HbrEffectApplication.Apply<HbrAttackUpPower>(context, target, 30m, target, null);
        if (receipt.Miracle && target.GetPower<AoiMiracleShieldPower>() is { } miracle)
            await PowerCmd.Decrement(miracle);
        }
        finally { OwnEffects.Value--; }
    }
    internal static bool BlocksPower(PowerModel power, Creature target, decimal offset, Creature applier) =>
        IsNullified(target) && applier != target && (offset > 0m && (power.Type == PowerType.Debuff || power is DestructionRatePower) ||
            offset < 0m && power.Type == PowerType.Buff);
    internal static async Task Finish(Task original, Scope scope)
    {
        if (scope == null) { await original; return; }
        bool completed = false;
        try
        {
            await original;
            await Flush(scope, scope.Pending.Select(item => item.Target).ToArray());
            foreach (Creature target in scope.Targets.Keys.ToArray()) await ApplyPending(new ThrowingPlayerChoiceContext(), target);
            completed = true;
        }
        finally { Close(scope); }
        if (completed && scope.MuuaHits != null)
            await MuuaFaithCombatHelper.ResolveHits(scope.Attacker, scope.MuuaHits);
        if (completed && scope.ButlerOwners != null)
            await ThirtyOneFDefenseCombatHelper.ResolveCounters(scope.Attacker, scope.ButlerOwners);
        if (!completed || scope.Attacker.IsDead) return;
        foreach (Creature target in scope.Targets.Keys.ToArray())
        {
            Player owner = target.Player;
            if (target.IsDead || owner?.PlayerCombatState?.Hand == null) continue;
            foreach (FrozenMistMeowHbrgoSaki card in owner.PlayerCombatState.Hand.Cards.OfType<FrozenMistMeowHbrgoSaki>().ToArray())
            {
                  if (ThirtyGProposalState.IsDisabled(card) || !card.HasUpgradeEffect(FrozenMistMeowHbrgoSaki.SenseHostility) || card.Pile?.Type != PileType.Hand || scope.Attacker.IsDead) continue;
                var key = (target.CombatState.RoundNumber, target.CombatState.CurrentSide,
                    OverclockImmediateTurnCoordinator.GetTurnToken(owner) ?? OrdinaryTurn);
                if (!Retaliations.GetOrCreateValue(card).Used.Add(key)) continue;
                await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), card, scope.Attacker);
            }
        }
    }
}
