using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Rewrite only the starting base. Native local modifiers, then native global
// modifiers (including Aina's debuff discount), still execute in their own order.
[HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.GetWithModifiers))]
internal static class IchikoFrugalBaseCostPatch
{
    // This hook runs before native canonical-cost early return; never read a
    // canonical card's mutable-only Owner while sorting library prototypes.
    private static int FrugalBase(int amount, CardModel card) => card?.IsMutable == true && HbrBattleStatsHelper.IsEnabled(card.Owner)
        ? IchikoEldestCombatHelper.GetFrugalBaseCost(card, amount) : amount;
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        FieldInfo baseField = AccessTools.Field(typeof(CardEnergyCost), "_base");
        FieldInfo cardField = AccessTools.Field(typeof(CardEnergyCost), "_card");
        MethodInfo rewrite = AccessTools.Method(typeof(IchikoFrugalBaseCostPatch), nameof(FrugalBase));
        bool replaced = false;
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (!replaced && instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, baseField))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, cardField);
                yield return new CodeInstruction(OpCodes.Call, rewrite);
                replaced = true;
            }
        }
        if (!replaced) throw new MissingFieldException(typeof(CardEnergyCost).FullName, "_base initial cost load");
    }
}
