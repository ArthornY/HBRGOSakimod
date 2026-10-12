using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NGlobalUi), nameof(NGlobalUi.Initialize))]
internal static class TopBarPowerLayoutInstallPatch
{
    private static void Postfix(NGlobalUi __instance, RunState runState) => NTopBarPowerLayout.Install(__instance, runState);
}

[HarmonyPatch(typeof(NPowerContainer), "UpdatePositions")]
internal static class TopBarPowerPositionsPatch
{
    private static bool Prefix(NPowerContainer __instance)
    {
        var layout = NTopBarPowerLayout.Current;
        if (layout?.Owns(__instance) != true) return true;
        // Multiple changes in one damage/action batch need one final arrangement.
        // FramePreDraw applies it before the next visible frame.
        layout.InvalidateLayout();
        return false;
    }
}

[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.RemoveCreatureNode))]
internal static class TopBarPowerCreatureRemovedPatch
{
    private static void Postfix(NCombatRoom __instance, NCreature __0) =>
        NTopBarPowerLayout.Current?.OnCreatureNodeRemoved(__instance, __0);
}

[HarmonyPatch(typeof(NPowerContainer), nameof(NPowerContainer.SetCreatureBounds))]
internal static class TopBarPowerBoundsPatch
{
    private static void Prefix(NPowerContainer __instance, out NTopBarPowerLayout __state)
    {
        var layout = NTopBarPowerLayout.Current;
        __state = layout?.Owns(__instance) == true ? layout : null;
        __state?.RestoreForNativeBounds();
    }

    private static void Postfix(NTopBarPowerLayout __state) => __state?.ResumeAfterNativeBounds();
}
