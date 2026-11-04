using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.SetAlignmentForCardHolder))]
internal static class HandCardHoverTipLayoutPatch
{
    private static void Postfix(NHoverTipSet __instance, NCardHolder __0)
    {
        if (__0 is not NHandCardHolder holder || __instance == null) return;
        // Track the existing set so native unhover, targeting and Remove keep
        // ownership of its lifetime. Other card holders retain native layout.
        NHandCardHoverTips layout = __instance.GetNodeOrNull<NHandCardHoverTips>("HandCardHoverTips");
        if (layout != null) { layout.Holder = holder; return; }
        __instance.AddChildSafely(new NHandCardHoverTips { Name = "HandCardHoverTips", Holder = holder });
    }
}
