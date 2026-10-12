using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NRelic), "Reload")]
internal static class TeamCollectionRelicLayoutPatch
{
    private sealed record OriginalLayout(
        TextureRect.StretchModeEnum IconStretch,
        TextureRect.StretchModeEnum OutlineStretch);

    private static readonly ConditionalWeakTable<NRelic, OriginalLayout> OriginalLayouts = new();

    private static void Postfix(NRelic __instance, RelicModel ____model)
    {
        if (__instance.Icon == null || __instance.Outline == null) return;

        if (____model is TeamCollectionRelic or Millsage)
        {
            OriginalLayouts.GetValue(__instance, relic => new OriginalLayout(
                relic.Icon.StretchMode, relic.Outline.StretchMode));
            // Wide team logos otherwise sit at the top of the square icon slot,
            // leaving a large gap above the native bottom-right counter.
            __instance.Icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            __instance.Outline.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        }
        else if (OriginalLayouts.TryGetValue(__instance, out OriginalLayout original))
        {
            __instance.Icon.StretchMode = original.IconStretch;
            __instance.Outline.StretchMode = original.OutlineStretch;
            OriginalLayouts.Remove(__instance);
        }
    }
}
