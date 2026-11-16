using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NGlobalUi), nameof(NGlobalUi.Initialize))]
internal static class VerticalPotionLayoutInstallPatch
{
    private static void Postfix(NGlobalUi __instance) => NVerticalPotionLayout.Install(__instance);
}

[HarmonyPatch]
internal static class VerticalPotionHudVisibilityPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "AnimHide", "AnimShow" })
            yield return AccessTools.DeclaredMethod(typeof(NTopBar), name);
        foreach (string name in new[] { "AnimHide", "AnimShow", "HideImmediately", "ShowImmediately" })
            yield return AccessTools.DeclaredMethod(typeof(NRelicInventory), name);
    }

    private static void Postfix(Node __instance, MethodBase __originalMethod) =>
        NVerticalPotionLayout.Current?.SetNativeHidden(__instance, __originalMethod.Name.Contains("Hide"));
}

[HarmonyPatch]
internal static class VerticalPotionNavigationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // v0.113 creates potion holders directly; the old navigation method
        // no longer exists. Reapply our links after that native layout step.
        yield return AccessTools.DeclaredMethod(typeof(NPotionContainer), "UpdateNavigation") ??
            AccessTools.DeclaredMethod(typeof(NPotionContainer), "GrowPotionHolders") ??
            throw new System.MissingMethodException(typeof(NPotionContainer).FullName,
                "UpdateNavigation / GrowPotionHolders");
        yield return AccessTools.DeclaredMethod(typeof(NRelicInventory), "UpdateNavigation");
    }

    private static void Postfix(Node __instance)
    {
        // Native relic and potion updates may replace controller neighbors.
        // Reapply the vertical links once, after native/container layout ends.
        NVerticalPotionLayout.Current?.InvalidateNativeNavigation(__instance);
    }
}

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.CreateAndShow),
    new[] { typeof(Control), typeof(IEnumerable<IHoverTip>), typeof(HoverTipAlignment) })]
internal static class VerticalPotionHoverFollowPatch
{
    private static void Postfix(NHoverTipSet __result, Control owner)
    {
        NVerticalPotionLayout layout = NVerticalPotionLayout.Current;
        layout?.PlaceHoverTipAboveHud(__result);
        if (__result == null || layout?.Owns(owner) != true) return;
        NHoverTipSet tips = __result;
        // Native OnFocus still places the tip below the slot after this call.
        // Attach only after that caller finishes, then dock on every final frame
        // so native following and refreshed damage previews cannot cover it.
        Callable.From(() =>
        {
            if (NVerticalPotionLayout.Usable(tips) && layout.Owns(owner))
                tips.AddChildSafely(new NVerticalPotionHoverPlacement { OwnerControl = owner });
        }).CallDeferred();
    }
}
