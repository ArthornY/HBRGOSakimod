using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Helpers;

// Fragile and independent HBR attack/defense down share an application boundary.
// Native receiving/giving hooks, including Artifact, precede slot selection.
internal static class HbrPercentDebuffApplication
{
    // Only this mod's authored effects call these adapters. Native game calls
    // remain native regardless of the recipient's HBR mode.
    internal static Task ApplyAttackDown(PlayerChoiceContext context, Creature target,
        decimal amount, Creature applier, CardModel source = null, bool silent = false) =>
        HbrBattleStatsHelper.IsEnabled(target)
            ? PowerCmd.Apply<HbrAttackDownPower>(context, target, amount, applier, source, silent)
            : PowerCmd.Apply<WeakPower>(context, target, amount, applier, source, silent);

    internal static Task ApplyDefenseDown(PlayerChoiceContext context, Creature target,
        decimal amount, Creature applier, CardModel source = null, bool silent = false) =>
        HbrBattleStatsHelper.IsEnabled(target)
            ? PowerCmd.Apply<HbrDefenseDownPower>(context, target, amount, applier, source, silent)
            : PowerCmd.Apply<VulnerablePower>(context, target, amount, applier, source, silent);

    internal static async Task ApplyAttackDown(PlayerChoiceContext context, IEnumerable<Creature> targets,
        decimal amount, Creature applier, CardModel source = null, bool silent = false)
    {
        foreach (Creature target in targets) await ApplyAttackDown(context, target, amount, applier, source, silent);
    }

    internal static async Task ApplyDefenseDown(PlayerChoiceContext context, IEnumerable<Creature> targets,
        decimal amount, Creature applier, CardModel source = null, bool silent = false)
    {
        foreach (Creature target in targets) await ApplyDefenseDown(context, target, amount, applier, source, silent);
    }

    internal static PowerModel Replacement(PowerModel incoming, Creature target, decimal percent)
    {
        PowerModel weakest = null;
        int count = 0;
        foreach (PowerModel existing in target.Powers.Where(p => p.GetType() == incoming.GetType()))
        {
            if (existing.Amount <= 0) continue;
            count++;
            if (weakest == null || HbrPercentDebuffHelper.GetEffectPercent(existing) < HbrPercentDebuffHelper.GetEffectPercent(weakest) ||
                HbrPercentDebuffHelper.GetEffectPercent(existing) == HbrPercentDebuffHelper.GetEffectPercent(weakest) && existing.Amount < weakest.Amount)
                weakest = existing;
        }
        return count < 2 ? null : percent >= HbrPercentDebuffHelper.GetEffectPercent(weakest) ? weakest : null;
    }

    internal static Task Apply(PlayerChoiceContext context, PowerModel power,
        Creature target, decimal modifiers, Creature applier, CardModel source, bool silent,
        decimal? resolvedPercent = null, int copiedModifiers = 0) =>
        ApplyCore(context, power, target, modifiers, applier, source, silent,
            resolvedPercent, copiedModifiers, allowExtraInstance: false);

    // This explicit card-only path copies a frozen pre-play snapshot. Ordinary
    // applications, including copies from every other source, retain two slots.
    internal static Task ReplayCopy(PlayerChoiceContext context, PowerModel original,
        Creature target, decimal percent, int modifiers, Creature applier,
        HotaruReplayPain source, bool silent = false)
    {
        if (source == null || !HbrPercentDebuffHelper.Supports(original) ||
            !HbrBattleStatsHelper.IsEnabled(target)) return Task.CompletedTask;
        PowerModel copy = ModelDb.GetById<PowerModel>(original.Id).ToMutable();
        return ApplyCore(context, copy, target, 1m, applier, source, silent,
            percent, modifiers, allowExtraInstance: true);
    }

    private static async Task ApplyCore(PlayerChoiceContext context, PowerModel power,
        Creature target, decimal modifiers, Creature applier, CardModel source, bool silent,
        decimal? resolvedPercent, int copiedModifiers, bool allowExtraInstance)
    {
        if (CombatManager.Instance.IsEnding || modifiers <= 0m || !target.CanReceivePowers ||
            target.CombatState is not { } combat ||
            NararaNullificationHelper.BlocksPowerChange(power, target, modifiers, source)) return;
        bool fast = FragileAutoPlayAnimationHelper.IsSuppressed(source);
        silent |= fast;
        power.AssertMutable();
        // Explicit non-generic callers may pass an already attached instance.
        // Applications create a new slot; never insert the same object twice
        // or overwrite the original slot's snapshot and provenance.
        if (power.Owner != null) power = ModelDb.GetById<PowerModel>(power.Id).ToMutable();
        power.Applier = applier;
        await Hook.BeforePowerAmountChanged(combat, power, modifiers, target, applier, source);
        decimal modified = modifiers;
        IEnumerable<AbstractModel> given = null;
        if (applier != null && combat.ContainsCreature(applier))
            modified = Hook.ModifyPowerAmountGiven(combat, power, applier, modified,
                target, source, out given);
        modified = Hook.ModifyPowerAmountReceived(combat, power, target, modified,
            applier, out IEnumerable<AbstractModel> received);
        if (combat.Players.Count > 1 && (target.IsPrimaryEnemy || target.IsSecondaryEnemy) &&
            power.ShouldScaleInMultiplayer)
            modified = power.GetScaledAmountForMultiplayer(combat, applier, modified, target, source);
        modified = IntegralStatAmountHelper.Normalize(modified, power);
        int count = (int)Math.Min(int.MaxValue, Math.Max(0m, decimal.Truncate(modified)));
        bool accepted = count > 0 && target.CanReceivePowers;
        decimal percent = resolvedPercent ?? HbrPercentDebuffHelper.ResolvePercent(power, count,
            applier ?? source?.Owner?.Creature, target);
        if (accepted && !allowExtraInstance)
        {
            int occupied = target.Powers.Count(p => p.GetType() == power.GetType() && p.Amount > 0);
            PowerModel replaced = Replacement(power, target, percent);
            accepted = occupied < 2 || replaced != null;
            if (replaced != null)
            {
                if (!fast) await PowerCmd.Remove(replaced);
                else
                {
                    // The native remove command's mutations/callback remain;
                    // its animation wait is suppressed for this autoplay only.
                    Creature previousOwner = replaced.Owner;
                    replaced.RemoveInternal();
                    await replaced.AfterRemoved(previousOwner);
                }
            }
        }
        if (accepted && target.CanReceivePowers && !CombatManager.Instance.IsEnding)
        {
            HbrPercentDebuffHelper.Configure(power, percent, copiedModifiers > 0 ? copiedModifiers : count);
            await power.BeforeApplied(target, 2m, applier, source);
            power.ApplyInternal(target, 2m, silent);
            CombatManager.Instance.History.PowerReceived(combat, power, 2m, applier);
            if (power.IsVisible && CombatManager.Instance.IsInProgress)
                await FragileAutoPlayAnimationHelper.WaitForPowerAnimation(
                    0.1f, 0.25f, false, default, power, source);
        }
        else accepted = false;
        if (given != null) await Hook.AfterModifyingPowerAmountGiven(combat, given, power);
        await Hook.AfterModifyingPowerAmountReceived(combat, received, power);
        if (!accepted) return;
        await power.AfterApplied(applier, source);
        await Hook.AfterPowerAmountChanged(combat, context, power, 2m, applier, source);
    }

