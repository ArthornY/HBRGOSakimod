using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Models.Capabilities;

namespace HBRGOSakiMod.Core.Helpers;

internal static class SaviorGrandEntranceCombatHelper
{
    internal sealed class DamageScope
    {
        internal DamageScope Previous;
        internal Creature Dealer;
        internal bool RawCritical;
        internal bool Active = true;
    }
    private sealed record RawReceipt(Creature Dealer);
    private sealed class MaxHpHeal
    {
        internal Creature Target;
        internal bool Claimed;
    }
    private static readonly AsyncLocal<DamageScope> Current = new();
    private static readonly AsyncLocal<int> DpFollowupDepth = new();
    private static readonly AsyncLocal<MaxHpHeal> MaxHpHealing = new();
    private static readonly ConditionalWeakTable<DamageResult, RawReceipt> RawReceipts = new();

    internal static bool HasUpgradeEffect(Creature owner, int id) =>
        owner?.GetPower<SaviorGrandEntrancePower>()?.HasUpgradeEffect(id) == true;
    internal static bool HasEnemyUpgrade(Creature target, int id) =>
        target?.CombatState?.GetOpponentsOf(target).Any(owner => HasUpgradeEffect(owner, id)) == true;
    internal static bool HasEnemyUpgradeEffect(Creature target, int id) => HasEnemyUpgrade(target, id);
    internal static bool CanConsumeDpFollowup(Creature dealer) =>
        DpFollowupDepth.Value == 0 && HasUpgradeEffect(dealer, 3);
    internal static int GetRawCriticalMinimumReduction(Creature owner) =>
        HasUpgradeEffect(owner, 0) ? DodgeCombatHelper.GetAmount(owner) : 0;
    internal static decimal GetDamageMultiplier(Creature target, Creature dealer, ValueProp props)
    {
        decimal multiplier = 1m;
        if (HasEnemyUpgrade(target, 1) && target.IsStunned) multiplier *= 1.25m;
        if (HasEnemyUpgrade(target, 6)) multiplier *= 1m + 0.1m * SixSenseCombatHelper.TargetResistanceBreakCount(target);
        return multiplier;
    }
    internal static bool TryGetHealingEnemyOwner(Creature target, out Creature owner)
    {
        // Native healing revives dead creatures and emits their Revived event.
        // Preserve that transition before converting a living enemy's healing.
        owner = target?.IsAlive == true
            ? target.CombatState?.GetOpponentsOf(target)
                .FirstOrDefault(candidate => candidate.IsAlive && HasUpgradeEffect(candidate, 4))
            : null;
        return owner != null;
    }
    internal static async Task ConvertEnemyHealing(Creature target, Creature owner, decimal amount)
    {
        using IDisposable computed = PrecalculatedDamageContext.Begin(null, target, amount);
        await CreatureCommandCompatibility.Damage(new ThrowingPlayerChoiceContext(), target, amount,
            ValueProp.Unpowered, owner, null);
    }
    internal static bool TakeMaxHpHeal(Creature target)
    {
        MaxHpHeal intent = MaxHpHealing.Value;
        if (intent == null || intent.Claimed || !ReferenceEquals(intent.Target, target)) return false;
        intent.Claimed = true;
        return true;
    }
    internal static async Task HealFromMaxHp(Creature target, decimal amount, bool playAnim = true)
    {
        MaxHpHeal previous = MaxHpHealing.Value;
        MaxHpHeal intent = new() { Target = target };
        MaxHpHealing.Value = intent;
        try { await CreatureCmd.Heal(target, amount, playAnim); }
        finally { if (ReferenceEquals(MaxHpHealing.Value, intent)) MaxHpHealing.Value = previous; }
    }
    internal static Task HealFromPhaseTransition(Creature target, decimal amount, bool playAnim = true) =>
        HealFromMaxHp(target, amount, playAnim);
    internal static DamageScope BeginDamage(Creature dealer, CardModel card, ValueProp props)
    {
        SoulRollBreakdown roll = card?.Capability<RollGlossaryCapability>()?.GetCurrentRollDetailsFor(card);
        DamageScope scope = new()
        {
            Previous = Current.Value,
            Dealer = dealer,
            RawCritical = props.IsPoweredAttack() && card?.Type == CardType.Attack &&
                card.Owner?.Creature == dealer && dealer?.HasPower<SaviorGrandEntrancePower>() == true &&
                roll != null && roll.BaseRoll <= RollKeywordRules.RollMaximum &&
                roll.BaseRoll >= RollKeywordRules.GetCriticalMinimum(card.Owner)
        };
        Current.Value = scope;
        return scope;
    }
    internal static void RestoreCaller(DamageScope scope)
    {
        if (scope != null && ReferenceEquals(Current.Value, scope)) Current.Value = scope.Previous;
    }
    internal static async Task<IEnumerable<DamageResult>> FinishDamage(Task<IEnumerable<DamageResult>> task, DamageScope scope)
    {
        try { return await task; }
        finally { CloseDamage(scope); }
    }
    internal static void CloseDamage(DamageScope scope)
    {
        if (scope == null) return;
        scope.Active = false;
        RestoreCaller(scope);
    }
    internal static void BindRawRollReceipts(IReadOnlyList<DamageResult> results)
    {
        DamageScope scope = Current.Value;
        if (scope?.Active != true || !scope.RawCritical || results == null) return;
        foreach (DamageResult result in results)
        {
            if (result == null || result.Receiver?.Side == scope.Dealer.Side) continue;
            RawReceipts.Remove(result);
            RawReceipts.Add(result, new RawReceipt(scope.Dealer));
        }
    }
    internal static async Task AfterDamageGiven(PlayerChoiceContext context, SaviorGrandEntrancePower power,
        Creature dealer, DamageResult result, ValueProp props, Creature target, CardModel cardSource)
    {
        if (dealer != power.Owner || target?.Side == power.Owner.Side || target == null || result == null) return;
        if (RawReceipts.TryGetValue(result, out RawReceipt receipt) && receipt.Dealer == dealer)
        {
            RawReceipts.Remove(result);
            if (target.IsAlive && target.Monster != null && !target.IsStunned) await CreatureCmd.Stun(target);
        }
        if (!CanConsumeDpFollowup(dealer) ||
            !HpOnlyDamageContext.TryTakeSaviorDpReceipt(result, out decimal spent) || spent <= 0m ||
            !target.IsHittable) return;
        DpFollowupDepth.Value++;
        try
        {
            using IDisposable computed = PrecalculatedDamageContext.Begin(cardSource, target, HbrResourceValueHelper.Fixed(dealer, 1m));
            await CreatureCommandCompatibility.Damage(context, target, HbrResourceValueHelper.Fixed(dealer, 1m), ValueProp.Unpowered, dealer, cardSource);
        }
        finally { DpFollowupDepth.Value--; }
    }
}
