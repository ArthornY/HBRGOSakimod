using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HpOnlyDamageContextPatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
        .Where(method => method.Name == nameof(CreatureCmd.Damage))
        .Where(method =>
        {
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length is 6 or 7 &&
                parameters[0].ParameterType == typeof(PlayerChoiceContext) &&
                parameters[1].ParameterType == typeof(IEnumerable<Creature>) &&
                parameters[2].ParameterType == typeof(decimal) &&
                parameters[3].ParameterType == typeof(ValueProp) &&
                parameters[4].ParameterType == typeof(Creature) &&
                parameters[5].ParameterType == typeof(CardModel);
        })
        .OrderByDescending(method => method.GetParameters().Length)
        .FirstOrDefault() ?? throw new MissingMethodException(typeof(CreatureCmd).FullName,
            nameof(CreatureCmd.Damage) + " canonical enumerable overload");

    [HarmonyPriority(Priority.First)]
    private static void Prefix(ref IEnumerable<Creature> targets, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource,
        out HpOnlyDamageContext.CommandScope __state) =>
        __state = HpOnlyDamageContext.Begin(ref targets, amount, props, dealer, cardSource);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(HpOnlyDamageContext.CommandScope __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        try { __result = HpOnlyDamageContext.Finish(__result, __state); }
        finally { HpOnlyDamageContext.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception,
        HpOnlyDamageContext.CommandScope __state)
    {
        if (__exception != null) HpOnlyDamageContext.Close(__state);
        else HpOnlyDamageContext.RestoreCaller(__state);
        return __exception;
    }
}
