using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Compatibility;

internal static class NativeActApiCompatibility
{
    private static readonly MethodInfo GenerateFromRun =
        typeof(ActModel).GetMethod("GenerateRooms", [typeof(RunState)]);
    private static readonly MethodInfo LegacyGenerate = GenerateFromRun == null
        ? typeof(ActModel).GetMethod("GenerateRooms") ?? throw new MissingMethodException(typeof(ActModel).FullName, "GenerateRooms")
        : null;

    internal static void GenerateRoomsCompat(this ActModel act, RunState run)
    {
        if (GenerateFromRun != null)
            CompatibilityReflection.Invoke(GenerateFromRun, act, [run]);
        else
            CompatibilityReflection.Invoke(LegacyGenerate, act,
                [run.Rng.UpFront, run.UnlockState, run.Players.Count > 1]);
    }
}
