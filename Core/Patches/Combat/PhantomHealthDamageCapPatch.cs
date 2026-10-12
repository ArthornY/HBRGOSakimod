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
            !PhantomHealthDamageCapHelper.TryGetMaximum(__instance, out decimal maximum)) return true;
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

[HarmonyPatch(typeof(HardenedShellPower), nameof(HardenedShellPower.DisplayAmount), MethodType.Getter)]
internal static class PhantomHealthHardenedBadgePatch
{
    private static bool Prefix(HardenedShellPower __instance, ref int __result)
    {
        if (!PhantomHealthDamageCapHelper.TryGetMaximum(__instance, out decimal maximum)) return true;
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
        if (amountOverride == null && PhantomHealthDamageCapHelper.TryGetMaximum(__instance, out decimal maximum))
            description.Add("Amount", maximum);
    }
}