    // A transformation of an existing slot, rather than a new application/copy.
    // Keep the authored modifier count and original applier; only its snapshotted
    // percentage and (optionally) remaining duration change. Receiving hooks must
    // accept the transformation before either value changes, so Artifact/Narara
    // cannot block one half while leaving the other applied.
    internal static async Task DoubleExisting(PlayerChoiceContext context, PowerModel power,
        decimal percentOffset, int durationOffset, Creature applier, CardModel source,
        bool doubleDuration = true, bool silent = false)
    {
        Creature target = power.Owner;
        if (CombatManager.Instance.IsEnding || target == null || power.Amount <= 0 ||
            !target.CanReceivePowers || !target.Powers.Contains(power) ||
            target.CombatState is not { } combat || !HbrBattleStatsHelper.IsEnabled(target)) return;
        decimal requested = doubleDuration ? durationOffset : 1m;
        if (requested <= 0m || NararaNullificationHelper.BlocksPowerChange(
            power, target, requested, source)) return;
        power.AssertMutable();
        await Hook.BeforePowerAmountChanged(combat, power, requested, target, applier, source);
        decimal modified = requested;
        IEnumerable<AbstractModel> given = null;
        if (applier != null && combat.ContainsCreature(applier))
            modified = Hook.ModifyPowerAmountGiven(combat, power, applier, modified,
                target, source, out given);
        modified = Hook.ModifyPowerAmountReceived(combat, power, target, modified,
            applier, out IEnumerable<AbstractModel> received);
        modified = IntegralStatAmountHelper.Normalize(modified, power);
        int acceptedOffset = (int)Math.Clamp(decimal.Truncate(modified), 0m, int.MaxValue);
        bool accepted = acceptedOffset > 0 && target.CanReceivePowers &&
            target.Powers.Contains(power) && power.Amount > 0 && !CombatManager.Instance.IsEnding;
        CombatManager.Instance.History.PowerReceived(combat, power, accepted ? modified : 0m, applier);
        if (accepted)
        {
            HbrPercentDebuffHelper.Configure(power, DamageAmplificationLimits.AddSaturated(
                HbrPercentDebuffHelper.GetEffectPercent(power), percentOffset), HbrPercentDebuffHelper.GetModifierAmount(power));
            // Deliberately do not run the new-application two-turn initializer.
            int duration = doubleDuration
                ? (int)Math.Min(int.MaxValue, (long)power.Amount + acceptedOffset) : power.Amount;
            int beforeDuration = power.Amount;
            power.SetAmount(duration, silent);
            if (power.Amount == beforeDuration) target.InvokePowerModified(power, 0, silent);
        }
        if (given != null) await Hook.AfterModifyingPowerAmountGiven(combat, given, power);
        await Hook.AfterModifyingPowerAmountReceived(combat, received, power);
        if (!accepted) return;
        await Hook.AfterPowerAmountChanged(combat, context, power, modified, applier, source);
        if (power.ShouldRemoveDueToAmount()) await PowerCmd.Remove(power);
        if (CombatManager.Instance.IsInProgress && target.IsMonster && target.IsAlive)
        {
            NCreature node = NCombatRoom.Instance?.GetCreatureNode(target);
            if (node != null)
            {
                try { await node.UpdateIntent(combat.Allies); }
                catch (ObjectDisposedException error) { Log.Error(error.ToString()); }
            }
        }
        if (power.IsVisible && CombatManager.Instance.IsInProgress)
            await FragileAutoPlayAnimationHelper.WaitForPowerAnimation(
                0.1f, 0.25f, false, default, power, source);
    }

    internal static async Task<int> Copy(PlayerChoiceContext context, PowerModel original,
        Creature applier, CardModel source, bool silent)
    {
        PowerModel copy = ModelDb.GetById<PowerModel>(original.Id).ToMutable();
        await Apply(context, copy, original.Owner, 1m, applier, source, silent,
            HbrPercentDebuffHelper.GetEffectPercent(original), HbrPercentDebuffHelper.GetModifierAmount(original));
        return copy.Owner != null ? copy.Amount : original.Amount;
    }
}
