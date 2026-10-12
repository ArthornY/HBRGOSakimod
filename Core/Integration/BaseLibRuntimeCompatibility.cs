#nullable enable

using System;
using System.Reflection;
using BaseLib.Abstracts;
using BaseLib.BaseLibScenes.Acts;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;

namespace HBRGOSakiMod.Core.Integration;

internal static class BaseLibRuntimeCompatibility
{
    private const string TreasurePatchTypeName =
        "BaseLib.Abstracts.CustomActModel+CustomActTreasureChest";

    private static readonly FieldInfo? TreasureRunStateField =
        AccessTools.Field(typeof(NTreasureRoom), "_runState");

    internal static void Install(Harmony harmony)
    {
        MethodInfo? ready = AccessTools.Method(
            typeof(NTreasureRoom),
            nameof(NTreasureRoom._Ready));
        Type? patchType = typeof(CustomActModel).Assembly.GetType(
            TreasurePatchTypeName,
            throwOnError: false);
        MethodInfo? baseLibPostfix = patchType == null
            ? null
            : AccessTools.Method(patchType, "InsertCustomChestVisualNode");
        MethodInfo? replacement = AccessTools.Method(
            typeof(BaseLibRuntimeCompatibility),
            nameof(InsertCustomChestVisualNode));

        if (ready == null || baseLibPostfix == null || replacement == null)
        {
            return;
        }

        harmony.Unpatch(ready, baseLibPostfix);
        harmony.Patch(ready, postfix: new HarmonyMethod(replacement));
    }

    private static void InsertCustomChestVisualNode(NTreasureRoom __instance)
    {
        try
        {
            IRunState? runState =
                TreasureRunStateField?.GetValue(__instance) as IRunState;
            if (runState?.Act is not CustomActModel customAct
                || string.IsNullOrEmpty(customAct.CustomChestScene))
            {
                return;
            }

            Node2D? chestNode =
                __instance.GetNodeOrNull<Node2D>("%ChestVisual");
            NButton? chestButton =
                __instance.GetNodeOrNull<NButton>("%Chest");
            Node? parent = chestNode?.GetParent();
            if (chestNode == null || chestButton == null || parent == null)
            {
                Log.Warn(
                    "Could not find the treasure-room chest nodes; "
                    + "using the vanilla chest visuals.");
                return;
            }

            foreach (Node child in parent.GetChildren())
            {
                if (child is NCustomTreasureRoomChest)
                {
                    return;
                }
            }

            NCustomTreasureRoomChest? customChest =
                NCustomTreasureRoomChest.Create(
                    __instance,
                    runState,
                    chestButton,
                    customAct.CustomChestScene);
            if (customChest == null)
            {
                Log.Warn(
                    $"Could not create custom treasure chest scene "
                    + $"'{customAct.CustomChestScene}'.");
                return;
            }

            chestNode.Visible = false;
            parent.AddChild(customChest);
        }
        catch (Exception exception)
        {
            Log.Warn(
                "Custom treasure chest compatibility failed; "
                + $"using the vanilla chest visuals. {exception}");
        }
    }
}

[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter._Ready))]
internal static class StaticMerchantReadyCompatibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NMerchantCharacter __instance)
    {
        return HasSpineVisual(__instance);
    }

    internal static bool HasSpineVisual(NMerchantCharacter instance)
    {
        return instance.GetChildCount() > 0
            && instance.GetChild(0).GetClass() == "SpineSprite";
    }
}

[HarmonyPatch(
    typeof(NMerchantCharacter),
    nameof(NMerchantCharacter.PlayAnimation))]
internal static class StaticMerchantAnimationCompatibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(NMerchantCharacter __instance)
    {
        return StaticMerchantReadyCompatibilityPatch.HasSpineVisual(
            __instance);
    }
}
