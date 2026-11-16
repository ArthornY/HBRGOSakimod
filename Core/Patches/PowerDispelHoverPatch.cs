using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace HBRGOSakiMod.Core.Patches;

// The native constructor receives fully formatted text. Decorating here
// avoids evaluating derived Description variables before their getter fills
// them and applies equally to smart, remote, compact and ordinary power tips.
[HarmonyPatch(typeof(HoverTip), MethodType.Constructor,
    new[] { typeof(PowerModel), typeof(string), typeof(bool) })]
internal static class UndispellablePowerFooterPatch
{
    private static void Prefix(PowerModel __0, ref string __1)
    {
        if (!PowerDispelRules.IsUndispellable(__0)) return;
        string footer = new LocString("card_keywords", "HBRGO_UNDISPELLABLE.footer").GetFormattedText();
        if (!__1.EndsWith(footer, System.StringComparison.Ordinal)) __1 += "\n" + footer;
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.HoverTips), MethodType.Getter)]
internal static class UndispellablePowerExplanationPatch
{
    private static void Postfix(PowerModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (!PowerDispelRules.IsUndispellable(__instance) || !__instance.IsVisible) return;
        __result = __result.Concat(new IHoverTip[] { new HoverTip(
            new LocString("card_keywords", "HBRGO_UNDISPELLABLE.title"),
            new LocString("card_keywords", "HBRGO_UNDISPELLABLE.description")) });
    }
}
