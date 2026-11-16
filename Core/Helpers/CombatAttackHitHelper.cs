using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// One original target may produce several physical results after redirection.
// Bind one shared hit identity before native AfterDamageGiven dispatch, while
// keeping exact DP loss readable independently from one-shot follow-up receipts.
internal static class CombatAttackHitHelper
{
    private sealed class Hit
    {
        internal Creature Dealer;
        internal CardModel Card;
        internal bool Connected;
        internal Creature PhysicalTarget;
        internal HashSet<Overclock> Owners;
    }
    private sealed record Result(Hit Hit, decimal DpDamage, bool Connected, bool Dodged);
    private static readonly ConditionalWeakTable<DamageResult, Result> Results = new();

    internal static void Bind(IReadOnlyList<DamageResult> results, Creature dealer, CardModel card,
        Creature dpReceiver, decimal dpSpent, IReadOnlySet<Creature> dodged)
        => BindPhysical(results, dealer, card, dpReceiver, dpSpent, dodged, HpOnlyDamageContext.CurrentPhysicalTarget);

    internal static void BindPhysical(IReadOnlyList<DamageResult> results, Creature dealer, CardModel card,
        Creature dpReceiver, decimal dpSpent, IReadOnlySet<Creature> dodged, Creature physicalTarget)
    {
        if (results?.Count is not > 0) return;
        DamageResult primary = results.FirstOrDefault(result => ReferenceEquals(result?.Receiver, dpReceiver)) ?? results[^1];
        Hit hit = new()
        {
            Dealer = dealer, Card = card, PhysicalTarget = physicalTarget,
            Connected = dpSpent > 0m || results.Any(result => result != null &&
                (dodged == null || !dodged.Contains(result.Receiver)) &&
                (result.BlockedDamage > 0 || result.UnblockedDamage > 0))
        };
        foreach (DamageResult result in results)
        {
            if (result == null) continue;
            decimal actualDp = ReferenceEquals(result, primary) ? Math.Max(0m, dpSpent) : 0m;
            bool connected = actualDp > 0m ||
                (dodged == null || !dodged.Contains(result.Receiver)) &&
                (result.BlockedDamage > 0 || result.UnblockedDamage > 0);
            Results.Remove(result);
            Results.Add(result, new(hit, actualDp, connected, dodged?.Contains(result.Receiver) == true));
        }
    }

    internal static bool TryClaim(Overclock owner, DamageResult result, Creature dealer, CardModel card)
    {
        if (result == null) return false;
        if (!Results.TryGetValue(result, out Result receipt))
        {
            // Synthetic/native callers outside the canonical command still
            // retain the ordinary positive-hit rule and per-owner idempotence.
            bool connected = result.BlockedDamage > 0 || result.UnblockedDamage > 0;
            receipt = new(new Hit { Dealer = dealer, Card = card, Connected = connected }, 0m, connected, false);
            Results.Add(result, receipt);
        }
        return receipt.Hit.Connected && ReferenceEquals(receipt.Hit.Dealer, dealer) &&
            ReferenceEquals(receipt.Hit.Card, card) &&
            (receipt.Hit.Owners ??= new HashSet<Overclock>(ReferenceEqualityComparer.Instance)).Add(owner);
    }

    internal static bool IsConnected(DamageResult result) => result != null &&
        (Results.TryGetValue(result, out Result receipt) ? receipt.Connected :
            result.BlockedDamage > 0 || result.UnblockedDamage > 0);

    internal static bool WasDodged(DamageResult result) => result != null &&
        Results.TryGetValue(result, out Result receipt) && receipt.Dodged;

    internal static Creature GetPhysicalTarget(DamageResult result) => result != null &&
        Results.TryGetValue(result, out Result receipt) ? receipt.Hit.PhysicalTarget : null;

    internal static decimal GetResourceDamage(DamageResult result) => result == null ? 0m :
        DamageAmplificationLimits.AddSaturated(Results.TryGetValue(result, out Result receipt) ? receipt.DpDamage : 0m,
            Math.Max(0m, (decimal)result.UnblockedDamage));
}
