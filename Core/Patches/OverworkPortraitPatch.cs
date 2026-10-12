using Godot;
using HarmonyLib;
using HBRGOSakiMod.Core.Helpers;
using HBRGOSakiMod.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(CardModel), nameof(CardModel.Portrait), MethodType.Getter)]
internal static class OverworkPortraitPatch
{
    private static bool Prefix(CardModel __instance, ref Texture2D __result)
    {
        if (__instance is not OverworkHbrgoSaki) return true;
        __result = OverworkPortraitHelper.GetPortrait();
        return false;
    }
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class OverworkPortraitReloadPatch
{
    private static void Postfix(NCard __instance)
    {
        if (!__instance.IsNodeReady() || __instance.Model is not OverworkHbrgoSaki) return;
        Texture2D texture = OverworkPortraitHelper.GetPortrait();
        foreach (string path in new[] { "%Portrait", "%AncientPortrait" })
        {
            if (__instance.GetNodeOrNull<TextureRect>(path) is not { } portrait) continue;
            portrait.Texture = texture;
        }
    }
}
