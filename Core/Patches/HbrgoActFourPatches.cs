using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Map;
using HBRGOSakiMod.Core.Models.Acts;
using HBRGOSakiMod.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Entities.Ascension;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace HBRGOSakiMod.Core.Patches;

internal static class HbrgoActFourRegistration
{
    internal static ActModel GetAct()
    {
        return ModelDb.Act<HbrgoActFour>();
    }
}

[HarmonyPatch]
internal static class HbrgoActFourMultiplayerScalingPatch
{
    private static MethodBase TargetMethod()
    {
        Assembly game = typeof(ModelDb).Assembly;
        System.Type model = game.GetType("MegaCrit.Sts2.Core.Models.MultiplayerScalingModel") ??
            game.GetType("MegaCrit.Sts2.Core.Models.Singleton.MultiplayerScalingModel", throwOnError: true);
        return AccessTools.Method(model, "GetMultiplayerScaling",
            [typeof(EncounterModel), typeof(int)]);
    }

    private static bool Prefix(
        EncounterModel encounter,
        int actIndex,
        ref decimal __result)
    {
        if (actIndex <= 2)
        {
            return true;
        }

        __result = encounter?.RoomType == RoomType.Boss
            ? 1.3m
            : 1.2m;
        return false;
    }
}

[HarmonyPatch(
    typeof(ModelDb),
    nameof(ModelDb.Acts),
    MethodType.Getter)]
internal static class HbrgoActFourActsPatch
{
    private static void Postfix(ref IEnumerable<ActModel> __result)
    {
        List<ActModel> acts = __result.ToList();
        ActModel actFour = HbrgoActFourRegistration.GetAct();
        if (acts.All(act => act.Id != actFour.Id))
        {
            acts.Add(actFour);
        }

        __result = acts
            .OrderBy(act => act.Index)
            .ThenByDescending(act => act.IsDefault)
            .ToList();
    }
}

[HarmonyPatch(
    typeof(ModelDb),
    nameof(ModelDb.ActsByIndex),
    MethodType.Getter)]
