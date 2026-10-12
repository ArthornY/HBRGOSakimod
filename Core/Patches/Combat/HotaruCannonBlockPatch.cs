using System;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HotaruCannonBlockScopePatch
{
    private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(typeof(CreatureCmd),
        nameof(CreatureCmd.GainBlock), new[] { typeof(Creature), typeof(decimal), typeof(ValueProp), typeof(CardPlay), typeof(bool) })
        ?? throw new MissingMethodException(typeof(CreatureCmd).FullName, "GainBlock canonical decimal overload");

    [HarmonyPriority(Priority.First)]
    private static void Prefix(Creature creature, decimal amount, ValueProp props, CardPlay cardPlay,
        out HotaruCannonBlockHelper.GainScope __state) =>
        __state = HotaruCannonBlockHelper.Begin(creature, amount, props, cardPlay);

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(HotaruCannonBlockHelper.GainScope __state, ref Task<decimal> __result)
    {
        if (__state == null) return;
        try { __result = HotaruCannonBlockHelper.Finish(__result, __state); }
        finally { HotaruCannonBlockHelper.RestoreCaller(__state); }
    }

    private static Exception Finalizer(Exception __exception, HotaruCannonBlockHelper.GainScope __state)
    {
        if (__exception != null) HotaruCannonBlockHelper.Close(__state);
        else HotaruCannonBlockHelper.RestoreCaller(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyBlock))]
internal static class HotaruCannonBlockRoundingPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Creature target, ValueProp props, CardPlay cardPlay, ref decimal __result)
    {
        if (HotaruCannonBlockHelper.RoundsCurrentGain(target, props, cardPlay))
            __result = HotaruCannonBlockHelper.FinalCurrentGain(__result);
    }
}
