using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Galvanic damages whoever plays a Galvanized card by its raw stack count, with
// no dealer. Like an enemy's Thorns, declare that amount as its owner's skill at
// the command boundary; native flags, attribution and stacks stay unchanged.
[HarmonyPatch]
internal static class HbrNativeGalvanicDamagePatch
{
    private static MethodBase TargetMethod() => HbrNativeEffectSkillModelCommand.Body(typeof(GalvanicPower));

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod) =>
        HbrNativeEffectSkillModelCommand.Wrap(instructions, __originalMethod, typeof(PowerModel));
}
