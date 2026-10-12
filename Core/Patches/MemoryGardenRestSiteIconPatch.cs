using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NRestSiteButton), "Reload")]
internal static class MemoryGardenRestSiteIconPatch
{
    private static void Postfix(NRestSiteButton __instance, RestSiteOption ____option)
    {
        if (!__instance.IsNodeReady() ||
            __instance.GetNodeOrNull<TextureRect>("%Icon") is not { } icon) return;
        if (____option is MemoryGardenRestSiteOption)
            HbrMemoryGardenIconHelper.Apply(icon);
        else if (____option is HotaruCannonUpgradeRestSiteOption)
        {
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        }
    }
}
