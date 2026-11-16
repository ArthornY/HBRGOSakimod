using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HBRGOSakiMod.Core.Patches.Combat;

// This native relic deliberately loops over single-target commands, rather
// than calling Apply(IEnumerable). Give its one poison volley one batch too.
[HarmonyPatch(typeof(TwistedFunnel), nameof(TwistedFunnel.BeforeSideTurnStart))]
internal static class ThirtyOneAYukiTwistedFunnelPatch
{
    private static void Prefix(TwistedFunnel __instance, out HbrDebuffApplicationScope.Batch __state)
    {
        var owner = __instance.Owner?.Creature;
        __state = owner?.GetPower<ThirtyOneAYukiDebuffPower>()?.Amount > 0
            ? HbrDebuffApplicationScope.BeginBatch(owner) : null;
    }

    private static void Postfix(HbrDebuffApplicationScope.Batch __state, ref Task __result)
    {
        if (__state == null) return;
        try { __result = Finish(__result, __state); }
        finally { HbrDebuffApplicationScope.RestoreCaller(__state); }
    }

    private static async Task Finish(Task task, HbrDebuffApplicationScope.Batch batch)
    {
        try { await task; }
        finally { batch.Dispose(); }
    }

    private static Exception Finalizer(Exception __exception, HbrDebuffApplicationScope.Batch __state)
    {
        if (__exception != null) __state?.Dispose();
        else HbrDebuffApplicationScope.RestoreCaller(__state);
        return __exception;
    }
}
