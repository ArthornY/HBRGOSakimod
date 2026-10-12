using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCreature), "ShowCreatureHoverTips")]
internal static class SixSenseResistanceHoverPatch
{
    private static bool Prefix(NCreature __instance)
    {
        var stateDisplay = __instance.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar");
        var healthBar = stateDisplay?.GetNodeOrNull<NHealthBar>("%HealthBar");
        // Badge hover owns its compact tooltip; do not reopen the creature's entire tip list over it.
        return healthBar == null ||
            !HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(healthBar,
                HBRGOSakiCombatUiIntegration.SixSenseResistanceRowAttachmentId,
                out NSixSenseResistanceRow row) || !row.IsHoveringResistance;
    }
}
