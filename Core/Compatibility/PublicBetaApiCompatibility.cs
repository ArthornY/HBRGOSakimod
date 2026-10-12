using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Compatibility;

internal static class AttackCommandCompatibilityExtensions
{
    private static readonly ConcurrentDictionary<Type, MethodInfo>
        FromCardMethods = new();

    internal static T FromCardCompat<T>(
        this T command,
        CardModel card,
        CardPlay cardPlay = null)
        where T : class
    {
        MethodInfo method = FromCardMethods.GetOrAdd(
            command.GetType(),
            static commandType => commandType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(candidate => candidate.Name == "FromCard")
                .Where(candidate =>
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    return parameters.Length is 1 or 2 &&
                        parameters[0].ParameterType == typeof(CardModel);
                })
                .OrderByDescending(candidate =>
                    candidate.GetParameters().Length)
                .FirstOrDefault() ?? throw new MissingMethodException(
                    commandType.FullName,
                    "FromCard"));
        object[] arguments = method.GetParameters().Length == 1
            ? [card]
            : [card, cardPlay];
        return (T)CompatibilityReflection.Invoke(
            method,
            command,
            arguments);
    }
}

internal static class CreatureCommandCompatibility
{
    private static readonly MethodInfo SingleTargetDamageMethod =
        FindDamageMethod(typeof(Creature));

    private static readonly MethodInfo MultipleTargetDamageMethod =
        FindDamageMethod(typeof(IEnumerable<Creature>));

    private static readonly MethodInfo LoseBlockMethod =
        typeof(CreatureCmd)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(candidate => candidate.Name == nameof(CreatureCmd.LoseBlock))
            .Where(candidate => candidate.GetParameters().Length is 2 or 4)
            .OrderByDescending(candidate =>
                candidate.GetParameters().Length)
            .FirstOrDefault() ?? throw new MissingMethodException(
                typeof(CreatureCmd).FullName,
                nameof(CreatureCmd.LoseBlock));

    internal static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer)
    {
        return Damage(
            choiceContext,
            target,
            amount,
            props,
            dealer,
            cardSource: null,
            cardPlay: null);
    }

    internal static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource,
        CardPlay cardPlay = null)
    {
        return InvokeDamage(
            SingleTargetDamageMethod,
            choiceContext,
            target,
            amount,
            props,
            dealer,
            cardSource,
            cardPlay);
    }

    internal static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        decimal amount,
        ValueProp props,
        Creature dealer)
    {
        return Damage(
            choiceContext,
            targets,
            amount,
            props,
            dealer,
            cardSource: null,
            cardPlay: null);
    }

    internal static Task<IEnumerable<DamageResult>> Damage(
        PlayerChoiceContext choiceContext,
        IEnumerable<Creature> targets,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource,
        CardPlay cardPlay = null)
    {
        return InvokeDamage(
            MultipleTargetDamageMethod,
            choiceContext,
            targets,
            amount,
            props,
            dealer,
            cardSource,
            cardPlay);
    }

    internal static Task LoseBlock(
        PlayerChoiceContext choiceContext,
        Creature target,
        decimal amount,
        Creature remover)
    {
        object[] arguments = LoseBlockMethod.GetParameters().Length == 2
            ? [target, amount]
            : [choiceContext, target, amount, remover];
        return (Task)CompatibilityReflection.Invoke(
            LoseBlockMethod,
            target: null,
            arguments);
    }

    private static MethodInfo FindDamageMethod(Type targetType)
    {
        return typeof(CreatureCmd)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(candidate => candidate.Name == nameof(CreatureCmd.Damage))
            .Where(candidate =>
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length is 6 or 7 &&
                    parameters[1].ParameterType == targetType &&
                    parameters[2].ParameterType == typeof(decimal) &&
                    parameters[3].ParameterType == typeof(ValueProp) &&
                    parameters[4].ParameterType == typeof(Creature) &&
                    parameters[5].ParameterType == typeof(CardModel);
            })
            .OrderByDescending(candidate =>
                candidate.GetParameters().Length)
            .FirstOrDefault() ?? throw new MissingMethodException(
                typeof(CreatureCmd).FullName,
                nameof(CreatureCmd.Damage));
    }

    private static Task<IEnumerable<DamageResult>> InvokeDamage(
        MethodInfo method,
        PlayerChoiceContext choiceContext,
        object target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        object[] arguments = method.GetParameters().Length == 6
            ? [choiceContext, target, amount, props, dealer, cardSource]
            : [choiceContext, target, amount, props, dealer, cardSource, cardPlay];
        return (Task<IEnumerable<DamageResult>>)
            CompatibilityReflection.Invoke(
                method,
                target: null,
                arguments);
    }
}

internal static class HookCompatibility
{
    private static readonly MethodInfo ModifyDamageMethod =
        typeof(Hook)
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(candidate =>
                candidate.Name == nameof(Hook.ModifyDamage) &&
                candidate.ReturnType == typeof(decimal))
            .Where(candidate =>
                candidate.GetParameters().Length is 10 or 11)
            .OrderByDescending(candidate =>
                candidate.GetParameters().Length)
            .FirstOrDefault() ?? throw new MissingMethodException(
                typeof(Hook).FullName,
                nameof(Hook.ModifyDamage));

    internal static decimal ModifyDamage(
        IRunState runState,
        ICombatState combatState,
        Creature target,
        Creature dealer,
        decimal damage,
        ValueProp props,
        CardModel cardSource,
        ModifyDamageHookType modifyDamageHookType,
        CardPreviewMode previewMode,
        out IEnumerable<AbstractModel> modifiers,
        CardPlay cardPlay = null)
    {
        object[] arguments = ModifyDamageMethod.GetParameters().Length == 10
            ?
            [
                runState,
                combatState,
                target,
                dealer,
                damage,
                props,
                cardSource,
                modifyDamageHookType,
                previewMode,
                null
            ]
            :
            [
                runState,
                combatState,
                target,
                dealer,
                damage,
                props,
                cardSource,
                cardPlay,
                modifyDamageHookType,
                previewMode,
                null
            ];
        decimal result = (decimal)CompatibilityReflection.Invoke(
            ModifyDamageMethod,
            target: null,
            arguments);
        modifiers = arguments[^1] as IEnumerable<AbstractModel> ??
            Array.Empty<AbstractModel>();
        return result;
    }
}

internal static class CardModelCompatibilityExtensions
{
    private static readonly MethodInfo CreateDupeMethod =
        typeof(CardModel)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(candidate => candidate.Name == nameof(CardModel.CreateDupe))
            .Where(candidate => candidate.GetParameters().Length is 0 or 1)
            .OrderByDescending(candidate =>
                candidate.GetParameters().Length)
            .FirstOrDefault() ?? throw new MissingMethodException(
                typeof(CardModel).FullName,
                nameof(CardModel.CreateDupe));

    internal static CardModel CreateDupeCompat(
        this CardModel card,
        Player newOwner)
    {
        object[] arguments = CreateDupeMethod.GetParameters().Length == 0
            ? Array.Empty<object>()
            : [newOwner];
        return (CardModel)CompatibilityReflection.Invoke(
            CreateDupeMethod,
            card,
            arguments);
    }
}

internal static class CompatibilityReflection
{
    internal static object Invoke(
        MethodInfo method,
        object target,
        object[] arguments)
    {
        try
        {
            return method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
