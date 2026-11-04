using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(HardToKillPower), nameof(HardToKillPower.ModifyDamageCap))]
internal static class PhantomHealthHardToKillCapPatch
{
    private static bool Prefix(HardToKillPower __instance, Creature target, ref decimal __result)
    {
        if (target != __instance.Owner ||
            !HbrDamageCapHelper.TryGetMaximum(__instance, out decimal maximum)) return true;
        __result = maximum;
        return false;
    }
}

[HarmonyPatch]
internal static class PhantomHealthHardenedCapPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(HardenedShellPower), nameof(HardenedShellPower.ModifyHpLostBeforeOstyLate));
        yield return AccessTools.Method(typeof(HardenedShellPower), nameof(HardenedShellPower.AfterDamageReceived));
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo amount = AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Amount));
        MethodInfo conversion = AccessTools.Method(typeof(decimal), "op_Implicit", new[] { typeof(int) });
        MethodInfo replacement = AccessTools.Method(typeof(PhantomHealthDamageCapHelper), nameof(PhantomHealthDamageCapHelper.GetHardenedMaximum));
        List<CodeInstruction> result = new(instructions);
        int matches = 0;
        for (int i = 0; i + 1 < result.Count; i++)
        {
            if (!result[i].Calls(amount) || !result[i + 1].Calls(conversion)) continue;
            result[i].opcode = OpCodes.Call;
            result[i].operand = replacement;
            result[i + 1].opcode = OpCodes.Nop;
            result[i + 1].operand = null;
            matches++;
        }
        if (matches != 1) throw new InvalidOperationException("Expected one native HardenedShell decimal cap read.");
        return result;
    }
}

[HarmonyPatch(typeof(HardenedShellPower), nameof(HardenedShellPower.ModifyHpLostBeforeOstyLate))]
internal static class HbrHardenedHpLossFloorPatch
{
    private static void Postfix(HardenedShellPower __instance, Creature target, ref decimal __result)
    {
        // A reduced maximum HP can put the new percentage budget below HP
        // already lost this turn. Native fixed caps assume this cannot happen.
        if (__result >= 0m || !HbrDamageCapHelper.TryGetMaximum(__instance, out decimal maximum) ||
            target != __instance.Owner ||
            PhantomHealthDamageCapHelper.GetReceivedThisTurn(__instance) <= maximum) return;
        __result = 0m;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.SetMaxHpInternal))]
internal static class HbrHardenedMaximumHpDisplayPatch
{
    private static readonly Action<PowerModel> RefreshBadge = AccessTools.MethodDelegate<Action<PowerModel>>(
        AccessTools.Method(typeof(PowerModel), "InvokeDisplayAmountChanged"));

    private static void Postfix(Creature __instance)
    {
        HardToKillPower hardToKill = __instance.GetPower<HardToKillPower>();
        if (HbrDamageCapHelper.TryGetMaximum(hardToKill, out _)) RefreshBadge(hardToKill);
        HardenedShellPower power = __instance.GetPower<HardenedShellPower>();
        if (power == null || !HbrDamageCapHelper.TryGetMaximum(power, out decimal maximum)) return;
        // Keep the native turn ledger. Only refresh the shell's exhausted-HP
        // display after maximum HP changes the available percentage budget.
        if (__instance.HpDisplay is HpDisplay.Normal or HpDisplay.InfiniteWithNumbers)
        {
            if (PhantomHealthDamageCapHelper.GetReceivedThisTurn(power) >= maximum)
                __instance.HpDisplay = HpDisplay.InfiniteWithNumbers;
            else if (__instance.HpDisplay == HpDisplay.InfiniteWithNumbers)
                __instance.HpDisplay = HpDisplay.Normal;
        }
        // Native power badges refresh on this event, not every frame or when
        // maximum HP changes. Do not rewrite the raw percentage to wake them.
        RefreshBadge(power);
    }
}

[HarmonyPatch(typeof(HardenedShellPower), nameof(HardenedShellPower.DisplayAmount), MethodType.Getter)]
internal static class PhantomHealthHardenedBadgePatch
{
    private static bool Prefix(HardenedShellPower __instance, ref int __result)
    {
        if (!HbrDamageCapHelper.TryGetMaximum(__instance, out decimal maximum)) return true;
        __result = (int)Math.Clamp(maximum - PhantomHealthDamageCapHelper.GetReceivedThisTurn(__instance),
            0m, BigCombatNumbers.NativeStatCap);
        return false;
    }
}

[HarmonyPatch(typeof(PowerModel), "AddDumbVariablesToDescription")]
internal static class PhantomHealthDamageCapDescriptionPatch
{
    private static void Postfix(PowerModel __instance, LocString description, int? amountOverride)
    {
        if (HbrDamageCapHelper.TryGetNativePercent(__instance, out int percent))
        {
            int displayedPercent = amountOverride ?? percent;
            description.Add("CapPercent", displayedPercent);
            description.Add("Amount", HbrDamageCapHelper.GetPercentMaximum(__instance.Owner, displayedPercent));
        }
        else if (amountOverride == null && HbrDamageCapHelper.TryGetMaximum(__instance, out decimal maximum))
            description.Add("Amount", maximum);
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.DisplayAmount), MethodType.Getter)]
internal static class HbrHardToKillBadgePatch
{
    private static void Postfix(PowerModel __instance, ref int __result)
    {
        if (__instance is HardToKillPower && HbrDamageCapHelper.TryGetMaximum(__instance, out decimal maximum))
            __result = (int)Math.Clamp(maximum, 0m, BigCombatNumbers.NativeStatCap);
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.Description), MethodType.Getter)]
internal static class HbrDamageCapDescriptionPatch
{
    private static void Postfix(PowerModel __instance, ref LocString __result)
    {
        if (!HbrDamageCapHelper.TryGetMaximum(__instance, out decimal maximum)) return;
        bool nativePercent = HbrDamageCapHelper.TryGetNativePercent(__instance, out int percent);
        string prefix = nativePercent ? "HBR_PERCENT_" : "HBR_FIXED_";
        LocString text = new("powers", prefix + (__instance is HardToKillPower
            ? "HARD_TO_KILL.description" : "HARDENED_SHELL.description"));
        text.AddVariablesFrom(__result);
        text.Add("Amount", maximum);
        if (nativePercent) text.Add("CapPercent", percent);
        __result = text;
    }
}
