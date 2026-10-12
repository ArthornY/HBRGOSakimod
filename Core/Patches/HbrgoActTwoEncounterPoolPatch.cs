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
internal static class HbrgoActTwoEncounterPoolPatch
{
    private const int ActTwoIndex = 1;

    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = AppendActTwoEncounters(__instance, __result);
    }

    internal static IEnumerable<EncounterModel> AppendActTwoEncounters(
        ActModel act,
        IEnumerable<EncounterModel> original,
        System.Func<EncounterModel, bool> predicate = null)
    {
        if (act.Index != ActTwoIndex)
        {
            return original;
        }

        List<EncounterModel> encounters = original.ToList();
        HashSet<ModelId> existingIds = encounters
            .Select(encounter => encounter.Id)
            .ToHashSet();
        encounters.AddRange(
            HbrgoActTwoEncounterRegistry.GetAll()
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
internal static class HbrgoActTwoBossEncounterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = HbrgoActTwoEncounterPoolPatch
            .AppendActTwoEncounters(
                __instance,
                __result,
                encounter => encounter.RoomType == RoomType.Boss);
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllMonsters),
    MethodType.Getter)]
internal static class HbrgoActTwoMonsterPoolPatch
{
    private const int ActTwoIndex = 1;

    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<MonsterModel> __result)
    {
        if (__instance.Index != ActTwoIndex)
        {
            return;
        }

        List<MonsterModel> monsters = __result.ToList();
        HashSet<ModelId> existingIds = monsters
            .Select(monster => monster.Id)
            .ToHashSet();
        monsters.AddRange(
            HbrgoActTwoEncounterRegistry.GetAll()
                .SelectMany(encounter => encounter.AllPossibleMonsters)
                .Where(monster => existingIds.Add(monster.Id)));
        __result = monsters;
    }
}