internal static class HbrgoActFourActsByIndexPatch
{
    private static void Postfix(
        ref IReadOnlyList<IReadOnlyList<ActModel>> __result)
    {
        List<List<ActModel>> actsByIndex = __result
            .Select(acts => acts.ToList())
            .ToList();
        while (actsByIndex.Count <= HbrgoActFourEncounterPool.ActIndex)
        {
            actsByIndex.Add(new List<ActModel>());
        }

        ActModel actFour = HbrgoActFourRegistration.GetAct();
        List<ActModel> fourthActOptions =
            actsByIndex[HbrgoActFourEncounterPool.ActIndex];
        if (fourthActOptions.All(act => act.Id != actFour.Id))
        {
            fourthActOptions.Add(actFour);
        }

        __result = actsByIndex
            .Select(acts => (IReadOnlyList<ActModel>)acts)
            .ToList();
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
internal static class HbrgoActFourDoubleBossPatch
{
    private const int ActThreeIndex = 2;

    private static bool Prefix(RunManager __instance)
    {
        RunState runState = __instance.DebugOnlyGetState();
        if (runState == null ||
            runState.Acts.All(act => act is not HbrgoActFour))
        {
            return true;
        }

        List<AncientEventModel> sharedAncients = runState.UnlockState
            .SharedAncients
            .ToList()
            .UnstableShuffle(runState.Rng.UpFront);
        foreach (ActModel act in runState.Acts.Skip(1))
        {
            int count = runState.Rng.UpFront.NextInt(
                sharedAncients.Count + 1);
            List<AncientEventModel> subset = sharedAncients
                .Take(count)
                .ToList();
            sharedAncients = sharedAncients.Except(subset).ToList();
            act.SetSharedAncientSubset(subset);
        }

        foreach (ActModel act in runState.Acts)
        {
            act.GenerateRoomsCompat(runState);
            if (__instance.ShouldApplyTutorialModifications())
            {
                act.ApplyDiscoveryOrderModifications(runState.UnlockState);
            }
        }

        ActModel actThree = runState.Acts.FirstOrDefault(
            act => act.Index == ActThreeIndex);
        if (actThree != null &&
            __instance.AscensionManager.HasLevel(
                AscensionLevel.DoubleBoss))
        {
            List<EncounterModel> candidates = actThree.AllBossEncounters
                .Where(encounter => encounter.Id != actThree.BossEncounter.Id)
                .ToList();
            if (candidates.Count > 0)
            {
                actThree.SetSecondBossEncounter(
                    runState.Rng.UpFront.NextItem(candidates));
            }
        }

        HbrgoActFour actFour = runState.Acts
            .OfType<HbrgoActFour>()
            .FirstOrDefault();
        actFour?.SetSecondBossEncounter(null);
        return false;
    }
}

[HarmonyPatch]
internal static class HbrgoActFourFirstSkullFeatherBossPatch
{
    private static MethodBase TargetMethod() => typeof(ActModel)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(ActModel.GenerateRooms) &&
            method.GetParameters()[0].ParameterType == typeof(Rng));

    private static void Postfix(ActModel __instance)
    {
        if (__instance is not HbrgoActFour actFour ||
            !HbrgoSakiVariantPreference
                .ShouldForceSkullFeatherNextActFour())
        {
            return;
        }

        EncounterModel skullFeather =
            ModelDb.Encounter<SkullFeatherBoss>();
        if (actFour.AllBossEncounters.All(
                encounter => encounter.Id != skullFeather.Id))
        {
            return;
        }

        actFour.SetBossEncounter(skullFeather);
        HbrgoSakiVariantPreference
            .ConsumeForcedSkullFeatherActFour();
    }
}

[HarmonyPatch(typeof(RoomSet), nameof(RoomSet.FromSave))]
internal static class HbrgoActFourRoomSetSaveCompatibilityPatch
{
    private static void Prefix(SerializableRoomSet save)
    {
        save.EventIds ??= new List<ModelId>();
        save.NormalEncounterIds ??= new List<ModelId>();
        save.EliteEncounterIds ??= new List<ModelId>();
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.CreateMap))]
internal static class HbrgoActFourMapCreationPatch
{
    private static bool Prefix(
        ActModel __instance,
        ref ActMap __result)
    {
        if (__instance is not HbrgoActFour)
        {
            return true;
        }

        __result = new HbrgoActFourMap();
        return false;
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.Ancient),
    MethodType.Getter)]
internal static class HbrgoActFourAncientPatch
{
    private static void Prefix(ActModel __instance)
    {
        if (__instance is HbrgoActFour actFour)
        {
            actFour.EnsureChihayaAnonTogawaSakikoAncient();
        }
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.Title),
    MethodType.Getter)]
internal static class HbrgoActTitlesPatch
{
    private static void Postfix(
        ActModel __instance,
        ref LocString __result)
    {
        string key = __instance.Index switch
        {
            0 => "HBRGO_ACT_ONE.title",
            1 => "HBRGO_ACT_TWO.title",
            2 => "HBRGO_ACT_THREE.title",
            3 => "HBRGO_ACT_FOUR.title",
            _ => null
        };
        if (key != null)
        {
            __result = new LocString("acts", key);
        }
    }
}

internal static class HbrgoActFourAssetPaths
{
    internal static bool AppliesTo(ActModel act)
    {
        return act is HbrgoActFour;
    }

    internal static string BackgroundScenePath =>
        SceneHelper.GetScenePath(
            "backgrounds/glory/glory_background");

    internal static string RestSiteBackgroundPath =>
        SceneHelper.GetScenePath("rest_site/glory_rest_site");

    internal static string MapTopPath =>
        ImageHelper.GetImagePath(
            "packed/map/map_bgs/glory/map_top_glory.png");

    internal static string MapMiddlePath =>
        ImageHelper.GetImagePath(
            "packed/map/map_bgs/glory/map_middle_glory.png");

