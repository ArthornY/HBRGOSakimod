using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NCursorManager), "UpdateCursor")]
internal static class HbrCursorInstallPatch
{
    internal static void ApplyToExistingManager(NCursorManager manager = null)
    {
        manager ??= NGame.Instance?.CursorManager;
        if (!GodotObject.IsInstanceValid(manager)) return;

        // Mod loading can await Steam after the cursor has already entered the tree.
        // Invoke its patched update now instead of waiting for another _EnterTree.
        AccessTools.Method(typeof(NCursorManager), "UpdateCursor").Invoke(manager, null);
    }

    private static void Prefix(ref Image ____cursorTilted, ref Image ____cursorNotTilted)
    {
        if (HbrCursorHelper.IsApplied(____cursorTilted) &&
            HbrCursorHelper.IsApplied(____cursorNotTilted)) return;

        Image image = HbrCursorHelper.GetImage();
        if (image == null) return;

        bool firstInstall = !HbrCursorHelper.IsOwnedImage(____cursorNotTilted);
        // Replace the base images, leaving native temporary drawing overrides intact.
        ____cursorTilted = image;
        ____cursorNotTilted = image;
        ApplyAuxiliaryShapes(image);
        if (firstInstall) Log.Info("[HBRGOSakiMod] HBR cursor applied globally.");
    }

    internal static void ApplyAuxiliaryShapes(Image image)
    {
        Input.SetCustomMouseCursor(image, Input.CursorShape.PointingHand, HbrCursorHelper.HotSpot);
        Input.SetCustomMouseCursor(image, Input.CursorShape.Help, HbrCursorHelper.HotSpot);
    }
}

[HarmonyPatch(typeof(NCursorManager), "RefreshCursorShown")]
internal static class HbrCursorVisibilityPatch
{
    private static void Postfix(NCursorManager __instance)
    {
        // Returning from a controller can reveal the cursor without a mouse click.
        if (Input.MouseMode != Input.MouseModeEnum.Hidden)
            HbrCursorInstallPatch.ApplyToExistingManager(__instance);
    }
}

[HarmonyPatch(typeof(NCursorManager), nameof(NCursorManager._EnterTree))]
internal static class HbrCursorEnterTreePatch
{
    private static void Postfix(Image ____cursorNotTilted)
    {
        HbrClickEffectHelper.EnsureInstalled();
        if (!HbrCursorHelper.IsApplied(____cursorNotTilted)) return;
        // Native _EnterTree resets Help even when this manager is already installed.
        HbrCursorInstallPatch.ApplyAuxiliaryShapes(____cursorNotTilted);
    }
}

[HarmonyPatch(typeof(NCursorManager), "HotSpot", MethodType.Getter)]
internal static class HbrCursorHotSpotPatch
{
    private static void Postfix(Image ____cursorNotTilted, Vector2? ____overriddenHotSpot,
        ref Vector2 __result)
    {
        if (!____overriddenHotSpot.HasValue && HbrCursorHelper.IsApplied(____cursorNotTilted))
            __result = HbrCursorHelper.HotSpot;
    }
}
