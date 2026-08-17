using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class StrongestShieldDamagePatch
{
    [ThreadStatic]
    private static int _actualDamageDepth;

    internal static bool IsResolvingActualDamage =>
        _actualDamageDepth > 0;

    private static MethodBase TargetMethod()
    {
        MethodInfo damageMethod =
            PrivateAccess.DeclaredMethod(
                typeof(CreatureCmd),
                nameof(CreatureCmd.Damage),
                typeof(PlayerChoiceContext),
                typeof(IEnumerable<Creature>),
                typeof(decimal),
                typeof(ValueProp),
                typeof(Creature),
                typeof(CardModel));

        AsyncStateMachineAttribute attribute = damageMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (attribute == null)
        {
            throw new InvalidOperationException("CreatureCmd.Damage no longer uses an async state machine.");
        }

        return PrivateAccess.Method(attribute.StateMachineType, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo originalModifyDamage =
            PrivateAccess.Method(typeof(Hook), nameof(Hook.ModifyDamage));
        MethodInfo replacementModifyDamage =
            PrivateAccess.Method(
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
        _actualDamageDepth++;
        try
        {
            return Hook.ModifyDamage(
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
        }
        finally
        {
            _actualDamageDepth--;
        }
    }
}
