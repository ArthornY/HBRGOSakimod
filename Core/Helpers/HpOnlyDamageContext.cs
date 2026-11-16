using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

// A one-shot intent is claimed by the real canonical Damage invocation. Nested
// commands get their own scope, so an HP supplement never turns a reaction or
// another simultaneous command into HP-only damage.
internal static class HpOnlyDamageContext
{
    private const ValueProp HpDamageProps = ValueProp.Unblockable | ValueProp.Unpowered;

    private sealed class PendingIntent
    {
        internal Creature Target;
        internal decimal Amount;
        internal Creature Dealer;
        internal CardModel Card;
        internal bool Active = true;
        internal bool Claimed;
    }

    internal sealed class CommandScope
    {
        internal CommandScope Previous;
        internal bool Active = true;
        internal bool Enabled;
        internal bool CaptureDp;
        internal bool CaptureMagic;
        internal bool CaptureSavior;
        internal bool CaptureHit;
        internal bool CaptureBlockBreak;
        internal bool PendingNativeBlockObserved;
        internal bool PendingNativeBlockBroken;
        internal bool HealthCost;
        internal HashSet<Creature> PendingDodges;
        internal Creature Target;
        internal Creature Dealer;
        internal CardModel Card;
        internal Creature PhysicalTarget;
        internal Creature PendingDpReceiver;
        internal decimal PendingDpSpent;
    }

    private sealed class DpReceipt
    {
        internal readonly decimal Spent;
        internal int MagicTaken;
        internal int SaviorTaken;
        internal DpReceipt(decimal spent, bool magic, bool savior)
        {
            Spent = spent;
            MagicTaken = magic ? 0 : 1;
            SaviorTaken = savior ? 0 : 1;
        }
    }

    private static readonly AsyncLocal<PendingIntent> Pending = new();
    private static readonly AsyncLocal<CommandScope> Current = new();
    private static readonly ConditionalWeakTable<DamageResult, DpReceipt> Receipts = new();
    private sealed record NativeBlockBreakReceipt(bool Observed, bool Broken);
    private static readonly ConditionalWeakTable<DamageResult, NativeBlockBreakReceipt> NativeBlockBreaks = new();

    internal static bool IsCurrentCommandFrom(Creature dealer) =>
        Current.Value is { Active: true } scope && ReferenceEquals(scope.Dealer, dealer);

    internal static bool IsCurrentHealthCost => Current.Value is { Active: true, HealthCost: true };

    internal static Creature CurrentPhysicalTarget =>
        Current.Value is { Active: true } scope ? scope.PhysicalTarget : null;

    internal static void RecordPhysicalTarget(Creature target)
    {
        if (Current.Value is { Active: true } scope) scope.PhysicalTarget = target;
    }

