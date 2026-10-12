using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class PhantomToughEggHatchPatch
{
    private static MethodBase TargetMethod() => MonsterCardRecipientPatch.GetAsyncBody(
        AccessTools.Method(typeof(ToughEgg), "HatchMove"));

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo remove = AccessTools.Method(typeof(PowerCmd), nameof(PowerCmd.Remove),
            new[] { typeof(PowerModel) });
        List<CodeInstruction> result = instructions.ToList();
        CodeInstruction[] calls = result.Where(instruction => instruction.Calls(remove)).ToArray();
        if (calls.Length != 1)
        {
            throw new InvalidOperationException("Expected one power-reset loop in ToughEgg.HatchMove.");
        }

        calls[0].opcode = OpCodes.Call;
        calls[0].operand = AccessTools.Method(typeof(PhantomToughEggHatchPatch), nameof(RemoveHatchPower));
        return result;
    }

    private static Task RemoveHatchPower(PowerModel power)
    {
        // Hatching clears ordinary powers, but these two implement phantom identity and DP.
        if (power.Owner?.PetOwner != null &&
            power.Owner.Monster is ToughEgg &&
            power.Owner.HasPower<PhantomPower>() &&
            power is PhantomPower or PhantomHighDimensionalShellPower)
        {
            return Task.CompletedTask;
        }

        return PowerCmd.Remove(power);
    }
}