    internal static string MapBottomPath =>
        ImageHelper.GetImagePath(
            "packed/map/map_bgs/glory/map_bottom_glory.png");
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.BackgroundScenePath),
    MethodType.Getter)]
internal static class HbrgoActFourBackgroundScenePatch
{
    private static void Postfix(
        ActModel __instance,
        ref string __result)
    {
        if (HbrgoActFourAssetPaths.AppliesTo(__instance))
        {
            __result = HbrgoActFourAssetPaths.BackgroundScenePath;
        }
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.RestSiteBackgroundPath),
    MethodType.Getter)]
internal static class HbrgoActFourRestSiteBackgroundPatch
{
    private static void Postfix(
        ActModel __instance,
        ref string __result)
    {
        if (HbrgoActFourAssetPaths.AppliesTo(__instance))
        {
            __result = HbrgoActFourAssetPaths.RestSiteBackgroundPath;
        }
    }
}

[HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter._Ready))]
internal static class HbrgoActFourRestSiteCharacterReadyPatch
{
    private static readonly FieldInfo CurrentActIndexField =
        AccessTools.Field(typeof(RunState), "_currentActIndex");

    private static void Prefix(
        NRestSiteCharacter __instance,
        out RestSiteActIndexState __state)
    {
        __state = default;
        RunState runState = __instance.Player?.RunState as RunState;
        if (runState == null ||
            runState.CurrentActIndex < HbrgoActFourEncounterPool.ActIndex ||
            runState.Act is not HbrgoActFour)
        {
            return;
        }

        __state = new RestSiteActIndexState(
            runState,
            runState.CurrentActIndex);
        CurrentActIndexField.SetValue(runState, 2);
    }

    private static void Postfix(RestSiteActIndexState __state)
    {
        RestoreActIndex(__state);
    }

    private static System.Exception Finalizer(
        System.Exception __exception,
        RestSiteActIndexState __state)
    {
        RestoreActIndex(__state);
        return __exception;
    }

    private static void RestoreActIndex(RestSiteActIndexState state)
    {
        if (state.RunState != null)
        {
            CurrentActIndexField.SetValue(
                state.RunState,
                state.OriginalActIndex);
        }
    }

    private readonly struct RestSiteActIndexState
    {
        internal RestSiteActIndexState(
            RunState runState,
            int originalActIndex)
        {
            RunState = runState;
            OriginalActIndex = originalActIndex;
        }

        internal RunState RunState { get; }

        internal int OriginalActIndex { get; }
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.MapTopBgPath),
    MethodType.Getter)]
internal static class HbrgoActFourMapTopPatch
{
    private static void Postfix(
        ActModel __instance,
        ref string __result)
    {
        if (HbrgoActFourAssetPaths.AppliesTo(__instance))
        {
            __result = HbrgoActFourAssetPaths.MapTopPath;
        }
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.MapMidBgPath),
    MethodType.Getter)]
internal static class HbrgoActFourMapMiddlePatch
{
    private static void Postfix(
        ActModel __instance,
        ref string __result)
    {
        if (HbrgoActFourAssetPaths.AppliesTo(__instance))
        {
            __result = HbrgoActFourAssetPaths.MapMiddlePath;
        }
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.MapBotBgPath),
    MethodType.Getter)]
internal static class HbrgoActFourMapBottomPatch
{
    private static void Postfix(
        ActModel __instance,
        ref string __result)
    {
        if (HbrgoActFourAssetPaths.AppliesTo(__instance))
        {
            __result = HbrgoActFourAssetPaths.MapBottomPath;
        }
    }
}

[HarmonyPatch(
    typeof(ActModel),
    nameof(ActModel.GenerateBackgroundAssets))]
internal static class HbrgoActFourBackgroundAssetsPatch
{
    private static bool Prefix(
        ActModel __instance,
        Rng rng,
        ref BackgroundAssets __result)
    {
        if (!HbrgoActFourAssetPaths.AppliesTo(__instance))
        {
            return true;
        }

        __result = new BackgroundAssets("glory", rng);
        return false;
    }
}
