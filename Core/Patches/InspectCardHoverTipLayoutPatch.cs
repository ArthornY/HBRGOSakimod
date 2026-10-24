using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NInspectCardScreen), "UpdateCardDisplay")]
internal static class InspectCardHoverTipLayoutPatch
{
    private static readonly AccessTools.FieldRef<NInspectCardScreen, NCard> InspectCard =
        AccessTools.FieldRefAccess<NInspectCardScreen, NCard>("_card");

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo native = AccessTools.Method(typeof(NHoverTipSet), nameof(NHoverTipSet.SetAlignment),
            new[] { typeof(Control), typeof(HoverTipAlignment) });
        MethodInfo replacement = AccessTools.Method(typeof(InspectCardHoverTipLayoutPatch), nameof(Align));
        List<CodeInstruction> result = instructions.ToList();
        int[] matches = result.Select((instruction, index) => (instruction, index))
            .Where(item => item.instruction.Calls(native)).Select(item => item.index).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException($"Expected one inspect tooltip alignment call; found {matches.Length}.");
        int index = matches[0];
        result[index] = new CodeInstruction(result[index]) { opcode = OpCodes.Call, operand = replacement };
        return result;
    }

    private static void Align(NHoverTipSet tips, Control anchor, HoverTipAlignment alignment)
    {
        tips.SetAlignment(anchor, alignment);
        if (tips == null) return;
        for (Node node = anchor; node != null; node = node.GetParent())
        {
            if (node is not NInspectCardScreen screen) continue;
            tips.AddChildSafely(new NInspectCardHoverTips
            {
                Inspect = screen, Card = InspectCard(screen), CardAnchor = anchor
            });
            break;
        }
    }
}
