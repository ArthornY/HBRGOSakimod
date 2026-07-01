using System;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace HBRGOSakiMod.Core.Patches;

internal static class HBRGOSakiFlashVfxScaleHelper
{
    private const float FlashScaleMultiplier = 0.5f;
    private const string ModNamespacePrefix = "HBRGOSakiMod.";

    internal static bool IsThisModModel(AbstractModel model)
    {
        return model.GetType().Namespace?.StartsWith(ModNamespacePrefix, StringComparison.Ordinal) == true;
    }

    internal static void HalveScale(Node2D node)
    {
        node.Scale *= FlashScaleMultiplier;
    }

    internal static void HalveScale(Control node)
    {
        node.Scale *= FlashScaleMultiplier;
    }
}

[HarmonyPatch(typeof(NPowerFlashVfx), nameof(NPowerFlashVfx.Create))]
internal static class NPowerFlashVfxCreatePatch
{
    private static void Postfix(PowerModel power, ref NPowerFlashVfx __result)
    {
        if (__result == null || !HBRGOSakiFlashVfxScaleHelper.IsThisModModel(power))
        {
            return;
        }

        HBRGOSakiFlashVfxScaleHelper.HalveScale(__result);
    }
}

[HarmonyPatch(typeof(NPowerAppliedVfx), nameof(NPowerAppliedVfx.Create))]
internal static class NPowerAppliedVfxCreatePatch
{
    private static void Postfix(PowerModel power, ref NPowerAppliedVfx __result)
    {
        if (__result == null || !HBRGOSakiFlashVfxScaleHelper.IsThisModModel(power))
        {
            return;
        }

        HBRGOSakiFlashVfxScaleHelper.HalveScale(__result);
    }
}

[HarmonyPatch(typeof(NRelicFlashVfx), nameof(NRelicFlashVfx.Create), new[] { typeof(RelicModel) })]
internal static class NRelicFlashVfxCreatePatch
{
    private static void Postfix(RelicModel relic, ref NRelicFlashVfx __result)
    {
        if (__result == null || !HBRGOSakiFlashVfxScaleHelper.IsThisModModel(relic))
        {
            return;
        }

        HBRGOSakiFlashVfxScaleHelper.HalveScale(__result);
    }
}

[HarmonyPatch(typeof(NRelicInventoryHolder), "DoFlash")]
internal static class NRelicInventoryHolderDoFlashPatch
{
    private static bool Prefix(NRelicInventoryHolder __instance)
    {
        RelicModel relic = __instance.Relic?.Model;
        if (relic == null || !HBRGOSakiFlashVfxScaleHelper.IsThisModModel(relic))
        {
            return true;
        }

        Node2D flash = PreloadManager.Cache
            .GetScene(SceneHelper.GetScenePath("vfx/relic_inventory_flash_vfx"))
            .Instantiate<Node2D>(PackedScene.GenEditState.Disabled);

        HBRGOSakiFlashVfxScaleHelper.HalveScale(flash);
        flash.GetNode<GpuParticles2D>("Particles").Texture = relic.Icon;
        flash.GlobalPosition = __instance.GlobalPosition + __instance.Size * 0.5f;
        NRun.Instance.GlobalUi.AboveTopBarVfxContainer.AddChildSafely(flash);
        return false;
    }
}

[HarmonyPatch(typeof(NCard), nameof(NCard.FlashRelicOnCard))]
internal static class NCardFlashRelicOnCardPatch
{
    private static void Postfix(NCard __instance, RelicModel relic)
    {
        if (!HBRGOSakiFlashVfxScaleHelper.IsThisModModel(relic))
        {
            return;
        }

        NRelicFlashVfx vfx = __instance.Body.GetChildren().OfType<NRelicFlashVfx>().LastOrDefault();
        if (vfx == null)
        {
            return;
        }

        HBRGOSakiFlashVfxScaleHelper.HalveScale(vfx);
    }
}
