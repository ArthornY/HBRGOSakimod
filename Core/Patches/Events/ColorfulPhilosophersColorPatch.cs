using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;

namespace HBRGOSakiMod.Core.Patches.Events;

[HarmonyPatch(typeof(ColorfulPhilosophers), "GenerateInitialOptions")]
internal static class ColorfulPhilosophersColorPatch
{
    private static readonly MethodInfo EnergyColorGetter =
        AccessTools.PropertyGetter(typeof(CardPoolModel), nameof(CardPoolModel.EnergyColorName));

    // These pools share energy art, but have distinct colors in this event.
    private static string GetEventColorName(CardPoolModel pool) => pool switch
    {
        HBRGOSaki30ACardPool => "hbrgo_saki_30a",
        HBRGOSaki31OCardPool => "hbrgo_saki_31o",
        _ => pool.EnergyColorName
    };

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo replacement = AccessTools.Method(
            typeof(ColorfulPhilosophersColorPatch), nameof(GetEventColorName));
        bool replaced = false;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Calls(EnergyColorGetter))
            {
                yield return instruction;
                continue;
            }

            replaced = true;
            yield return new CodeInstruction(instruction) { opcode = OpCodes.Call, operand = replacement };
        }

        if (!replaced)
            throw new InvalidOperationException("Colorful Philosophers color lookup was not found.");
    }
}
