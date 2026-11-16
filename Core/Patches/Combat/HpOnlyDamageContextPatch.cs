using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
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
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;

    [HarmonyPriority(Priority.First)]
    private static void Prefix(object[] __args, decimal amount,
        ValueProp props, Creature dealer, CardModel cardSource,
        out HpOnlyDamageContext.CommandScope __state)
    {
        IEnumerable<Creature> originalTargets = NativeDamageApiCompatibility.ReadTargets(__args);
        IEnumerable<Creature> targets = originalTargets;
        __state = HpOnlyDamageContext.Begin(ref targets, amount, props, dealer, cardSource);
        NativeDamageApiCompatibility.WriteTargets(__args, originalTargets, targets);
    }

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
