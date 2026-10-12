using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HbrgoSakiAdditiveDamagePatch
{
    private static MethodBase TargetMethod() => PrivateAccess.Method(typeof(Hook), "ModifyDamageInternal");

    private static bool Prefix(
        IRunState runState,
        ICombatState combatState,
        Creature target,
        Creature dealer,
        decimal damage,
        ValueProp props,
        CardModel cardSource,
        ModifyDamageHookType modifyDamageHookType,
        object[] __args,
        ref decimal __result,
        ref List<AbstractModel> modifiers)
    {
        // Stable and beta expose different hook signatures. Extract the optional
        // play once, then keep all actor calculations in the common pipeline.
        CardPlay cardPlay = null;
        foreach (object argument in __args)
        {
            if (argument is not CardPlay play) continue;
            cardPlay = play;
            break;
        }
        __result = HbrCombatDamagePipeline.Calculate(runState, combatState, target, dealer,
            damage, props, cardSource, modifyDamageHookType, cardPlay, out modifiers);
        return false;
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ref List<AbstractModel> modifiers)
    {
        // Multi-target previews merge this output with AddRange.
        modifiers ??= new List<AbstractModel>();
    }
}
