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
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets) await ApplyAttackDown(context, target, amount, applier, source, silent);
    }

    internal static async Task ApplyDefenseDown(PlayerChoiceContext context, IEnumerable<Creature> targets,
        decimal amount, Creature applier, CardModel source = null, bool silent = false)
    {
        using var batch = HbrDebuffApplicationScope.BeginBatch(applier);
        foreach (Creature target in targets) await ApplyDefenseDown(context, target, amount, applier, source, silent);
    }

    internal static PowerModel Replacement(PowerModel incoming, Creature target, decimal percent)
    {
        return HbrEffectRules.Select(incoming, target, percent,
            HbrEffectProfileResolver.Resolve(incoming)).Replaced;
    }

    internal static Task Apply(PlayerChoiceContext context, PowerModel power,
        Creature target, decimal modifiers, Creature applier, CardModel source, bool silent,
        decimal? resolvedPercent = null, int copiedModifiers = 0) =>
        ApplyCore(context, power, target, modifiers, applier, source, silent,
            resolvedPercent, copiedModifiers, allowExtraInstance: false);

    // Explicit HBR skill strength. Amount remains one independent application;
    // Artifact, native giving/receiving hooks and the common slot policy remain.
    internal static Task ApplyBasis<T>(PlayerChoiceContext context, Creature target,
        decimal basis, Creature applier, CardModel source = null,
        HbrEffectProfile? profile = null, bool silent = false) where T : PowerModel
    {
        PowerModel power = ModelDb.Power<T>().ToMutable();
        if (!HbrPercentDebuffHelper.Supports(power))
            throw new ArgumentException("An HBR percentage debuff is required.", nameof(T));
        return basis <= 0m ? Task.CompletedTask : ApplyCore(context, power, target, 1m,
            applier, source, silent, null, 1, false, profile, basis);
    }

    // Resource break is a fixed penalty, not a new skill roll. Keep the native
    // receiving hooks and ordinary slot policy, but do not scale its potency.
    internal static Task ApplyDpBreakAttackDown(PlayerChoiceContext context, Creature target)
    {
        HbrAttackDownPower power = (HbrAttackDownPower)ModelDb.Power<HbrAttackDownPower>().ToMutable();
        if (target.CombatState?.CurrentSide == target.Side) power.WaitForNextOwnerTurn();
        return ApplyCore(context, power, target, 1m, null, null, false, 30m, 1, false,
            new(HbrEffectLimit.Default, HbrEffectExit.OwnerTurnEnd, 1));
    }

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
            percent, modifiers, allowExtraInstance: true,
            profileOverride: ((IHbrEffectInstance)original).EffectProfile with { Limit = HbrEffectLimit.None });
    }

    private static async Task ApplyCore(PlayerChoiceContext context, PowerModel power,
        Creature target, decimal modifiers, Creature applier, CardModel source, bool silent,
        decimal? resolvedPercent, int copiedModifiers, bool allowExtraInstance,
        HbrEffectProfile? profileOverride = null, decimal? basisOverride = null)
    {
        if (ThirtyOneBEnemyActionHelper.TryDeferPower(power, target, modifiers, applier,
            () => ApplyCore(context, power, target, modifiers, applier, source, silent, resolvedPercent,
                copiedModifiers, allowExtraInstance, profileOverride, basisOverride))) return;
        if (CombatManager.Instance.IsEnding || modifiers <= 0m || !target.CanReceivePowers ||
            target.CombatState is not { } combat ||
            ThirtyOneBEnemyActionHelper.BlocksPower(power, target, modifiers, applier) ||
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
        Creature caster = applier ?? source?.Owner?.Creature;
        HbrDebuffApplicationScope.Claim bonus = accepted && resolvedPercent == null
            ? HbrDebuffApplicationScope.GetClaim(caster, power.GetType(), target, source) : null;
        decimal boost = bonus?.Multiplier ?? 1m;
        decimal percent = resolvedPercent ?? HbrPercentDebuffHelper.ResolveBasis(power,
            (basisOverride ?? HbrEnemyDebuffDurationHelper.BasisForTableMonster(power, caster) ??
                (power is FragilePower ? HbrFragilePowerHelper.BasePercent(count)
                : HbrPercentDebuffHelper.BasePercent(count))) * boost, caster, target);
        HbrEffectProfile profile = profileOverride ?? HbrEnemyDebuffDurationHelper.ForTableMonster(power, caster) ??
            HbrEffectProfileResolver.Resolve(power, source);
        if (allowExtraInstance) profile = profile with { Limit = HbrEffectLimit.None };
        if (accepted && !allowExtraInstance)
        {
            HbrEffectSlotDecision slot = HbrEffectRules.Select(power, target, percent, profile);
            PowerModel replaced = slot.Replaced;
            accepted = slot.Accepted;
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
            HbrPercentDebuffHelper.ConfigureProfile(power, profile);
            await power.BeforeApplied(target, profile.DefaultRemaining, applier, source);
            power.ApplyInternal(target, profile.DefaultRemaining, silent);
            CombatManager.Instance.History.PowerReceived(combat, power, profile.DefaultRemaining, applier);
            if (power.IsVisible && CombatManager.Instance.IsInProgress)
                await FragileAutoPlayAnimationHelper.WaitForPowerAnimation(
                    0.1f, 0.25f, false, default, power, source);
        }
        else accepted = false;
        if (given != null) await Hook.AfterModifyingPowerAmountGiven(combat, given, power);
        await Hook.AfterModifyingPowerAmountReceived(combat, received, power);
        if (!accepted)
        { if (bonus is { Batched: false }) bonus.Release(); return; }
        if (bonus != null) await bonus.Complete();
        await power.AfterApplied(applier, source);
        await Hook.AfterPowerAmountChanged(combat, context, power, profile.DefaultRemaining, applier, source);
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
        await ApplyCore(context, copy, original.Owner, 1m, applier, source, silent,
            HbrPercentDebuffHelper.GetEffectPercent(original), HbrPercentDebuffHelper.GetModifierAmount(original),
            allowExtraInstance: false, profileOverride: ((IHbrEffectInstance)original).EffectProfile);
        return copy.Owner != null ? copy.Amount : original.Amount;
    }
}
