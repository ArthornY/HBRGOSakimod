using System.Reflection;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Patches;

// v0.111.0 assigns the frame, title banner and portrait border in NCard.UpdatePortrait, called from
// Reload and on its own from UpdateVisuals, instead of in Reload; put the HBR frame back after it.
// The stable game has no UpdatePortrait (it does all of this in Reload), so the patch is skipped there.
[HarmonyPatch]
internal static class HbrCardFramePatch
{
    private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(typeof(NCard), "UpdatePortrait");

    private static bool Prepare() => TargetMethod() != null;

    private static void Postfix(NCard __instance)
    {
        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance, HBRGOSakiCombatUiIntegration.CardStyleAttachmentId, out NHbrCardStyle style))
        {
            style.ReapplyFrame();
        }
    }
}

// NCard.UpdateTypePlaque sizes and centres the plaque in a deferred call, after Reload has already styled the card.
[HarmonyPatch(typeof(NCard), "UpdateTypePlaqueSizeAndPosition")]
internal static class HbrCardStylePatch
{
    private static void Postfix(NCard __instance)
    {
        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance, HBRGOSakiCombatUiIntegration.CardStyleAttachmentId, out NHbrCardStyle style))
        {
            style.LayoutTypePlaque();
        }
    }
}

// Vanilla rewrites the title on every visual update; memory cards get their 「」 back afterwards.
[HarmonyPatch(typeof(NCard), "UpdateTitleLabel")]
internal static class HbrCardTitlePatch
{
    private static void Postfix(NCard __instance)
    {
        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance, HBRGOSakiCombatUiIntegration.CardStyleAttachmentId, out NHbrCardStyle style))
        {
            style.DecorateTitle();
        }
    }
}
