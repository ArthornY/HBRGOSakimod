using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Hook.AfterDeath is an ordinary, non-generic Task entry in both profiles.
// Leave native listener dispatch and death completion intact, then account the
// single confirmed death for all eligible permanent cards and physical copies.
[HarmonyPatch]
internal static class TengenBrilliantMoveDeathPatch
{
    private static MethodBase TargetMethod() => typeof(Hook).GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == "AfterDeath" && !method.IsGenericMethod && method.ReturnType == typeof(Task) &&
            method.GetParameters() is var args && args.Length is 5 or 6 &&
            args.Count(p => p.ParameterType == typeof(IRunState)) == 1 &&
            args.Count(p => p.ParameterType == typeof(ICombatState)) == 1 &&
            args.Count(p => p.ParameterType == typeof(Creature)) == 1 &&
            args.Count(p => p.ParameterType == typeof(bool)) == 1 &&
            args.Count(p => p.ParameterType == typeof(float)) == 1 &&
            args.Count(p => p.ParameterType == typeof(CardModel)) == args.Length - 5);
    private static void Prefix(object[] __args, out TengenBrilliantMoveCombatHelper.DeathReceipt __state) =>
        __state = TengenBrilliantMoveCombatHelper.CaptureDeath(__args.OfType<ICombatState>().Single(),
            __args.OfType<Creature>().Single(), __args.OfType<bool>().Single());
    private static void Postfix(TengenBrilliantMoveCombatHelper.DeathReceipt __state, ref Task __result)
    { if (__state != null) __result = Finish(__result, __state); }
    private static async Task Finish(Task native, TengenBrilliantMoveCombatHelper.DeathReceipt receipt)
    { await native; TengenBrilliantMoveCombatHelper.ConfirmDeath(receipt); }
}
