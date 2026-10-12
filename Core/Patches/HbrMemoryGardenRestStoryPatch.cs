using System.Threading.Tasks;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(RestSiteRoom), nameof(RestSiteRoom.EnterInternal))]
internal static class HbrMemoryGardenRestStoryEnterPatch
{
    private static void Prefix(RestSiteRoom __instance, IRunState __0, bool __1,
        out HbrMemoryGardenRestStoryPresentation.Entry __state)
    {
        __state = HbrMemoryGardenRestStoryPresentation.CaptureEntry(__0, __instance, __1);
    }

    private static void Postfix(HbrMemoryGardenRestStoryPresentation.Entry __state, ref Task __result)
    {
        if (__state?.Claimed == true)
            __result = HbrMemoryGardenRestStoryPresentation.PresentAfterEntry(__result, __state);
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), nameof(NRestSiteRoom._Ready))]
internal static class HbrMemoryGardenRestStoryPreparingUiPatch
{
    private static void Postfix(NRestSiteRoom __instance, RestSiteRoom ____room)
    {
        if (!HbrMemoryGardenRestStoryPresentation.IsPreparing(____room)) return;
        // The original EnterInternal may await other AfterRoomEntered hooks after
        // creating this node. Do not expose actionable fire options during that gap.
        __instance.DisableOptions();
        __instance.Visible = false;
    }
}
