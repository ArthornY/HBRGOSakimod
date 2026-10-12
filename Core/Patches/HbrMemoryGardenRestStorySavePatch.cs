using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
internal static class HbrMemoryGardenRestStorySavePatch
{
    private static void Postfix(RunState __result) =>
        HbrMemoryGardenRestStory.RestoreObtainedCards(__result);
}
