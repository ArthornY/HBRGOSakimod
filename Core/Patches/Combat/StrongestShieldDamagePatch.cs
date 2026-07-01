using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class StrongestShieldDamagePatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo damageMethod = AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
            .Single(method =>
            {
                if (method.Name != nameof(CreatureCmd.Damage))
                {
                    return false;
                }

                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length == 6 &&
                       parameters[1].ParameterType == typeof(IEnumerable<Creature>) &&
                       parameters[2].ParameterType == typeof(decimal) &&
                       parameters[3].ParameterType == typeof(ValueProp);
            });

        AsyncStateMachineAttribute attribute = damageMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (attribute == null)
        {
            throw new InvalidOperationException("CreatureCmd.Damage no longer uses an async state machine.");
        }

        return AccessTools.Method(attribute.StateMachineType, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo originalModifyDamage = AccessTools.Method(typeof(Hook), nameof(Hook.ModifyDamage));
        MethodInfo replacementModifyDamage = AccessTools.Method(
            typeof(StrongestShieldDamagePatch),
            nameof(ModifyDamageWithStrongestShield));

        int replacementCount = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(originalModifyDamage))
            {
                replacementCount++;
                yield return new CodeInstruction(instruction)
                {
                    operand = replacementModifyDamage
                };
                continue;
            }

            yield return instruction;
        }

        if (replacementCount == 0)
        {
            throw new InvalidOperationException("Expected to replace at least 1 Hook.ModifyDamage call, replaced 0.");
        }
    }

    private static decimal ModifyDamageWithStrongestShield(
        IRunState runState,
        ICombatState combatState,
        Creature target,
        Creature dealer,
        decimal damage,
        ValueProp props,
        CardModel cardSource,
        ModifyDamageHookType modifyDamageHookType,
        CardPreviewMode previewMode,
        out IEnumerable<AbstractModel> modifiers)
    {
        decimal modifiedDamage = Hook.ModifyDamage(
            runState,
            combatState,
            target,
            dealer,
            damage,
            props,
            cardSource,
            modifyDamageHookType,
            previewMode,
            out modifiers);

        return StrongestShieldPower.ModifyIncomingAttackDamage(target, dealer, modifiedDamage, props, cardSource);
    }
}
