using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.Portrait), MethodType.Getter)]
internal static class PhantomPortraitPatch
{
    private static bool Prefix(CardModel __instance, ref Texture2D __result)
    {
        if (__instance is not PhantomChoiceHbrgoSaki card) return true;
        __result = PhantomPortraitHelper.GetPortrait(card.MonsterId);
        return false;
    }
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class PhantomPortraitReloadPatch
{
    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady() || __instance.Model is not PhantomChoiceHbrgoSaki card) return;
        Texture2D texture = PhantomPortraitHelper.GetPortrait(card.MonsterId);
        foreach (string path in new[] { "%Portrait", "%AncientPortrait" })
        {
            if (__instance.GetNodeOrNull<TextureRect>(path) is { } portrait) portrait.Texture = texture;
        }
    }
}
