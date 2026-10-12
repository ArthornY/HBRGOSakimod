using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(Creature), nameof(Creature.Block), MethodType.Getter)]
internal static class DodgeVisibleBlockPatch
{
    private static void Postfix(Creature __instance, ref int __result)
    {
        if (!DodgeCombatHelper.ReadsNativeBlock(__instance) && DodgeCombatHelper.CountsAsBlock(__instance))
            __result = (int)Math.Min(int.MaxValue, (long)__result + DodgeCombatHelper.GetAmount(__instance));
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.Block), MethodType.Setter)]
internal static class DodgeVisibleBlockChangedPatch
{
    private static void Prefix(Creature __instance, out (int Raw, int Effective) __state) =>
        __state = (DodgeCombatHelper.GetNativeBlock(__instance),
            DodgeCombatHelper.GetEffectiveBlock(__instance));
    private static void Postfix(Creature __instance, (int Raw, int Effective) __state)
    {
        // Gain/loss/damage scopes finish their own notification after raw
        // reads end. Direct zeroing (including native turn clear) ends here.
        if (!DodgeCombatHelper.ReadsNativeBlock(__instance) &&
            DodgeCombatHelper.CountsAsBlock(__instance) &&
            DodgeCombatHelper.GetNativeBlock(__instance) != __state.Raw)
            DodgeCombatHelper.NotifyBlockChanged(__instance, __state.Effective);
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.GainBlockInternal))]
internal static class DodgeBlockGainPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Creature __instance, decimal amount,
        out DodgeCombatHelper.RawBlockScope __state)
    {
        __state = null;
        if (DodgeCombatHelper.ConvertBlockGain(__instance, amount)) return false;
        __state = DodgeCombatHelper.BeginNativeBlock(__instance);
        return true;
    }
    private static void Postfix(DodgeCombatHelper.RawBlockScope __state) => __state?.Dispose();
    private static void Finalizer(DodgeCombatHelper.RawBlockScope __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(Creature), nameof(Creature.LoseBlockInternal))]
internal static class DodgeBlockLossPatch
{
    private static void Prefix(Creature __instance, out DodgeCombatHelper.RawBlockScope __state) =>
        __state = DodgeCombatHelper.BeginNativeBlock(__instance);
    private static void Postfix(DodgeCombatHelper.RawBlockScope __state) => __state?.Dispose();
    private static void Finalizer(DodgeCombatHelper.RawBlockScope __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
internal static class DodgeBlockDamagePatch
{
    internal sealed class State
    {
        internal DodgeCombatHelper.RawBlockScope Raw;
    }
    // Existing phantom/red-crimson routing runs first. Its nested physical
    // receiver gets its own call here; the player's displayed Block is not spent.
    [HarmonyPriority(Priority.Last)]
    private static bool Prefix(Creature __instance, decimal amount, ref decimal __result, out State __state)
    {
        __state = new State();
        if (DodgeCombatHelper.TryDodge(__instance, amount))
        {
            __result = amount;
            return false;
        }
        __state.Raw = DodgeCombatHelper.BeginNativeBlock(__instance);
        return true;
    }

    [HarmonyPriority(Priority.First)]
    private static void Postfix(Creature __instance, decimal amount, ref decimal __result, State __state)
    {
        __state?.Raw?.Dispose();
        // Explicit HP-only commands were intentionally completed by the earlier
        // DP-bypass prefix. They are still damage, and can spend actual Dodge.
        if (__state == null && HpOnlyDamageContext.BypassesDpForBlockReceiver(__instance))
        {
            Creature receiver = HpOnlyDamageContext.GetCurrentHpDamageTarget();
            if (receiver?.GetPower<DamageTransferPower>()?.GetLivingRedCrimson() is { } redirected)
                receiver = redirected;
            if (DodgeCombatHelper.TryDodge(receiver, amount)) __result = amount;
        }
    }
    private static void Finalizer(State __state) => __state?.Raw?.Dispose();
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.SetAmount))]
internal static class DodgePowerAmountPatch
{
    private static void Prefix(PowerModel __instance, ref int amount, out (Creature Owner, int Block) __state)
    {
        __state = default;
        if (__instance is not DodgePower || !__instance.IsMutable) return;
        Creature owner = __instance.Owner;
        // Unbound snapshots may retain80. ApplyInternal binds the real owner
        // before SetAmount, and that path uses the live owner cap.
        amount = Math.Clamp(amount, 0, owner == null
            ? DodgePower.ExpandedMaximumAmount : DodgeCombatHelper.GetMaximum(owner));
        __state = (owner, DodgeCombatHelper.GetEffectiveBlock(owner));
    }
    private static void Postfix(PowerModel __instance, (Creature Owner, int Block) __state)
    {
        if (__state.Owner?.GetPower<DodgePower>() == __instance)
            DodgeCombatHelper.NotifyBlockChanged(__state.Owner, __state.Block);
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.ApplyInternal))]
internal static class DodgePowerApplyPatch
{
    private static void Prefix(PowerModel __instance, Creature owner, ref decimal amount, out int __state)
    {
        __state = __instance is DodgePower ? DodgeCombatHelper.GetEffectiveBlock(owner) : 0;
        if (__instance is DodgePower && __instance.IsMutable)
            amount = Math.Clamp(amount, 0m, DodgeCombatHelper.GetMaximum(owner));
    }
    private static void Postfix(PowerModel __instance, Creature owner, int __state)
    {
        if (__instance is DodgePower) DodgeCombatHelper.NotifyBlockChanged(owner, __state);
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.RemoveInternal))]
internal static class DodgePowerRemovePatch
{
    private static void Prefix(PowerModel __instance, out (Creature Owner, int Block) __state) =>
        __state = __instance is DodgePower && __instance.IsMutable
            ? (__instance.Owner, DodgeCombatHelper.GetEffectiveBlock(__instance.Owner)) : default;
    private static void Postfix((Creature Owner, int Block) __state)
    {
        if (__state.Owner != null) DodgeCombatHelper.NotifyBlockChanged(__state.Owner, __state.Block);
    }
}
