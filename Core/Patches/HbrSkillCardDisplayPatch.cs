using System;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NCard), nameof(NCard.UpdateVisuals))]
internal static class HbrSkillCardDisplayPatch
{
    private static readonly AccessTools.FieldRef<NCard, Creature> PreviewTarget =
        AccessTools.FieldRefAccess<NCard, Creature>("_previewTarget");
    private static readonly AccessTools.FieldRef<NCard, bool> ForceUnpowered =
        AccessTools.FieldRefAccess<NCard, bool>("_forceUnpoweredPreview");

    private static void Prefix(NCard __instance, out HbrCardDescriptionContext.Scope __state)
    {
        __state = null;
        var card = __instance.Model;
        if (!HbrSkillCardDescription.IsEnabled(card) &&
            !HbrPercentDebuffCardDescription.IsEnabled(card)) return;
        bool detail = false;
        for (Node node = __instance.GetParent(); node != null; node = node.GetParent())
        {
            if (node is not NInspectCardScreen) continue;
            detail = true;
            break;
        }
        bool unpowered = ForceUnpowered(__instance);
        bool live = !unpowered && card.IsMutable && card.Owner != null &&
            card.CombatState != null && (card.Pile?.Type is PileType.Hand or PileType.Play ||
                card.UpgradePreviewType == CardUpgradePreviewType.Combat);
        __state = HbrCardDescriptionContext.Begin(card, detail, live,
            PreviewTarget(__instance) ?? card.CurrentTarget, unpowered);
    }

    private static void Postfix(HbrCardDescriptionContext.Scope __state) => __state?.Dispose();

    private static Exception Finalizer(Exception __exception, HbrCardDescriptionContext.Scope __state)
    {
        __state?.Dispose();
        return __exception;
    }
}
