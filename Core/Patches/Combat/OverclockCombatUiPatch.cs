using System.Linq;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Activate))]
internal static class OverclockCombatUiPatch
{
    private const string GaugeName = "HbrgoSakiOverclockGauge";
    private const string ExtraTurnOverlayName = "HbrgoSakiOverclockExtraTurnOverlay";

    private static void Postfix(NCombatUi __instance, CombatState state)
    {
        NOverclockGauge gauge = __instance.GetNodeOrNull<NOverclockGauge>(GaugeName);
        if (gauge == null)
        {
            gauge = new NOverclockGauge
            {
                Name = GaugeName
            };
            __instance.AddChildSafely(gauge);
        }

        Player localPlayer = LocalContext.GetMe(state);
        Overclock overclock = state.Players
            .Where(player => player.Creature?.Side == localPlayer.Creature?.Side)
            .Select(player => player.GetRelic<Overclock>())
            .FirstOrDefault(relic => relic != null);
        gauge.BindRelic(overclock);

        NOverclockExtraTurnOverlay overlay =
            __instance.GetNodeOrNull<NOverclockExtraTurnOverlay>(ExtraTurnOverlayName);
        if (overlay == null)
        {
            overlay = new NOverclockExtraTurnOverlay
            {
                Name = ExtraTurnOverlayName
            };
            __instance.AddChildSafely(overlay);
        }

        overlay.BindRelic(overclock, localPlayer);
    }
}
