using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Native effects keep their entire original application/hook chain. Reserve
// synchronously, then settle the bonus after the awaited native mutation.
internal static class ThirtyOneAYukiNativeDebuff
{
    internal sealed record Application(HbrDebuffApplicationScope.Claim Claim, PowerModel Power, int Before);
    internal static Application Begin(PowerModel power, Creature target,
        Creature applier, CardModel source, ref decimal amount) 
    {
        if (amount <= 0m || target?.CanReceivePowers != true || power.Type != PowerType.Debuff ||
            power is HbrEffectPower || HbrPercentDebuffHelper.Supports(power)) return null;
        var claim = HbrDebuffApplicationScope.GetClaim(applier ?? source?.Owner?.Creature, power.GetType(), target, source);
        if (claim.Multiplier == 1m) return null;
        amount *= claim.Multiplier;
        return new(claim, power, power.Amount);
    }
    internal static async Task Finish(Task original, Application state)
    {
        try
        {
            await original;
            if (state.Power.Amount > state.Before) await state.Claim.Complete();
            else if (!state.Claim.Batched) state.Claim.Release();
        }
        catch (Exception error) { await SettleFailure(state, error); throw; }
    }
    internal static async Task<int> Finish(Task<int> original, Application state)
    {
        try
        {
            int result = await original;
            if (result > 0) await state.Claim.Complete();
            else if (!state.Claim.Batched) state.Claim.Release();
            return result;
        }
        catch (Exception error) { await SettleFailure(state, error); throw; }
    }

    // Visual waits/callbacks can fail after the native mutation. A bonus that
    // already changed a target must still be paid, without masking that failure.
    private static async Task SettleFailure(Application state, Exception original)
    {
        try
        {
            if (state.Power.Amount > state.Before) await state.Claim.Complete();
            else state.Claim.Release();
        }
        catch (Exception cleanup) { original.Data["HBRGOSakiMod.YukiCleanupFailure"] = cleanup; }
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply), new[] { typeof(PlayerChoiceContext),
    typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool) })]
internal static class ThirtyOneAYukiNativeApplyPatch
{
    private static void Prefix(PowerModel power, Creature target, Creature applier, CardModel cardSource,
        ref decimal amount, out ThirtyOneAYukiNativeDebuff.Application __state) =>
        __state = PowerCmd.FindExistingInstanceForStacking(power, target, applier) == null
            ? ThirtyOneAYukiNativeDebuff.Begin(power, target, applier, cardSource, ref amount) : null;
    private static void Postfix(ThirtyOneAYukiNativeDebuff.Application __state, ref Task __result)
    { if (__state != null) __result = ThirtyOneAYukiNativeDebuff.Finish(__result, __state); }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount))]
internal static class ThirtyOneAYukiNativeModifyPatch
{
    private static void Prefix(PowerModel power, Creature applier, CardModel cardSource,
        ref decimal offset, out ThirtyOneAYukiNativeDebuff.Application __state) =>
        __state = ThirtyOneAYukiNativeDebuff.Begin(power, power.Owner, applier, cardSource, ref offset);
    private static void Postfix(ThirtyOneAYukiNativeDebuff.Application __state, ref Task<int> __result)
    { if (__state != null) __result = ThirtyOneAYukiNativeDebuff.Finish(__result, __state); }
}
