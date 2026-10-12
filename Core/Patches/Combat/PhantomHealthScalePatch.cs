using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetDefaultScaleTo))]
internal static class PhantomDefaultHealthScalePatch
{
    private static void Prefix(NCreature __instance, ref float size)
    {
        if (__instance.Entity?.Monster?.CanChangeScale != false)
            PhantomHealthScaleHelper.ScaleNativeDefault(__instance, ref size);
    }
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetScaleAndHue))]
internal static class PhantomScaleAndHuePatch
{
    private static void Prefix(NCreature __instance, ref float scale) =>
        PhantomHealthScaleHelper.ScaleNativeDefault(__instance, ref scale);
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.ScaleTo))]
internal static class PhantomScaleTweenStartPatch
{
    private static void Prefix(NCreature __instance) =>
        PhantomHealthScaleHelper.BeginTween(__instance);
}

[HarmonyPatch(typeof(NCreature), "DoScaleTween")]
internal static class PhantomScaleTweenPatch
{
    private static void Prefix(NCreature __instance, ref Vector2 scale) =>
        PhantomHealthScaleHelper.AdjustTween(__instance, ref scale);

    private static void Postfix(NCreature __instance) =>
        PhantomHealthScaleHelper.RefreshBounds(__instance);
}
