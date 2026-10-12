using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using Godot;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.ShowHoverTips))]
internal static class PhantomHoverTipPlacementPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo create = AccessTools.DeclaredMethod(typeof(NHoverTipSet), nameof(NHoverTipSet.CreateAndShow),
            new[] { typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment) });
        List<CodeInstruction> result = instructions.ToList();
        int[] matches = result.Select((instruction, index) => (instruction, index))
            .Where(item => item.instruction.Calls(create)).Select(item => item.index).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"Expected one creature hover-tip creation; found {matches.Length}.");
        // Keep the native hand/targeting guards, tip list and owner lifecycle.
        // Observe its returned set instead of creating a second tooltip.
        result.InsertRange(matches[0] + 1, new[]
        {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(
                typeof(PhantomHoverTipLayoutHelper), nameof(PhantomHoverTipLayoutHelper.Track)))
        });
        return result;
    }
}
