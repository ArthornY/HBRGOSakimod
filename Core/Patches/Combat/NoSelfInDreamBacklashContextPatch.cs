using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using HarmonyLib;
using HBRGOSakiMod.Core.Compatibility;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class NoSelfInDreamBacklashContextPatch
{
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;

    [HarmonyPriority(Priority.First)]
    private static void Prefix(object[] __args, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource, out NoSelfInDreamBacklashContext.Scope __state)
    {
        __state = NoSelfInDreamBacklashContext.Begin(
            NativeDamageApiCompatibility.ReadTargets(__args), amount, props, dealer, cardSource);
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NoSelfInDreamBacklashContext.Scope __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        try { __result = NoSelfInDreamBacklashContext.Finish(__result, __state); }
        finally { NoSelfInDreamBacklashContext.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception, NoSelfInDreamBacklashContext.Scope __state)
    {
        if (__exception != null) NoSelfInDreamBacklashContext.Close(__state);
        else NoSelfInDreamBacklashContext.RestoreCaller(__state);
        return __exception;
    }
}
