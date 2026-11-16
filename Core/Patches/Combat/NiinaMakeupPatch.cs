using System;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Powers;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
internal static class NiinaMakeupAttackPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(AttackCommand __instance, out NiinaMakeupCombatHelper.AttackScope __state) =>
        __state = NiinaMakeupCombatHelper.BeginAttack(__instance);
    private static void Postfix(NiinaMakeupCombatHelper.AttackScope __state, ref Task<AttackCommand> __result)
    {
        if (__state == null) return;
        try { __result = NiinaMakeupCombatHelper.Finish(__result, __state); }
        finally { NiinaMakeupCombatHelper.RestoreCaller(__state); }
    }
    private static Exception Finalizer(Exception __exception, NiinaMakeupCombatHelper.AttackScope __state)
    { if (__exception != null) __state?.Dispose(); else NiinaMakeupCombatHelper.RestoreCaller(__state); return __exception; }
}
[HarmonyPatch(typeof(DeflectorProtection), nameof(DeflectorProtection.Dp), MethodType.Setter)]
internal static class NiinaMakeupDeflectorPatch
{
    private static void Postfix(DeflectorProtection __instance)
    { if (__instance.IsMutable && __instance.Dp <= 0) NiinaMakeupCombatHelper.OnDpChanged(__instance.Owner?.Creature); }
}
[HarmonyPatch(typeof(HighDimensionalShellPower), "FinishNativeDpWrite")]
internal static class NiinaMakeupShellPatch
{
    private static void Postfix(HighDimensionalShellPower __instance)
    { if (__instance.CurrentDp <= 0m) NiinaMakeupCombatHelper.OnDpChanged(__instance.Owner); }
}
[HarmonyPatch(typeof(HighDimensionalShellPower), nameof(HighDimensionalShellPower.AfterRemoved))]
internal static class NiinaMakeupShellRemovedPatch
{ private static void Prefix(Creature oldOwner) => NiinaMakeupCombatHelper.OnDpChanged(oldOwner); }
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterDamageGiven))]
internal static class NiinaSakuraRecoveryPatch
{
    private static void Postfix(Creature dealer, DamageResult results, ValueProp props, Creature target,
        CardModel cardSource, ref Task __result)
    {
        if (NiinaNarcissismCombatHelper.IsOwnAttack(cardSource, dealer, target, props)) __result = Finish(__result, cardSource, results);
    }
    private static async Task Finish(Task native, CardModel card, DamageResult result)
    { await native; NiinaNarcissismCombatHelper.RecordDamage(card, result); }
}
