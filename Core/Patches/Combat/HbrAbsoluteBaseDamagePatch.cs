using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrAbsoluteBaseDamageCommandPatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
        .Where(method => method.Name == nameof(CreatureCmd.Damage))
        .Where(method =>
        {
            var p = method.GetParameters();
            return p.Length is 6 or 7 && p[1].ParameterType == typeof(IEnumerable<Creature>) &&
                p[2].ParameterType == typeof(decimal) && p[3].ParameterType == typeof(ValueProp) &&
                p[4].ParameterType == typeof(Creature) && p[5].ParameterType == typeof(CardModel);
        }).OrderByDescending(method => method.GetParameters().Length).First();

    [HarmonyPriority(Priority.First)]
    private static void Prefix(Creature dealer, CardModel cardSource, decimal amount, ValueProp props,
        out HbrAbsoluteBaseDamageContext.CommandScope __state) =>
        __state = HbrAbsoluteBaseDamageContext.BeginCommand(dealer, cardSource, amount, props);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(HbrAbsoluteBaseDamageContext.CommandScope __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        if (__state == null) return;
        try { __result = HbrAbsoluteBaseDamageContext.Finish(__result, __state); }
        finally { HbrAbsoluteBaseDamageContext.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception, HbrAbsoluteBaseDamageContext.CommandScope __state)
    {
        if (__exception != null) HbrAbsoluteBaseDamageContext.Close(__state);
        else HbrAbsoluteBaseDamageContext.RestoreCaller(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class HbrAbsoluteBaseDamageHookPatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(Hook))
        .Where(method => method.Name == nameof(Hook.ModifyDamage))
        .OrderByDescending(method => method.GetParameters().Length).First();

    [HarmonyPriority(Priority.First)]
    private static void Prefix(Creature target, Creature dealer, CardModel cardSource, decimal damage,
        ValueProp props, out HbrAbsoluteBaseDamageContext.HookScope __state) =>
        __state = HbrAbsoluteBaseDamageContext.BeginHook(target, dealer, cardSource, damage, props);

    private static Exception Finalizer(Exception __exception, HbrAbsoluteBaseDamageContext.HookScope __state)
    {
        __state?.Dispose();
        return __exception;
    }
}