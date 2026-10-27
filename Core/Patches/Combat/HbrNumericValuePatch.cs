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
internal static class HbrNumericCriticalScopePatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
        .Where(method => method.Name == nameof(CreatureCmd.Damage))
        .Where(method =>
        {
            var p = method.GetParameters();
            return p.Length is 6 or 7 && p[1].ParameterType == typeof(IEnumerable<Creature>) &&
                p[2].ParameterType == typeof(decimal) && p[3].ParameterType == typeof(ValueProp) &&
                p[4].ParameterType == typeof(Creature) && p[5].ParameterType == typeof(CardModel);
        }).OrderBy(method => method.GetParameters().Length).First();

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(ref IEnumerable<Creature> targets, ValueProp props, Creature dealer,
        CardModel cardSource, out HbrCriticalDamageContext.Scope __state)
    {
        int count = 1;
        if (targets != null && dealer?.IsDead != true && props.IsPoweredAttack() &&
            HbrBattleStatsHelper.IsEnabled(dealer) && !targets.TryGetNonEnumeratedCount(out count))
        {
            // Native Damage snapshots targets synchronously before its first
            // modifier/RNG/await. Snapshot a lazy source at that same boundary
            // and pass it on, so area defense is correct without enumerating
            // a target query twice or changing the subsequent hit targets.
            Creature[] snapshot = targets.ToArray();
            targets = snapshot;
            count = snapshot.Length;
        }
        __state = HbrCriticalDamageContext.Begin(dealer, cardSource, props, count);
    }

    private static void Postfix(HbrCriticalDamageContext.Scope __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        if (__state == null) return;
        try { __result = HbrCriticalDamageContext.Finish(__result, __state); }
        finally { HbrCriticalDamageContext.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception, HbrCriticalDamageContext.Scope __state)
    {
        if (__exception != null) HbrCriticalDamageContext.Close(__state);
        else HbrCriticalDamageContext.RestoreCaller(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyBlock))]
internal static class HbrNumericBlockPatch
{
    // Harmony executes higher-priority postfixes first. Convert resource units
    // before the cannon/coffin Priority.Last boundaries perform final rounding.
    [HarmonyPriority(Priority.Normal)]
    private static void Postfix(Creature target, ValueProp props, ref decimal __result) =>
        __result = HbrDamageSkillAdapter.ConvertBlock(target, __result, props);
}
