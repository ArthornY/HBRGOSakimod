using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch]
internal static class NararaShibaEventCombatCompatibilityPatch
{
    private static readonly MethodInfo CloneEncounter = AccessTools.Method(
        typeof(EncounterModel), nameof(EncounterModel.ToMutable), Type.EmptyTypes);

    private static bool Prepare() => typeof(EventModel).Assembly.GetType(
        "MegaCrit.Sts2.Core.Multiplayer.Game.EventCombatSynchronizer") != null;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Stable accepts the event's mutable encounter directly. Beta first waits
        // for every player, then clones the encounter inside its new synchronizer.
        Type synchronizer = typeof(EventModel).Assembly.GetType(
            "MegaCrit.Sts2.Core.Multiplayer.Game.EventCombatSynchronizer");
        if (synchronizer == null) yield break;

        MethodInfo enterCombat = AccessTools.DeclaredMethod(synchronizer, "EnterCombat", Type.EmptyTypes);
        if (enterCombat == null || enterCombat.IsStatic || enterCombat.ReturnType != typeof(void))
            throw new MissingMethodException(synchronizer.FullName, "void EnterCombat()");

        yield return enterCombat;
    }

    private static EncounterModel GetMutableEncounter(EncounterModel encounter) =>
        // Story encounters carry their continuation on this exact instance; the
        // legacy door event also supplies a mutable Narara without a StoryState.
        encounter is NararaShibaBoss { IsMutable: true }
            ? encounter
            : encounter.ToMutable();

    private static bool IsSameEncounter(EncounterModel first, EncounterModel other)
    {
        if (ReferenceEquals(first, other)) return true;
        // Each player's shared-event instance prepares its own mutable encounter.
        // Preserve the synchronizer's mismatch guard, including continuation data.
        return first is NararaShibaBoss { IsMutable: true } firstNarara
            && other is NararaShibaBoss { IsMutable: true } otherNarara
            && firstNarara.Id == otherNarara.Id
            && firstNarara.InitialFloor == otherNarara.InitialFloor
            && string.Equals(firstNarara.StoryState?.Serialize(), otherNarara.StoryState?.Serialize(),
                StringComparison.Ordinal);
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> result = instructions.ToList();
        int[] matches = result.Select((instruction, index) => (instruction, index))
            .Where(item => item.instruction.Calls(CloneEncounter))
            .Select(item => item.index)
            .ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(
                $"Expected one EncounterModel.ToMutable call in EventCombatSynchronizer.EnterCombat; found {matches.Length}.");

        int[] comparisons = Enumerable.Range(2, result.Count - 2)
            .Where(index => result[index].opcode == OpCodes.Beq || result[index].opcode == OpCodes.Beq_S)
            .Where(index => result[index - 1].opcode == OpCodes.Ldloc_0
                && result[index - 2].opcode == OpCodes.Ldfld
                && result[index - 2].operand is FieldInfo
                {
                    Name: "canonicalEncounter",
                    FieldType: var fieldType
                } && fieldType == typeof(EncounterModel))
            .ToArray();
        if (comparisons.Length != 1)
            throw new InvalidOperationException(
                $"Expected one encounter identity comparison in EventCombatSynchronizer.EnterCombat; found {comparisons.Length}.");

        int index = matches[0];
        result[index] = new CodeInstruction(result[index])
        {
            opcode = OpCodes.Call,
            operand = AccessTools.Method(typeof(NararaShibaEventCombatCompatibilityPatch), nameof(GetMutableEncounter))
        };

        int comparison = comparisons[0];
        object equalTarget = result[comparison].operand;
        result[comparison] = new CodeInstruction(result[comparison])
        {
            opcode = OpCodes.Call,
            operand = AccessTools.Method(typeof(NararaShibaEventCombatCompatibilityPatch), nameof(IsSameEncounter))
        };
        result.Insert(comparison + 1, new CodeInstruction(OpCodes.Brtrue, equalTarget));
        return result;
    }
}
