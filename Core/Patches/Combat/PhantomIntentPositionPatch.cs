using System;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Integration;
using HBRGOSakiMod.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HBRGOSakiMod.Core.Patches.Combat;

[HarmonyPatch(typeof(NCreature), "UpdateBounds", new[] { typeof(Node) })]
internal static class PhantomIntentPositionPatch
{
    private static void Prefix(NCreature __instance, Node boundsContainer,
        ref float ____tempScale, out float? __state)
    {
        __state = null;
        PhantomHealthScaleHelper.RememberBounds(__instance, boundsContainer);
        NPhantomCombatLayout.ResetStateDisplayForNativeBounds(__instance);
        if (NPhantomCombatLayout.UsesHopperLayout(__instance.Entity))
        {
            // ScaleTo sets its final temporary factor before the tween moves.
            // Use the actual visual geometry for phantom hitboxes throughout
            // that tween, while preserving the native scale state itself.
            __state = ____tempScale;
            ____tempScale = 1f;
        }
    }

    private static void Postfix(NCreature __instance, ref float ____tempScale, float? __state)
    {
        if (__state.HasValue) ____tempScale = __state.Value;
        NCombatRoom room = NCombatRoom.Instance;
        if (room != null && HBRGOSakiCombatUiIntegration.Registry.TryGetAttached(
                room, HBRGOSakiCombatUiIntegration.PhantomCombatLayoutAttachmentId,
                out NPhantomCombatLayout layout))
        {
            layout.RefreshCreature(__instance);
        }
    }

    private static void Finalizer(Exception __exception, ref float ____tempScale, float? __state)
    {
        if (__exception != null && __state.HasValue) ____tempScale = __state.Value;
    }
}
