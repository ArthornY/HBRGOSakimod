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
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Patching;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch]
internal static class HotaruCannonDamageContextPatch
{
    private static System.Reflection.MethodBase TargetMethod() => NativeDamageApiCompatibility.Command;
    [HarmonyPriority(Priority.First)]
    private static void Prefix(object[] __args, decimal amount, ValueProp props,
        Creature dealer, CardModel cardSource, out HotaruCannonStatsHelper.CommandScope __state)
    {
        IEnumerable<Creature> originalTargets = NativeDamageApiCompatibility.ReadTargets(__args);
        IEnumerable<Creature> targets = originalTargets;
        __state = HotaruCannonStatsHelper.Begin(ref targets, amount, props, dealer, cardSource,
            __args.OfType<MegaCrit.Sts2.Core.Entities.Cards.CardPlay>().FirstOrDefault());
        NativeDamageApiCompatibility.WriteTargets(__args, originalTargets, targets);
    }
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(HotaruCannonStatsHelper.CommandScope __state, ref Task<IEnumerable<DamageResult>> __result)
    {
        if (__state == null) return;
        try { __result = HotaruCannonStatsHelper.Finish(__result, __state); }
        finally { HotaruCannonStatsHelper.RestoreCaller(__state); }
    }
    private static Exception Finalizer(Exception __exception, HotaruCannonStatsHelper.CommandScope __state)
    {
        if (__exception != null) HotaruCannonStatsHelper.Close(__state);
        else HotaruCannonStatsHelper.RestoreCaller(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class HotaruCannonFinalDamageFloorPatch
{
    private static MethodBase TargetMethod() => PrivateAccess.Method(typeof(Hook), "ModifyDamageInternal");
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Creature target, Creature dealer, CardModel cardSource, ValueProp props,
        ModifyDamageHookType modifyDamageHookType, ref decimal __result)
    {
        if (modifyDamageHookType == ModifyDamageHookType.All &&
            HotaruCannonStatsHelper.IsCurrentDamage(target, dealer, cardSource, props))
            __result = decimal.Floor(Math.Max(0m, __result));
        if (modifyDamageHookType == ModifyDamageHookType.All)
            HotaruCannonStatsHelper.RecordFinalDamage(target, dealer, cardSource, __result);
    }
}

[HarmonyPatch(typeof(ItsMygoIncomingDamageCapture), nameof(ItsMygoIncomingDamageCapture.AppendResults))]
internal static class HotaruCannonResultReceiptPatch
{
    private static void Prefix(ref IEnumerable<DamageResult> __1)
    {
        if (!HotaruCannonStatsHelper.CapturesCurrentResults) return;
        IReadOnlyList<DamageResult> hits = __1 as IReadOnlyList<DamageResult> ?? __1.ToArray();
        __1 = hits;
        HotaruCannonStatsHelper.BindReceipts(hits);
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class HotaruCannonAfterHitUpgradePatch
{
    private static void Postfix(PlayerChoiceContext __0, DamageResult __3, ref Task __result)
    {
        if (HotaruCannonStatsHelper.TryTakeReceipt(__3, out CannonHitReceipt receipt))
            __result = HotaruCannonStatsHelper.FinishAfterDamageGiven(__result, __0, receipt);
    }
}

[HarmonyPatch(typeof(HpOnlyDamageContext), nameof(HpOnlyDamageContext.RecordDpAbsorption))]
internal static class HotaruCannonDpAbsorptionPatch
{
    private static void Postfix(Creature __0, decimal __1) =>
        HotaruCannonStatsHelper.RecordDpAbsorption(__0, __1);
}
