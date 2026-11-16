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
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrNumericCriticalScopePatch
{
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(ValueProp props, Creature dealer,
        CardModel cardSource, out HbrCriticalDamageContext.Scope __state)
    {
        // Enemy defense references belong to skill profiles. Critical sampling
        // needs no target count or extra materialization of native target queries.
        __state = HbrCriticalDamageContext.Begin(dealer, cardSource, props);
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
    private static void Postfix(Creature target, ValueProp props, CardModel cardSource, ref decimal __result)
    {
        __result = HbrDamageSkillAdapter.ConvertBlock(target, __result, props);
        __result = DamageAmplificationLimits.MultiplySaturated(__result, 1m +
            HbrCardEffectBonusHelper.GetPercent(cardSource, cardSource?.Owner?.Creature, props) / 100m);
    }
}
