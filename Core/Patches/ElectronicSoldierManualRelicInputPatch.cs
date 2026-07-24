using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NClickableControl), nameof(NClickableControl._GuiInput))]
internal static class ElectronicSoldierManualRelicInputPatch
{
    private static void Postfix(NClickableControl __instance, InputEvent inputEvent)
    {
        if (__instance is not NRelicInventoryHolder relicHolder)
        {
            return;
        }

        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })
        {
            return;
        }

        switch (relicHolder.Relic?.Model)
        {
            case ElectronicSoldierManual manual:
                NSixSenseStatusScreen.Show(manual);
                break;
            case SoulOfAnchor soulOfAnchor:
                NSoulRollHistoryScreen.Show(soulOfAnchor);
                break;
            default:
                return;
        }

        relicHolder.GetViewport().SetInputAsHandled();
    }
}
