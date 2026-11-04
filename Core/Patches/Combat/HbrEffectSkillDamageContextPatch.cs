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
internal static class HbrEffectSkillDamageContextPatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CreatureCmd))
        .Where(method => method.Name == nameof(CreatureCmd.Damage))
        .Where(method => method.GetParameters() is var p && p.Length is 6 or 7 &&
            p[0].ParameterType == typeof(PlayerChoiceContext) &&
            p[1].ParameterType == typeof(IEnumerable<Creature>) && p[2].ParameterType == typeof(decimal) &&
            p[3].ParameterType == typeof(ValueProp) && p[4].ParameterType == typeof(Creature) &&
            p[5].ParameterType == typeof(CardModel))
        .OrderByDescending(method => method.GetParameters().Length).First();

    [HarmonyPriority(Priority.First)]
    private static void Prefix(ref IEnumerable<Creature> targets, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource, out HbrEffectSkillDamageContext.CommandScope __state) =>
        __state = HbrEffectSkillDamageContext.BeginCommand(ref targets, amount, props, dealer, cardSource);

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
