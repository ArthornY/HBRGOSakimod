using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal enum HbrEffectLimit { Default, Only, None }
internal enum HbrEffectExit { Count, PlayerTurnEnd, EnemyTurnEnd, Eternal, EnemyTurnStart, OwnerTurnStart, OwnerTurnEnd }
internal readonly record struct HbrEffectProfile(HbrEffectLimit Limit, HbrEffectExit Exit, int DefaultRemaining);

internal interface IHbrEffectInstance
{
    decimal EffectPotency { get; }
    int Remaining { get; }
    HbrEffectProfile EffectProfile { get; }
}

internal readonly record struct HbrEffectSlotDecision(bool Accepted, PowerModel Replaced);

// Shared original-game effect policy. Only currently falls back to Default;
// no potency-equality shortcut merges independently acquired applications.
internal static class HbrEffectRules
{
    internal static HbrEffectSlotDecision Select(PowerModel incoming, Creature owner,
        decimal potency, HbrEffectProfile profile)
    {
        if (profile.Limit == HbrEffectLimit.None) return new(true, null);
        PowerModel[] occupied = owner.Powers.Where(power => !ReferenceEquals(power, incoming) &&
            power.GetType() == incoming.GetType() && power is IHbrEffectInstance instance &&
            instance.Remaining > 0 && instance.EffectProfile.Limit != HbrEffectLimit.None).ToArray();
        if (occupied.Length < 2) return new(true, null);
        PowerModel weakest = occupied.OrderBy(power => ((IHbrEffectInstance)power).EffectPotency)
            .ThenBy(power => ((IHbrEffectInstance)power).Remaining).First();
        return potency >= ((IHbrEffectInstance)weakest).EffectPotency
            ? new(true, weakest) : new(false, null);
    }

    internal static async Task<bool> Enforce(PowerModel incoming, decimal potency, HbrEffectProfile profile)
    {
        HbrEffectSlotDecision decision = Select(incoming, incoming.Owner, potency, profile);
        if (!decision.Accepted) { await PowerCmd.Remove(incoming); return false; }
        if (decision.Replaced != null) await PowerCmd.Remove(decision.Replaced);
        return true;
    }

    internal static bool ShouldTick(PowerModel power, HbrEffectProfile profile, CombatSide side,
        IEnumerable<Creature> participants = null) =>
        power.Owner != null && power.Amount > 0 && !ExtraTurnDurationHelper.ShouldSuppressDurationTick(power) &&
        !HBRGOSakiMod.Core.Models.Powers.HotaruUnfadingScarsPower.PreventsNaturalDecay(power) &&
        (profile.Exit != HbrEffectExit.PlayerTurnEnd || participants == null || participants.Contains(power.Owner)) &&
        (profile.Exit == HbrEffectExit.PlayerTurnEnd && side == CombatSide.Player ||
         profile.Exit == HbrEffectExit.EnemyTurnEnd && side == CombatSide.Enemy ||
         profile.Exit == HbrEffectExit.OwnerTurnEnd && side == power.Owner.Side && participants != null &&
            (participants.Contains(power.Owner) || power.Owner.PetOwner is { } owner && participants.Contains(owner.Creature)));

    internal static async Task Tick(PowerModel power, HbrEffectProfile profile, CombatSide side,
        IEnumerable<Creature> participants = null)
    {
        if (!ShouldTick(power, profile, side, participants)) return;
        // The original exit condition counts every matching side end, without
        // vanilla's first-turn duration skip.
        await PowerCmd.Decrement(power);
    }

    internal static async Task TickStart(PowerModel power, HbrEffectProfile profile, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (power.Owner == null || power.Amount <= 0 || ExtraTurnDurationHelper.ShouldSuppressDurationTick(power) ||
            HBRGOSakiMod.Core.Models.Powers.HotaruUnfadingScarsPower.PreventsNaturalDecay(power)) return;
        bool matches = profile.Exit == HbrEffectExit.EnemyTurnStart && side == CombatSide.Enemy ||
            profile.Exit == HbrEffectExit.OwnerTurnStart && side == power.Owner.Side && participants.Contains(power.Owner);
        if (matches) await PowerCmd.Decrement(power);
    }
}
