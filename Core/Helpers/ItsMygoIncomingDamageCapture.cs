using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Patches.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ItsMygoIncomingDamageCapture
{
    private sealed class RawDamage
    {
        internal Creature Target { get; }
        internal decimal Amount { get; }

        internal RawDamage(Creature target, decimal amount)
        {
            Target = target;
            Amount = Math.Max(0m, amount);
        }
    }

    private sealed class CaptureScope
    {
        internal List<DamageResult> Results { get; }
        internal RawDamage Captured { get; set; }

        internal CaptureScope(List<DamageResult> results)
        {
            Results = results;
        }
    }

    // A damage command calculates every target before firing AfterDamageReceived.
    // Keep each capture with that command's result list, then with the exact result.
    private static readonly ConditionalWeakTable<List<DamageResult>, RawDamage>
        PendingResults = new();
    private static readonly ConditionalWeakTable<DamageResult, RawDamage>
        CapturedResults = new();

    [ThreadStatic]
    private static Stack<CaptureScope> _scopes;

    internal static int ScopeDepth => _scopes?.Count ?? 0;

    internal static void Begin(List<DamageResult> results)
    {
        _scopes ??= new Stack<CaptureScope>();
        _scopes.Push(new CaptureScope(results));
    }

    internal static void Capture(Creature target, decimal rawAmount)
    {
        if (target == null || _scopes?.Count is not > 0 ||
            !StrongestShieldDamagePatch.IsResolvingActualDamage)
        {
            return;
        }

        _scopes.Peek().Captured = new RawDamage(target, rawAmount);
    }

    internal static void LimitPendingCapture(Creature target, decimal allowance)
    {
        if (_scopes?.Count is not > 0) return;
        CaptureScope scope = _scopes.Peek();
        if (scope.Captured?.Target == target)
            scope.Captured = new RawDamage(target, Math.Min(scope.Captured.Amount, Math.Max(0m, allowance)));
    }

    internal static void End()
    {
        if (_scopes?.Count is not > 0)
        {
            return;
        }

        CaptureScope scope = _scopes.Pop();
        if (scope.Results == null)
        {
            return;
        }

        PendingResults.Remove(scope.Results);
        if (scope.Captured != null)
        {
            PendingResults.Add(scope.Results, scope.Captured);
        }
    }

    internal static void RestoreScopeDepth(int depth)
    {
        while (_scopes?.Count > depth)
        {
            _scopes.Pop();
        }
    }

    internal static void AppendResults(
        List<DamageResult> destination,
        IEnumerable<DamageResult> results)
    {
        IReadOnlyList<DamageResult> hitResults = results as IReadOnlyList<DamageResult> ?? results.ToArray();
        HpOnlyDamageContext.BindDpReceipts(hitResults);
        CardDamageHistoryHelper.RecordHit(hitResults);
        HighHitDamageVisualHelper.RecordHit(hitResults);
        PendingResults.TryGetValue(destination, out RawDamage capture);
        PendingResults.Remove(destination);
        foreach (DamageResult result in hitResults)
        {
            // Redirected damage belongs to its original recipient. Do not assign
            // the full incoming hit to a pet/phantom's separate damage result.
            if (capture != null && result.Receiver == capture.Target)
            {
                BindRaw(capture.Target, result, capture.Amount);
            }

            destination.Add(result);
        }
    }

    internal static void BindRaw(
        Creature target,
        DamageResult result,
        decimal rawAmount)
    {
        if (target == null || result == null || result.Receiver != target)
        {
            return;
        }

        CapturedResults.Remove(result);
        CapturedResults.Add(result, new RawDamage(target, rawAmount));
    }

    internal static bool TryGetRaw(
        Creature target,
        DamageResult result,
        out decimal rawAmount)
    {
        rawAmount = 0m;
        if (target == null || result == null || result.Receiver != target ||
            !CapturedResults.TryGetValue(result, out RawDamage capture) ||
            capture.Target != target)
        {
            return false;
        }

        rawAmount = capture.Amount;
        return true;
    }
}
