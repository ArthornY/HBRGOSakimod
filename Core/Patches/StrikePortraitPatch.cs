using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class StrikePortraitPatch
{
    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady())
        {
            return;
        }
        ModNodeAttachmentRegistry.EnsureReadyAttachments(__instance);
        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance, HBRGOSakiCombatUiIntegration.StrikePortraitAttachmentId,
                out NHbrgoStrikePortrait portrait))
        {
            portrait.Refresh(force: true);
        }
        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance, HBRGOSakiCombatUiIntegration.CardStyleAttachmentId,
                out NHbrCardStyle style))
        {
            style.Refresh();
        }
    }
}
