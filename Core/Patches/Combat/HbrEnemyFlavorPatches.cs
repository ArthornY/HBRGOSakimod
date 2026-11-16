using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsValidTarget))]
internal static class HbrProvokeCardTargetPatch
{
    private static void Postfix(CardModel __instance, Creature target, ref bool __result)
    { if (__result && !HbrProvokeTargetingHelper.IsAllowedTarget(__instance, target)) __result = false; }
}

// Synchronous re-evaluation before the native factory captures NextMove. The
// original async body, native attack and action/choice scheduling remain native.
[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
internal static class HbrEnemyFlavorBeforeMovePatch
{
    private static void Prefix(MonsterModel __instance)
    { if (__instance is HbrgoMoveTableMonster { HasFlavorMechanics: true }) HbrEnemyFlavorSchedulingHelper.RefreshOwner(__instance.Creature); }
}
