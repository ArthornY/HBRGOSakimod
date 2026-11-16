using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Compatibility;

internal static class NativeRewardApiCompatibility
{
    private static readonly ConstructorInfo CardConstructor = typeof(CardReward).GetConstructors()
        .Single(ctor => ctor.GetParameters()[0].ParameterType == typeof(CardCreationOptions));
    private static readonly ConstructorInfo RelicConstructor = typeof(RelicReward).GetConstructors()
        .Single(ctor => ctor.GetParameters()[0].ParameterType == typeof(RelicModel));

    internal static CardReward Card(CardCreationOptions options, int choices, Player player)
    {
        ParameterInfo[] parameters = CardConstructor.GetParameters();
        object[] args = new object[parameters.Length];
        args[0] = options; args[1] = choices; args[2] = player;
        for (int i = 3; i < args.Length; i++)
        {
            if (!parameters[i].HasDefaultValue)
                throw new MissingMethodException("Unexpected required CardReward argument: " + parameters[i].Name);
            args[i] = parameters[i].DefaultValue;
        }
        return (CardReward)Create(CardConstructor, args);
    }

    internal static RelicReward Relic(RelicModel relic, Player player)
    {
        ParameterInfo[] parameters = RelicConstructor.GetParameters();
        object[] args = parameters.Length == 2
            ? [relic, player] : [relic, player, parameters[2].DefaultValue];
        return (RelicReward)Create(RelicConstructor, args);
    }

    private static object Create(ConstructorInfo constructor, object[] args)
    {
        try { return constructor.Invoke(args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }
}
