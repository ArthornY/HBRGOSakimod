using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Capabilities;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class ExtraServingCombatHelper
{
    internal sealed class Payment
    {
        internal CardModel Card;
        internal Payment Previous;
        internal bool Active = true, EnergySeen;
        internal int Paid, Debt;
    }
    private sealed class TurnLedger
    {
        internal ICombatState Combat;
        internal readonly HashSet<(int, object)> Settled = new();
    }
    private static readonly AsyncLocal<Payment> Current = new();
    private static readonly ConditionalWeakTable<Player, TurnLedger> Turns = new();
    internal static bool Eligible(CardModel card) => card is IExtraServingCard && card.IsMutable &&
        card.Owner?.Creature is { IsAlive: true, CombatState: not null } && HbrBattleStatsHelper.IsEnabled(card.Owner) &&
        !card.EnergyCost.CostsX && card.EnergyCost.Canonical >= 0;
    internal static (int, object) Turn(Player player) => (player.PlayerCombatState?.TurnNumber ?? 0,
        OverclockImmediateTurnCoordinator.GetTurnToken(player));
    internal static Payment BeginPayment(CardModel card)
    {
        if (!Eligible(card)) return null;
        Payment value = new() { Card = card, Previous = Current.Value }; Current.Value = value; return value;
    }
    internal static void ClampPayment(CardModel card, ref int amount)
    {
        Payment value = Current.Value;
        if (value?.Active != true || value.EnergySeen || !ReferenceEquals(value.Card, card)) return;
        value.EnergySeen = true;
        int requested = Math.Max(0, amount), available = Math.Max(0, card.Owner.PlayerCombatState?.Energy ?? 0);
        value.Paid = Math.Min(requested, available); value.Debt = requested - value.Paid; amount = value.Paid;
    }
    internal static void RestoreCaller(Payment value)
    { if (value != null && ReferenceEquals(Current.Value, value)) Current.Value = value.Previous; }
    internal static void Close(Payment value)
    { if (value != null) value.Active = false; RestoreCaller(value); }
    internal static async Task<(int, int)> FinishPayment(Task<(int, int)> original, Payment value)
    {
        try
        {
            var paid = await original;
            if (value.EnergySeen && value.Debt > 0) AddDebt(value.Card.Owner, value.Debt);
            return value.EnergySeen ? (value.Paid, paid.Item2) : paid;
        }
        finally { Close(value); }
    }
    internal static void AddDebt(Player player, int amount)
    {
        if (amount <= 0 || player?.Creature is not { IsAlive: true, CombatState: not null } holder || !HbrBattleStatsHelper.IsEnabled(player)) return;
        HbrEnergyDebtPower power = holder.GetPower<HbrEnergyDebtPower>();
        if (power == null)
        {
            power = (HbrEnergyDebtPower)ModelDb.Power<HbrEnergyDebtPower>().ToMutable();
            power.ApplyInternal(holder, amount);
            power.RegisterNewCharge(player, amount);
        }
        else
        {
            int before = power.Amount;
            power.SetAmount((int)Math.Clamp((long)before + amount, 0L, int.MaxValue));
            power.RegisterNewCharge(player, Math.Max(0, power.Amount - before));
        }
    }
    internal static Task AfterEnergyReset(Player player)
    {
        if (player?.Creature is not { IsAlive: true, CombatState: { } combat } holder || !HbrBattleStatsHelper.IsEnabled(player)) return Task.CompletedTask;
        TurnLedger ledger = Turns.GetOrCreateValue(player);
        if (!ReferenceEquals(ledger.Combat, combat)) { ledger.Combat = combat; ledger.Settled.Clear(); }
        var turn = Turn(player);
        if (!ledger.Settled.Add(turn) || holder.GetPower<HbrEnergyDebtPower>() is not { Amount: > 0 } debt) return Task.CompletedTask;
        // A paid play in an earlier turn-start callback must not postpone old
        // debt, and its newly borrowed part belongs to the following reset.
        int due = Math.Max(0, debt.Amount - debt.NewDebtInTurn(player));
        if (due <= 0) return Task.CompletedTask;
        int left = debt.Amount - due;
        if (left <= 0) debt.RemoveInternal(); else debt.SetAmount(left);
        player.PlayerCombatState.LoseEnergy(Math.Min(due, Math.Max(0, player.PlayerCombatState.Energy)));
        return Task.CompletedTask;
    }
}
