using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrgoActFourEncounterPool
{
    internal const int ActIndex = 3;

    internal static IEnumerable<EncounterModel> Append(
        ActModel act,
        IEnumerable<EncounterModel> original)
    {
        if (act.Index != ActIndex)
        {
            return original;
        }

        List<EncounterModel> encounters = original.ToList();
        HashSet<ModelId> existingIds = encounters
            .Select(encounter => encounter.Id)
            .ToHashSet();
        encounters.AddRange(
            HbrgoActFourEncounterRegistry.GetUnlocked()
                .Where(encounter => existingIds.Add(encounter.Id)));
        return encounters;
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllEncounters),
    MethodType.Getter)]
internal static class HbrgoActFourAllEncounterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = HbrgoActFourEncounterPool.Append(
            __instance,
            __result);
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllBossEncounters),
    MethodType.Getter)]
internal static class HbrgoActFourBossEncounterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = HbrgoActFourEncounterPool.Append(
            __instance,
            __result);
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllMonsters),
    MethodType.Getter)]
internal static class HbrgoActFourMonsterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<MonsterModel> __result)
    {
        if (__instance.Index != HbrgoActFourEncounterPool.ActIndex)
        {
            return;
        }

        List<MonsterModel> monsters = __result.ToList();
        HashSet<ModelId> existingIds = monsters
            .Select(monster => monster.Id)
            .ToHashSet();
        monsters.AddRange(
            HbrgoActFourEncounterRegistry.GetUnlocked()
                .SelectMany(encounter => encounter.AllPossibleMonsters)
                .Where(monster => existingIds.Add(monster.Id)));
        __result = monsters;
    }
}
