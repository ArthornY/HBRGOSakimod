using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal sealed class RollPowerReservation
{
    internal readonly List<CountedRollPower> Powers = new();
    internal bool Consumed;
    public int BonusPercent { get; internal set; }
    public int ExtraDice { get; internal set; }
}

internal static class HbrRollPowerHelper
{
    private sealed class MergeState
    {
        internal int Deferrals;
        internal bool Merging;
    }

    private static readonly ConditionalWeakTable<Creature, MergeState> Merges = new();

    internal static IEnumerable<CountedRollPower> Available(Creature owner) =>
        owner?.Powers.OfType<CountedRollPower>().Where(power => power.Amount != 0 && power.AvailableUses > 0)
        ?? Enumerable.Empty<CountedRollPower>();

    private static int Sum(IEnumerable<int> amounts)
    {
        long result = 0;
        foreach (int amount in amounts) result += amount;
        return (int)Math.Clamp(result, int.MinValue, int.MaxValue);
    }

    internal static int GetAvailableBonus(Creature owner) =>
        Sum(Available(owner).OfType<RollBonusPower>().Select(power => power.Amount));

    internal static int GetAvailableExtraDice(Creature owner) =>
        Sum(Available(owner).OfType<AdvantageDicePower>().Select(power => Math.Max(0, power.Amount)));

    internal static RollPowerReservation Reserve(Creature owner)
    {
        RollPowerReservation reservation = null;
        // A reservation spans a logical roll, including all its advantage dice.
        // It owns one use per group, never another card/cannon/shield's uses.
        foreach (CountedRollPower power in Available(owner).ToArray())
        {
            if (!power.TryReserveRoll()) continue;
            reservation ??= new RollPowerReservation();
            reservation.Powers.Add(power);
        }
        if (reservation == null) return null;
        reservation.BonusPercent = Sum(reservation.Powers.OfType<RollBonusPower>().Select(power => power.Amount));
        reservation.ExtraDice = Sum(reservation.Powers.OfType<AdvantageDicePower>().Select(power => Math.Max(0, power.Amount)));
        return reservation;
    }

    internal static async Task ConsumeReservation(PlayerChoiceContext context,
        RollPowerReservation reservation, CardModel source)
    {
        if (reservation == null || reservation.Consumed) return;
        reservation.Consumed = true;
        foreach (CountedRollPower power in reservation.Powers)
            await power.ConsumeReservedRolls(context, source, 1);
    }

    internal static async Task ConsumeReservations(PlayerChoiceContext context,
        IEnumerable<RollPowerReservation> reservations, CardModel source)
    {
        if (reservations == null) return;
        foreach (RollPowerReservation reservation in reservations.ToArray())
            await ConsumeReservation(context, reservation, source);
    }

    internal static async Task Apply<T>(PlayerChoiceContext context, Creature target, int value, int uses,
        Creature applier, CardModel cardSource, bool silent) where T : CountedRollPower
    {
        if (target == null || value == 0 || uses <= 0) return;
        var power = (T)ModelDb.Power<T>().ToMutable();
        power.ConfigureUses(uses);
        // The normal command retains receiving/giving hooks and provenance.
        // RollGlossary excludes CountedRollPower from its authored-value scale;
        // N is stored separately and is not an amount seen by those modifiers.
        await PowerCmd.Apply(context, power, target, value, applier, cardSource, silent);
    }

    internal static async Task DoubleAll(PlayerChoiceContext context, Creature owner,
        Creature applier, CardModel source)
    {
        if (owner == null) return;
        var snapshot = owner.Powers.OfType<CountedRollPower>()
            .Select(power => (Power: power, Amount: power.Amount)).ToArray();
        if (snapshot.Length == 0) return;
        MergeState state = Merges.GetOrCreateValue(owner);
        state.Deferrals++;
        try
        {
            foreach (var entry in snapshot)
                if (entry.Amount != 0 && owner.Powers.Contains(entry.Power))
                    await PowerCmd.ModifyAmount(context, entry.Power, entry.Amount, applier, source);
        }
        finally { state.Deferrals--; }
        await MergeSameValues(context, owner);
    }

    internal static async Task MergeSameValues(PlayerChoiceContext context, Creature owner)
    {
        if (owner == null) return;
        MergeState state = Merges.GetOrCreateValue(owner);
        if (state.Deferrals > 0 || state.Merging) return;
        state.Merging = true;
        try
        {
            // Keep stable instance identity and native ModelId. Native saves
            // and NPowerContainer already support Instanced powers by object.
            foreach (var group in owner.Powers.OfType<CountedRollPower>()
                .Where(power => power.RemainingUses > 0 && power.Amount != 0)
                .GroupBy(power => (power.GetType(), power.Amount)).Select(group => group.ToArray()).ToArray())
            {
                CountedRollPower kept = group[0];
                foreach (CountedRollPower extra in group.Skip(1))
                {
                    if (!owner.Powers.Contains(kept) || !owner.Powers.Contains(extra)) continue;
                    kept.Absorb(extra);
                    await PowerCmd.Remove(extra);
                }
            }
        }
        finally { state.Merging = false; }
    }
}
