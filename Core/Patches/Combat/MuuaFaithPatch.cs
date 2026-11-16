using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(DeflectorProtection), nameof(DeflectorProtection.Dp), MethodType.Setter)]
internal static class MuuaFaithDeflectorPatch
{
    private static void Prefix(DeflectorProtection __instance, out int __state) => __state = __instance.Dp;
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(DeflectorProtection __instance, int __state)
    { if (__state > 0 && __instance.Dp <= 0) MuuaFaithCombatHelper.OnDpZero(__instance.Owner?.Creature, __state); }
}
[HarmonyPatch(typeof(HighDimensionalShellPower), "FinishNativeDpWrite")]
internal static class MuuaFaithShellPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(HighDimensionalShellPower __instance, HighDimensionalShellPower.NativeDpWrite write)
    { if (write?.Previous > 0m && __instance.CurrentDp <= 0m) MuuaFaithCombatHelper.OnDpZero(__instance.Owner, write.Previous); }
}
[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Remove), new[] { typeof(PowerModel) })]
internal static class MuuaFaithShellRemovalPatch
{
    private static void Prefix(PowerModel power, out MuuaFaithCombatHelper.RemovalScope __state) =>
        __state = MuuaFaithCombatHelper.BeginRemoval(power);
    private static void Postfix(MuuaFaithCombatHelper.RemovalScope __state, ref Task __result)
    {
        if (__state == null) return;
        try { __result = MuuaFaithCombatHelper.FinishRemoval(__result, __state); }
        finally { MuuaFaithCombatHelper.RestoreRemovalCaller(__state); }
    }
    private static Exception Finalizer(Exception __exception, MuuaFaithCombatHelper.RemovalScope __state)
    { if (__exception != null) MuuaFaithCombatHelper.CloseRemoval(__state); else MuuaFaithCombatHelper.RestoreRemovalCaller(__state); return __exception; }
}
[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.SetAmount))]
internal static class FaithNativeCounterPatch
{
    private static void Prefix(PowerModel __instance, out int __state) => __state = __instance is HbrFaithPower ? __instance.Amount : 0;
    private static void Postfix(PowerModel __instance, int __state)
    { if (__instance is HbrFaithPower faith) faith.SyncNativeAmount(__state); }
}
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCombatEnd))]
internal static class MuuaFaithCombatEndPatch
{ private static void Prefix(ICombatState combatState) => MuuaFaithCombatHelper.Clear(combatState); }
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
internal static class MuuaFaithResetPatch
{ private static void Prefix(CombatManager __instance) => MuuaFaithCombatHelper.Clear(__instance.DebugOnlyGetState()); }
