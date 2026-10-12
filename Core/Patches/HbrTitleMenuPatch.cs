using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
internal static class HbrTitleMenuInstallPatch
{
    private static void Prefix(NMainMenu __instance) => HbrWebsiteMenuEntry.Install(__instance);

    private static void Postfix(NMainMenu __instance) => NHbrTitleMenu.Install(__instance);
}

[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu.RefreshButtons))]
internal static class HbrTitleMenuRefreshPatch
{
    private static void Postfix(NMainMenu __instance) => NHbrTitleMenu.Find(__instance)?.Refresh();
}

[HarmonyPatch(typeof(NMainMenu), "OnSubmenuStackChanged")]
internal static class HbrTitleMenuSubmenuPatch
{
    private static void Postfix(NMainMenu __instance) => NHbrTitleMenu.Find(__instance)?.Refresh();
}

[HarmonyPatch(typeof(NMainMenu), "get_DefaultFocusedControl")]
internal static class HbrTitleMenuFocusPatch
{
    private static bool Prefix(NMainMenu __instance, ref Control __result)
    {
        NHbrTitleMenu title = NHbrTitleMenu.Find(__instance);
        if (title == null || title.UsesNativeMenu) return true;
        __result = title.DefaultFocusedControl;
        return false;
    }
}
