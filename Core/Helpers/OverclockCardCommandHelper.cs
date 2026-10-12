using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Helpers;

internal static class OverclockCardCommandHelper
{
    private static readonly MethodInfo ExhaustMethod = ResolveExhaust();
    private static readonly object[] DefaultArguments = CreateArguments();

    private static MethodInfo ResolveExhaust()
    {
        MethodInfo method = typeof(CardCmd).GetMethod(nameof(CardCmd.Exhaust),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
            null, new[] { typeof(PlayerChoiceContext), typeof(CardModel), typeof(bool), typeof(bool) }, null);
        if (method == null || !typeof(Task).IsAssignableFrom(method.ReturnType))
            throw new MissingMethodException(typeof(CardCmd).FullName,
                "Exhaust(PlayerChoiceContext, CardModel, bool, bool) returning Task");
        return method;
    }

    private static object[] CreateArguments()
    {
        ParameterInfo[] parameters = ExhaustMethod.GetParameters();
        object[] arguments = new object[parameters.Length];
        bool foundEthereal = false;
        for (int index = 2; index < parameters.Length; index++)
        {
            if (!parameters[index].HasDefaultValue || parameters[index].DefaultValue is not bool defaultValue)
                throw new MissingMethodException(typeof(CardCmd).FullName,
                    "Exhaust boolean metadata default: " + parameters[index].Name);
            arguments[index] = defaultValue;
            if (parameters[index].Name == "causedByEthereal")
            {
                arguments[index] = true;
                foundEthereal = true;
            }
        }
        if (!foundEthereal)
            throw new MissingMethodException(typeof(CardCmd).FullName, "Exhaust causedByEthereal parameter");
        return arguments;
    }

    internal static async Task ExhaustEthereal(PlayerChoiceContext context, CardModel card)
    {
        object[] arguments = (object[])DefaultArguments.Clone();
        arguments[0] = context;
        arguments[1] = card;
        Task task;
        try
        {
            // Beta returns Task<CardPileAddResult?>; await its Task base without
            // emitting a MemberRef to the stable-only Task-returning overload.
            task = ExhaustMethod.Invoke(null, arguments) as Task ??
                throw new InvalidOperationException("CardCmd.Exhaust did not return a Task.");
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
        await task;
    }
}
