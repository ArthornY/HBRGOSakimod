using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Config;
using HBRGOSakiMod.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllEncounters),
    MethodType.Getter)]
internal static class HbrgoActOneEncounterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = AppendActOneEncounters(__instance, __result);
    }

    internal static IEnumerable<EncounterModel> AppendActOneEncounters(
        ActModel act,
        IEnumerable<EncounterModel> original,
        System.Func<EncounterModel, bool> predicate = null)
    {
        if (act.Index != 0)
        {
            return original;
        }

        List<EncounterModel> encounters = original.ToList();
        HashSet<ModelId> existingIds = encounters
            .Select(encounter => encounter.Id)
            .ToHashSet();
        encounters.AddRange(
            HbrgoActOneEncounterRegistry.GetAll()
                .Where(encounter => predicate == null || predicate(encounter))
                .Where(encounter => existingIds.Add(encounter.Id)));
        return encounters;
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllWeakEncounters),
    MethodType.Getter)]
internal static class HbrgoActOneWeakEncounterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = HbrgoActOneEncounterPoolPatch.AppendActOneEncounters(
            __instance,
            __result,
            encounter =>
                encounter.RoomType == RoomType.Monster &&
                encounter.IsWeak);
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllRegularEncounters),
    MethodType.Getter)]
internal static class HbrgoActOneRegularEncounterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = HbrgoActOneEncounterPoolPatch.AppendActOneEncounters(
            __instance,
            __result,
            encounter =>
                encounter.RoomType == RoomType.Monster &&
                !encounter.IsWeak);
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllEliteEncounters),
    MethodType.Getter)]
internal static class HbrgoActOneEliteEncounterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = HbrgoActOneEncounterPoolPatch.AppendActOneEncounters(
            __instance,
            __result,
            encounter => encounter.RoomType == RoomType.Elite);
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllBossEncounters),
    MethodType.Getter)]
internal static class HbrgoActOneBossEncounterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<EncounterModel> __result)
    {
        __result = HbrgoActOneEncounterPoolPatch.AppendActOneEncounters(
            __instance,
            __result,
            encounter => encounter.RoomType == RoomType.Boss);
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.AllMonsters),
    MethodType.Getter)]
internal static class HbrgoActOneMonsterPoolPatch
{
    private static void Postfix(
        ActModel __instance,
        ref IEnumerable<MonsterModel> __result)
    {
        if (__instance.Index != 0)
        {
            return;
        }

        List<MonsterModel> monsters = __result.ToList();
        HashSet<ModelId> existingIds = monsters
            .Select(monster => monster.Id)
            .ToHashSet();
        monsters.AddRange(
            HbrgoActOneEncounterRegistry.GetAll()
                .SelectMany(encounter => encounter.AllPossibleMonsters)
                .Where(monster => existingIds.Add(monster.Id)));
        __result = monsters;
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
internal static class HbrgoEliteBossEncounterConfigPatch
{
    private static readonly FieldInfo RoomsField = AccessTools.Field(
        typeof(ActModel),
        "_rooms");
    private static readonly PropertyInfo StateProperty =
        AccessTools.Property(typeof(RunManager), "State");

    private static void Postfix(RunManager __instance)
    {
        if (!HBRGOSakiConfig.ForceModEliteBossEncounters &&
            !HBRGOSakiConfig.ForceModBossEncounters)
        {
            return;
        }

        if (StateProperty.GetValue(__instance) is not RunState state)
        {
            return;
        }

        Rng rng = state.Rng.UpFront;
        foreach (ActModel act in state.Acts)
        {
            if (HBRGOSakiConfig.ForceModEliteBossEncounters)
            {
                ForceModElites(act, rng);
            }

            if (HBRGOSakiConfig.ForceModBossEncounters)
            {
                ForceModBosses(act, rng);
            }
        }
    }

    private static void ForceModElites(ActModel act, Rng rng)
    {
        List<EncounterModel> modElites = act.AllEliteEncounters
            .Where(encounter => encounter is IHbrgoEncounter)
            .ToList();
        if (modElites.Count == 0 ||
            RoomsField.GetValue(act) is not RoomSet rooms)
        {
            return;
        }

        int encounterCount = rooms.eliteEncounters.Count;
        rooms.eliteEncounters.Clear();
        List<EncounterModel> bag = new(modElites);
        for (int i = 0; i < encounterCount; i++)
        {
            if (i % bag.Count == 0)
            {
                rng.ShuffleCompat(bag);
            }

            rooms.eliteEncounters.Add(bag[i % bag.Count]);
        }
    }

    private static void ForceModBosses(ActModel act, Rng rng)
    {
        List<EncounterModel> allBosses = act.AllBossEncounters.ToList();
        List<EncounterModel> modBosses = allBosses
            .Where(encounter => encounter is IHbrgoEncounter)
            .ToList();
        if (modBosses.Count == 0)
        {
            return;
        }

        EncounterModel boss = rng.NextItem(modBosses);
        act.SetBossEncounter(boss);
        if (!act.HasSecondBoss)
        {
            return;
        }

        List<EncounterModel> secondBossCandidates = modBosses
            .Where(encounter => encounter.Id != boss.Id)
            .ToList();
        if (secondBossCandidates.Count == 0)
        {
            secondBossCandidates = allBosses
                .Where(encounter => encounter is not IHbrgoEncounter)
                .Where(encounter => encounter.Id != boss.Id)
                .ToList();
        }

        if (secondBossCandidates.Count == 0)
        {
            secondBossCandidates = allBosses
                .Where(encounter => encounter.Id != boss.Id)
                .ToList();
        }

        act.SetSecondBossEncounter(
            secondBossCandidates.Count > 0
                ? rng.NextItem(secondBossCandidates)
                : null);
    }
}
