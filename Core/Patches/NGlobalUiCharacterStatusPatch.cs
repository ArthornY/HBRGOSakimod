using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NGlobalUi), nameof(NGlobalUi.Initialize))]
internal static class NGlobalUiCharacterStatusPatch
{
    private const string WidgetName = "HBRGOCharacterStatusWidget";

    private static void Postfix(NGlobalUi __instance, RunState runState)
    {
        NHbrgoSakiMapStatusWidget widget =
            __instance.GetNodeOrNull<NHbrgoSakiMapStatusWidget>(WidgetName);
        Player localPlayer = LocalContext.GetMe(runState);
        if (widget == null)
        {
            widget = new NHbrgoSakiMapStatusWidget
            {
                Name = WidgetName
            };
            widget.Bind(
                localPlayer,
                __instance.TopBar.Map,
                __instance.MapScreen);
            __instance.AddChild(widget);
            return;
        }

        widget.Bind(
            localPlayer,
            __instance.TopBar.Map,
            __instance.MapScreen);
    }
}
