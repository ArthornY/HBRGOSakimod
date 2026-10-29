using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

// All existing card/cannon/phantom applications cross this one boundary.
// Native receiving/giving hooks, including Artifact, precede slot selection.
internal static class HbrFragileApplication
{
    internal static FragilePower Replacement(Creature target, decimal percent)
    {
        FragilePower weakest = null;
        int count = 0;
        foreach (FragilePower existing in target.Powers.OfType<FragilePower>())
        {
            if (existing.Amount <= 0) continue;
            count++;
            if (weakest == null || existing.HbrEffectPercent < weakest.HbrEffectPercent ||
                existing.HbrEffectPercent == weakest.HbrEffectPercent && existing.Amount < weakest.Amount)
                weakest = existing;
        }
        return count < 2 ? null : percent >= weakest.HbrEffectPercent ? weakest : null;
    }

    internal static async Task Apply(PlayerChoiceContext context, FragilePower power,
        Creature target, decimal modifiers, Creature applier, CardModel source, bool silent,
        decimal? resolvedPercent = null, int copiedModifiers = 0)
    {
        if (CombatManager.Instance.IsEnding || modifiers <= 0m || !target.CanReceivePowers ||
            target.CombatState is not { } combat ||
            NararaNullificationHelper.BlocksPowerChange(power, target, modifiers, source)) return;
        bool fast = FragileAutoPlayAnimationHelper.IsSuppressed(source);
        silent |= fast;
        power.AssertMutable();
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
        decimal percent = resolvedPercent ?? HbrFragilePowerHelper.ResolvePercent(count,
            applier ?? source?.Owner?.Creature, target);
        if (accepted)
        {
            int occupied = target.Powers.OfType<FragilePower>().Count(p => p.Amount > 0);
            FragilePower replaced = Replacement(target, percent);
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
            power.ConfigureHbr(percent, copiedModifiers > 0 ? copiedModifiers : count);
            await power.BeforeApplied(target, 2m, applier, source);
            power.ApplyInternal(target, 2m, silent);
            CombatManager.Instance.History.PowerReceived(combat, power, 2m, applier);
            if (power.IsVisible && CombatManager.Instance.IsInProgress)
                await FragileAutoPlayAnimationHelper.WaitForPowerAnimation(
                    0.1f, 0.25f, false, default, power, source);
            if (target.Side == CombatSide.Player) power.SkipNextDurationTick = true;
        }
        else accepted = false;
        if (given != null) await Hook.AfterModifyingPowerAmountGiven(combat, given, power);
        await Hook.AfterModifyingPowerAmountReceived(combat, received, power);
        if (!accepted) return;
        await power.AfterApplied(applier, source);
        await Hook.AfterPowerAmountChanged(combat, context, power, 2m, applier, source);
    }

    internal static async Task<int> Copy(PlayerChoiceContext context, FragilePower original,
        Creature applier, CardModel source, bool silent)
    {
        var copy = (FragilePower)ModelDb.Power<FragilePower>().ToMutable();
        await Apply(context, copy, original.Owner, 1m, applier, source, silent,
            original.HbrEffectPercent, original.ModifierAmount);
        return copy.Owner != null ? copy.Amount : original.Amount;
    }
}