    internal static async Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext, Creature target, decimal amount,
        Creature dealer, CardModel cardSource)
    {
        if (target == null || amount <= 0m) return Array.Empty<DamageResult>();

        PendingIntent previous = Pending.Value;
        PendingIntent intent = new()
        {
            Target = target,
            Amount = amount,
            Dealer = dealer,
            Card = cardSource
        };
        Pending.Value = intent;
        try
        {
            // Call the canonical enumerable overload directly. It still owns
            // all normal damage, HP-loss, death, targeting and history hooks.
            return await CreatureCommandCompatibility.Damage(choiceContext,
                new[] { target }, amount, HpDamageProps, dealer, cardSource);
        }
        finally
        {
            intent.Active = false;
            if (ReferenceEquals(Pending.Value, intent)) Pending.Value = previous;
        }
    }

    internal static CommandScope Begin(ref IEnumerable<Creature> targets,
        decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
    {
        PendingIntent intent = Pending.Value;
        bool enabled = false;
        if (intent?.Active == true && !intent.Claimed && props == HpDamageProps &&
            intent.Amount == amount && ReferenceEquals(intent.Dealer, dealer) &&
            ReferenceEquals(intent.Card, cardSource))
        {
            // Only inspect/materialize targets for an unclaimed HP intent.
            // Retain a materialized sequence for the native command as well.
            IReadOnlyList<Creature> candidates = targets as IReadOnlyList<Creature>;
            if (candidates == null && targets != null)
                targets = candidates = targets.ToArray();
            enabled = candidates?.Count == 1 &&
                ReferenceEquals(candidates[0], intent.Target);
            if (enabled) intent.Claimed = true;
        }

        bool captureMagic = !enabled && !props.HasFlag(ValueProp.Unblockable) &&
            dealer?.HasPower<MagicSwordEnhancementPower>() == true && dealer.HasPower<MagicSwordModePower>();
        bool captureSavior = !enabled && !props.HasFlag(ValueProp.Unblockable) &&
            SaviorGrandEntranceCombatHelper.CanConsumeDpFollowup(dealer);
        bool captureHit = props.IsPoweredAttack() && dealer != null;
        bool captureBlockBreak = (dealer?.Player ?? dealer?.PetOwner)?.GetRelic<HandDrill>() != null;
        CommandScope scope = new()
        {
            Previous = Current.Value,
            Enabled = enabled,
            CaptureDp = captureMagic || captureSavior || captureHit || captureBlockBreak,
            CaptureMagic = captureMagic,
            CaptureSavior = captureSavior,
            CaptureHit = captureHit,
            CaptureBlockBreak = captureBlockBreak,
            HealthCost = HbrHealthCostContext.TryClaim(ref targets, amount, props, dealer, cardSource),
            Target = enabled ? intent.Target : null,
            Dealer = dealer,
            Card = cardSource
        };
        Current.Value = scope;
        return scope;
    }

    internal static bool IsCurrentHpDamage(Creature target, Creature dealer,
        CardModel cardSource, ValueProp props)
    {
        CommandScope scope = Current.Value;
        return scope?.Active == true && scope.Enabled && props == HpDamageProps &&
            ReferenceEquals(scope.Target, target) &&
            ReferenceEquals(scope.Dealer, dealer) &&
            ReferenceEquals(scope.Card, cardSource);
    }

    internal static bool BypassesDpForBlockReceiver(Creature receiver)
    {
        CommandScope scope = Current.Value;
        return receiver != null && scope?.Active == true && scope.Enabled &&
            (ReferenceEquals(receiver, scope.Target) ||
             ReferenceEquals(receiver, scope.Target?.PetOwner?.Creature));
    }

    internal static Creature GetCurrentHpDamageTarget() =>
        Current.Value is { Active: true, Enabled: true } scope ? scope.Target : null;

    internal static void RestoreCaller(CommandScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope))
            Current.Value = scope.Previous;
    }

    internal static async Task<IEnumerable<DamageResult>> Finish(
        Task<IEnumerable<DamageResult>> original, CommandScope scope)
    {
        try { return await original; }
        finally { Close(scope); }
    }

    internal static void Close(CommandScope scope)
    {
        if (scope == null) return;
        scope.Active = false;
        scope.PhysicalTarget = null;
        scope.PendingDpReceiver = null;
        scope.PendingDpSpent = 0m;
        scope.PendingNativeBlockObserved = scope.PendingNativeBlockBroken = false;
        scope.PendingDodges?.Clear();
        RestoreCaller(scope);
    }

    // Ordinary commands also have a scope. A snapshot is bound before their
    // AfterDamageGiven hooks can cause another hit to overwrite shell state.
    internal static void RecordDpAbsorption(Creature receiver, decimal spent)
    {
        CommandScope scope = Current.Value;
        if (scope?.Active != true || !scope.CaptureDp || receiver == null || spent <= 0m) return;
        scope.PendingDpReceiver = receiver;
        scope.PendingDpSpent = DamageAmplificationLimits.AddSaturated(scope.PendingDpSpent, spent);
    }

    internal static void RecordDodge(Creature receiver)
    {
        CommandScope scope = Current.Value;
        if (scope?.Active != true || !(scope.CaptureHit || scope.CaptureBlockBreak) || receiver == null) return;
        scope.PendingDodges ??= new HashSet<Creature>(ReferenceEqualityComparer.Instance);
        scope.PendingDodges.Add(receiver);
    }

    // Called before the DP/31F additions to DamageBlockInternal's return value.
    // Redirected physical calls share this hit; a true break on any of them
    // preserves the native notification instead of inferring it from DP loss.
    internal static void RecordNativeBlockResult(Creature receiver, decimal nativeBlocked)
    {
        CommandScope scope = Current.Value;
        if (scope?.Active != true || !scope.CaptureBlockBreak || receiver == null) return;
        scope.PendingNativeBlockObserved = true;
        scope.PendingNativeBlockBroken |= nativeBlocked > 0m &&
            DodgeCombatHelper.GetNativeBlock(receiver) == 0 &&
            scope.PendingDodges?.Contains(receiver) != true;
    }

    internal static bool TryGetNativeBlockBreak(DamageResult result, out bool broken)
    {
        broken = false;
        if (result == null || !NativeBlockBreaks.TryGetValue(result, out NativeBlockBreakReceipt receipt) ||
            !receipt.Observed) return false;
        broken = receipt.Broken;
        return true;
    }

    internal static void BindDpReceipts(IReadOnlyList<DamageResult> hitResults)
    {
        CommandScope scope = Current.Value;
        if (scope?.Active != true || !scope.CaptureDp) return;

        if (scope.CaptureBlockBreak && hitResults != null)
        {
            foreach (DamageResult hit in hitResults)
            {
                if (hit == null) continue;
                NativeBlockBreaks.Remove(hit);
                NativeBlockBreaks.Add(hit, new(scope.PendingNativeBlockObserved, scope.PendingNativeBlockBroken));
            }
        }
        scope.PendingNativeBlockObserved = scope.PendingNativeBlockBroken = false;

        Creature receiver = scope.PendingDpReceiver;
        Creature physicalTarget = scope.PhysicalTarget;
        scope.PhysicalTarget = null;
        decimal spent = scope.PendingDpSpent;
        // One append belongs to one original target, including its redirected
        // results. Clear before binding so this cannot be reused by a later hit.
        scope.PendingDpReceiver = null;
        scope.PendingDpSpent = 0m;
        if (scope.CaptureHit)
            CombatAttackHitHelper.BindPhysical(hitResults, scope.Dealer, scope.Card, receiver, spent, scope.PendingDodges, physicalTarget);
        scope.PendingDodges?.Clear();
        if (!scope.CaptureMagic && !scope.CaptureSavior) return;
        if (receiver == null || spent <= 0m || hitResults?.Count is not > 0) return;

        DamageResult result = null;
        for (int index = 0; index < hitResults.Count; index++)
        {
            if (!ReferenceEquals(hitResults[index]?.Receiver, receiver)) continue;
            result = hitResults[index];
            break;
        }
        // DP may be resolved on a pet while a fully blocked hit returns only
        // the original owner's result. The final entry is the original-target
        // receipt, after any pet result and its transferred overkill.
        result ??= hitResults[hitResults.Count - 1];
        if (result == null) return;
        Receipts.Remove(result);
        Receipts.Add(result, new DpReceipt(spent, scope.CaptureMagic, scope.CaptureSavior));
    }

    internal static bool TryTakeDpReceipt(DamageResult result, out decimal spent)
        => TryTake(result, false, out spent);

    internal static bool TryTakeSaviorDpReceipt(DamageResult result, out decimal spent)
        => TryTake(result, true, out spent);

    private static bool TryTake(DamageResult result, bool savior, out decimal spent)
    {
        spent = 0m;
        if (result == null || !Receipts.TryGetValue(result, out DpReceipt receipt)) return false;
        int previous = savior ? Interlocked.Exchange(ref receipt.SaviorTaken, 1)
            : Interlocked.Exchange(ref receipt.MagicTaken, 1);
        if (previous != 0) return false;
        spent = receipt.Spent;
        if (Volatile.Read(ref receipt.MagicTaken) != 0 && Volatile.Read(ref receipt.SaviorTaken) != 0)
            Receipts.Remove(result);
        return spent > 0m;
    }
}
