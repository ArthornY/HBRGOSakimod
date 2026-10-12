using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HBRGOSakiMod.Core.Patches.Combat;

// Older combat states can still contain sleep inherited before the transfer
// filter existed. Remove it without calling Lagavulin's wake moves or animation.
[HarmonyPatch(typeof(AsleepPower), nameof(AsleepPower.AfterDamageReceived))]
internal static class RedCrimsonAsleepDamagePatch
{
    private static bool Prefix(
        AsleepPower __instance,
        Creature target,
        ref Task __result)
    {
        if (__instance.Owner?.Monster is not RedCrimsonBossMonster ||
            target != __instance.Owner)
        {
            return true;
        }

        __result = PowerCmd.Remove(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class RedCrimsonAsleepTurnEndPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Clean up before native sleep can also remove inherited Plating.
        yield return AccessTools.Method(
            typeof(AsleepPower), nameof(AsleepPower.BeforeSideTurnEndVeryEarly));
        yield return AccessTools.Method(
            typeof(AsleepPower), nameof(AsleepPower.AfterSideTurnEnd));
    }

    private static bool Prefix(
        AsleepPower __instance,
        IEnumerable<Creature> participants,
        ref Task __result)
    {
        if (__instance.Owner?.Monster is not RedCrimsonBossMonster ||
            !participants.Contains(__instance.Owner))
        {
            return true;
        }

        __result = PowerCmd.Remove(__instance);
        return false;
    }
}
