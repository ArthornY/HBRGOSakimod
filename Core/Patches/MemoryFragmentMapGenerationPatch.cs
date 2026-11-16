using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Map;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.GenerateRooms))]
internal static class MemoryFragmentRoomGenerationScopePatch
{
    private static void Prefix(RunManager __instance, out IDisposable __state) =>
        __state = MemoryFragmentMapHelper.BeginGeneration(__instance.DebugOnlyGetState());
    private static void Postfix(IDisposable __state) => __state?.Dispose();
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.CreateMap))]
internal static class MemoryFragmentMapGenerationScopePatch
{
    private static void Prefix(RunState runState, out IDisposable __state) =>
        __state = MemoryFragmentMapHelper.BeginGeneration(runState);
    private static void Postfix(IDisposable __state) => __state?.Dispose();
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyGeneratedMap))]
internal static class MemoryFragmentModifiedMapGenerationScopePatch
{
    private static void Prefix(IRunState runState, out IDisposable __state) =>
        __state = MemoryFragmentMapHelper.BeginGeneration(runState);
    private static void Postfix(IDisposable __state) => __state?.Dispose();
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(ActModel), nameof(ActModel.GetNumberOfRooms))]
internal static class MemoryFragmentExtraRowsPatch
{
    private static void Postfix(ActModel __instance, ref int __result)
    {
        if (MemoryFragmentMapHelper.ShouldExtendAct(__instance))
            __result += MemoryFragmentMapHelper.ExtraRows;
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterMapGenerated))]
internal static class MemoryFragmentMapMarkersPatch
{
    private static void Postfix(IRunState runState, ActMap map, int actIndex, ref Task __result) =>
        __result = ConfigureAfterGeneration(__result, runState, map, actIndex);

    private static async Task ConfigureAfterGeneration(Task original, IRunState runState, ActMap map, int actIndex)
    {
        await original;
        MemoryFragmentMapHelper.ConfigureMap(runState, map, actIndex);
        HbrThirtyOneDScoutingHelper.EnsureScouted(runState, map, actIndex);
    }
}

[HarmonyPatch(typeof(GoldenPathActMap), MethodType.Constructor, new[] { typeof(IRunState) })]
internal static class MemoryFragmentGoldenPathRowsPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (instruction.operand is MethodInfo method && method.Name == "ToList" &&
                method.DeclaringType == typeof(System.Linq.Enumerable) && method.IsGenericMethod &&
                method.GetGenericArguments()[0] == typeof(MapPointType))
            {
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Call,
                    AccessTools.Method(typeof(MemoryFragmentGoldenPathRowsPatch), nameof(ExtendRoute)));
            }
        }
    }

    private static List<MapPointType> ExtendRoute(List<MapPointType> points, IRunState runState)
    {
        if (!MemoryFragmentMapHelper.IsEnabled(runState) || runState.Act?.Index is not (>= 0 and <= 2))
            return points;

        // Golden Path has a fixed list instead of calling GetNumberOfRooms. Add
        // after index two so its native multiplayer RemoveAt(2) remains intact.
        points.Insert(Math.Min(3, points.Count), MapPointType.Unknown);
        points.Insert(Math.Max(0, points.Count - 1), MapPointType.Monster);
        return points;
    }
}
