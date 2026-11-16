using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Guard the actual HP-write calls inside Damage, after block/DP, redirection
// and all native HP-loss modifiers. Kill/SetCurrentHp/phase restoration never
// pass through this adapter. Explicit native cost callsites carry receipts.
[HarmonyPatch]
internal static class MariaLifesaveDamagePatch
{
    private static MethodBase TargetMethod() => NativeDamageApiCompatibility.MoveNext;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        FieldInfo dealer = AccessTools.GetDeclaredFields(__originalMethod.DeclaringType)
            .Single(field => field.Name == "dealer" && field.FieldType == typeof(Creature));
        FieldInfo card = AccessTools.GetDeclaredFields(__originalMethod.DeclaringType)
            .Single(field => field.Name == "cardSource" && field.FieldType == typeof(CardModel));
        MethodInfo native = AccessTools.Method(typeof(Creature), nameof(Creature.LoseHpInternal));
        MethodInfo adapter = AccessTools.Method(typeof(MariaLifesaveDamagePatch), nameof(LoseHp));
        int count = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(native))
            {
                CodeInstruction load = new(OpCodes.Ldarg_0);
                load.labels.AddRange(instruction.labels); load.blocks.AddRange(instruction.blocks);
                yield return load;
                yield return new(OpCodes.Ldfld, dealer);
                yield return new(OpCodes.Ldarg_0);
                yield return new(OpCodes.Ldfld, card);
                yield return new(OpCodes.Call, adapter);
                count++;
            }
            else yield return instruction;
        }
        int expected = NativeDamageApiCompatibility.UsesTargetData ? 3 : 2;
        if (count != expected) throw new InvalidOperationException("Expected " + expected + " actual HP-write sites; found " + count);
    }

    private static DamageResult LoseHp(Creature target, decimal amount, ValueProp props, Creature dealer, CardModel source)
    {
        if (target.Side != CombatSide.Player) return target.LoseHpInternal(amount, props);
        decimal hp = LargeCreatureHealthHelper.GetCurrentHp(target);
        bool selfCost = HpOnlyDamageContext.IsCurrentHealthCost;
        if (!selfCost && hp > 0m && decimal.Floor(Math.Clamp(amount, 0m, DamageLimitHelper.MaximumSingleHitDamage)) >= hp &&
            MariaRepentanceCombatHelper.TryConsumeLifesave(target))
            amount = Math.Max(0m, hp - 1m);
        return target.LoseHpInternal(amount, props);
    }
}
