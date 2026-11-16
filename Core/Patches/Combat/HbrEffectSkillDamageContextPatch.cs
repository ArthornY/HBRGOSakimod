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
internal static class HbrEffectSkillDamageContextPatch
{
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;

    [HarmonyPriority(Priority.First)]
    private static void Prefix(object[] __args, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource, out HbrEffectSkillDamageContext.CommandScope __state)
    {
        IEnumerable<Creature> originalTargets = NativeDamageApiCompatibility.ReadTargets(__args);
        IEnumerable<Creature> targets = originalTargets;
        __state = HbrEffectSkillDamageContext.BeginCommand(ref targets, amount, props, dealer, cardSource);
        NativeDamageApiCompatibility.WriteTargets(__args, originalTargets, targets);
    }

    private static void Postfix(HbrEffectSkillDamageContext.CommandScope __state,
        ref Task<IEnumerable<DamageResult>> __result)
    {
        if (__state == null) return;
        try { __result = HbrEffectSkillDamageContext.Finish(__result, __state); }
        finally { HbrEffectSkillDamageContext.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception, HbrEffectSkillDamageContext.CommandScope __state)
    {
        if (__exception != null) HbrEffectSkillDamageContext.Close(__state);
        else HbrEffectSkillDamageContext.RestoreCaller(__state);
        return __exception;
    }
}
