using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HBRGOSakiMod.Core.Models.Monsters;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Helpers;

internal static class DamageModifierCompatibility
{
    private static readonly Assembly ModAssembly =
        typeof(DamageModifierCompatibility).Assembly;

    private static readonly Type[] DamageParameterTypes =
    [
        typeof(Creature),
        typeof(decimal),
        typeof(ValueProp),
        typeof(Creature),
        typeof(CardModel)
    ];

    private static readonly Type[] DamageCapParameterTypes =
    [
        typeof(Creature),
        typeof(ValueProp),
        typeof(Creature),
        typeof(CardModel)
    ];

    private delegate decimal DamageInvoker(AbstractModel model, Creature target,
        decimal amount, ValueProp props, Creature dealer, CardModel cardSource,
        CardPlay cardPlay);

    private delegate decimal LegacyDamageInvoker<TModel>(TModel model, Creature target,
        decimal amount, ValueProp props, Creature dealer, CardModel cardSource)
        where TModel : AbstractModel;

    private delegate decimal CapInvoker(AbstractModel model, Creature target,
        ValueProp props, Creature dealer, CardModel cardSource, CardPlay cardPlay);

    private static readonly ConcurrentDictionary<
        (Type Type, string Name), Lazy<DamageInvoker>> LegacyInvokers = new();

    private static readonly MethodInfo LegacyInvokerFactory =
        typeof(DamageModifierCompatibility).GetMethod(nameof(CreateLegacyInvoker),
            BindingFlags.Static | BindingFlags.NonPublic);

    private static readonly DamageInvoker RuntimeAdditive =
        CreateRuntimeDamageInvoker(FindRuntimeMethod(
            nameof(AbstractModel.ModifyDamageAdditive),
            DamageParameterTypes.Length));

    private static readonly DamageInvoker RuntimeMultiplicative =
        CreateRuntimeDamageInvoker(FindRuntimeMethod(
            nameof(AbstractModel.ModifyDamageMultiplicative),
            DamageParameterTypes.Length));

    private static readonly CapInvoker RuntimeCap =
        CreateRuntimeCapInvoker(FindRuntimeMethod(
            nameof(AbstractModel.ModifyDamageCap),
            DamageCapParameterTypes.Length));

    internal static decimal ModifyDamageAdditive(
        AbstractModel model,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        return (GetLegacyInvoker(model.GetType(), nameof(AbstractModel.ModifyDamageAdditive))
            ?? RuntimeAdditive)(model, target, amount, props, dealer, cardSource, cardPlay);
    }

    internal static decimal ModifyDamageMultiplicative(
        AbstractModel model,
        Creature target,
        decimal amount,
        ValueProp props,
        Creature dealer,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        return (GetLegacyInvoker(model.GetType(), nameof(AbstractModel.ModifyDamageMultiplicative))
            ?? RuntimeMultiplicative)(model, target, amount, props, dealer, cardSource, cardPlay);
    }

    internal static decimal ModifyDamageCap(
        AbstractModel model,
        Creature target,
        ValueProp props,
        Creature dealer,
        CardModel cardSource,
        CardPlay cardPlay)
    {
        // Public beta added CardPlay to the native virtual cap hook. Invoke our
        // stable-signature power explicitly so both runtimes enforce immunity.
        if (model is NararaNullificationPower nullification)
            return nullification.GetNullificationDamageCap(target, cardSource);
        if (model is HotaruCannonModel cannon)
            return cannon.GetCannonDamageCap(target);
        return RuntimeCap(model, target, props, dealer, cardSource, cardPlay);
    }

    private static DamageInvoker CreateRuntimeDamageInvoker(MethodInfo method)
    {
        if (method.GetParameters().Length != DamageParameterTypes.Length)
            return method.CreateDelegate<DamageInvoker>();

        var invoke = method.CreateDelegate<LegacyDamageInvoker<AbstractModel>>();
        return (model, target, amount, props, dealer, cardSource, _) =>
            invoke(model, target, amount, props, dealer, cardSource);
    }

    private static CapInvoker CreateRuntimeCapInvoker(MethodInfo method)
    {
        if (method.GetParameters().Length != DamageCapParameterTypes.Length)
            return method.CreateDelegate<CapInvoker>();

        var invoke = method.CreateDelegate<
            Func<AbstractModel, Creature, ValueProp, Creature, CardModel, decimal>>();
        return (model, target, props, dealer, cardSource, _) =>
            invoke(model, target, props, dealer, cardSource);
    }

    private static DamageInvoker GetLegacyInvoker(
        Type modelType,
        string methodName)
    {
        return LegacyInvokers.GetOrAdd(
            (modelType, methodName),
            static key => new Lazy<DamageInvoker>(() =>
            {
                MethodInfo method = key.Type.GetMethod(
                    key.Name,
                    BindingFlags.Instance | BindingFlags.Public,
                    binder: null,
                    DamageParameterTypes,
                    modifiers: null);
                if (method?.DeclaringType?.Assembly != ModAssembly) return null;
                try
                {
                    return (DamageInvoker)LegacyInvokerFactory
                        .MakeGenericMethod(method.DeclaringType).Invoke(null, [method]);
                }
                catch (TargetInvocationException exception) when (exception.InnerException != null)
                {
                    ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                    throw;
                }
            })).Value;
    }

    private static DamageInvoker CreateLegacyInvoker<TModel>(MethodInfo method)
        where TModel : AbstractModel
    {
        // Open instance delegates keep virtual dispatch and Harmony detours. Do not
        // emit a direct nonvirtual call or snapshot a native function pointer.
        var invoke = method.CreateDelegate<LegacyDamageInvoker<TModel>>();
        return (model, target, amount, props, dealer, cardSource, _) =>
            invoke((TModel)model, target, amount, props, dealer, cardSource);
    }

    private static MethodInfo FindRuntimeMethod(
        string methodName,
        int legacyParameterCount)
    {
        MethodInfo method = typeof(AbstractModel)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(candidate => candidate.Name == methodName)
            .Where(candidate =>
            {
                int parameterCount = candidate.GetParameters().Length;
                return parameterCount == legacyParameterCount ||
                    parameterCount == legacyParameterCount + 1;
            })
            .OrderByDescending(candidate =>
                candidate.GetParameters().Length)
            .FirstOrDefault();
        return method ?? throw new MissingMethodException(
            typeof(AbstractModel).FullName,
            methodName);
    }

}
