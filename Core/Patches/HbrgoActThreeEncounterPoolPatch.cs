using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllEncounters),
    MethodType.Getter)]
internal static class HbrgoActThreeEncounterPoolPatch
{
    private const int ActThreeIndex = 2;

    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = AppendActThreeEncounters(__instance, __result);
    }

    internal static IEnumerable<EncounterModel> AppendActThreeEncounters(
        ActModel act,
        IEnumerable<EncounterModel> original,
        System.Func<EncounterModel, bool> predicate = null)
    {
        if (act.Index != ActThreeIndex)
        {
            return original;
        }

        List<EncounterModel> encounters = original.ToList();
        HashSet<ModelId> existingIds = encounters
            .Select(encounter => encounter.Id)
            .ToHashSet();
        encounters.AddRange(
            HbrgoActThreeEncounterRegistry.GetAll()
                .Where(encounter =>
                    predicate == null || predicate(encounter))
                .Where(encounter => existingIds.Add(encounter.Id)));
        return encounters;
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllBossEncounters),
    MethodType.Getter)]
internal static class HbrgoActThreeBossEncounterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = HbrgoActThreeEncounterPoolPatch
            .AppendActThreeEncounters(
                __instance,
                __result,
                encounter => encounter.RoomType == RoomType.Boss);
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllMonsters),
    MethodType.Getter)]
internal static class HbrgoActThreeMonsterPoolPatch
{
    private const int ActThreeIndex = 2;

    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<MonsterModel> __result)
    {
        if (__instance.Index != ActThreeIndex)
        {
            return;
        }

        List<MonsterModel> monsters = __result.ToList();
        HashSet<ModelId> existingIds = monsters
            .Select(monster => monster.Id)
            .ToHashSet();
        monsters.AddRange(
            HbrgoActThreeEncounterRegistry.GetAll()
                .SelectMany(encounter => encounter.AllPossibleMonsters)
                .Where(monster => existingIds.Add(monster.Id)));
        __result = monsters;
    }
}
