using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class HbrEffectApplication
{
    internal static Task ApplyMindsEye(PlayerChoiceContext context, Creature target,
        Creature applier, CardModel source = null) =>
        Apply<MindsEyePower>(context, target, MindsEyePower.BasePercent, applier, source);

    internal static Task Apply<T>(PlayerChoiceContext context, Creature target, decimal basis,
        Creature applier, CardModel source = null, int? remaining = null,
        HbrEffectProfile? profile = null, bool silent = false, decimal? potencyOverride = null) where T : HbrEffectPower =>
        Apply(context, (T)ModelDb.Power<T>().ToMutable(), target, basis, applier, source,
            remaining, profile, silent, potencyOverride);

    internal static async Task Apply(PlayerChoiceContext context, HbrEffectPower power,
        Creature target, decimal basis, Creature applier, CardModel source, int? remaining = null,
        HbrEffectProfile? profile = null, bool silent = false, decimal? potencyOverride = null)
    {
        if (ThirtyOneBEnemyActionHelper.TryDeferPower(power, target, basis, applier,
            () => Apply(context, power, target, basis, applier, source, remaining, profile, silent, potencyOverride))) return;
        if (basis <= 0m || target?.CanReceivePowers != true || !power.CanApplyTo(target) || target.CombatState is not { } combat ||
            ThirtyOneBEnemyActionHelper.BlocksPower(power, target, basis, applier) ||
            CombatManager.Instance.IsEnding || NararaNullificationHelper.BlocksPowerChange(power, target, basis, source)) return;
        if (power.Owner != null) power = (HbrEffectPower)ModelDb.GetById<PowerModel>(power.Id).ToMutable();
        power.Applier = applier;
        await Hook.BeforePowerAmountChanged(combat, power, basis, target, applier, source);
        decimal modified = basis;
        IEnumerable<AbstractModel> given = null;
        if (applier != null && combat.ContainsCreature(applier))
            modified = Hook.ModifyPowerAmountGiven(combat, power, applier, modified, target, source, out given);
        modified = Hook.ModifyPowerAmountReceived(combat, power, target, modified, applier, out IEnumerable<AbstractModel> received);
        if (combat.Players.Count > 1 && (target.IsPrimaryEnemy || target.IsSecondaryEnemy) && power.ShouldScaleInMultiplayer)
            modified = power.GetScaledAmountForMultiplayer(combat, applier, modified, target, source);
        HbrEffectProfile selected = profile ?? (power is HbrInstancedEnemyStatusPower status
            ? status.ApplicationProfile(target) : HbrEffectProfileResolver.Resolve(power, source));
        int count = remaining ?? selected.DefaultRemaining;
        if (profile == null && power is (HbrImprisonPower or HbrActionLockPower) && !HbrBattleStatsHelper.IsEnabled(target))
            count = selected.DefaultRemaining;
        bool accepted = modified > 0m && count > 0 && target.CanReceivePowers;
        HbrDebuffApplicationScope.Claim bonus = accepted && power.Type == MegaCrit.Sts2.Core.Entities.Powers.PowerType.Debuff
            ? HbrDebuffApplicationScope.GetClaim(applier ?? source?.Owner?.Creature, power.GetType(), target, source) : null;
        if (bonus != null) modified *= bonus.Multiplier;
        decimal potency = potencyOverride ?? power.ResolvePotency(modified, applier ?? source?.Owner?.Creature, target);
        if (accepted)
        {
            HbrEffectSlotDecision slot = HbrEffectRules.Select(power, target, potency, selected);
            accepted = slot.Accepted;
            if (slot.Replaced != null) await MegaCrit.Sts2.Core.Commands.PowerCmd.Remove(slot.Replaced);
        }
        if (accepted && !CombatManager.Instance.IsEnding)
        {
            power.ConfigureEffect(potency, selected);
            await power.BeforeApplied(target, count, applier, source);
            accepted = target.CanReceivePowers && !CombatManager.Instance.IsEnding;
            if (accepted)
            {
                power.ApplyInternal(target, count, silent);
                CombatManager.Instance.History.PowerReceived(combat, power, count, applier);
            }
        }
        else accepted = false;
        if (given != null) await Hook.AfterModifyingPowerAmountGiven(combat, given, power);
        await Hook.AfterModifyingPowerAmountReceived(combat, received, power);
        if (!accepted)
        { if (bonus is { Batched: false }) bonus.Release(); return; }
        if (bonus != null) await bonus.Complete();
        await power.AfterApplied(applier, source);
        await Hook.AfterPowerAmountChanged(combat, context, power, count, applier, source);
        await CombatPreviewRefreshHelper.RefreshCreatureIntent(target);
    }
}
