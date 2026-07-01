using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace HBRGOSakiMod.Core.Patches;

[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter._Ready))]
internal static class NMerchantCharacterReadyPatch
{
    private static bool Prefix(NMerchantCharacter __instance)
    {
        return !IsHbrgoSakiMerchant(__instance);
    }

    internal static bool IsHbrgoSakiMerchant(NMerchantCharacter character)
    {
        return character.Name == "HBRGOSakiMerchant";
    }
}

[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter.PlayAnimation))]
internal static class NMerchantCharacterPlayAnimationPatch
{
    private static bool Prefix(NMerchantCharacter __instance)
    {
        return !NMerchantCharacterReadyPatch.IsHbrgoSakiMerchant(__instance);
    }
}
