using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NetFullCombatState), nameof(NetFullCombatState.FromRun))]
internal static class LargeCombatSnapshotCapturePatch
{
    private static void Prefix(IRunState runState, out LargeCombatSnapshotHelper.CaptureScope __state) =>
        __state = LargeCombatSnapshotHelper.BeginCapture(runState);

    private static void Postfix(NetFullCombatState __result, LargeCombatSnapshotHelper.CaptureScope __state) =>
        LargeCombatSnapshotHelper.FinishCapture(__state, __result);

    private static void Finalizer(LargeCombatSnapshotHelper.CaptureScope __state) =>
        LargeCombatSnapshotHelper.EndCapture(__state);

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo[] getters =
        {
            AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.CurrentHp)),
            AccessTools.PropertyGetter(typeof(Creature), nameof(Creature.MaxHp)),
            AccessTools.PropertyGetter(typeof(PowerModel), nameof(PowerModel.Amount))
        };
        MethodInfo[] replacements =
        {
            AccessTools.DeclaredMethod(typeof(LargeCombatSnapshotHelper), nameof(LargeCombatSnapshotHelper.CaptureCurrentHp)),
            AccessTools.DeclaredMethod(typeof(LargeCombatSnapshotHelper), nameof(LargeCombatSnapshotHelper.CaptureMaxHp)),
            AccessTools.DeclaredMethod(typeof(LargeCombatSnapshotHelper), nameof(LargeCombatSnapshotHelper.CapturePowerAmount))
        };
        int[] counts = new int[3];
        List<CodeInstruction> result = instructions.ToList();
        for (int index = 0; index < result.Count; index++)
        {
            for (int getter = 0; getter < getters.Length; getter++)
            {
                if (!result[index].Calls(getters[getter])) continue;
                counts[getter]++;
                // Static wrappers have identical stack inputs/outputs. Preserve
                // labels and exception blocks on the original instruction.
                result[index] = new CodeInstruction(result[index])
                {
                    opcode = OpCodes.Call,
                    operand = replacements[getter]
                };
                break;
            }
        }
        if (counts.Any(count => count != 1))
            throw new InvalidOperationException("Expected one current HP, maximum HP and power amount capture boundary in NetFullCombatState.FromRun.");
        return result;
    }
}

// This extends the complete snapshot, after every native field. Matching mod
// versions are required on both peers and for replay reading. The native
// checksum hashes these serialized bytes, including the frozen exact string.
[HarmonyPatch(typeof(NetFullCombatState), nameof(NetFullCombatState.Serialize))]
internal static class LargeCombatSnapshotSerializePatch
{
    private static void Postfix(NetFullCombatState __instance, PacketWriter writer) =>
        LargeCombatSnapshotHelper.Serialize(__instance, writer);
}

[HarmonyPatch(typeof(NetFullCombatState), nameof(NetFullCombatState.Deserialize))]
internal static class LargeCombatSnapshotDeserializePatch
{
    private static void Postfix(NetFullCombatState __instance, PacketReader reader) =>
        LargeCombatSnapshotHelper.Deserialize(__instance, reader);
}

[HarmonyPatch(typeof(NetFullCombatState), nameof(NetFullCombatState.Anonymized))]
internal static class LargeCombatSnapshotAnonymizePatch
{
    private static void Postfix(NetFullCombatState __instance, NetFullCombatState __result) =>
        LargeCombatSnapshotHelper.Copy(__instance, __result);
}

[HarmonyPatch(typeof(NetFullCombatState), nameof(NetFullCombatState.ToString))]
internal static class LargeCombatSnapshotDescriptionPatch
{
    private static void Postfix(NetFullCombatState __instance, ref string __result) =>
        __result += "\n" + LargeCombatSnapshotHelper.Describe(__instance);
}
