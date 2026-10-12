using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
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

    [ThreadStatic]
    private static int _intentPreviewDepth;

    internal static bool IsResolvingActualDamage =>
        _actualDamageDepth > 0 && _intentPreviewDepth == 0;

    internal static void BeginIntentPreview() => _intentPreviewDepth++;

    internal static void EndIntentPreview() => _intentPreviewDepth--;

    private static MethodBase TargetMethod()
    {
        MethodInfo damageMethod = AccessTools
            .GetDeclaredMethods(typeof(CreatureCmd))
            .Where(method => method.Name == nameof(CreatureCmd.Damage))
            .Where(method =>
            {
                ParameterInfo[] parameters = method.GetParameters();
                return parameters.Length is 6 or 7 &&
                    parameters[0].ParameterType ==
                        typeof(PlayerChoiceContext) &&
                    parameters[1].ParameterType ==
                        typeof(IEnumerable<Creature>) &&
                    parameters[2].ParameterType == typeof(decimal) &&
                    parameters[3].ParameterType == typeof(ValueProp) &&
                    parameters[4].ParameterType == typeof(Creature) &&
                    parameters[5].ParameterType == typeof(CardModel);
            })
            .OrderBy(method => method.GetParameters().Length)
            .FirstOrDefault() ?? throw new MissingMethodException(
                typeof(CreatureCmd).FullName,
                nameof(CreatureCmd.Damage));

        AsyncStateMachineAttribute attribute = damageMethod.GetCustomAttribute<AsyncStateMachineAttribute>();
        if (attribute == null)
        {
            return damageMethod;
        }

        return PrivateAccess.Method(attribute.StateMachineType, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo stableReplacement =
            PrivateAccess.Method(
                typeof(StrongestShieldDamagePatch),
                nameof(ModifyDamageWithStrongestShield),
                typeof(IRunState),
                typeof(ICombatState),
                typeof(Creature),
                typeof(Creature),
                typeof(decimal),
                typeof(ValueProp),
                typeof(CardModel),
                typeof(ModifyDamageHookType),
                typeof(CardPreviewMode),
                typeof(IEnumerable<AbstractModel>).MakeByRefType());
        MethodInfo betaReplacement =
            PrivateAccess.Method(
                typeof(StrongestShieldDamagePatch),
                nameof(ModifyDamageWithStrongestShield),
                typeof(IRunState),
                typeof(ICombatState),
                typeof(Creature),
                typeof(Creature),
                typeof(decimal),
                typeof(ValueProp),
                typeof(CardModel),
                typeof(CardPlay),
                typeof(ModifyDamageHookType),
                typeof(CardPreviewMode),
                typeof(IEnumerable<AbstractModel>).MakeByRefType());

        int replacementCount = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.operand is MethodInfo calledMethod &&
                calledMethod.DeclaringType == typeof(Hook) &&
                calledMethod.Name == nameof(Hook.ModifyDamage))
            {
                replacementCount++;
                yield return new CodeInstruction(instruction)
                {
                    operand = calledMethod.GetParameters().Length == 11
                        ? betaReplacement
                        : stableReplacement
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
            decimal modified = HookCompatibility.ModifyDamage(
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
            return LimitCardDamage(target, cardSource, modified);
        }
        finally
        {
            _actualDamageDepth--;
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
        CardPlay cardPlay,
        ModifyDamageHookType modifyDamageHookType,
        CardPreviewMode previewMode,
        out IEnumerable<AbstractModel> modifiers)
    {
        _actualDamageDepth++;
        try
        {
            decimal modified = HookCompatibility.ModifyDamage(
                runState,
                combatState,
                target,
                dealer,
                damage,
                props,
                cardSource,
                modifyDamageHookType,
                previewMode,
                out modifiers,
                cardPlay);
            return LimitCardDamage(target, cardSource, modified);
        }
        finally
        {
            _actualDamageDepth--;
        }
    }

    private static decimal LimitCardDamage(Creature target, CardModel cardSource, decimal damage)
    {
        // Only the actual command wrapper spends the budget. Nested card/intent
        // previews inside a modifier can never consume it.
        decimal allowed = CardPlayContextHelper.LimitIssuedDamage(cardSource, damage);
        if (allowed < damage) ItsMygoIncomingDamageCapture.LimitPendingCapture(target, allowed);
        return allowed;
    }
}
