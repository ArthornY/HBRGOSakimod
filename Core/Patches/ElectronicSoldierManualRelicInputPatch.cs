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

        if (relicHolder.Relic?.Model is not ElectronicSoldierManual manual)
        {
            return;
        }

        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })
        {
            return;
        }

        relicHolder.GetViewport().SetInputAsHandled();
        NSixSenseStatusScreen.Show(manual);
    }
}
