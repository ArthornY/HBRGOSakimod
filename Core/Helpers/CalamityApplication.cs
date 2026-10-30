using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Helpers;

internal static class CalamityApplication
{
    // Both native Apply overloads and an existing-instance ModifyAmount enter
    // here. Test the incoming application before max(old,new): a weaker refresh
    // is still a debuff application that Artifact or immunity can reject.
    internal static async Task<int> Apply(PlayerChoiceContext context, HbrCalamityPower supplied,
        Creature target, decimal incoming, Creature applier, CardModel source, bool silent)
    {
        HbrCalamityPower existing = target?.GetPower<HbrCalamityPower>();
        HbrCalamityPower power = existing ?? supplied;
        if (CombatManager.Instance.IsEnding || incoming <= 0m || target?.CanReceivePowers != true ||
            target.CombatState is not { } combat ||
            NararaNullificationHelper.BlocksPowerChange(power, target, incoming, source))
            return existing?.Amount ?? 0;
        silent |= FragileAutoPlayAnimationHelper.IsSuppressed(source);
        power.AssertMutable();
        if (existing == null) power.Applier = applier;
        await Hook.BeforePowerAmountChanged(combat, power, incoming, target, applier, source);
        decimal modified = incoming;
        IEnumerable<AbstractModel> given = null;
        if (applier != null && combat.ContainsCreature(applier))
            modified = Hook.ModifyPowerAmountGiven(combat, power, applier, modified, target, source, out given);
        modified = Hook.ModifyPowerAmountReceived(combat, power, target, modified, applier,
            out IEnumerable<AbstractModel> received);
        if (existing == null && combat.Players.Count > 1 &&
            (target.IsPrimaryEnemy || target.IsSecondaryEnemy) && power.ShouldScaleInMultiplayer)
            modified = power.GetScaledAmountForMultiplayer(combat, applier, modified, target, source);
        int effective = (int)Math.Clamp(decimal.Truncate(modified), 0m, BigCombatNumbers.NativeStatCap);
        bool accepted = effective > 0 && target.CanReceivePowers && !CombatManager.Instance.IsEnding;
        int before = existing?.Amount ?? 0;
        int next = Math.Max(before, effective);
        if (accepted && existing == null)
        {
            await power.BeforeApplied(target, next, applier, source);
            accepted = target.CanReceivePowers && !CombatManager.Instance.IsEnding;
        }
        if (accepted)
        {
            power.RefreshDuration();
            if (existing == null) power.ApplyInternal(target, next, silent);
            else
            {
                power.SetAmount(next, silent);
                if (next == before) target.InvokePowerModified(power, 0, silent);
            }
            CombatManager.Instance.History.PowerReceived(combat, power, next - before, applier);
        }
        if (given != null) await Hook.AfterModifyingPowerAmountGiven(combat, given, power);
        await Hook.AfterModifyingPowerAmountReceived(combat, received, power);
        if (!accepted) return existing?.Amount ?? 0;
        if (existing == null) await power.AfterApplied(applier, source);
        await Hook.AfterPowerAmountChanged(combat, context, power, next - before, applier, source);
        // Reapplying a stronger reduction changes this enemy's next action;
        // preserve the native ModifyAmount intent refresh for the same entity.
        if (existing != null && CombatManager.Instance.IsInProgress && target.IsMonster && target.IsAlive)
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(target);
            if (node != null)
            {
                try { await node.UpdateIntent(combat.Allies); }
                catch (ObjectDisposedException error) { Log.Error(error.ToString()); }
            }
        }
        if (power.IsVisible && CombatManager.Instance.IsInProgress)
            await FragileAutoPlayAnimationHelper.WaitForPowerAnimation(
                0.1f, 0.25f, false, default, power, source);
        return power.Amount;
    }
}
