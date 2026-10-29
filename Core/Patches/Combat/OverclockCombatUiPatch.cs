using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Activate))]
internal static class OverclockCombatUiPatch
{
    private static void Postfix(NCombatUi __instance, CombatState state)
    {
        ModNodeAttachmentRegistry.EnsureReadyAttachments(__instance);

        Player localPlayer = LocalContext.GetMe(state);
        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance,
                HBRGOSakiCombatUiIntegration.CardSelectionPortraitAttachmentId,
                out NSakiCardSelectionPortrait selectionPortrait))
        {
            selectionPortrait.BindPlayer(localPlayer);
        }

        Overclock overclock = state.Players
            .Where(player =>
                player.Creature?.Side == localPlayer?.Creature?.Side)
            .Select(player => player.GetRelic<Overclock>())
            .FirstOrDefault(relic => relic != null);
        SoulOfAnchor rollHistory =
            localPlayer?.GetRelic<SoulOfAnchor>();

        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance,
                HBRGOSakiCombatUiIntegration.GaugeAttachmentId,
                out NOverclockGauge gauge))
        {
            gauge.BindRelic(overclock);
        }

        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance,
                HBRGOSakiCombatUiIntegration.ExtraTurnOverlayAttachmentId,
                out NOverclockExtraTurnOverlay overlay))
        {
            overlay.BindRelic(overclock, localPlayer);
        }

        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance,
                HBRGOSakiCombatUiIntegration.RollHistoryWidgetAttachmentId,
                out NRollHistoryWidget rollHistoryWidget))
        {
            rollHistoryWidget.BindHistory(rollHistory);
        }

        if (HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                __instance,
                HBRGOSakiCombatUiIntegration.CardDamageHistoryWidgetAttachmentId,
                out NCardDamageHistoryWidget damageHistoryWidget))
        {
            damageHistoryWidget.BindPlayer(rollHistory != null ? localPlayer : null);
            if (rollHistoryWidget != null) damageHistoryWidget.PlaceBelow(rollHistoryWidget);
        }
    }
}
