using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Localization;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class HbrCardFaceRulesHoverPatch
{
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is EldestOshimaIsHereHbrgoSaki)
            __result = (__result ?? Enumerable.Empty<IHoverTip>()).Append(HoverTipCompatibility.CreateKeyword(new LocString("cards", "HBRGO_CALL_NAME.title"),
                new LocString("cards", "HBRGO_CALL_NAME.description"), "HBRGO_CALL_NAME"));
    }
}
